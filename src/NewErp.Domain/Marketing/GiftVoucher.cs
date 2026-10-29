using System.Globalization;

using NewErp.Domain.Common;

namespace NewErp.Domain.Marketing;

/// <summary>
/// Подарунковий сертифікат магазину (dd_voucher OpenCart). Сайт списує його на оформленні, ERP — у ручних замовленнях;
/// кожне списання — рядок dd_voucher_history з мінусом, тож залишок = сума + історія. Як у Java-версії.
/// </summary>
/// <param name="ThemeId">Картинка сертифіката старого сайту (dd_voucher_theme), 0 — без неї.</param>
public sealed record GiftVoucher(string Code, string FromName, string FromEmail, string ToName, string ToEmail, int ThemeId, string Message,
    decimal Amount, bool Active)
{
    /// <summary>dd_voucher.code — varchar(10).</summary>
    public const int MaxCodeLength = 10;

    // Згенеровані коди: 8 символів без схожих 0/O й 1/I, щоб легко диктувати по телефону.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int GeneratedLength = 8;

    /// <summary>Перевіряє й чистить те, що ввів менеджер. Код — великими літерами; порожній треба згенерувати заздалегідь.</summary>
    public static GiftVoucher Of(string? code, string? fromName, string? fromEmail, string? toName, string? toEmail, int? themeId, string? message,
        decimal? amount, bool? active)
    {
        var cleanCode = code?.Trim().ToUpperInvariant() ?? "";
        if (cleanCode.Length is < 1 or > MaxCodeLength || !cleanCode.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)))
            throw new DomainException($"Код сертифіката — від 1 до {MaxCodeLength} латинських літер або цифр.");
        if (amount is not { } sum || sum <= 0 || decimal.Round(sum, 2) != sum)
            throw new DomainException("Сума сертифіката — більше 0, не більше двох знаків після коми.");
        if (string.IsNullOrWhiteSpace(toName))
            throw new DomainException("Вкажіть, кому сертифікат.");

        var cleanToName = toName.Trim();
        var cleanFromName = Text(fromName);
        // dd_voucher: імена varchar(64), email varchar(96).
        if (cleanToName.Length > 64 || cleanFromName.Length > 64 || Text(fromEmail).Length > 96 || Text(toEmail).Length > 96)
            throw new DomainException("Ім'я — не довше 64 символів, email — не довше 96.");

        return new GiftVoucher(cleanCode, cleanFromName, Text(fromEmail), cleanToName, Text(toEmail), themeId ?? 0, Text(message), sum,
            active ?? true);
    }

    public static string NewCode(Random random)
    {
        var code = new char[GeneratedLength];
        for (var i = 0; i < code.Length; i++)
            code[i] = Alphabet[random.Next(Alphabet.Length)];
        return new string(code);
    }

    /// <param name="spent">Сума рядків історії (списання з мінусом, повернення з плюсом).</param>
    public static decimal Balance(decimal amount, decimal? spent) => amount + (spent ?? 0);

    /// <summary>
    /// Чи приймають його сайт і ERP, як getVoucher OpenCart: увімкнений і, якщо куплений у замовленні (orderId &gt; 0), це
    /// замовлення оплачене. Залишок перевіряють окремо.
    /// </summary>
    public static bool Usable(bool active, int orderId, bool orderPaid) => active && (orderId == 0 || orderPaid);

    /// <summary>Перевіряє списання в замовленні.</summary>
    /// <param name="left">Скільки замовленню ще треба: його сума мінус завдаток.</param>
    public static void RequireRedeemable(decimal? amount, decimal balance, decimal left, bool usable)
    {
        if (!usable)
            throw new DomainException("Сертифікат зараз не можна використати: він вимкнений або куплений у ще не оплаченому замовленні.");
        if (amount is not { } sum || sum <= 0 || decimal.Round(sum, 2) != sum)
            throw new DomainException("Сума списання — більше 0, не більше двох знаків після коми.");
        if (sum > balance)
            throw new DomainException($"На сертифікаті лише {Plain(balance)} грн.");
        if (sum > left)
            throw new DomainException($"Це більше, ніж лишилось сплатити за замовлення ({Plain(left)} грн).");
    }

    /// <summary>Сума без зайвих нулів, як toPlainString у Java-версії: 650, 650.5.</summary>
    public static string Plain(decimal value) => value.ToString("0.##########", CultureInfo.InvariantCulture);

    private static string Text(string? value) => value?.Trim() ?? "";
}
