namespace NewErp.Application.Sms;

/// <summary>
/// Надсилання SMS через зовнішній сервіс. Зараз зареєстровано лише заглушку, яка нічого не надсилає;
/// справжній відправник (SMSClub) з'явиться разом з новим токеном.
/// </summary>
public interface ISmsSender
{
    /// <param name="phone">Номер у вигляді 380XXXXXXXXX.</param>
    Task<SmsSendResult> SendAsync(string phone, string text, CancellationToken ct);
}
