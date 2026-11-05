using NewErp.Domain.Orders;
using NewErp.Domain.Reports;

namespace NewErp.Application.Reports;

/// <summary>CustomerId 0 — гість.</summary>
public sealed record OnlineVisitorDto(string Ip, int CustomerId, string? Customer, string Url, string Referer, DateTime DateAdded);

/// <summary>Sold — штук в оформлених замовленнях за весь час; Percent — продано на 100 переглядів, null без переглядів.</summary>
public sealed record ProductViewsDto(int ProductId, string Name, string Model, int Viewed, int Sold, decimal? Percent);

public sealed record ProductViewsRow(int ProductId, string Name, string Model, int Viewed, int Sold);

/// <summary>Points — залишок: нараховано мінус витрачено.</summary>
public sealed record CustomerRewardDto(int CustomerId, string? Customer, int Points, DateTime LastChange);

public sealed record TaxReportRow(DateOnly Start, DateOnly End, string Title, int Orders, decimal Total);

public sealed record TaxReportDto(DateOnly From, DateOnly To, IReadOnlyList<TaxReportRow> Rows, decimal Total);

public sealed record TaxFact(string Title, DateTime DateAdded, decimal Value);

public interface IStoreReportStore
{
    Task<IReadOnlyList<OnlineVisitorDto>> OnlineAsync(int limit, CancellationToken ct);

    /// <summary>Найпереглядуваніші увімкнені товари.</summary>
    Task<IReadOnlyList<ProductViewsRow>> ProductViewsAsync(int limit, CancellationToken ct);

    /// <summary>Покупці з ненульовим залишком, найбільші спершу.</summary>
    Task<IReadOnlyList<CustomerRewardDto>> RewardsAsync(int limit, CancellationToken ct);

    Task<IReadOnlyList<TaxFact>> TaxesAsync(DateTime from, DateTime toExclusive, int? statusId, int excludedStatusId, CancellationToken ct);
}

/// <summary>
/// Звіти про те, що записує вітрина, а ERP не показував: хто зараз на сайті, перегляди товарів, бонуси покупців і податки
/// замовлень («Customers Online», «Products Viewed», «Customer Reward Points» і «Tax» адмінки старого сайту).
/// </summary>
public sealed class StoreReportService(IStoreReportStore store, TimeProvider clock)
{
    private const int OnlineLimit = 200;
    private const int ViewsLimit = 100;
    private const int RewardsLimit = 200;

    public Task<IReadOnlyList<OnlineVisitorDto>> OnlineAsync(CancellationToken ct) => store.OnlineAsync(OnlineLimit, ct);

    public async Task<IReadOnlyList<ProductViewsDto>> ProductViewsAsync(CancellationToken ct) =>
        (await store.ProductViewsAsync(ViewsLimit, ct))
            .Select(r => new ProductViewsDto(r.ProductId, r.Name, r.Model, r.Viewed, r.Sold,
                r.Viewed > 0 ? Math.Round(r.Sold * 100m / r.Viewed, 2) : null))
            .ToList();

    public Task<IReadOnlyList<CustomerRewardDto>> RewardsAsync(CancellationToken ct) => store.RewardsAsync(RewardsLimit, ct);

    /// <summary>Рядки податку оформлених замовлень за періодами й назвами податку, як старий звіт «Tax».</summary>
    public async Task<TaxReportDto> TaxesAsync(ReportFilter filter, CancellationToken ct)
    {
        var (from, to) = ReportService.PeriodOf(filter, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var facts = await store.TaxesAsync(
            from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue), filter.StatusId, CustomerRisk.CancelledStatusId, ct);
        var rows = facts
            .GroupBy(f => f.Title)
            .SelectMany(g => SalesReport.Group(g.Select(f => new SaleFact(f.DateAdded, f.Value, 0)), filter.Group, from, to, fillGaps: false)
                .Select(p => new TaxReportRow(p.Start, p.End, g.Key, p.Orders, p.Total)))
            .OrderBy(r => r.Start)
            .ThenBy(r => r.Title, StringComparer.Ordinal)
            .ToList();
        return new TaxReportDto(from, to, rows, rows.Sum(r => r.Total));
    }
}
