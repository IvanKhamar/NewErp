using NewErp.Application.Common;
using NewErp.Domain.Common;

namespace NewErp.Application.Orders;

/// <param name="RewardPoints">Сума балів рядків замовлення, як рахує OpenCart.</param>
public sealed record OrderCredits(int CustomerId, int RewardPoints, bool RewardAdded, int AffiliateId, decimal Commission, bool CommissionAdded);

/// <summary>
/// Бонусні бали клієнта (dd_customer_reward) і комісія партнера (dd_customer_transaction: в OpenCart 3 партнер — це клієнт,
/// а комісія — транзакція на його балансі).
/// </summary>
public interface IOrderCreditStore
{
    Task<OrderCredits?> GetAsync(int orderId, CancellationToken ct);

    Task AddRewardAsync(int customerId, int orderId, int points, string description, DateTime now, CancellationToken ct);

    /// <summary>Лише бали, які дало замовлення (додатні рядки); витрачені на нього лишаються.</summary>
    Task RemoveRewardAsync(int orderId, CancellationToken ct);

    Task AddCommissionAsync(int affiliateId, int orderId, decimal amount, string description, DateTime now, CancellationToken ct);

    Task RemoveCommissionAsync(int orderId, CancellationToken ct);
}

/// <summary>
/// Бонусні бали замовлення й комісія партнера, нараховані й зняті вручну, як кнопки сторінки замовлення старої адмінки
/// (пункт 40). Рухаються гроші, тож кожна зміна йде в журнал аудиту.
/// </summary>
public sealed class OrderCreditService(IOrderCreditStore store, TimeProvider clock, IAuditLog audit)
{
    public async Task<OrderCreditsDto> GetAsync(int orderId, CancellationToken ct)
    {
        var c = await CreditsAsync(orderId, ct);
        return new OrderCreditsDto(orderId, c.CustomerId, c.RewardPoints, c.RewardAdded, c.AffiliateId, c.Commission, c.CommissionAdded);
    }

    /// <summary>Повторне натискання нічого не додає, як на старому сайті.</summary>
    public async Task<OrderCreditsDto> AddRewardAsync(int orderId, int userId, CancellationToken ct)
    {
        var c = await CreditsAsync(orderId, ct);
        if (c.CustomerId == 0)
            throw new DomainException("Замовлення оформлене без облікового запису клієнта: бонуси нікому нарахувати.");
        if (c.RewardPoints <= 0)
            throw new DomainException("Товари замовлення не дають бонусних балів.");
        if (!c.RewardAdded)
        {
            await store.AddRewardAsync(c.CustomerId, orderId, c.RewardPoints, $"Замовлення №{orderId}", Now(), ct);
            audit.Record($"reward added: order {orderId}, customer {c.CustomerId}, points {c.RewardPoints}, by user {userId}");
        }

        return await GetAsync(orderId, ct);
    }

    public async Task<OrderCreditsDto> RemoveRewardAsync(int orderId, int userId, CancellationToken ct)
    {
        var c = await CreditsAsync(orderId, ct);
        if (c.RewardAdded)
        {
            await store.RemoveRewardAsync(orderId, ct);
            audit.Record($"reward removed: order {orderId}, customer {c.CustomerId}, by user {userId}");
        }

        return await GetAsync(orderId, ct);
    }

    public async Task<OrderCreditsDto> AddCommissionAsync(int orderId, int userId, CancellationToken ct)
    {
        var c = await CreditsAsync(orderId, ct);
        if (c.AffiliateId == 0)
            throw new DomainException("Замовлення прийшло не від партнера: комісію нікому нарахувати.");
        if (c.Commission <= 0)
            throw new DomainException("Комісія замовлення нульова.");
        if (!c.CommissionAdded)
        {
            await store.AddCommissionAsync(c.AffiliateId, orderId, c.Commission, $"Замовлення №{orderId}", Now(), ct);
            audit.Record($"commission added: order {orderId}, affiliate {c.AffiliateId}, amount {c.Commission}, by user {userId}");
        }

        return await GetAsync(orderId, ct);
    }

    public async Task<OrderCreditsDto> RemoveCommissionAsync(int orderId, int userId, CancellationToken ct)
    {
        var c = await CreditsAsync(orderId, ct);
        if (c.CommissionAdded)
        {
            await store.RemoveCommissionAsync(orderId, ct);
            audit.Record($"commission removed: order {orderId}, affiliate {c.AffiliateId}, by user {userId}");
        }

        return await GetAsync(orderId, ct);
    }

    private async Task<OrderCredits> CreditsAsync(int orderId, CancellationToken ct) =>
        await store.GetAsync(orderId, ct) ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");

    private DateTime Now() => clock.GetLocalNow().DateTime;
}
