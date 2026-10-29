using NewErp.Application.Common;
using NewErp.Domain.Marketing;

namespace NewErp.Application.Marketing;

/// <summary>Сертифікати OpenCart: dd_voucher, його списання в dd_voucher_history і картинки в dd_voucher_theme.</summary>
public interface IVoucherStore
{
    /// <summary>Новіші вгорі; search шукає частину коду або імені отримувача чи відправника.</summary>
    Task<IReadOnlyList<VoucherRow>> ListAsync(string search, CancellationToken ct);

    Task<VoucherRow?> FindAsync(int id, CancellationToken ct);

    /// <summary>Найновіший, якщо код повторюється (OpenCart цього не забороняє).</summary>
    Task<VoucherRow?> FindByCodeAsync(string code, CancellationToken ct);

    /// <summary>Такий код уже має інший сертифікат (не exceptId).</summary>
    Task<bool> CodeTakenAsync(string code, int? exceptId, CancellationToken ct);

    Task<int> AddAsync(GiftVoucher voucher, DateTime now, CancellationToken ct);

    Task UpdateAsync(int id, GiftVoucher voucher, CancellationToken ct);

    Task DeleteAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<VoucherHistoryRow>> HistoryAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<LookupItem>> ThemesAsync(CancellationToken ct);

    /// <summary>Сума й завдаток оформленого замовлення; null, якщо такого немає.</summary>
    Task<OrderSums?> OrderSumsAsync(int orderId, CancellationToken ct);

    /// <summary>Сертифікат, списаний у цьому замовленні, і скільки (з плюсом), за dd_voucher_history.</summary>
    Task<SpentInOrder?> SpentInOrderAsync(int orderId, CancellationToken ct);

    /// <summary>
    /// Списує суму сертифіката в замовленні так, як OpenCart: рядок історії з мінусом, рядок підсумку voucher з мінусом і менша
    /// сума замовлення.
    /// </summary>
    Task RedeemAsync(int orderId, int voucherId, string code, decimal amount, DateTime now, CancellationToken ct);

    /// <summary>Повертає списання: рядок історії й рядок підсумку зникають, сума замовлення знову більша.</summary>
    Task ReleaseAsync(int orderId, int voucherId, decimal amount, CancellationToken ct);
}

public sealed record OrderSums(decimal Total, decimal Prepayment);

public sealed record SpentInOrder(int VoucherId, decimal Amount);

/// <param name="Spent">Сума рядків історії (списання з мінусом).</param>
/// <param name="OrderStatusId">Статус замовлення, в якому сертифікат куплено; null, коли OrderId = 0.</param>
public sealed record VoucherRow(int Id, int OrderId, string Code, string FromName, string FromEmail, string ToName, string ToEmail, int ThemeId,
    string ThemeName, string Message, decimal Amount, bool Active, DateTime CreatedAt, decimal Spent, int? OrderStatusId);
