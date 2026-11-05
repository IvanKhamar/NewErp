using Microsoft.EntityFrameworkCore;

using NewErp.Application.Reports;
using NewErp.Domain.Reports;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Reports;

public sealed class RemarketingStore(ErpDbContext db) : IRemarketingStore
{
    private const string NoSource = "(без джерела)";

    // Збережений нульовий час менший за будь-який справжній: у запиті він стає null, читати його в DateTime не можна.
    private static readonly DateTime FirstTime = new(1000, 1, 1);

    public async Task<IReadOnlyList<UtmSale>> UtmSalesAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, UtmFilter utm, CancellationToken ct)
    {
        var query =
            from r in db.RemarketingOrders.AsNoTracking()
            join o in db.DdOrders on r.OrderId equals o.OrderId
            where o.DateAdded >= @from && o.DateAdded < toExclusive
                && (statusId != null ? o.OrderStatusId == statusId : o.OrderStatusId > 0 && o.OrderStatusId != excludedStatusId)
            select new { r, o };
        if (!string.IsNullOrEmpty(utm.Source))
            query = query.Where(x => x.r.UtmSource == utm.Source);
        if (!string.IsNullOrEmpty(utm.Medium))
            query = query.Where(x => x.r.UtmMedium == utm.Medium);
        if (!string.IsNullOrEmpty(utm.Campaign))
            query = query.Where(x => x.r.UtmCampaign == utm.Campaign);

        var rows = await query
            .Select(x => new
            {
                x.r.UtmSource, x.o.DateAdded, x.o.Total,
                Products = db.DdOrderProducts.Where(p => p.OrderId == x.o.OrderId).Sum(p => (int?)p.Quantity) ?? 0,
            })
            .ToListAsync(ct);
        return rows.Select(r => new UtmSale(r.UtmSource == "" ? NoSource : r.UtmSource, new SaleFact(r.DateAdded, r.Total, r.Products))).ToList();
    }

    public async Task<IReadOnlyList<string>> SourcesAsync(CancellationToken ct) =>
        await db.RemarketingOrders.AsNoTracking().Where(r => r.UtmSource != "").Select(r => r.UtmSource).Distinct().OrderBy(v => v).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> MediumsAsync(CancellationToken ct) =>
        await db.RemarketingOrders.AsNoTracking().Where(r => r.UtmMedium != "").Select(r => r.UtmMedium).Distinct().OrderBy(v => v).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> CampaignsAsync(CancellationToken ct) =>
        await db.RemarketingOrders.AsNoTracking().Where(r => r.UtmCampaign != "").Select(r => r.UtmCampaign).Distinct().OrderBy(v => v).ToListAsync(ct);

    public async Task<RemarketingRow?> OrderRowAsync(int orderId, CancellationToken ct)
    {
        var row = await db.RemarketingOrders.AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .Select(r => new
            {
                r.FirstReferrer, r.LastReferrer, r.UtmSource, r.UtmMedium, r.UtmCampaign, r.UtmContent, r.UtmTerm, r.UtmReferrer,
                SuccessPage = r.SuccessPage < FirstTime ? (DateTime?)null : r.SuccessPage,
                Ga4 = r.Ga4 < FirstTime ? (DateTime?)null : r.Ga4,
                Facebook = r.Facebook < FirstTime ? (DateTime?)null : r.Facebook,
                FacebookLead = r.FacebookLead < FirstTime ? (DateTime?)null : r.FacebookLead,
                Esputnik = r.Esputnik < FirstTime ? (DateTime?)null : r.Esputnik,
                Telegram = r.Telegram < FirstTime ? (DateTime?)null : r.Telegram,
                Tiktok = r.Tiktok < FirstTime ? (DateTime?)null : r.Tiktok,
            })
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return null;
        return new RemarketingRow(
            new Dictionary<string, string>
            {
                ["first_referrer"] = row.FirstReferrer, ["last_referrer"] = row.LastReferrer, ["utm_source"] = row.UtmSource, ["utm_medium"] = row.UtmMedium,
                ["utm_campaign"] = row.UtmCampaign, ["utm_content"] = row.UtmContent, ["utm_term"] = row.UtmTerm, ["utm_referrer"] = row.UtmReferrer,
            },
            new Dictionary<string, DateTime?>
            {
                ["success_page"] = row.SuccessPage, ["ga4"] = row.Ga4, ["facebook"] = row.Facebook, ["facebook_lead"] = row.FacebookLead,
                ["esputnik"] = row.Esputnik, ["telegram"] = row.Telegram, ["tiktok"] = row.Tiktok,
            });
    }
}
