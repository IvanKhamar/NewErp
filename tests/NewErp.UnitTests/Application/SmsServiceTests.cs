using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Sms;
using NewErp.Domain.Common;
using NewErp.Domain.Sms;

namespace NewErp.UnitTests.Application;

public class SmsServiceTests
{
    private readonly FakeLog _log = new();
    private readonly FakeSender _sender = new();
    private readonly SmsService _service;

    public SmsServiceTests()
    {
        _log.Phones[1] = "+38 067 123 45 67";
        _service = new SmsService(_log, _sender, new FakeTimeProvider());
    }

    [Fact]
    public async Task Send_UsesNormalizedPhoneAndLogsServiceResponse()
    {
        var result = await _service.SendToOrderAsync(1, new SendSmsRequest("ТТН 20450000000000"), CancellationToken.None);

        Assert.Equal(("380671234567", "ТТН 20450000000000"), _sender.Last);
        Assert.True(result.Sent);
        var logged = Assert.Single(_log.Messages);
        Assert.Equal(("380671234567", "OK"), (logged.Phone, logged.Response));
    }

    [Fact]
    public async Task Send_NotSent_StillLogged()
    {
        _sender.Result = new SmsSendResult(false, "insufficient funds");

        var result = await _service.SendToOrderAsync(1, new SendSmsRequest("Текст"), CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal("insufficient funds", Assert.Single(_log.Messages).Response);
    }

    [Fact]
    public async Task Send_BadPhone_NothingSentOrLogged()
    {
        _log.Phones[2] = "без номера";

        await Assert.ThrowsAsync<DomainException>(() => _service.SendToOrderAsync(2, new SendSmsRequest("Текст"), CancellationToken.None));
        Assert.Null(_sender.Last);
        Assert.Empty(_log.Messages);
    }

    [Fact]
    public async Task Send_UnknownOrder_NotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SendToOrderAsync(42, new SendSmsRequest("Текст"), CancellationToken.None));

    private sealed class FakeSender : ISmsSender
    {
        public SmsSendResult Result { get; set; } = new(true, "OK");

        public (string Phone, string Text)? Last { get; private set; }

        public Task<SmsSendResult> SendAsync(string phone, string text, CancellationToken ct)
        {
            Last = (phone, text);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeLog : ISmsLog
    {
        public Dictionary<int, string> Phones { get; } = [];

        public List<SmsMessage> Messages { get; } = [];

        public Task<string?> GetOrderPhoneAsync(int orderId, CancellationToken ct) => Task.FromResult(Phones.GetValueOrDefault(orderId));

        public Task<IReadOnlyList<SmsDto>> ListForOrderAsync(int orderId, CancellationToken ct) => throw new NotSupportedException();

        public void Add(SmsMessage message) => Messages.Add(message);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
