using NewErp.Application.Common;
using NewErp.Domain.Sms;

namespace NewErp.Application.Sms;

/// <summary>Ручне SMS клієнту замовлення (менеджер сам пише текст), як на старому сайті.</summary>
public sealed class SmsService(ISmsLog log, ISmsSender sender, TimeProvider clock)
{
    public async Task<SendSmsResultDto> SendToOrderAsync(int orderId, SendSmsRequest request, CancellationToken ct)
    {
        var rawPhone = await log.GetOrderPhoneAsync(orderId, ct)
            ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        var text = SmsMessage.RequireText(request.Text);
        var phone = SmsMessage.NormalizePhone(rawPhone);

        // Як на старому сайті: номер ТТН з іншого замовлення в тексті — майже напевно помилка копіювання.
        if (!request.Force)
        {
            var owners = await log.FindOrderIdsByTtnAsync(SmsMessage.FindTtnNumbers(text), ct);
            if (owners.FirstOrDefault(o => o.Value != orderId) is { Key: not null } foreign)
                return new SendSmsResultDto(false, null, new TtnMismatchDto(foreign.Key, foreign.Value));
        }

        var result = await sender.SendAsync(phone, text, ct);

        // Журнал пишемо завжди, навіть якщо сервіс відмовив: так видно, що менеджер намагався написати клієнту.
        var message = new SmsMessage(orderId, phone, text, result.Response, clock.GetLocalNow().DateTime);
        log.Add(message);
        await log.SaveChangesAsync(ct);

        return new SendSmsResultDto(result.Sent, new SmsDto(message.Id, orderId, phone, text, message.Response, message.SentAt));
    }
}
