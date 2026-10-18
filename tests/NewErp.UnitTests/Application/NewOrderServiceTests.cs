using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Orders;
using NewErp.Domain.Common;
using NewErp.Domain.Orders;

namespace NewErp.UnitTests.Application;

public class NewOrderServiceTests
{
    private readonly OrderServiceTests.FakeOrderStore _orders = new();
    private readonly FakeNewOrderStore _store;
    private readonly NewOrderService _service;

    public NewOrderServiceTests()
    {
        _store = new FakeNewOrderStore(_orders);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _orders.Statuses.UnionWith([24, 19]);
        _orders.ActiveUsers.UnionWith([1, 2]);
        _store.Products[5] = new ProductPickDto(5, "Сукня лляна", "SL-01", 1500, 3,
            [new(50, 501, "Розмір", "S", 1, 0), new(50, 502, "Розмір", "M", 2, 1600)]);
        _store.Products[6] = new ProductPickDto(6, "Пояс", "P-01", 200, 10, []);
        _service = new NewOrderService(_store, _orders, new OrderService(_orders, clock, []), clock);
    }

    private static CreateOrderRequest Request(params NewOrderLineRequest[] lines) => new(
        "Тарас", "Шевченко", "+38 067 123 45 67", "", "@taras", 0, 24, null,
        "novaposhta_warehouse.novaposhta_warehouse", "Нова Пошта", "Київ", "Відділення №1", "", "", "",
        "cod", "Накладений платіж", lines, ShippingCost: 0, Discount: 100, Prepayment: 200);

    [Fact]
    public async Task Create_SavesOrderWithProductNames_ThenSetsStatusAndManager()
    {
        var result = await _service.CreateAsync(Request(new NewOrderLineRequest(5, 502, 1, 1600), new(6, null, 2, 200)), userId: 2, CancellationToken.None);

        var saved = Assert.Single(_store.Created);
        Assert.Equal(result.OrderId, _store.LastId);
        Assert.Equal((1900m, 200m), (saved.Draft.Total, saved.Draft.Prepayment));
        Assert.Equal(["Сукня лляна", "Пояс"], saved.Lines.Select(l => l.Name));
        Assert.Equal("M", saved.Lines[0].Option!.Value);
        Assert.Equal((2, 2), _orders.Managers[result.OrderId]); // відповідальний — той, хто створив
        var status = Assert.Single(_orders.SavedChanges);
        Assert.Equal((0, 24), (status.OldStatusId, status.NewStatusId));
        Assert.Equal(CustomerRiskLevel.New, result.Risk.Level);
    }

    [Fact]
    public async Task Create_ProductWithSizes_RequiresSize()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request(new NewOrderLineRequest(5, null, 1, 1500)), 2, CancellationToken.None));

        Assert.Contains("розмір", error.Message);
        Assert.Empty(_store.Created);
    }

    [Theory]
    [InlineData(99, null, "Товару 99")]
    [InlineData(5, 999, "розміру")]
    public async Task Create_UnknownProductOrOption_Fails(int productId, int? optionId, string message)
    {
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(Request(new NewOrderLineRequest(productId, optionId, 1, 1000)), 2, CancellationToken.None));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public async Task Create_UnknownStatusOrInactiveManager_Fails()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(Request(new NewOrderLineRequest(6, null, 1, 200)) with { StatusId = 77 }, 2, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(Request(new NewOrderLineRequest(6, null, 1, 200)) with { ManagerId = 9 }, 2, CancellationToken.None));
        Assert.Empty(_store.Created);
    }

    [Fact]
    public async Task CheckRisk_SearchesByPhoneKey_AndNameOnlyWithBothParts()
    {
        _store.CustomerOrders = [(10, 22), (11, 19)];

        var risk = await _service.CheckRiskAsync(new CustomerRiskQuery("+38 (067) 123-45-67", "Тарас", null, " "), CancellationToken.None);

        Assert.Equal(("671234567", null, null, null), _store.LastRiskQuery);
        Assert.Equal((CustomerRiskLevel.Red, 1, 50m), (risk.Level, risk.CancelledOrders, risk.SuspicionRate));
        Assert.Equal(new DateTime(2022, 10, 18, 9, 0, 0), _store.LastSince);
    }

    [Fact]
    public async Task CheckRisk_NothingToSearchBy_IsNewWithoutQuery()
    {
        var risk = await _service.CheckRiskAsync(new CustomerRiskQuery("123"), CancellationToken.None);

        Assert.Equal(CustomerRiskLevel.New, risk.Level);
        Assert.Null(_store.LastRiskQuery);
    }

    private sealed class FakeNewOrderStore(OrderServiceTests.FakeOrderStore orders) : INewOrderStore
    {
        public Dictionary<int, ProductPickDto> Products { get; } = [];
        public List<NewOrderRecord> Created { get; } = [];
        public int LastId { get; private set; } = 500;
        public IReadOnlyList<(int, int)> CustomerOrders { get; set; } = [];
        public (string?, string?, string?, string?)? LastRiskQuery { get; private set; }
        public DateTime? LastSince { get; private set; }

        public Task<IReadOnlyList<ProductPickDto>> SearchProductsAsync(string term, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProductPickDto>>(Products.Values.Where(p => p.Name.Contains(term)).ToList());

        public Task<IReadOnlyList<ProductPickDto>> GetProductsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProductPickDto>>(productIds.Where(Products.ContainsKey).Select(id => Products[id]).ToList());

        public Task<IReadOnlyList<CustomerPickDto>> SearchCustomersAsync(string term, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CustomerPickDto>>([]);

        public Task<IReadOnlyList<(int OrderId, int StatusId)>> FindCustomerOrdersAsync(
            string? phoneKey, string? firstname, string? lastname, string? email, DateTime since, CancellationToken ct)
        {
            LastRiskQuery = (phoneKey, firstname, lastname, email);
            LastSince = since;
            return Task.FromResult(CustomerOrders);
        }

        public Task<OrderMethodsDto> ListMethodsAsync(CancellationToken ct) => Task.FromResult(new OrderMethodsDto([], []));

        public Task<bool> SourceExistsAsync(int sourceId, CancellationToken ct) => Task.FromResult(sourceId == 1);

        public Task<int> CreateAsync(NewOrderRecord order, CancellationToken ct)
        {
            Created.Add(order);
            orders.Orders[++LastId] = 0; // замовлення без статусу, як пише справжнє сховище
            return Task.FromResult(LastId);
        }
    }
}
