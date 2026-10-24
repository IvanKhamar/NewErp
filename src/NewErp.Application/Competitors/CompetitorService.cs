using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Competitors;

namespace NewErp.Application.Competitors;

/// <summary>
/// Моніторинг цін конкурентів (як «моніторинг цін конкурентів» старої CRM): сторінки наших товарів на сайтах конкурентів,
/// перевірка, що читає ціну з кожної сторінки, ціни, вписані вручну, і наша ціна проти їхньої.
/// </summary>
public sealed class CompetitorService(ICompetitorStore store, IPageFetcher fetcher, TimeProvider clock)
{
    public Task<IReadOnlyList<CompetitorDto>> CompetitorsAsync(CancellationToken ct) => store.ListCompetitorsAsync(ct);

    public async Task<CompetitorDto> CreateCompetitorAsync(SaveCompetitorRequest request, CancellationToken ct)
    {
        var competitor = new Competitor(request.Name, request.SiteUrl, request.PriceRegex, request.IsActive, Now());
        store.Add(competitor);
        await store.SaveChangesAsync(ct);
        return await RequireCompetitorDtoAsync(competitor.Id, ct);
    }

    public async Task<CompetitorDto> UpdateCompetitorAsync(int id, SaveCompetitorRequest request, CancellationToken ct)
    {
        var competitor = await store.FindCompetitorAsync(id, ct) ?? throw CompetitorNotFound(id);
        competitor.Update(request.Name, request.SiteUrl, request.PriceRegex, request.IsActive);
        await store.SaveChangesAsync(ct);
        return await RequireCompetitorDtoAsync(id, ct);
    }

    public Task<IReadOnlyList<CompetitorLinkDto>> LinksAsync(int? productId, CancellationToken ct) => store.ListLinksAsync(productId, ct);

    public async Task<CompetitorLinkDto> AddLinkAsync(AddCompetitorLinkRequest request, CancellationToken ct)
    {
        var competitor = await store.FindCompetitorAsync(request.CompetitorId, ct) ?? throw CompetitorNotFound(request.CompetitorId);
        if (!await store.ProductExistsAsync(request.ProductId, ct))
            throw new NotFoundException($"Товар {request.ProductId} не знайдено.");
        var link = new CompetitorLink(competitor, request.ProductId, request.Url, Now());
        if (await store.LinkExistsAsync(competitor.Id, request.ProductId, ct))
            throw new DomainException($"Для цього товару в «{competitor.Name}» сторінка вже є.");

        store.Add(link);
        await store.SaveChangesAsync(ct);
        return await RequireLinkDtoAsync(link.Id, ct);
    }

    public async Task DeleteLinkAsync(int id, CancellationToken ct)
    {
        await RequireLinkAsync(id, ct);
        await store.DeleteLinkAsync(id, ct);
    }

    public async Task<CompetitorLinkDto> SetPriceAsync(int linkId, CompetitorPriceRequest request, CancellationToken ct)
    {
        var link = await RequireLinkAsync(linkId, ct);
        if (link.RecordPrice(request.Price, CompetitorLink.Manual, Now()) is { } history)
            store.Add(history);
        await store.SaveChangesAsync(ct);
        return await RequireLinkDtoAsync(linkId, ct);
    }

    public async Task<IReadOnlyList<CompetitorPriceDto>> HistoryAsync(int linkId, CancellationToken ct)
    {
        await RequireLinkAsync(linkId, ct);
        return await store.HistoryAsync(linkId, ct);
    }

    /// <summary>Читає ціну з однієї сторінки зараз.</summary>
    public async Task<CompetitorLinkDto> CheckAsync(int linkId, CancellationToken ct)
    {
        await CheckOneAsync(linkId, ct);
        return await RequireLinkDtoAsync(linkId, ct);
    }

