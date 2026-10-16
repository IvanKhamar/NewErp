using System.Text.RegularExpressions;

using NewErp.Domain.Common;

namespace NewErp.Domain.Sms;

/// <summary>Надіслане (або спробуване) SMS клієнту, рядок журналу dd_sms_log старого сайту.</summary>
public partial class SmsMessage
{
    /// <summary>Розумна межа: довше — це вже кілька SMS, і менеджер, найпевніше, помилився.</summary>
    public const int MaxLength = 670;

    private SmsMessage() { }

    public SmsMessage(int orderId, string phone, string text, string response, DateTime sentAt)
    {
        OrderId = orderId;
        Phone = phone;
        Text = text;
        Response = response;
        SentAt = sentAt;
    }

    public int Id { get; private set; }

    public int OrderId { get; private set; }

    /// <summary>Передзамовлення; NewErp їх ще не підтримує, тож завжди 0.</summary>
    public int PreOrderId { get; private set; }

    public string Phone { get; private set; } = "";

    public string Text { get; private set; } = "";

    /// <summary>Відповідь SMS-сервісу як є (для заглушки — пояснення, що SMS не надсилалось).</summary>
    public string Response { get; private set; } = "";

    public DateTime SentAt { get; private set; }

    public static string RequireText(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Текст SMS порожній.");
        if (trimmed.Length > MaxLength)
            throw new DomainException($"Текст SMS довший за {MaxLength} символів.");
        return trimmed;
    }

    /// <summary>
    /// Приводить український номер до вигляду 380XXXXXXXXX, який чекає SMS-сервіс:
    /// «+38 (067) 123-45-67», «0671234567» і «380671234567» дають одне й те саме.
    /// </summary>
    public static string NormalizePhone(string? phone)
    {
        var digits = NonDigits().Replace(phone ?? "", "");
        if (digits.Length == 10 && digits.StartsWith('0'))
            digits = "38" + digits;
        if (digits.Length != 12 || !digits.StartsWith("380", StringComparison.Ordinal))
            throw new DomainException($"Номер \"{phone}\" не схожий на український мобільний.");
        return digits;
    }

    /// <summary>Номери ТТН Нової Пошти в тексті: рівно 14 цифр поспіль (не частина довшого числа).</summary>
    public static IReadOnlyList<string> FindTtnNumbers(string text) =>
        TtnNumber().Matches(text).Select(m => m.Value).Distinct().ToList();

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"(?<!\d)\d{14}(?!\d)")]
    private static partial Regex TtnNumber();
}
