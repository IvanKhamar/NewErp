namespace NewErp.Application.Sms;

/// <summary>
/// Надсилання SMS через зовнішній сервіс. Справжній відправник (SMSClub) працює лише з токеном у user-secrets,
/// без нього зареєстровано заглушку, яка нічого не надсилає.
/// </summary>
public interface ISmsSender
{
    /// <summary>Чи підключено справжній сервіс. Типово так; заглушка повертає false.</summary>
    bool IsConfigured => true;

    /// <param name="phone">Номер у вигляді 380XXXXXXXXX.</param>
    Task<SmsSendResult> SendAsync(string phone, string text, CancellationToken ct);
}
