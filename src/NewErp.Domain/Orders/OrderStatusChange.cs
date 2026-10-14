using NewErp.Domain.Common;

namespace NewErp.Domain.Orders;

/// <summary>Зміна статусу замовлення, яку треба записати в журнал.</summary>
public sealed record OrderStatusChange(int OrderId, int OldStatusId, int NewStatusId, int UserId, DateTime ChangedAt)
{
    public static OrderStatusChange Create(int orderId, int currentStatusId, int newStatusId, int userId, DateTime now)
    {
        if (newStatusId <= 0)
            throw new DomainException("Статус має бути додатним числом.");
        if (newStatusId == currentStatusId)
            throw new DomainException("Замовлення вже має цей статус.");
        if (userId <= 0)
            throw new DomainException("Не вказано користувача, який змінює статус.");

        return new OrderStatusChange(orderId, currentStatusId, newStatusId, userId, now);
    }
}