    /// <summary>Читає ціни всіх сторінок активних конкурентів; одна невдала сторінка не зупиняє решту.</summary>
    public async Task<CompetitorCheckResult> CheckAllAsync(CancellationToken ct)
    {
        var ids = await store.ActiveLinkIdsAsync(ct);
        int changed = 0, failed = 0;
        foreach (var id in ids)
        {
            var outcome = await CheckOneAsync(id, ct);
            changed += outcome == Outcome.Changed ? 1 : 0;
            failed += outcome == Outcome.Failed ? 1 : 0;
        }
        return new CompetitorCheckResult(ids.Count, changed, failed);
    }

    public async Task<CompetitorReportDto> ReportAsync(CancellationToken ct)
    {
        var active = (await store.ListCompetitorsAsync(ct)).Where(c => c.IsActive).ToList();
        var byProduct = (await store.ListLinksAsync(null, ct)).GroupBy(l => (l.ProductId, l.ProductName)).ToList();
        var ours = await store.OurPricesAsync(byProduct.Select(g => g.Key.ProductId).ToList(), ct);

        var rows = byProduct.Select(g =>
        {
            var prices = active.Select(c => g.FirstOrDefault(l => l.CompetitorId == c.Id)?.LastPrice).ToList();
            var lowest = prices.Where(p => p is not null).Min();
            var our = ours.GetValueOrDefault(g.Key.ProductId);
            decimal? difference = lowest is { } low && our > 0 ? Math.Round((our - low) * 100 / low, 1, MidpointRounding.AwayFromZero) : null;
            return new CompetitorReportRow(g.Key.ProductId, g.Key.ProductName, our, prices, lowest, difference);
        }).OrderBy(r => r.ProductName).ToList();
        return new CompetitorReportDto(active.Select(c => new CompetitorColumn(c.Id, c.Name)).ToList(), rows);
    }

    private enum Outcome { Same, Changed, Failed }

    private async Task<Outcome> CheckOneAsync(int linkId, CancellationToken ct)
    {
        var link = await RequireLinkAsync(linkId, ct);
        var competitor = await store.FindCompetitorAsync(link.CompetitorId, ct) ?? throw CompetitorNotFound(link.CompetitorId);
        var before = link.LastPrice;
        Outcome outcome;
        try
        {
            var html = await fetcher.FetchAsync(new Uri(link.Url), competitor.Host, ct);
            if (PriceExtractor.Extract(html, competitor.PriceRegex) is { } found)
            {
                var price = Math.Round(found, 2, MidpointRounding.AwayFromZero);
                if (link.RecordPrice(price, CompetitorLink.Auto, Now()) is { } history)
                    store.Add(history);
                outcome = before == price ? Outcome.Same : Outcome.Changed;
            }
            else
            {
                link.RecordFailure("Ціну на сторінці не знайдено.", Now());
                outcome = Outcome.Failed;
            }
        }
        catch (PageFetchException e)
        {
            link.RecordFailure(e.Message, Now());
            outcome = Outcome.Failed;
        }
        // Кожну сторінку зберігаємо одразу: сайти відповідають довго, і зроблене не має пропасти через наступну.
        await store.SaveChangesAsync(ct);
        return outcome;
    }

    private async Task<CompetitorDto> RequireCompetitorDtoAsync(int id, CancellationToken ct) =>
        await store.GetCompetitorDtoAsync(id, ct) ?? throw CompetitorNotFound(id);

    private async Task<CompetitorLink> RequireLinkAsync(int id, CancellationToken ct) =>
        await store.FindLinkAsync(id, ct) ?? throw LinkNotFound(id);

    private async Task<CompetitorLinkDto> RequireLinkDtoAsync(int id, CancellationToken ct) =>
        await store.GetLinkDtoAsync(id, ct) ?? throw LinkNotFound(id);

    private static NotFoundException CompetitorNotFound(int id) => new($"Конкурента {id} не знайдено.");

    private static NotFoundException LinkNotFound(int id) => new($"Сторінку {id} не знайдено.");

    private DateTime Now() => clock.GetLocalNow().DateTime;
}
