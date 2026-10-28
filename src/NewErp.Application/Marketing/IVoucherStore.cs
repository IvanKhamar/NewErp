using NewErp.Application.Common;
using NewErp.Domain.Marketing;

namespace NewErp.Application.Marketing;

/// <summary>Сертифікати OpenCart: dd_voucher, його списання в dd_voucher_history і картинки в dd_voucher_theme.</summary>
public interface IVoucherStore
{
    /// <summary>Новіші вгорі; search шукає частину коду або імені отримувача чи відправника.</summary>
    Task<IReadOnlyList<VoucherRow>> ListAsync(string search, CancellationToken ct);

    Task<VoucherRow?> FindAsync(int id, CancellationToken ct);

    /// <summary>Такий код уже має інший сертифікат (не exceptId).</summary>
    Task<bool> CodeTakenAsync(string code, int? exceptId, CancellationToken ct);

    Task<int> AddAsync(GiftVoucher voucher, DateTime now, CancellationToken ct);

    Task UpdateAsync(int id, GiftVoucher voucher, CancellationToken ct);

    Task DeleteAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<VoucherHistoryRow>> HistoryAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<LookupItem>> ThemesAsync(CancellationToken ct);
}

/// <param name="Spent">Сума рядків історії (списання з мінусом).</param>
/// <param name="OrderStatusId">Статус замовлення, в якому сертифікат куплено; null, коли OrderId = 0.</param>
public sealed record VoucherRow(int Id, int OrderId, string Code, string FromName, string FromEmail, string ToName, string ToEmail, int ThemeId,
    string ThemeName, string Message, decimal Amount, bool Active, DateTime CreatedAt, decimal Spent, int? OrderStatusId);
