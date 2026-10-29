using System.Text;

using NewErp.Api.Auth;
using NewErp.Application.Marketing;
using NewErp.Domain.Marketing;

namespace NewErp.Api.Endpoints;

public static class MarketingEndpoints
{
    public static void MapMarketingEndpoints(this IEndpointRouteBuilder app)
    {
        var marketing = app.MapGroup("/marketing").WithTags("Маркетинг").RequireAuthorization(Policies.Orders);

        marketing.MapGet("/birthdays", (BirthdayPeriod? period, MarketingService service, CancellationToken ct) =>
            service.BirthdaysAsync(period ?? BirthdayPeriod.Today, ct))
            .WithSummary("Іменинники періоду (Today, Tomorrow, Week, Month, All) з даними останнього замовлення й кількістю привітань цього року");

        marketing.MapPost("/birthdays/coupon-code", (MarketingService service, CancellationToken ct) => service.NewCouponCodeAsync(ct))
            .WithSummary("Новий код купона DN…, якого ще немає (сам купон створюється разом із SMS)");

        marketing.MapPost("/birthdays/sms", (BirthdaySmsRequest request, MarketingService service, CancellationToken ct) =>
            service.SendBirthdaySmsAsync(request, ct))
            .WithSummary("Привітання одному клієнту; з кодом — спершу купон зі знижкою на BirthdayCouponDays днів");

        marketing.MapPost("/birthdays/sms-bulk", (BulkSmsRequest request, MarketingService service, CancellationToken ct) =>
            service.SendBulkAsync(request, ct))
            .WithSummary("Однаковий текст багатьом номерам (до 500), без купонів");

        marketing.MapGet("/customers/orders", (string phone, MarketingService service, CancellationToken ct) => service.CustomerOrdersAsync(phone, ct))
            .WithSummary("Замовлення клієнта за телефоном");

        marketing.MapGet("/contacts.csv", async (ContactField field, ContactSource[] sources, DateOnly? from, DateOnly? to,
            MarketingService service, TimeProvider clock, CancellationToken ct) =>
        {
            var request = new ContactExportRequest(field, sources.Aggregate(ContactSource.None, (all, s) => all | s), from, to);
            var csv = await service.ExportCsvAsync(request, ct);
            var name = $"contacts-{(field == ContactField.Phone ? "phones" : "emails")}-{clock.GetLocalNow():yyyy-MM-dd-HHmm}.csv";
            // Excel відкриває CSV у UTF-8 правильно лише з BOM на початку.
            return Results.File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)], "text/csv; charset=utf-8", name);
        })
            .WithSummary("Унікальні телефони чи e-mail клієнтів (джерела: Orders, Questions, Subscribers) за період, CSV для Excel");

        var vouchers = marketing.MapGroup("/vouchers");

        vouchers.MapGet("", (string? search, VoucherService service, CancellationToken ct) => service.ListAsync(search, ct))
            .WithSummary("Подарункові сертифікати з залишком (новіші вгорі, до 500); search — код або ім'я");

        vouchers.MapGet("/{id:int}", (int id, VoucherService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithSummary("Сертифікат з історією списань");

        vouchers.MapGet("/themes", (VoucherService service, CancellationToken ct) => service.ThemesAsync(ct))
            .WithSummary("Теми (картинки) сертифікатів старого сайту");

        vouchers.MapPost("", (SaveVoucherRequest request, VoucherService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .WithSummary("Новий сертифікат; порожній код згенерується (8 символів без 0/O й 1/I)");

        vouchers.MapPut("/{id:int}", (int id, SaveVoucherRequest request, VoucherService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct))
            .WithSummary("Змінити сертифікат (історія списань лишається)");

        vouchers.MapDelete("/{id:int}", async (int id, VoucherService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        })
            .WithSummary("Видалити сертифікат, яким ще не платили (інакше — вимкнути)");

        vouchers.MapPost("/{id:int}/sms", (int id, SendVoucherSmsRequest request, VoucherService service, CancellationToken ct) =>
            service.SendSmsAsync(id, request.Phone, ct))
            .WithSummary("Надіслати код і залишок отримувачу SMS (текст — Vouchers:SmsText), із записом у журнал SMS");
    }
}
