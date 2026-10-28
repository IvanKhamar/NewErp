using NewErp.Application.Delivery;
using NewErp.Domain.Delivery;

namespace NewErp.UnitTests.Application;

/// <summary>Підроблений клієнт Нової Пошти: запам'ятовує запити й відповідає заданим.</summary>
internal sealed class FakeNovaPoshtaClient : INovaPoshtaClient
{
    public bool IsConfigured { get; set; } = true;

    public bool CanCreateDocuments { get; set; } = true;

    public IReadOnlyList<SenderOption> Senders { get; set; } =
        [new SenderOption("fop-a", "ФОП А", false), new SenderOption("fop-b", "ФОП Б", true)];

    public IReadOnlyList<DropoffPointOption> DropoffPoints { get; set; } = [new DropoffPointOption("kyiv", "Київ, №1", true)];

    public Func<string, TrackingInfo> Answer { get; set; } = number => new(number, "Статус 0", 0, "", false, null, null, null);

    public List<IReadOnlyList<TrackingRequest>> Batches { get; } = [];

    public NewShipment? Created { get; private set; }

    public List<(string SenderId, string Ref)> Deleted { get; } = [];

    public List<NovaPoshtaWarehouse> Warehouses { get; } = [];

    public Task<IReadOnlyList<TrackingInfo>> TrackAsync(IReadOnlyList<TrackingRequest> documents, CancellationToken ct)
    {
        Batches.Add(documents);
        return Task.FromResult<IReadOnlyList<TrackingInfo>>(documents.Select(d => Answer(d.Number)).ToList());
    }

    public Task<CreatedShipment> CreateAsync(NewShipment shipment, CancellationToken ct)
    {
        Created = shipment;
        return Task.FromResult(new CreatedShipment("doc-ref", "20450000009999", 85m));
    }

    public Task DeleteAsync(string senderId, string documentRef, CancellationToken ct)
    {
        Deleted.Add((senderId, documentRef));
        return Task.CompletedTask;
    }

    public Task<byte[]> PrintAsync(string senderId, string documentRef, ShipmentPrintKind kind, CancellationToken ct) =>
        Task.FromResult("%PDF"u8.ToArray());

    public Task<IReadOnlyList<NovaPoshtaWarehouse>> FetchWarehousesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NovaPoshtaWarehouse>>(Warehouses);
}

/// <summary>Сховище ТТН у пам'яті.</summary>
internal sealed class FakeShipmentRepository : IShipmentRepository
{
    public List<OpenShipment> Open { get; } = [];

    public Dictionary<int, OrderShippingInfo> Orders { get; } = [];

    public List<StoredShipment> Stored { get; } = [];

    public List<int> DeletedIds { get; } = [];

    public bool Saved { get; private set; }

    public (DateOnly From, DateOnly To)? ReminderRange { get; private set; }

    public Task<ShipmentDto?> GetForOrderAsync(int orderId, CancellationToken ct) =>
        Task.FromResult(Stored.LastOrDefault(s => s.Document.OrderId == orderId) is { } s
            ? new ShipmentDto(s.Document.Id, orderId, s.Document.Carrier, s.Document.Number, s.Document.Cost, "", 0, ShipmentTone.Info,
                false, "", false, null, null, null)
            : null);

    public Task<IReadOnlyList<OpenShipment>> ListOpenForTrackingAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OpenShipment>>(Open);

    public Task<IReadOnlyList<ReminderDto>> ListRemindersAsync(DateOnly keepingFrom, DateOnly keepingTo, CancellationToken ct)
    {
        ReminderRange = (keepingFrom, keepingTo);
        return Task.FromResult<IReadOnlyList<ReminderDto>>([]);
    }

    public Task<bool> MarkRemindedAsync(int orderId, DateTime remindedAt, CancellationToken ct) => Task.FromResult(false);

    public Task<StoredShipment?> GetStoredAsync(int orderId, CancellationToken ct) =>
        Task.FromResult(Stored.LastOrDefault(s => s.Document.OrderId == orderId));

    public Task<OrderShippingInfo?> GetOrderShippingInfoAsync(int orderId, CancellationToken ct) =>
        Task.FromResult(Orders.GetValueOrDefault(orderId));

    public Task<InternationalOrderInfo?> GetInternationalOrderInfoAsync(int orderId, CancellationToken ct) =>
        Task.FromResult<InternationalOrderInfo?>(null);

    public void Add(ShipmentDocument document, string senderId, int userId, DateTime createdAt) =>
        Stored.Add(new StoredShipment(document, senderId));

    public Task DeleteAsync(int documentId, CancellationToken ct)
    {
        DeletedIds.Add(documentId);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saved = true;
        return Task.CompletedTask;
    }
}

/// <summary>Довідник: одне місто з одним відділенням.</summary>
internal sealed class FakeWarehouseDirectory : IWarehouseDirectory
{
    public static readonly CityDto Kyiv = new("city-kyiv", "Київ");
    public static readonly WarehouseDto Branch1 = new("wh-1", "Відділення №1: вул. Хрещатик, 1", "Branch", 30);

    public IReadOnlyList<NovaPoshtaWarehouse>? Replaced { get; private set; }

    public Task<IReadOnlyList<CityDto>> SearchCitiesAsync(string search, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CityDto>>(Kyiv.Name.StartsWith(search, StringComparison.Ordinal) ? [Kyiv] : []);

    public Task<IReadOnlyList<WarehouseDto>> SearchWarehousesAsync(string cityRef, string search, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WarehouseDto>>(cityRef == Kyiv.Ref ? [Branch1] : []);

    public Task<CityDto?> FindCityAsync(string name, CancellationToken ct) => Task.FromResult(name == Kyiv.Name ? Kyiv : null);

    public Task<WarehouseDto?> FindWarehouseAsync(string cityRef, string name, CancellationToken ct) =>
        Task.FromResult(cityRef == Kyiv.Ref && name == Branch1.Name ? Branch1 : null);

    public Task<bool> WarehouseExistsAsync(string cityRef, string warehouseRef, CancellationToken ct) =>
        Task.FromResult(cityRef == Kyiv.Ref && warehouseRef == Branch1.Ref);

    public Task ReplaceAllAsync(IReadOnlyList<NovaPoshtaWarehouse> warehouses, CancellationToken ct)
    {
        Replaced = warehouses;
        return Task.CompletedTask;
    }
}
