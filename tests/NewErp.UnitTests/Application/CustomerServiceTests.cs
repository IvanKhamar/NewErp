using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Customers;

namespace NewErp.UnitTests.Application;

public class CustomerServiceTests
{
    private readonly FakeCustomerStore _store = new();
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new CustomerService(_store, clock);
        _store.Customers[5] = new CustomerProfile(5, "Олена", "Коваль", "", "+38 (067) 123-45-67", "Роздріб", true, false,
            new DateTime(2025, 1, 1), [], []);
        _store.Orders = [new(10, new DateTime(2026, 1, 1), 22, "Відправлено", 1200m, "UAH", false), new(11, new DateTime(2026, 2, 1), 19, "Відмінено", 500m, "UAH", true)];
    }

    [Fact]
    public async Task Get_SearchesOrdersByPhoneKey_AndComputesStatsAndRisk()
    {
        var details = await _service.GetAsync(5, default);

        Assert.Equal("671234567", _store.PhoneKey);
        Assert.Equal((2, 1, 1200m, "Red"), (details.Stats.OrderCount, details.Stats.CancelledCount, details.Stats.Spent, details.Stats.RiskLevel));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(6, default));
    }

    [Fact]
    public async Task AddNote_TrimsAndStampsTime_UnknownCustomerIsNotFound()
    {
        await _service.AddNoteAsync(5, new AddCustomerNoteRequest(" Постійна клієнтка "), default);

        Assert.Equal((5, "Постійна клієнтка", new DateTime(2026, 10, 19, 9, 0, 0)), Assert.Single(_store.Notes));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.AddNoteAsync(6, new AddCustomerNoteRequest("x"), default));
    }

    [Fact]
    public async Task List_NormalizesPagingAndSearch()
    {
        await _service.ListAsync(new CustomerFilter(" Коваль ", Page: 0, PageSize: 1000), default);

        Assert.Equal("Коваль", _store.LastFilter!.Search);
        Assert.True(_store.LastFilter.Page >= 1 && _store.LastFilter.PageSize <= 100);
    }

    private sealed class FakeCustomerStore : ICustomerStore
    {
        public Dictionary<int, CustomerProfile> Customers { get; } = [];
        public IReadOnlyList<CustomerOrderItem> Orders { get; set; } = [];
        public List<(int, string, DateTime)> Notes { get; } = [];
        public string? PhoneKey { get; private set; }
        public CustomerFilter? LastFilter { get; private set; }

        public Task<PagedResult<CustomerListItem>> ListAsync(CustomerFilter filter, CancellationToken ct)
        {
            LastFilter = filter;
            return Task.FromResult(new PagedResult<CustomerListItem>([], filter.Page, filter.PageSize, 0));
        }

        public Task<IReadOnlyList<CustomerGroupDto>> GroupsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CustomerGroupDto>>([]);

        public Task<CustomerProfile?> GetAsync(int customerId, CancellationToken ct) => Task.FromResult(Customers.GetValueOrDefault(customerId));

        public Task<IReadOnlyList<CustomerOrderItem>> OrdersAsync(int customerId, string? phoneKey, CancellationToken ct)
        {
            PhoneKey = phoneKey;
            return Task.FromResult(Orders);
        }

        public Task<bool> AddNoteAsync(int customerId, string comment, DateTime now, CancellationToken ct)
        {
            if (!Customers.ContainsKey(customerId))
                return Task.FromResult(false);
            Notes.Add((customerId, comment, now));
            return Task.FromResult(true);
        }
    }
}
