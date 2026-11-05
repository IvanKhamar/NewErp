using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Delivery;
using NewErp.Domain.Common;
using NewErp.Domain.Delivery;

namespace NewErp.UnitTests.Application;

public class ShipmentServiceTests
{
    private readonly FakeShipmentRepository _repository = new();
    private readonly FakeWarehouseDirectory _directory = new();
    private readonly FakeNovaPoshtaClient _client = new();
    private readonly ShipmentService _service;

    public ShipmentServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _repository.Orders[1] = new OrderShippingInfo("Тарас", "Шевченко", "+38 067 123 45 67", "Київ", FakeWarehouseDirectory.Branch1.Name, 1500, "cod");
        _repository.Orders[2] = new OrderShippingInfo("Леся", "Українка", "0671234567", "Ковель", "Відділення №3", 900, "bank_transfer");
        _service = new ShipmentService(_repository, _directory, _client, clock);
    }

    private static CreateShipmentRequest Request(
        string phone = "+38 067 123 45 67", string warehouseRef = "wh-1", decimal cashOnDelivery = 1500, string? senderId = null) =>
        new("Тарас", "Шевчен`ко", "", phone, FakeWarehouseDirectory.Kyiv.Ref, warehouseRef, 1500, cashOnDelivery, false,
            [SeatPreset.All[0].Seat], senderId);

    [Fact]
    public async Task Form_TakesRecipientFromOrder_AndFindsCityAndBranchInDirectory()
    {
        var form = await _service.FormAsync(1, CancellationToken.None);

        Assert.Equal(("Тарас", "Шевченко"), (form.FirstName, form.LastName));
        Assert.Equal(FakeWarehouseDirectory.Kyiv, form.City);
        Assert.Equal(FakeWarehouseDirectory.Branch1, form.Warehouse);
        Assert.Equal((1500m, 1500m), (form.DeclaredCost, form.CashOnDelivery)); // оплата при отриманні — накладений платіж
        Assert.Equal(3, form.SeatPresets.Count);
        Assert.False(form.TestMode);
    }

    [Fact]
    public async Task Form_CashOnDelivery_MinusPrepayment()
    {
        _repository.Orders[1] = _repository.Orders[1] with { Prepayment = 300 };

        var form = await _service.FormAsync(1, CancellationToken.None);

        Assert.Equal((1500m, 1200m), (form.DeclaredCost, form.CashOnDelivery));
    }

    [Fact]
    public async Task Form_UnknownCity_LeavesTextOnly_AndNoCashOnDeliveryForPrepaid()
    {
        var form = await _service.FormAsync(2, CancellationToken.None);

        Assert.Null(form.City);
        Assert.Equal(("Ковель", "Відділення №3"), (form.CityName, form.WarehouseName));
        Assert.Equal(0, form.CashOnDelivery);
    }

    [Fact]
    public async Task Create_SendsNormalizedDataWithDefaultSender_AndSavesDocumentWithRef()
    {
        var shipment = await _service.CreateAsync(1, Request(), userId: 7, CancellationToken.None);

        var sent = _client.Created!;
        Assert.Equal(("fop-b", "kyiv"), (sent.SenderId, sent.DropoffPointId)); // типові з налаштувань
        Assert.Equal("380671234567", sent.Phone);
        Assert.Equal("Шевчен'ко", sent.LastName); // «`» Нова Пошта не приймає
        Assert.Equal(new DateOnly(2026, 10, 16), sent.ShipDate);
        Assert.Equal("20450000009999", shipment.Number);
        var stored = Assert.Single(_repository.Stored);
        Assert.Equal(("doc-ref", "fop-b"), (stored.Document.Ref, stored.SenderId));
        Assert.True(_repository.Saved);
    }

    private static CreateShipmentRequest ToDoor(string? street = "street-1", string? building = "22", string? flat = "5") =>
        Request(warehouseRef: "") with { ToDoor = true, StreetRef = street, Building = building, Flat = flat };

    [Fact]
    public async Task Create_ToTheDoor_NeedsNoBranch_AndPassesTheAddress()
    {
        await _service.CreateAsync(1, ToDoor(building: " 22 "), 7, CancellationToken.None);

        var sent = _client.Created!;
        Assert.Equal((true, "street-1", "22", "5", ""), (sent.ToDoor, sent.StreetRef, sent.Building, sent.Flat, sent.WarehouseRef));
    }

    [Theory]
    [InlineData(null, "22", "", "Для доставки на адресу оберіть вулицю з довідника.")]
    [InlineData("street-1", " ", "", "Для доставки на адресу вкажіть номер будинку.")]
    [InlineData("street-1", "12345678901", "", "Номер будинку чи квартири задовгий.")]
    [InlineData("street-1", "22", "12345678901", "Номер будинку чи квартири задовгий.")]
    public async Task Create_ToTheDoor_BadAddress_IsRefused(string? street, string? building, string? flat, string message)
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(1, ToDoor(street, building, flat), 7, CancellationToken.None));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Create_ToTheDoor_NeedsACityOfTheDirectory()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(1, ToDoor() with { CityRef = "unknown" }, 7, CancellationToken.None));

        Assert.Equal("Оберіть місто з довідника.", error.Message);
    }

    [Fact]
    public async Task Create_ToABranch_IgnoresTheAddressFields()
    {
        await _service.CreateAsync(1, Request() with { StreetRef = "street-1", Building = "22" }, 7, CancellationToken.None);

        Assert.Equal((false, "", "wh-1"), (_client.Created!.ToDoor, _client.Created.StreetRef, _client.Created.WarehouseRef));
    }

    [Theory]
    [InlineData("city-kyiv", "Хр", 1)]
    [InlineData("city-kyiv", " Х ", 0)] // від двох літер
    [InlineData("unknown", "Хр", 0)] // лише місто з довідника
    [InlineData(null, "Хр", 0)]
    public async Task SearchStreets_AsksTheApiOnlyWhenItIsWorthIt(string? cityRef, string search, int found) =>
        Assert.Equal(found, (await _service.SearchStreetsAsync(cityRef, search, CancellationToken.None)).Count);

    [Fact]
    public async Task Create_ChosenSender_IsUsed()
    {
        await _service.CreateAsync(1, Request(senderId: "fop-a"), 7, CancellationToken.None);

        Assert.Equal("fop-a", _client.Created!.SenderId);
    }

    [Theory]
    [InlineData("123", "wh-1", 0, "fop-a")]        // телефон
    [InlineData("0671234567", "wh-other", 0, "fop-a")] // відділення не з цього міста
    [InlineData("0671234567", "wh-1", -1, "fop-a")]    // від'ємний накладений платіж
    [InlineData("0671234567", "wh-1", 0, "nobody")]    // невідомий відправник
    public async Task Create_InvalidRequest_NothingSent(string phone, string warehouseRef, decimal cod, string senderId)
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(1, Request(phone, warehouseRef, cod, senderId), 7, CancellationToken.None));

        Assert.Null(_client.Created);
        Assert.Empty(_repository.Stored);
    }

    [Fact]
    public async Task Create_NoSeats_Rejected() =>
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(1, Request() with { Seats = [] }, 7, CancellationToken.None));

    [Fact]
    public async Task Create_SecondShipment_Rejected()
    {
        await _service.CreateAsync(1, Request(), 7, CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(1, Request(), 7, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithoutKey_Rejected()
    {
        _client.CanCreateDocuments = false;

        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(1, Request(), 7, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_UsesSenderThatCreatedIt_ThenRemovesLocally()
    {
        _repository.Stored.Add(new StoredShipment(new ShipmentDocument(1, "20450000000001", 70, "ref-1"), "fop-a"));

        await _service.DeleteAsync(1, CancellationToken.None);

        Assert.Equal([("fop-a", "ref-1")], _client.Deleted);
        Assert.Single(_repository.DeletedIds);
    }

    [Fact]
    public async Task Delete_OldSiteDocument_UsesDefaultSender()
    {
        _repository.Stored.Add(new StoredShipment(new ShipmentDocument(1, "20450000000001", 70, "ref-1"), null));

        await _service.DeleteAsync(1, CancellationToken.None);

        Assert.Equal("fop-b", Assert.Single(_client.Deleted).SenderId);
    }

    [Fact]
    public async Task Delete_ReceivedParcel_Rejected()
    {
        var document = new ShipmentDocument(1, "20450000000001", 70, "ref-1");
        document.ApplyTracking(new TrackingInfo(document.Number, "Отримано", 9, "", false, null, null, null));
        _repository.Stored.Add(new StoredShipment(document, "fop-a"));

        await Assert.ThrowsAsync<DomainException>(() => _service.DeleteAsync(1, CancellationToken.None));
        Assert.Empty(_client.Deleted);
    }

    [Fact]
    public async Task Print_NoShipment_NotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.PrintAsync(1, ShipmentPrintKind.Document, CancellationToken.None));

    [Fact]
    public async Task RefreshWarehouses_EmptyAnswer_KeepsDirectory()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.RefreshWarehousesAsync(CancellationToken.None));

        Assert.Null(_directory.Replaced);
    }

    [Fact]
    public async Task RefreshWarehouses_ReplacesDirectory()
    {
        _client.Warehouses.Add(new NovaPoshtaWarehouse("wh-9", "Поштомат №9", "Postomat", "city-kyiv", "Київ", 20, working: true));

        var result = await _service.RefreshWarehousesAsync(CancellationToken.None);

        Assert.Equal(new WarehouseRefreshResult(true, 1), result);
        Assert.Single(_directory.Replaced!);
    }
}
