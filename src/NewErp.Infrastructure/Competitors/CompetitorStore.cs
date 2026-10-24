using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Competitors;
using NewErp.Domain.Competitors;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Competitors;

public sealed class CompetitorStore(ErpDbContext db, IOptions<ErpOptions> options) : ICompetitorStore
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<IReadOnlyList<CompetitorDto>> ListCompetitorsAsync(CancellationToken ct) =>
        await Competitors(db.Competitors).ToListAsync(ct);

    public Task<CompetitorDto?> GetCompetitorDtoAsync(int id, CancellationToken ct) =>
        Competitors(db.Competitors.Where(c => c.Id == id)).FirstOrDefaultAsync(ct);

    public Task<Competitor?> FindCompetitorAsync(int id, CancellationToken ct) =>
        db.Competitors.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CompetitorLinkDto>> ListLinksAsync(int? productId, CancellationToken ct) =>
        await Links(productId is { } id ? db.CompetitorLinks.Where(l => l.ProductId == id) : db.CompetitorLinks).ToListAsync(ct);

    public Task<CompetitorLinkDto?> GetLinkDtoAsync(int id, CancellationToken ct) =>
        Links(db.CompetitorLinks.Where(l => l.Id == id)).FirstOrDefaultAsync(ct);

    public Task<CompetitorLink?> FindLinkAsync(int id, CancellationToken ct) =>
        db.CompetitorLinks.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<int>> ActiveLinkIdsAsync(CancellationToken ct) =>
        await (from l in db.CompetitorLinks
               join c in db.Competitors on l.CompetitorId equals c.Id
               where c.IsActive
               orderby l.Id
               select l.Id).ToListAsync(ct);

    public Task<bool> LinkExistsAsync(int competitorId, int productId, CancellationToken ct) =>
        db.CompetitorLinks.AnyAsync(l => l.CompetitorId == competitorId && l.ProductId == productId, ct);

    public Task<bool> ProductExistsAsync(int productId, CancellationToken ct) =>
        db.DdProducts.AnyAsync(p => p.ProductId == productId, ct);

    public async Task<IReadOnlyList<CompetitorPriceDto>> HistoryAsync(int linkId, CancellationToken ct) =>
        await db.CompetitorPrices.AsNoTracking()
            .Where(p => p.LinkId == linkId)
            .OrderByDescending(p => p.CheckedAt).ThenByDescending(p => p.Id)
            .Select(p => new CompetitorPriceDto(p.Price, p.Source, p.CheckedAt))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, decimal>> OurPricesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
        await db.DdProducts.AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.Price, ct);

    public void Add(Competitor competitor) => db.Competitors.Add(competitor);

    public void Add(CompetitorLink link) => db.CompetitorLinks.Add(link);

    public void Add(CompetitorPrice price) => db.CompetitorPrices.Add(price);

    public async Task DeleteLinkAsync(int linkId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.CompetitorPrices.Where(p => p.LinkId == linkId).ExecuteDeleteAsync(ct);
        await db.CompetitorLinks.Where(l => l.Id == linkId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private IQueryable<CompetitorDto> Competitors(IQueryable<Competitor> competitors) =>
        from c in competitors.AsNoTracking()
        orderby c.Name
        select new CompetitorDto(c.Id, c.Name, c.SiteUrl, c.PriceRegex, c.IsActive, db.CompetitorLinks.Count(l => l.CompetitorId == c.Id));

    // Назва товару — з опису мовою Erp:LanguageId; товар без опису показується як «Товар N».
    private IQueryable<CompetitorLinkDto> Links(IQueryable<CompetitorLink> links) =>
        from l in links.AsNoTracking()
        join c in db.Competitors on l.CompetitorId equals c.Id
        join pd in db.DdProductDescriptions.Where(d => d.LanguageId == _languageId) on l.ProductId equals pd.ProductId into names
        from pd in names.DefaultIfEmpty()
        orderby pd.Name, c.Name
        select new CompetitorLinkDto(
            l.Id, l.CompetitorId, c.Name, l.ProductId, pd == null ? "Товар " + l.ProductId : pd.Name, l.Url,
            l.LastPrice, l.LastCheckedAt, l.LastError);
}
