using Microsoft.Extensions.Logging;

using NewErp.Application.Sms;

namespace NewErp.Infrastructure.Sms;

/// <summary>
/// Заглушка: нічого не надсилає, лише пише в лог застосунку. Так можна перевіряти весь сценарій на локальній базі,
/// де телефони вигадані й можуть належати реальним людям.
/// </summary>
public sealed partial class StubSmsSender(ILogger<StubSmsSender> logger) : ISmsSender
{
    public const string Response = "НЕ НАДІСЛАНО: заглушка NewErp, справжній SMS-сервіс не підключено";

    public Task<SmsSendResult> SendAsync(string phone, string text, CancellationToken ct)
    {
        LogStub(phone, text.Length);
        return Task.FromResult(new SmsSendResult(false, Response));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS-заглушка: на {Phone} не надіслано повідомлення довжиною {Length}")]
    private partial void LogStub(string phone, int length);
}
