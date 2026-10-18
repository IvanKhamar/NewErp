using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Marketing;
using NewErp.Application.Sms;
using NewErp.Domain.Common;
using NewErp.Domain.Marketing;
using NewErp.Domain.Sms;

namespace NewErp.UnitTests.Application;

public class MarketingServiceTests
{
    private readonly FakeStore _store = new();
    private readonly FakeSender _sender = new();
    private readonly FakeLog _log = new();
    private readonly MarketingService _service;

    public MarketingServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new MarketingService(_store, _sender, _log, Options.Create(new MarketingOptions { BulkSmsDelayMilliseconds = 0 }), clock);
    }

    [Fact]
    public async Task Birthdays_OnePerPhone_FromLatestOrder_WithPickupCityAndSentCount()
    {
        _store.Orders.AddRange([
            new BirthdayOrder("0671112233", "Оля", "Стара", "", "Київ", "Нова Пошта", "", 1, 1, new DateTime(2025, 1, 1)),
            new BirthdayOrder("+38 067 111 22 33", "Ольга", "Нова", "", "", "Самовивіз з магазину", "@olga", 18, 10, new DateTime(2026, 5, 1)),
            new BirthdayOrder("0501234567", "Іра", "Інша", "", "Одеса", "", "", 19, 10, new DateTime(2026, 5, 1)),
        ]);
        _store.SmsPhones.AddRange(["380671112233", "+380671112233"]);

        var today = await _service.BirthdaysAsync(BirthdayPeriod.Today, CancellationToken.None);
        var week = await _service.BirthdaysAsync(BirthdayPeriod.Week, CancellationToken.None);

        var olga = Assert.Single(today);
        Assert.Equal(("380671112233", "Ольга", "Львів", "@olga", 2), (olga.Phone, olga.FirstName, olga.City, olga.Instagram, olga.SentThisYear));
        Assert.Equal(["Ольга", "Іра"], week.Select(c => c.FirstName));
        Assert.Equal(new DateTime(2026, 1, 1), _store.SmsSince);
    }

    [Fact]
    public async Task Sms_WithCoupon_CreatesCouponThenSendsAndLogsAsBirthday()
    {
        var result = await _service.SendBirthdaySmsAsync(new BirthdaySmsRequest("067 111 22 33", " Вітаємо! DNAB12CD34 ", "dnab12cd34"), CancellationToken.None);

        Assert.Equal(new BirthdaySmsResult(true, 7, "OK"), result);
        var coupon = Assert.Single(_store.Coupons);
        Assert.Equal(("DNAB12CD34", "ДН 380671112233 18.10.2026", 20m, new DateOnly(2026, 10, 18), new DateOnly(2026, 11, 17)),
            (coupon.Code, coupon.Name, coupon.DiscountPercent, coupon.Start, coupon.End));
        var message = Assert.Single(_log.Messages);
        Assert.Equal((0, SmsMessage.BirthdayMarker, "380671112233", "Вітаємо! DNAB12CD34"), (message.OrderId, message.PreOrderId, message.Phone, message.Text));
    }

    [Fact]
    public async Task Sms_RejectsTakenCoupon_BeforeSending()
    {
        _store.Coupons.Add(new CouponDraft("DNAB12CD34", "", 20, default, default, default));

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.SendBirthdaySmsAsync(new BirthdaySmsRequest("0671112233", "Текст", "DNAB12CD34"), CancellationToken.None));
        Assert.Empty(_log.Messages);
    }

    [Fact]
    public async Task Bulk_SendsOncePerNumber_AndReportsBadNumbers()
    {
        _sender.Results.Enqueue(new SmsSendResult(false, "Помилка"));

        var result = await _service.SendBulkAsync(new BulkSmsRequest(["0671112233", "+380671112233", "123", "0501234567"], "Вітаємо!"), CancellationToken.None);

        Assert.Equal((1, 1), (result.Sent, result.NotSent));
        Assert.Equal(["123"], result.Skipped);
        Assert.Equal(2, _log.Messages.Count);
    }

    [Fact]
    public async Task CustomerOrders_MatchNormalizedPhone()
    {
        _store.Phones.AddRange([(1, "+38 (067) 111-22-33"), (2, "0501234567"), (3, "0671112233")]);

        await _service.CustomerOrdersAsync("067-111-22-33", CancellationToken.None);

        Assert.Equal([1, 3], _store.LastOrderIds);
    }

    [Fact]
    public async Task Export_NeedsSource_AndBuildsCsv()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.ExportCsvAsync(new ContactExportRequest(ContactField.Phone, ContactSource.None), CancellationToken.None));

        _store.Values.AddRange(["0671112233", "bad"]);
        var csv = await _service.ExportCsvAsync(new ContactExportRequest(ContactField.Phone, ContactSource.Orders | ContactSource.Questions), CancellationToken.None);

        Assert.Equal("Ім'я;Прізвище;Місто;Телефон;E-mail\r\n;;;380671112233;\r\n", csv);
    }

    private sealed class FakeStore : IMarketingStore
    {
        public List<BirthdayOrder> Orders { get; } = [];
        public List<string> SmsPhones { get; } = [];
        public DateTime SmsSince { get; private set; }
        public List<CouponDraft> Coupons { get; } = [];
        public List<(int OrderId, string Phone)> Phones { get; } = [];
        public IReadOnlyCollection<int> LastOrderIds { get; private set; } = [];
        public List<string> Values { get; } = [];

        public Task<IReadOnlyList<BirthdayOrder>> BirthdayOrdersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<BirthdayOrder>>(Orders);

        public Task<IReadOnlyList<string>> BirthdaySmsPhonesAsync(DateTime since, CancellationToken ct)
        {
            SmsSince = since;
            return Task.FromResult<IReadOnlyList<string>>(SmsPhones);
        }

        public Task<bool> CouponExistsAsync(string code, CancellationToken ct) => Task.FromResult(Coupons.Any(c => c.Code == code));

        public Task<int> AddCouponAsync(CouponDraft coupon, CancellationToken ct)
        {
            Coupons.Add(coupon);
            return Task.FromResult(7);
        }

        public Task<IReadOnlyList<(int OrderId, string Phone)>> OrderPhonesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<(int OrderId, string Phone)>>(Phones);

        public Task<IReadOnlyList<CustomerOrderDto>> OrdersAsync(IReadOnlyCollection<int> orderIds, CancellationToken ct)
        {
            LastOrderIds = orderIds;
            return Task.FromResult<IReadOnlyList<CustomerOrderDto>>([]);
        }

        public Task<IReadOnlyList<string>> ContactValuesAsync(ContactExportRequest request, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(Values);

        public Task<IReadOnlyList<OrderContact>> OrderContactsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<OrderContact>>([]);
    }

    private sealed class FakeSender : ISmsSender
    {
        public Queue<SmsSendResult> Results { get; } = new();

        public Task<SmsSendResult> SendAsync(string phone, string text, CancellationToken ct) =>
            Task.FromResult(Results.TryDequeue(out var result) ? result : new SmsSendResult(true, "OK"));
    }

    private sealed class FakeLog : ISmsLog
    {
        public List<SmsMessage> Messages { get; } = [];

        public Task<string?> GetOrderPhoneAsync(int orderId, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<SmsDto>> ListForOrderAsync(int orderId, CancellationToken ct) => Task.FromResult<IReadOnlyList<SmsDto>>([]);

        public Task<IReadOnlyDictionary<string, int>> FindOrderIdsByTtnAsync(IReadOnlyCollection<string> numbers, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

        public void Add(SmsMessage message) => Messages.Add(message);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
