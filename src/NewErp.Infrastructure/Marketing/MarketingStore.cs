using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Marketing;
using NewErp.Domain.Marketing;
using NewErp.Domain.Sms;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Marketing;

public sealed class MarketingStore(ErpDbContext db, IOptions<ErpOptions> options) : IMarketingStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<BirthdayOrder>> BirthdayOrdersAsync(CancellationToken ct) =>
        await db.DdOrders.AsNoTracking()
            .Where(o => o.BirthdayDay > 0 && o.BirthdayMonth > 0)
            .Select(o => new BirthdayOrder(o.Telephone, o.Firstname, o.Lastname, o.Email, o.ShippingCity, o.ShippingMethod, o.Instagram,
                o.BirthdayDay, o.BirthdayMonth, o.DateAdded))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> BirthdaySmsPhonesAsync(DateTime since, CancellationToken ct) =>
        await db.SmsMessages.AsNoTracking()
            .Where(m => m.PreOrderId == SmsMessage.BirthdayMarker && m.SentAt >= since)
            .Select(m => m.Phone)
            .ToListAsync(ct);

    public Task<bool> CouponExistsAsync(string code, CancellationToken ct) => db.DdCoupons.AnyAsync(c => c.Code == code, ct);

    public async Task<int> AddCouponAsync(CouponDraft coupon, CancellationToken ct)
    {
        // Як createCoupon старого сайту: відсоток, без доставки й мінімальної суми, одне використання.
        var row = new DdCoupon
        {
            Code = coupon.Code, Name = coupon.Name.Length > 128 ? coupon.Name[..128] : coupon.Name, Type = "P", Discount = coupon.DiscountPercent,
            Logged = false, Shipping = false, Total = 0, DateStart = coupon.Start.ToDateTime(TimeOnly.MinValue),
            DateEnd = coupon.End.ToDateTime(TimeOnly.MinValue), UsesTotal = 1, UsesCustomer = "0", Status = true, DateAdded = coupon.CreatedAt,
        };
        db.DdCoupons.Add(row);
        await db.SaveChangesAsync(ct);
        return row.CouponId;
    }

    public async Task<IReadOnlyList<(int OrderId, string Phone)>> OrderPhonesAsync(CancellationToken ct) =>
        (await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderStatusId > 0 && o.Telephone != "")
            .Select(o => new { o.OrderId, o.Telephone })
            .ToListAsync(ct))
        .Select(o => (o.OrderId, o.Telephone)).ToList();

    public async Task<IReadOnlyList<CustomerOrderDto>> OrdersAsync(IReadOnlyCollection<int> orderIds, CancellationToken ct)
    {
        var orders = await (
            from o in db.DdOrders
            join s in db.DdOrderStatuses.Where(s => s.LanguageId == _languageId) on o.OrderStatusId equals s.OrderStatusId into statuses
            from s in statuses.DefaultIfEmpty()
            where orderIds.Contains(o.OrderId)
            orderby o.DateAdded descending
            select new { o.OrderId, o.DateAdded, o.Total, o.PaymentMethod, Status = s == null ? "" : s.Name })
            .ToListAsync(ct);
        var lines = (await db.DdOrderProducts.AsNoTracking()
                .Where(p => orderIds.Contains(p.OrderId))
                .OrderBy(p => p.OrderProductId)
                .Select(p => new { p.OrderId, p.Name, p.Quantity })
                .ToListAsync(ct))
            .ToLookup(p => p.OrderId);

        return orders.Select(o => new CustomerOrderDto(o.OrderId, o.DateAdded, o.Total, o.PaymentMethod, o.Status,
                string.Join(", ", lines[o.OrderId].Select(l => $"{l.Name} x{l.Quantity}"))))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ContactValuesAsync(ContactExportRequest request, CancellationToken ct)
    {
        var from = request.From?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue;
        var to = request.To?.ToDateTime(TimeOnly.MaxValue) ?? DateTime.MaxValue;
        var phone = request.Field == ContactField.Phone;
        var values = new List<string>();

        if (request.Sources.HasFlag(ContactSource.Orders))
            values.AddRange(await db.DdOrders.AsNoTracking()
                .Where(o => o.OrderStatusId > 0 && o.DateAdded >= from && o.DateAdded <= to)
                .Select(o => phone ? o.Telephone : o.Email)
                .Where(v => v != "")
                .ToListAsync(ct));
        // Як на старому сайті: у запитаннях є лише телефон, у підписках — лише e-mail.
        if (phone && request.Sources.HasFlag(ContactSource.Questions))
            values.AddRange(await db.DdQuestions.AsNoTracking()
                .Where(q => q.Telephone != "" && q.DateAdded >= from && q.DateAdded <= to)
                .Select(q => q.Telephone)
                .ToListAsync(ct));
        if (!phone && request.Sources.HasFlag(ContactSource.Subscribers))
            values.AddRange(await db.DdSubscribes.AsNoTracking()
                .Where(s => s.Email != "" && s.DateAdded >= from && s.DateAdded <= to)
                .Select(s => s.Email)
                .ToListAsync(ct));
        return values;
    }

    public async Task<IReadOnlyList<OrderContact>> OrderContactsAsync(CancellationToken ct) =>
        await db.DdOrders.AsNoTracking()
            .Where(o => o.OrderStatusId > 0)
            .Select(o => new OrderContact(o.Telephone, o.Email, o.Firstname, o.Lastname, o.ShippingCity, o.DateAdded))
            .ToListAsync(ct);
}
