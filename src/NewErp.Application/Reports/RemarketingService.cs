using NewErp.Application.Common;
using NewErp.Domain.Orders;
using NewErp.Domain.Reports;

namespace NewErp.Application.Reports;

/// <summary>BySource — рядок на кожен період і джерело, як «групувати за utm» старого звіту.</summary>
public sealed record UtmFilter(string? Source = null, string? Medium = null, string? Campaign = null, bool BySource = false);

public sealed record UtmSale(string Source, SaleFact Fact);

/// <summary>UtmSource заповнено, коли групуємо за джерелом.</summary>
public sealed record UtmReportRow(DateOnly Start, DateOnly End, string? UtmSource, int Orders, int Products, decimal Total);

/// <summary>Sources, Mediums, Campaigns — усі бачені значення, для фільтрів.</summary>
public sealed record UtmReportDto(
    DateOnly From, DateOnly To, IReadOnlyList<UtmReportRow> Rows, int Orders, decimal Total,
    IReadOnlyList<string> Sources, IReadOnlyList<string> Mediums, IReadOnlyList<string> Campaigns);

public sealed record PixelEventDto(string Name, DateTime SentAt);

/// <summary>Мітки UTM і звідки прийшов (лише заповнені) та події пікселів, які справді надіслано.</summary>
public sealed record OrderRemarketingDto(int OrderId, IReadOnlyDictionary<string, string> Utm, IReadOnlyList<PixelEventDto> Events);

/// <summary>Рядок таблиці модуля: текстові поля за назвою колонки й час подій (null — подію не надіслано).</summary>
public sealed record RemarketingRow(IReadOnlyDictionary<string, string> Texts, IReadOnlyDictionary<string, DateTime?> Events);

public interface IRemarketingStore
{
    /// <summary>Замовлення періоду, що мають рядок ремаркетингу, з їхнім джерелом.</summary>
    Task<IReadOnlyList<UtmSale>> UtmSalesAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, UtmFilter utm, CancellationToken ct);

    Task<IReadOnlyList<string>> SourcesAsync(CancellationToken ct);

    Task<IReadOnlyList<string>> MediumsAsync(CancellationToken ct);

    Task<IReadOnlyList<string>> CampaignsAsync(CancellationToken ct);

    Task<RemarketingRow?> OrderRowAsync(int orderId, CancellationToken ct);
}

/// <summary>
/// Звідки приходять замовлення й коли пікселі вітрини надіслали події. Таблицю dd_remarketing_orders пише модуль вітрини
/// «Remarketing All in One», самі пікселі живуть на вітрині. ERP лише читає: звіт UTM старого сайту й блок картки замовлення.
/// </summary>
public sealed class RemarketingService(IRemarketingStore store, TimeProvider clock)
{
    /// <summary>Події модуля (назви колонок) і як їх називає картка замовлення.</summary>
    public static readonly IReadOnlyList<(string Column, string Name)> PixelEvents =
    [
        ("success_page", "Сторінка «Дякуємо»"),
        ("ga4", "Google Analytics 4"),
        ("facebook", "Meta (Facebook) Purchase"),
        ("facebook_lead", "Meta (Facebook) Lead"),
        ("esputnik", "eSputnik"),
        ("telegram", "Telegram"),
        ("tiktok", "TikTok"),
    ];

    public static readonly IReadOnlyList<string> TextColumns =
        ["first_referrer", "last_referrer", "utm_source", "utm_medium", "utm_campaign", "utm_content", "utm_term", "utm_referrer"];

    public async Task<UtmReportDto> UtmReportAsync(ReportFilter filter, UtmFilter utm, CancellationToken ct)
    {
        var (from, to) = ReportService.PeriodOf(filter, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var sales = await store.UtmSalesAsync(
            from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue), filter.StatusId, CustomerRisk.CancelledStatusId, utm, ct);
        var rows = sales
            .GroupBy(s => utm.BySource ? s.Source : null)
            .SelectMany(g => SalesReport.Group(g.Select(s => s.Fact), filter.Group, from, to, fillGaps: false)
                .Select(p => new UtmReportRow(p.Start, p.End, g.Key, p.Orders, p.Products, p.Total)))
            .OrderBy(r => r.Start)
            .ThenBy(r => r.UtmSource ?? "", StringComparer.Ordinal)
            .ToList();
        return new UtmReportDto(
            from, to, rows, rows.Sum(r => r.Orders), rows.Sum(r => r.Total),
            await store.SourcesAsync(ct), await store.MediumsAsync(ct), await store.CampaignsAsync(ct));
    }

    public async Task<OrderRemarketingDto> ForOrderAsync(int orderId, CancellationToken ct)
    {
        var row = await store.OrderRowAsync(orderId, ct) ?? throw new NotFoundException($"У замовлення {orderId} немає даних ремаркетингу.");
        var utm = new Dictionary<string, string>();
        foreach (var column in TextColumns)
            if (row.Texts.GetValueOrDefault(column) is { Length: > 0 } text)
                utm[column] = text;
        var events = PixelEvents
            .Where(e => row.Events.GetValueOrDefault(e.Column) is not null)
            .Select(e => new PixelEventDto(e.Name, row.Events[e.Column]!.Value))
            .ToList();
        return new OrderRemarketingDto(orderId, utm, events);
    }
}
