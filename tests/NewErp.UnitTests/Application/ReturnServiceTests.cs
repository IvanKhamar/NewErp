using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Returns;
using NewErp.Domain.Common;
using NewErp.Domain.Returns;

namespace NewErp.UnitTests.Application;

public class ReturnServiceTests
{
    private readonly FakeReturnStore _store = new();
    private readonly ReturnService _service;

    public ReturnServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new ReturnService(_store, clock);
        _store.Orders[7] = new ReturnSourceOrder(7, "Тарас", "Шевченко", "0671234567", "", 0, new DateTime(2026, 10, 1),
            [new(101, "Сукня", "SL-01", 2)], new Dictionary<int, int> { [101] = 1 });
    }

    [Fact]
    public async Task Create_TakesCustomerAndProductFromOrder_CountsAlreadyReturned()
    {
        var id = await _service.CreateAsync(new CreateReturnRequest(7, 101, 1, ReasonId: 1, StatusId: 1, Comment: " розмір "), default);

        var record = Assert.Single(_store.Created);
        Assert.Equal((55, "Сукня", "Тарас", new DateTime(2026, 10, 19, 9, 0, 0)), (id, record.Line.Name, record.Order.Firstname, record.Now));
        Assert.Contains("від 1 до 1", (await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(new CreateReturnRequest(7, 101, 2, 1, 1), default))).Message);
    }

    [Fact]
    public async Task Create_UnknownOrderStatusReasonOrAction_Fails()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(new CreateReturnRequest(8, 101, 1, 1, 1), default));
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(new CreateReturnRequest(7, 101, 1, 1, 9), default));
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(new CreateReturnRequest(7, 101, 1, 9, 1), default));
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(new CreateReturnRequest(7, 101, 1, 1, 1, ActionId: 9), default));
        Assert.Empty(_store.Created);
    }

    [Fact]
    public async Task AddHistory_ChecksStatusAndAction_TrimsComment_NotFoundForMissingReturn()
    {
        await _service.AddHistoryAsync(55, new AddReturnHistoryRequest(2, "  отримали  ", 1), default);
        Assert.Equal((55, 2, "отримали", (int?)1), _store.History.Single());

        await Assert.ThrowsAsync<DomainException>(() => _service.AddHistoryAsync(55, new AddReturnHistoryRequest(9), default));
        await Assert.ThrowsAsync<DomainException>(() => _service.AddHistoryAsync(55, new AddReturnHistoryRequest(2, null, 9), default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.AddHistoryAsync(56, new AddReturnHistoryRequest(2), default));
    }

    private sealed class FakeReturnStore : IReturnStore
    {
        public Dictionary<int, ReturnSourceOrder> Orders { get; } = [];
        public List<NewReturnRecord> Created { get; } = [];
        public List<(int, int, string, int?)> History { get; } = [];

        public Task<PagedResult<ReturnListItem>> ListAsync(ReturnFilter filter, CancellationToken ct) => throw new NotSupportedException();

        public Task<ReturnDetails?> GetAsync(int returnId, CancellationToken ct) => Task.FromResult<ReturnDetails?>(null);

        public Task<ReturnLookupsDto> LookupsAsync(CancellationToken ct) => Task.FromResult(new ReturnLookupsDto(
            [new(1, "Очікує"), new(2, "Отримано")], [new(1, "Не підійшов розмір")], [new(1, "Повернено кошти")]));

        public Task<ReturnSourceOrder?> GetSourceOrderAsync(int orderId, CancellationToken ct) => Task.FromResult(Orders.GetValueOrDefault(orderId));

        public Task<int> CreateAsync(NewReturnRecord record, CancellationToken ct)
        {
            Created.Add(record);
            return Task.FromResult(55);
        }

        public Task<bool> AddHistoryAsync(int returnId, int statusId, string comment, int? actionId, DateTime now, CancellationToken ct)
        {
            if (returnId != 55)
                return Task.FromResult(false);
            History.Add((returnId, statusId, comment, actionId));
            return Task.FromResult(true);
        }
    }
}
