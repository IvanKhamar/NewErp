namespace NewErp.Application.Sms;

/// <summary>Force: надіслати, навіть якщо номер ТТН у тексті належить іншому замовленню (людина підтвердила).</summary>
public sealed record SendSmsRequest(string Text, bool Force = false);

public sealed record SmsDto(int Id, int OrderId, string Phone, string Text, string Response, DateTime SentAt);

/// <summary>Номер ТТН у тексті SMS належить іншому замовленню — найімовірніше, менеджер вставив не той номер.</summary>
public sealed record TtnMismatchDto(string Ttn, int TtnOrderId);

/// <summary>
/// Sent: чи SMS справді пішло клієнту (заглушка завжди повертає false).
/// Якщо TtnMismatch не null, SMS не надсилалось і в журнал не потрапило: треба підтвердити (Force) або виправити текст.
/// </summary>
public sealed record SendSmsResultDto(bool Sent, SmsDto? Message, TtnMismatchDto? TtnMismatch = null);

/// <summary>Результат від SMS-сервісу. Response — сирий текст відповіді, він зберігається в журналі.</summary>
public sealed record SmsSendResult(bool Sent, string Response);
