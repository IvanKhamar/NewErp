using NewErp.Domain.Competitors;

namespace NewErp.Application.Competitors;

public interface ICompetitorStore
{
    Task<IReadOnlyList<CompetitorDto>> ListCompetitorsAsync(CancellationToken ct);

    Task<CompetitorDto?> GetCompetitorDtoAsync(int id, CancellationToken ct);

    /// <summary>Конкурент для зміни.</summary>
    Task<Competitor?> FindCompetitorAsync(int id, CancellationToken ct);

    /// <summary>Сторінки (одного товару, якщо вказано) за товаром і конкурентом.</summary>
    Task<IReadOnlyList<CompetitorLinkDto>> ListLinksAsync(int? productId, CancellationToken ct);

    Task<CompetitorLinkDto?> GetLinkDtoAsync(int id, CancellationToken ct);

    /// <summary>Сторінка для зміни.</summary>
    Task<CompetitorLink?> FindLinkAsync(int id, CancellationToken ct);

    /// <summary>Id сторінок активних конкурентів, для перевірки всіх.</summary>
    Task<IReadOnlyList<int>> ActiveLinkIdsAsync(CancellationToken ct);

    Task<bool> LinkExistsAsync(int competitorId, int productId, CancellationToken ct);

    Task<bool> ProductExistsAsync(int productId, CancellationToken ct);

    Task<IReadOnlyList<CompetitorPriceDto>> HistoryAsync(int linkId, CancellationToken ct);

    /// <summary>Наші ціни товарів (dd_product.price).</summary>
    Task<IReadOnlyDictionary<int, decimal>> OurPricesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);

    void Add(Competitor competitor);

    void Add(CompetitorLink link);

    void Add(CompetitorPrice price);

    /// <summary>Видаляє сторінку разом з її історією цін.</summary>
    Task DeleteLinkAsync(int linkId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
