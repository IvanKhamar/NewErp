namespace NewErp.Application.Marketing;

/// <summary>Подарункові сертифікати (секція Vouchers у appsettings).</summary>
public sealed class VoucherOptions
{
    public const string SectionName = "Vouchers";

    /// <summary>
    /// Куплений у замовленні сертифікат діє, лише коли замовлення в одному з цих статусів (config_complete_status OpenCart).
    /// Значення — в appsettings, бо прив'язка конфігурації дописує до масиву, а не замінює його.
    /// </summary>
    public int[] PaidStatusIds { get; set; } = [];

    /// <summary>SMS отримувачу; підставляються {amount} і {code}.</summary>
    public string SmsText { get; set; } = "Вам подарунковий сертифікат Vidro на {amount} грн. Код: {code}. Назвіть його менеджеру або введіть на сайті.";
}

/// <summary>Новий чи змінений сертифікат. Порожній код нового згенерується; Active = null — увімкнений.</summary>
public sealed record SaveVoucherRequest(string? Code, string? FromName, string? FromEmail, string? ToName, string? ToEmail, int? ThemeId,
    string? Message, decimal? Amount, bool? Active);

/// <summary>Телефон вводять щоразу: у dd_voucher немає колонки телефону.</summary>
public sealed record SendVoucherSmsRequest(string? Phone);

/// <summary>
/// Сертифікат із залишком. Usable: сайт і ручні замовлення приймають його зараз (увімкнений і, якщо куплений у замовленні,
/// оплачений); залишок ще має бути більше 0.
/// </summary>
public sealed record VoucherCardDto(int Id, int OrderId, string Code, string FromName, string FromEmail, string ToName, string ToEmail,
    int ThemeId, string ThemeName, string Message, decimal Amount, decimal Balance, bool Active, bool Usable, DateTime CreatedAt,
    IReadOnlyList<VoucherHistoryRow> History);

/// <summary>Amount = null: скільки дозволяють залишок сертифіката й замовлення.</summary>
public sealed record RedeemVoucherRequest(string? Code, decimal? Amount);

/// <summary>Сертифікат, списаний у замовленні: скільки ним сплачено, нова сума замовлення й що лишилось на сертифікаті.</summary>
public sealed record OrderVoucherDto(int OrderId, int VoucherId, string Code, decimal Amount, decimal OrderTotal, decimal VoucherBalance);

/// <summary>Списання (сума з мінусом) чи повернення (з плюсом) у замовленні.</summary>
public sealed record VoucherHistoryRow(int OrderId, string Customer, decimal Amount, DateTime Date);
