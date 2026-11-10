using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

/// <summary>Сценарії зміни замовлення: статус і відповідальний менеджер.</summary>
public sealed class OrderService(IOrderStore store, TimeProvider clock, IEnumerable<IOrderStatusHandler> handlers, IAuditLog audit)
{
    public async Task<ChangeOrderStatusResult> ChangeStatusAsync(int orderId, ChangeOrderStatusRequest request, int userId, CancellationToken ct)
    {
        var currentStatusId = await store.GetStatusIdAsync(orderId, ct)
            ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");

        if (!await store.StatusExistsAsync(request.StatusId, ct))
            throw new DomainException($"Статусу {request.StatusId} не існує.");
        await RequireActiveUserAsync(userId, ct);

        var change = OrderStatusChange.Create(orderId, currentStatusId, request.StatusId, userId, Now());

        // Обробники (наприклад, списання розхідників) працюють до збереження статусу: якщо котрийсь упаде,
        // статус лишиться старим і дію можна просто повторити. Повторне списання вони відсікають самі.
        var warnings = new List<string>();
        foreach (var handler in handlers)
            warnings.AddRange(await handler.OnStatusChangingAsync(change, ct));

        await store.SaveStatusChangeAsync(change, ct);
        return new ChangeOrderStatusResult(warnings);
    }

    /// <summary>
    /// Змінює склад замовлення так, щоб інші модулі (склад виробів) це врахували. Як editOrder старого сайту:
    /// спершу замовлення ніби виходить зі свого статусу (обробники повертають те, що воно тримало), потім
    /// змінюються рядки, і замовлення ніби входить у статус знову (обробники беруть уже за новим складом).
    /// Статус і журнал статусів при цьому не змінюються.
    /// </summary>
    public async Task<IReadOnlyList<string>> ReapplyStatusAsync(int orderId, int statusId, Func<Task> change, int userId, CancellationToken ct)
    {
        if (statusId <= 0)
        {
            await change();
            return [];
        }

        var now = Now();
        foreach (var handler in handlers)
            await handler.OnStatusChangingAsync(new OrderStatusChange(orderId, statusId, 0, userId, now), ct);
        await change();
        var warnings = new List<string>();
        foreach (var handler in handlers)
            warnings.AddRange(await handler.OnStatusChangingAsync(new OrderStatusChange(orderId, 0, statusId, userId, now), ct));
        return warnings;
    }

    public async Task AssignManagerAsync(int orderId, AssignManagerRequest request, int userId, CancellationToken ct)
    {
        if (await store.GetStatusIdAsync(orderId, ct) is null)
            throw new NotFoundException($"Замовлення {orderId} не знайдено.");

        await RequireActiveUserAsync(request.ManagerId, ct);
        await RequireActiveUserAsync(userId, ct);

        await store.SaveManagerAsync(orderId, request.ManagerId, userId, Now(), ct);
    }

    public async Task SetShippingDateAsync(int orderId, ShippingDateRequest request, CancellationToken ct)
    {
        await RequireOrderAsync(orderId, ct);
        await store.SaveShippingDateAsync(orderId, request.DateShipping, ct);
    }

    /// <summary>Номер рахунку: наступний за префіксом замовлення, один раз; замовлення, що вже має номер, його зберігає.</summary>
    public async Task<InvoiceDto> CreateInvoiceAsync(int orderId, CancellationToken ct)
    {
        await RequireOrderAsync(orderId, ct);
        return new InvoiceDto(await store.CreateInvoiceNoAsync(orderId, ct));
    }

    /// <summary>
    /// Видаляє замовлення, на яке ніщо не посилається. Як на старому сайті, воно спершу виходить зі свого статусу,
    /// тож обробники повертають те, що воно тримало (пошиті вироби, розхідники); їхні попередження повертаються.
    /// </summary>
    public async Task<ChangeOrderStatusResult> DeleteAsync(int orderId, int userId, CancellationToken ct)
    {
        var statusId = await RequireOrderAsync(orderId, ct);
        await RequireActiveUserAsync(userId, ct);
        var blockers = await store.DeleteBlockersAsync(orderId, ct);
        if (blockers.Count > 0)
            throw new DomainException("Замовлення не можна видалити: " + string.Join("; ", blockers) + ". Скасуйте його статусом.");

        var warnings = new List<string>();
        if (statusId > 0)
        {
            var leave = new OrderStatusChange(orderId, statusId, 0, userId, Now());
            foreach (var handler in handlers)
                warnings.AddRange(await handler.OnStatusChangingAsync(leave, ct));
        }

        await store.DeleteAsync(orderId, ct);
        audit.Record($"order deleted: order {orderId}, status {statusId}, by user {userId}");
        return new ChangeOrderStatusResult(warnings);
    }

    private async Task<int> RequireOrderAsync(int orderId, CancellationToken ct) =>
        await store.GetStatusIdAsync(orderId, ct) ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");

    private async Task RequireActiveUserAsync(int userId, CancellationToken ct)
    {
        if (!await store.IsActiveUserAsync(userId, ct))
            throw new DomainException($"Користувача {userId} не знайдено або його вимкнено.");
    }

    private DateTime Now() => clock.GetLocalNow().DateTime;
}