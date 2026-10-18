using NewErp.Domain.Partners;

namespace NewErp.Application.Partners;

public interface IPartnerRepository
{
    Task<IReadOnlyList<PartnerListItemDto>> ListAsync(CancellationToken ct);

    Task<PartnerDto?> GetDtoAsync(int id, CancellationToken ct);

    /// <summary>Партнер разом з відповідностями (для зміни чи для фіда).</summary>
    Task<Partner?> GetAsync(int id, CancellationToken ct);

    Task<Partner?> GetByCodeAsync(string code, CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct);

    void Add(Partner partner);

    void Remove(Partner partner);

    Task SaveChangesAsync(CancellationToken ct);

    Task<IReadOnlyList<ShopCategoryDto>> ListCategoriesAsync(CancellationToken ct);

    /// <summary>Товари (і вимкнені теж), у назві яких є term, або з таким id.</summary>
    Task<IReadOnlyList<PartnerProductPickDto>> SearchProductsAsync(string term, int limit, CancellationToken ct);

    /// <summary>Увімкнені товари з цих категорій або з цими id, з фото, категоріями й опціями.</summary>
    Task<IReadOnlyList<FeedProduct>> GetFeedProductsAsync(
        IReadOnlyCollection<int> categoryIds, IReadOnlyCollection<int> productIds, CancellationToken ct);

    Task<IReadOnlyDictionary<int, string>> GetCategoryNamesAsync(IReadOnlyCollection<int> categoryIds, CancellationToken ct);
}
