using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Orders;
using NewErp.Domain.Common;
using NewErp.Domain.Orders;

namespace NewErp.UnitTests.Application;

public class OrderServiceTests
{
    private readonly FakeOrderStore _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _store.Orders[100] = 1;
        _store.Statuses.UnionWith([1, 5, 7]);
        _store.ActiveUsers.UnionWith([1, 2]);
        _handler = new FakeStatusHandler(_store);
        _service = new OrderService(_store, _clock, [_handler]);
    }

    private readonly FakeStatusHandler _handler;

    [Fact]
    public async Task ChangeStatus_RunsHandlersBeforeSavingAndReturnsTheirWarnings()
    {
        _handler.Warnings = ["Бракує 2 м: Льон"];

        var result = await _service.ChangeStatusAsync(100, new ChangeOrderStatusRequest(StatusId: 5), userId: 1, CancellationToken.None);

        Assert.Equal(["Бракує 2 м: Льон"], result.Warnings);
        Assert.Equal(0, _handler.SavedChangesSeen); // обробник працював ще до збереження статусу
        Assert.Single(_store.SavedChanges);
    }

    [Fact]
    public async Task ChangeStatus_HandlerFails_StatusNotSaved()
    {
        _handler.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ChangeStatusAsync(100, new ChangeOrderStatusRequest(StatusId: 5), userId: 1, CancellationToken.None));
        Assert.Empty(_store.SavedChanges);
    }

    private sealed class FakeStatusHandler(FakeOrderStore store) : IOrderStatusHandler
    {
        public IReadOnlyList<string> Warnings { get; set; } = [];

        public bool Fail { get; set; }

        /// <summary>Скільки змін статусу вже було збережено в момент виклику обробника.</summary>
        public int SavedChangesSeen { get; private set; } = -1;

        public Task<IReadOnlyList<string>> OnStatusChangingAsync(OrderStatusChange change, CancellationToken ct)
        {
            if (Fail)
                throw new InvalidOperationException("Збій обробника");
            SavedChangesSeen = store.SavedChanges.Count;
            return Task.FromResult(Warnings);
        }
    }

    [Fact]
    public async Task ChangeStatus_SavesChangeWithOldStatusAndTime()
    {
        await _service.ChangeStatusAsync(100, new ChangeOrderStatusRequest(StatusId: 5), userId: 1, CancellationToken.None);

        var saved = Assert.Single(_store.SavedChanges);
        Assert.Equal(new OrderStatusChange(100, 1, 5, 1, new DateTime(2026, 10, 14, 9, 0, 0)), saved);
    }

    [Fact]
    public async Task ChangeStatus_UnknownOrder_NotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeStatusAsync(999, new ChangeOrderStatusRequest(5), userId: 1, CancellationToken.None));

    [Fact]
    public async Task ChangeStatus_UnknownStatus_Rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.ChangeStatusAsync(100, new ChangeOrderStatusRequest(42), userId: 1, CancellationToken.None));
        Assert.Empty(_store.SavedChanges);
    }

    [Fact]
    public async Task ChangeStatus_InactiveUser_Rejected() =>
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.ChangeStatusAsync(100, new ChangeOrderStatusRequest(5), userId: 3, CancellationToken.None));

    [Fact]
    public async Task AssignManager_SavesManager()
    {
        await _service.AssignManagerAsync(100, new AssignManagerRequest(ManagerId: 2), userId: 1, CancellationToken.None);

        Assert.Equal((2, 1), _store.Managers[100]);
    }

    [Fact]
    public async Task AssignManager_InactiveManager_Rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.AssignManagerAsync(100, new AssignManagerRequest(ManagerId: 3), userId: 1, CancellationToken.None));
        Assert.Empty(_store.Managers);
    }

    /// <summary>Підробка (fake) сховища в пам'яті: тест перевіряє логіку сервісу без бази даних.</summary>
    internal sealed class FakeOrderStore : IOrderStore
    {
        public Dictionary<int, int> Orders { get; } = [];
        public HashSet<int> Statuses { get; } = [];
        public HashSet<int> ActiveUsers { get; } = [];
        public List<OrderStatusChange> SavedChanges { get; } = [];
        public Dictionary<int, (int ManagerId, int ByUserId)> Managers { get; } = [];

        public Task<int?> GetStatusIdAsync(int orderId, CancellationToken ct) =>
            Task.FromResult(Orders.TryGetValue(orderId, out var s) ? s : (int?)null);

        public Task<bool> StatusExistsAsync(int statusId, CancellationToken ct) => Task.FromResult(Statuses.Contains(statusId));

        public Task<bool> IsActiveUserAsync(int userId, CancellationToken ct) => Task.FromResult(ActiveUsers.Contains(userId));

        public Task SaveStatusChangeAsync(OrderStatusChange change, CancellationToken ct)
        {
            SavedChanges.Add(change);
            Orders[change.OrderId] = change.NewStatusId;
            return Task.CompletedTask;
        }

        public Task SaveManagerAsync(int orderId, int managerId, int assignedByUserId, DateTime assignedAt, CancellationToken ct)
        {
            Managers[orderId] = (managerId, assignedByUserId);
            return Task.CompletedTask;
        }
    }
}