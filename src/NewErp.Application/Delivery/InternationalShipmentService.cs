using System.Globalization;

using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Delivery;

namespace NewErp.Application.Delivery;

/// <summary>
/// Накладні за кордон через Nova Post International, як вкладка «Міжнародна» діалогу ТТН старого сайту. Документ лежить разом
/// із внутрішніми (dd_novaposhta_document) з carrier = international; видалення — через ShipmentService. Як у Java-версії.
/// </summary>
public sealed class InternationalShipmentService(IShipmentRepository repository, INovaPostInternationalClient client, TimeProvider clock)
{
    /// <summary>Запис відправника (erp_shipment_sender) міжнародної накладної: відправник один, з налаштувань.</summary>
    public const string SenderId = "international";

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

    public async Task<InternationalFormDto> FormAsync(int orderId, CancellationToken ct)
    {
        var order = await repository.GetInternationalOrderInfoAsync(orderId, ct) ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        // У замовленні адреса одним рядком: вона йде у вулицю, будинок вводять окремо.
        var recipient = new InternationalShipment.RecipientInfo(order.FirstName, order.LastName, order.Telephone, order.Email, CountryCode(order.Country),
            order.Region, order.City, order.Address, "", "", order.PostCode);
        var items = order.Lines.Select(l => new InternationalShipment.Item(l.Name, "", "", 0, l.Quantity, l.Total)).ToList();
        return new InternationalFormDto(recipient, new InternationalShipment.TermsInfo(), items, order.Total, order.CurrencyCode, SeatPreset.All,
            client.CanCreateDocuments, client.CanCreateDocuments && !client.IsConfigured);
    }

    /// <summary>Усі країни, крім України, за українською назвою (власної таблиці країн у базі магазину немає).</summary>
    public static IReadOnlyList<CountryDto> Countries()
    {
        var comparer = StringComparer.Create(Ukrainian, ignoreCase: false);
        return CountryNames.All.Where(c => c.Code != "UA").OrderBy(c => c.Ukrainian, comparer).Select(c => new CountryDto(c.Code, c.Ukrainian)).ToList();
    }

    public async Task<IReadOnlyList<HsCodeDto>> HsCodesAsync(string? keyword, string? countryCode, CancellationToken ct) =>
        keyword is null || keyword.Trim().Length < 2
            ? []
            : await client.SearchHsCodesAsync(keyword.Trim(), (countryCode ?? "").Trim().ToUpperInvariant(), ct);

    public async Task<ShipmentDto> CreateAsync(int orderId, CreateInternationalShipmentRequest request, int userId, CancellationToken ct)
    {
        if (!client.CanCreateDocuments)
            throw new DomainException("Ключ API Nova Post International не задано, тож міжнародну накладну створити не можна.");
        if (await repository.GetInternationalOrderInfoAsync(orderId, ct) is null)
            throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        if (await repository.GetStoredAsync(orderId, ct) is not null)
            throw new DomainException("За замовленням уже є ТТН. Щоб створити нову, спершу видаліть стару.");

        var shipment = InternationalShipment.Of(orderId, request.Recipient, request.Terms, request.Items, request.Parcels, request.DeclaredCost,
            request.Currency);
        var created = await client.CreateAsync(shipment, ct);
        repository.Add(new ShipmentDocument(orderId, created.Number, created.Cost, created.Ref, ShipmentDocument.International), SenderId, userId,
            clock.GetLocalNow().DateTime);
        await repository.SaveChangesAsync(ct);
        return (await repository.GetForOrderAsync(orderId, ct))!;
    }

    /// <summary>ISO-код країни замовлення за українською чи англійською назвою; порожньо, якщо невідома або Україна.</summary>
    public static string CountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return "";
        var name = country.Trim();
        return CountryNames.All.FirstOrDefault(c => c.Code != "UA" && (c.Code.Equals(name, StringComparison.OrdinalIgnoreCase)
            || c.Ukrainian.Equals(name, StringComparison.OrdinalIgnoreCase) || c.English.Equals(name, StringComparison.OrdinalIgnoreCase)))?.Code ?? "";
    }
}
