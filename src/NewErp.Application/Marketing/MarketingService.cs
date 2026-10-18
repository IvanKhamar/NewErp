using Microsoft.Extensions.Options;

using NewErp.Application.Sms;
using NewErp.Domain.Common;
using NewErp.Domain.Marketing;
using NewErp.Domain.Sms;

namespace NewErp.Application.Marketing;

/// <summary>
/// Маркетинг, як на старому сайті: іменинники з купоном і SMS-привітанням (marketing/birthday)
/// та вивантаження контактів клієнтів для розсилок (marketing/export).
/// </summary>
public sealed class MarketingService(IMarketingStore store, ISmsSender sender, ISmsLog smsLog, IOptions<MarketingOptions> options, TimeProvider clock)
{
    private const int MaxBulkPhones = 500;
    private readonly MarketingOptions _options = options.Value;

    /// <summary>Іменинники періоду; кожен номер — один раз, з даними його найновішого замовлення з днем народження.</summary>
    public async Task<IReadOnlyList<BirthdayContactDto>> BirthdaysAsync(BirthdayPeriod period, CancellationToken ct)
    {
        var now = clock.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var sent = (await store.BirthdaySmsPhonesAsync(new DateTime(now.Year, 1, 1), ct))
            .Select(ContactExport.CleanPhone).OfType<string>()
            .CountBy(p => p).ToDictionary();

        return (await store.BirthdayOrdersAsync(ct))
            .Select(o => (Order: o, Phone: ContactExport.CleanPhone(o.Phone)))
            .Where(x => x.Phone is not null)
            .GroupBy(x => x.Phone!)
            .Select(g => (Phone: g.Key, g.MaxBy(x => x.Order.DateAdded).Order))
            .Where(x => Birthdays.InPeriod(x.Order.Day, x.Order.Month, period, today))
            .Select(x => new BirthdayContactDto(
                x.Phone, x.Order.FirstName.Trim(), x.Order.LastName.Trim(), x.Order.Email, City(x.Order), x.Order.Instagram,
                x.Order.Day, x.Order.Month, Birthdays.Next(x.Order.Day, x.Order.Month, today)!.Value, sent.GetValueOrDefault(x.Phone)))
            .OrderBy(c => period == BirthdayPeriod.Week ? c.NextBirthday.DayNumber : c.Month * 100 + c.Day)
            .ThenBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToList();
    }

    /// <summary>Новий код купона, якого ще немає в dd_coupon.</summary>
    public async Task<CouponCodeDto> NewCouponCodeAsync(CancellationToken ct)
    {
        string code;
        do
            code = BirthdayCoupon.NewCode(Random.Shared);
        while (await store.CouponExistsAsync(code, ct));
        return new CouponCodeDto(code);
    }

    /// <summary>Привітання одному клієнту: якщо є код, спершу створюється купон, потім SMS іде й пишеться в журнал.</summary>
    public async Task<BirthdaySmsResult> SendBirthdaySmsAsync(BirthdaySmsRequest request, CancellationToken ct)
    {
        var phone = SmsMessage.NormalizePhone(request.Phone);
        var text = SmsMessage.RequireText(request.Text);
        var now = clock.GetLocalNow().DateTime;

        int? couponId = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var code = BirthdayCoupon.RequireCode(request.CouponCode);
            if (await store.CouponExistsAsync(code, ct))
                throw new DomainException($"Купон {code} уже є, згенеруй новий код.");
            var start = DateOnly.FromDateTime(now);
            couponId = await store.AddCouponAsync(new CouponDraft(
                code, $"ДН {phone} {now:dd.MM.yyyy}", _options.BirthdayDiscountPercent, start, start.AddDays(_options.BirthdayCouponDays), now), ct);
        }

        var result = await SendAsync(phone, text, now, ct);
        return new BirthdaySmsResult(result.Sent, couponId, result.Response);
    }

    /// <summary>Однаковий текст багатьом іменинникам (без купонів), з паузою між SMS.</summary>
    public async Task<BulkSmsResult> SendBulkAsync(BulkSmsRequest request, CancellationToken ct)
    {
        var text = SmsMessage.RequireText(request.Text);
        var phones = request.Phones ?? [];
        if (phones.Count == 0)
            throw new DomainException("Немає одержувачів.");
        if (phones.Count > MaxBulkPhones)
            throw new DomainException($"За раз — не більше {MaxBulkPhones} одержувачів.");

        var valid = phones.Select(p => (Raw: p, Clean: ContactExport.CleanPhone(p))).ToList();
        int sent = 0, notSent = 0;
        foreach (var phone in valid.Select(p => p.Clean).OfType<string>().Distinct())
        {
            if (sent + notSent > 0 && _options.BulkSmsDelayMilliseconds > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(_options.BulkSmsDelayMilliseconds), clock, ct);
            if ((await SendAsync(phone, text, clock.GetLocalNow().DateTime, ct)).Sent)
                sent++;
            else
                notSent++;
        }
        return new BulkSmsResult(sent, notSent, valid.Where(p => p.Clean is null).Select(p => p.Raw).ToList());
    }

    /// <summary>Замовлення клієнта за номером телефону (номери порівнюються після приведення до 380XXXXXXXXX).</summary>
    public async Task<IReadOnlyList<CustomerOrderDto>> CustomerOrdersAsync(string? phone, CancellationToken ct)
    {
        var clean = SmsMessage.NormalizePhone(phone);
        var ids = (await store.OrderPhonesAsync(ct)).Where(o => ContactExport.CleanPhone(o.Phone) == clean).Select(o => o.OrderId).ToList();
        return ids.Count == 0 ? [] : await store.OrdersAsync(ids, ct);
    }

    /// <summary>CSV з унікальними контактами для розсилки.</summary>
    public async Task<string> ExportCsvAsync(ContactExportRequest request, CancellationToken ct)
    {
        if (request.Sources == ContactSource.None)
            throw new DomainException("Вибери, звідки брати контакти.");
        if (request is { From: { } from, To: { } to } && from > to)
            throw new DomainException("Початок періоду пізніший за кінець.");

        var values = await store.ContactValuesAsync(request, ct);
        var rows = ContactExport.Build(request.Field, values, await store.OrderContactsAsync(ct));
        return ContactExport.ToCsv(request.Field, rows);
    }

    private async Task<SmsSendResult> SendAsync(string phone, string text, DateTime now, CancellationToken ct)
    {
        var result = await sender.SendAsync(phone, text, ct);
        smsLog.Add(SmsMessage.Birthday(phone, text, result.Response, now));
        await smsLog.SaveChangesAsync(ct);
        return result;
    }

    private string City(BirthdayOrder order) =>
        !string.IsNullOrWhiteSpace(order.ShippingCity) ? order.ShippingCity.Trim()
        : order.ShippingMethod.Contains(_options.PickupMethodMarker, StringComparison.CurrentCultureIgnoreCase) ? _options.PickupCity
        : "";
}
