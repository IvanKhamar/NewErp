using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

/// <summary>Сценарії зміни замовлення: статус і відповідальний менеджер.</summary>
public sealed class OrderService(IOrderStore store, TimeProvider clock)
{
    public async Task ChangeStatusAsync(int orderId, ChangeOrderStatusRequest request, int userId, CancellationToken ct)
    {
        var currentStatusId = await store.GetStatusIdAsync(orderId, ct)
            ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");

        if (!await store.StatusExistsAsync(request.StatusId, ct))
            throw new DomainException($"Статусу {request.StatusId} не існує.");
        await RequireActiveUserAsync(userId, ct);

        var change = OrderStatusChange.Create(orderId, currentStatusId, request.StatusId, userId, Now());
        await store.SaveStatusChangeAsync(change, ct);
    }

    public async Task AssignManagerAsync(int orderId, AssignManagerRequest request, int userId, CancellationToken ct)
    {
        if (await store.GetStatusIdAsync(orderId, ct) is null)
            throw new NotFoundException($"Замовлення {orderId} не знайдено.");

        await RequireActiveUserAsync(request.ManagerId, ct);
        await RequireActiveUserAsync(userId, ct);

        await store.SaveManagerAsync(orderId, request.ManagerId, userId, Now(), ct);
    }

    private async Task RequireActiveUserAsync(int userId, CancellationToken ct)
    {
        if (!await store.IsActiveUserAsync(userId, ct))
            throw new DomainException($"Користувача {userId} не знайдено або його вимкнено.");
    }

    private DateTime Now() => clock.GetLocalNow().DateTime;
}