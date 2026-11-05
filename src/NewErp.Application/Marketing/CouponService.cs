using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Marketing;

namespace NewErp.Application.Marketing;

/// <summary>Type: «P» — відсоток, «F» — сума. DateStart і DateEnd null — без межі (0000-00-00 у даних старого сайту).</summary>
public sealed record CouponListItem(
    int Id, string Name, string Code, string Type, decimal Discount, decimal Total, DateOnly? DateStart, DateOnly? DateEnd,
    int UsesTotal, bool Active, int Used);

public sealed record CouponHistoryRow(int OrderId, int CustomerId, decimal Amount, DateTime DateAdded);

public sealed record CouponCardDto(
    int Id, string Name, string Code, string Type, decimal Discount, decimal Total, bool Logged, bool Shipping,
    DateOnly? DateStart, DateOnly? DateEnd, int UsesTotal, int UsesCustomer, bool Active, DateTime DateAdded,
    IReadOnlyList<LookupItem> Products, IReadOnlyList<LookupItem> Categories,
    IReadOnlyList<LookupItem> ExcludedProducts, IReadOnlyList<LookupItem> ExcludedCategories,
    IReadOnlyList<CouponHistoryRow> History);

/// <summary>Total — мінімальна сума замовлення; UsesTotal і UsesCustomer: 0 — без обмежень; Logged — лише для того, хто ввійшов.</summary>
public sealed record SaveCouponRequest(
    string? Name = null, string? Code = null, string Type = Coupon.Percent, decimal? Discount = null, decimal Total = 0,
    bool Logged = false, bool Shipping = false, DateOnly? DateStart = null, DateOnly? DateEnd = null, int UsesTotal = 1, int UsesCustomer = 1,
    bool Active = true, IReadOnlyList<int>? ProductIds = null, IReadOnlyList<int>? CategoryIds = null,
    IReadOnlyList<int>? ExcludedProductIds = null, IReadOnlyList<int>? ExcludedCategoryIds = null);

public interface ICouponStore
{
    Task<IReadOnlyList<CouponListItem>> ListAsync(string search, int limit, CancellationToken ct);

    Task<CouponCardDto?> GetAsync(int id, CancellationToken ct);

    Task<bool> CodeTakenAsync(string code, int? exceptId, CancellationToken ct);

    Task<IReadOnlyList<int>> MissingProductsAsync(IReadOnlyList<int> ids, CancellationToken ct);

    Task<IReadOnlyList<int>> MissingCategoriesAsync(IReadOnlyList<int> ids, CancellationToken ct);

    Task<int> AddAsync(Coupon coupon, DateTime now, CancellationToken ct);

    /// <summary>Замінює купон і його списки; false — такого купона немає.</summary>
    Task<bool> UpdateAsync(int id, Coupon coupon, CancellationToken ct);

    Task<int> UsedAsync(int id, CancellationToken ct);

    Task DeleteAsync(int id, CancellationToken ct);
}

/// <summary>Купони старого сайту з товарами й категоріями, на які вони діють чи не діють. Застосовує їх вітрина, ERP лише зберігає.</summary>
public sealed class CouponService(ICouponStore store, TimeProvider clock, IAuditLog audit)
{
    private const int ListLimit = 500;

    public Task<IReadOnlyList<CouponListItem>> ListAsync(string? search, CancellationToken ct) =>
        store.ListAsync(search?.Trim() ?? "", ListLimit, ct);

    public async Task<CouponCardDto> GetAsync(int id, CancellationToken ct) =>
        await store.GetAsync(id, ct) ?? throw new NotFoundException($"Купон {id} не знайдено.");

    public async Task<CouponCardDto> CreateAsync(SaveCouponRequest request, int userId, CancellationToken ct)
    {
        var coupon = await CheckedAsync(request, null, ct);
        var id = await store.AddAsync(coupon, clock.GetLocalNow().DateTime, ct);
        audit.Record($"coupon created: {id}, {coupon.Type} {coupon.Discount}, by user {userId}");
        return await GetAsync(id, ct);
    }

    public async Task<CouponCardDto> UpdateAsync(int id, SaveCouponRequest request, int userId, CancellationToken ct)
    {
        var coupon = await CheckedAsync(request, id, ct);
        if (!await store.UpdateAsync(id, coupon, ct))
            throw new NotFoundException($"Купон {id} не знайдено.");
        audit.Record($"coupon changed: {id}, {coupon.Type} {coupon.Discount}, by user {userId}");
        return await GetAsync(id, ct);
    }

    /// <summary>Купон, яким уже платили, лишається для історії замовлень: його можна лише вимкнути.</summary>
    public async Task DeleteAsync(int id, int userId, CancellationToken ct)
    {
        await GetAsync(id, ct);
        if (await store.UsedAsync(id, ct) > 0)
            throw new DomainException("Купон уже використали в замовленнях — його можна лише вимкнути.");
        await store.DeleteAsync(id, ct);
        audit.Record($"coupon deleted: {id}, by user {userId}");
    }

    private async Task<Coupon> CheckedAsync(SaveCouponRequest request, int? id, CancellationToken ct)
    {
        var coupon = Coupon.Of(
            request.Name, request.Code, request.Type, request.Discount, request.Total, request.Logged, request.Shipping,
            request.DateStart, request.DateEnd, request.UsesTotal, request.UsesCustomer, request.Active,
            request.ProductIds, request.CategoryIds, request.ExcludedProductIds, request.ExcludedCategoryIds);
        if (await store.CodeTakenAsync(coupon.Code, id, ct))
            throw new DomainException($"Купон з кодом {coupon.Code} уже є.");
        var products = await store.MissingProductsAsync([.. coupon.ProductIds, .. coupon.ExcludedProductIds], ct);
        if (products.Count > 0)
            throw new DomainException($"Товарів {string.Join(", ", products)} немає.");
        var categories = await store.MissingCategoriesAsync([.. coupon.CategoryIds, .. coupon.ExcludedCategoryIds], ct);
        if (categories.Count > 0)
            throw new DomainException($"Категорій {string.Join(", ", categories)} немає.");
        return coupon;
    }
}
