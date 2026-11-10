using Microsoft.EntityFrameworkCore;

using NewErp.Application.Orders;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Orders;

/// <summary>Бонусні бали замовлення (dd_customer_reward) і комісія партнера (dd_customer_transaction).</summary>
public sealed class OrderCreditStore(ErpDbContext db) : IOrderCreditStore
{
    public async Task<OrderCredits?> GetAsync(int orderId, CancellationToken ct)
    {
        var order = await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new { o.CustomerId, o.AffiliateId, o.Commission })
            .FirstOrDefaultAsync(ct);
        if (order is null)
            return null;

        var points = await db.DdOrderProducts.Where(p => p.OrderId == orderId).SumAsync(p => (int?)p.Reward, ct) ?? 0;
        var rewardAdded = await db.CustomerRewards.AnyAsync(r => r.OrderId == orderId && r.Points > 0, ct);
        var commissionAdded = await db.CustomerTransactions.AnyAsync(t => t.OrderId == orderId, ct);
        return new OrderCredits(order.CustomerId, points, rewardAdded, order.AffiliateId, order.Commission, commissionAdded);
    }

    public async Task AddRewardAsync(int customerId, int orderId, int points, string description, DateTime now, CancellationToken ct)
    {
        db.CustomerRewards.Add(new CustomerReward { CustomerId = customerId, OrderId = orderId, Points = points, Description = description, DateAdded = now });
        await db.SaveChangesAsync(ct);
    }

    public Task RemoveRewardAsync(int orderId, CancellationToken ct) =>
        db.CustomerRewards.Where(r => r.OrderId == orderId && r.Points > 0).ExecuteDeleteAsync(ct);

    public async Task AddCommissionAsync(int affiliateId, int orderId, decimal amount, string description, DateTime now, CancellationToken ct)
    {
        db.CustomerTransactions.Add(new CustomerTransaction { CustomerId = affiliateId, OrderId = orderId, Amount = amount, Description = description, DateAdded = now });
        await db.SaveChangesAsync(ct);
    }

    public Task RemoveCommissionAsync(int orderId, CancellationToken ct) =>
        db.CustomerTransactions.Where(t => t.OrderId == orderId).ExecuteDeleteAsync(ct);
}
