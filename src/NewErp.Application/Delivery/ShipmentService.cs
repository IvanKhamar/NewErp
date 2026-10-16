using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Delivery;
using NewErp.Domain.Sms;

namespace NewErp.Application.Delivery;

/// <summary>
/// ТТН Нової Пошти: створення, видалення, друк і довідник відділень (на основі модуля старого сайту).
/// Поки лише доставка на відділення чи в поштомат; адресна доставка кур'єром — окремим кроком.
/// </summary>
public sealed class ShipmentService(
    IShipmentRepository repository, IWarehouseDirectory directory, INovaPoshtaClient client, TimeProvider clock)
{
    /// <summary>Скільки підказок показувати в автодоповненні міста чи відділення.</summary>
    public const int SuggestionLimit = 20;

    /// <summary>Код оплати при отриманні в OpenCart: для нього пропонуємо накладений платіж.</summary>
    public const string CashOnDeliveryPaymentCode = "cod";

    public async Task<ShipmentFormDto> FormAsync(int orderId, CancellationToken ct)
    {
        var order = await repository.GetOrderShippingInfoAsync(orderId, ct)
            ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");

        // Місто й відділення в замовленні записані текстом; якщо такі є в довіднику, одразу підставляємо.
        var city = string.IsNullOrWhiteSpace(order.City) ? null : await directory.FindCityAsync(order.City.Trim(), ct);
        var warehouse = city is null || string.IsNullOrWhiteSpace(order.Warehouse)
            ? null
            : await directory.FindWarehouseAsync(city.Ref, order.Warehouse.Trim(), ct);

        return new ShipmentFormDto(
            order.FirstName, order.LastName, order.Telephone, order.City, city, order.Warehouse, warehouse,
            order.Total,
            order.PaymentCode == CashOnDeliveryPaymentCode ? order.Total : 0,
            client.Senders, client.DropoffPoints, SeatPreset.All, client.CanCreateDocuments,
            client.CanCreateDocuments && !client.IsConfigured);
    }

    public async Task<ShipmentDto> CreateAsync(int orderId, CreateShipmentRequest request, int userId, CancellationToken ct)
    {
        if (!client.CanCreateDocuments)
            throw new DomainException("Ключ API Нової Пошти не задано, тож ТТН створити не можна.");
        if (await repository.GetOrderShippingInfoAsync(orderId, ct) is null)
            throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        if (await repository.GetStoredAsync(orderId, ct) is not null)
            throw new DomainException("За замовленням уже є ТТН. Щоб створити нову, спершу видаліть стару.");

        var shipment = await ValidateAsync(orderId, request, ct);
        var created = await client.CreateAsync(shipment, ct);

        var document = new ShipmentDocument(orderId, created.Number, created.Cost, created.Ref);
        repository.Add(document, shipment.SenderId, userId, clock.GetLocalNow().DateTime);
        await repository.SaveChangesAsync(ct);
        return (await repository.GetForOrderAsync(orderId, ct))!;
    }

    /// <summary>Видаляє ТТН спершу в Новій Пошті, потім у нас. Якщо Нова Пошта відмовила, запис лишається.</summary>
    public async Task DeleteAsync(int orderId, CancellationToken ct)
    {
        var stored = await RequireStoredAsync(orderId, ct);
        if (stored.Document.IsFinal)
            throw new DomainException("Посилку вже отримано чи повернено, таку ТТН видалити не можна.");

        await client.DeleteAsync(SenderOf(stored), stored.Document.Ref, ct);
        await repository.DeleteAsync(stored.Document.Id, ct);
    }

    public async Task<(byte[] Pdf, string FileName)> PrintAsync(int orderId, ShipmentPrintKind kind, CancellationToken ct)
    {
        var stored = await RequireStoredAsync(orderId, ct);
        var pdf = await client.PrintAsync(SenderOf(stored), stored.Document.Ref, kind, ct);
        var prefix = kind == ShipmentPrintKind.Document ? "ttn" : "marking";
        return (pdf, $"{prefix}-{stored.Document.Number}.pdf");
    }

    public Task<IReadOnlyList<CityDto>> SearchCitiesAsync(string? search, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(search)
            ? Task.FromResult<IReadOnlyList<CityDto>>([])
            : directory.SearchCitiesAsync(search.Trim(), SuggestionLimit, ct);

    public Task<IReadOnlyList<WarehouseDto>> SearchWarehousesAsync(string cityRef, string? search, CancellationToken ct) =>
        directory.SearchWarehousesAsync(cityRef, search?.Trim() ?? "", SuggestionLimit, ct);

    /// <summary>Завантажує свіжий довідник відділень з API і замінює ним локальний (і вітрини старого сайту).</summary>
    public async Task<WarehouseRefreshResult> RefreshWarehousesAsync(CancellationToken ct)
    {
        if (!client.IsConfigured)
            return new WarehouseRefreshResult(false, 0);

        var warehouses = await client.FetchWarehousesAsync(ct);
        // Порожня відповідь — майже напевно збій, а не «всі відділення закрилися»: довідник не чіпаємо.
        if (warehouses.Count == 0)
            throw new DomainException("Нова Пошта повернула порожній довідник відділень, локальний довідник не змінено.");
        await directory.ReplaceAllAsync(warehouses, ct);
        return new WarehouseRefreshResult(true, warehouses.Count);
    }

    private async Task<NewShipment> ValidateAsync(int orderId, CreateShipmentRequest request, CancellationToken ct)
    {
        var firstName = Name(request.FirstName, "Ім'я");
        var lastName = Name(request.LastName, "Прізвище");
        var phone = SmsMessage.NormalizePhone(request.Phone);

        if (string.IsNullOrWhiteSpace(request.CityRef) || string.IsNullOrWhiteSpace(request.WarehouseRef))
            throw new DomainException("Оберіть місто й відділення з довідника.");
        if (!await directory.WarehouseExistsAsync(request.CityRef, request.WarehouseRef, ct))
            throw new DomainException("Такого відділення в цьому місті немає в довіднику.");
        if (request.DeclaredCost <= 0)
            throw new DomainException("Оголошена вартість має бути більшою за нуль.");
        if (request.CashOnDelivery < 0)
            throw new DomainException("Накладений платіж не може бути від'ємним.");
        if (request.Seats is not { Count: > 0 })
            throw new DomainException("Додайте хоча б одне місце (коробку).");
        if (request.Seats.Any(s => s.Weight <= 0 || s.Width < 0 || s.Length < 0 || s.Height < 0))
            throw new DomainException("Вага місця має бути більшою за нуль, а розміри — не від'ємними.");

        var sender = Pick(client.Senders, request.SenderId, s => s.Id, s => s.IsDefault, "відправника");
        var point = Pick(client.DropoffPoints, request.DropoffPointId, p => p.Id, p => p.IsDefault, "місце відправки");

        return new NewShipment(
            sender.Id, point.Id, orderId, DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
            firstName, lastName, request.MiddleName?.Trim() ?? "", phone, request.CityRef, request.WarehouseRef,
            request.DeclaredCost, request.CashOnDelivery, request.SenderPays, request.Seats);
    }

    /// <summary>Як старий сайт: апостроф «`» Нова Пошта не приймає, замінюємо на «'».</summary>
    private static string Name(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new DomainException($"Поле «{field}» обов'язкове.")
            : value.Trim().Replace('`', '\'');

    private static T Pick<T>(IReadOnlyList<T> options, string? id, Func<T, string> idOf, Func<T, bool> isDefault, string what)
    {
        if (id is not null)
            return options.FirstOrDefault(o => idOf(o) == id) ?? throw new DomainException($"Невідомо {what}: {id}.");
        return options.FirstOrDefault(isDefault) ?? options.FirstOrDefault()
            ?? throw new DomainException($"У налаштуваннях Нової Пошти не задано {what}.");
    }

    private async Task<StoredShipment> RequireStoredAsync(int orderId, CancellationToken ct) =>
        await repository.GetStoredAsync(orderId, ct) ?? throw new NotFoundException($"У замовлення {orderId} немає ТТН.");

    /// <summary>ТТН, створені на старому сайті, не мають запису про відправника — для них беремо типового.</summary>
    private string SenderOf(StoredShipment stored) =>
        stored.SenderId ?? Pick(client.Senders, null, s => s.Id, s => s.IsDefault, "відправника").Id;
}
