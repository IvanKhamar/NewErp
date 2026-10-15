namespace NewErp.Application.Sms;

public sealed record SendSmsRequest(string Text);

public sealed record SmsDto(int Id, int OrderId, string Phone, string Text, string Response, DateTime SentAt);

/// <summary>Sent: чи SMS справді пішло клієнту (заглушка завжди повертає false). Запис у журналі є в обох випадках.</summary>
public sealed record SendSmsResultDto(bool Sent, SmsDto Message);

/// <summary>Результат від SMS-сервісу. Response — сирий текст відповіді, він зберігається в журналі.</summary>
public sealed record SmsSendResult(bool Sent, string Response);
