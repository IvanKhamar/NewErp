using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.Application.Catalog;

// Масові ціни (Python first, пункт 46) з переглядом перед записом, і фільтри розмірів (пункт 52).

/// <summary>
/// Target price — звичайна ціна; sale — відсоток акції групи 1 (set_percent) або сама акція (remove).
/// Товари: будь-якої з категорій і будь-якого з виробників; «усі товари» треба сказати явно.
/// </summary>
public sealed record PriceChangeRequest(
    string Target = PriceChangeService.TargetPrice,
    string Operation = PriceChange.Add,
    decimal Amount = 0,
    string Unit = PriceChange.Amount,
    IReadOnlyList<int>? CategoryIds = null,
    IReadOnlyList<int>? ManufacturerIds = null,
    bool AllProducts = false);

/// <summary>Old/New — ціни для target price, відсотки акції для sale (null — без акції).</summary>
public sealed record PriceChangeRow(int ProductId, string Name, string Model, decimal? Old, decimal? New);

/// <summary>Rows — перші <see cref="PriceChangeService.PreviewRows"/> змінених.</summary>
public sealed record PriceChangePreview(int Total, int Changed, IReadOnlyList<PriceChangeRow> Rows);

public sealed record PriceChangeResult(int Changed);

/// <summary>SalePercent — відсоток акції групи 1; null — акції немає.</summary>
public sealed record PricedProduct(int ProductId, string Name, string Model, decimal Price, decimal? SalePercent);

public interface IPriceStore
{
    /// <summary>Порожній список — без обмеження за ним.</summary>
    Task<IReadOnlyList<PricedProduct>> ProductsAsync(IReadOnlyCollection<int> categoryIds, IReadOnlyCollection<int> manufacturerIds, CancellationToken ct);

    Task SetPricesAsync(IReadOnlyDictionary<int, decimal> prices, CancellationToken ct);

    /// <summary>Акції групи 1 отримують цей відсоток; товар без акції — нову безстрокову.</summary>
    Task SetSalePercentsAsync(IReadOnlyCollection<int> productIds, decimal percent, CancellationToken ct);

    Task RemoveSalesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);
}

public sealed class PriceChangeService(IPriceStore store, IAuditLog audit)
{
    public const string TargetPrice = "price";
    public const string TargetSale = "sale";
    public const string SetPercent = "set_percent";
    public const string Remove = "remove";
    public const int PreviewRows = 500;
    private const int MaxIds = 500;
    private const decimal MaxAmount = 1_000_000;

    public async Task<PriceChangePreview> PreviewAsync(PriceChangeRequest request, CancellationToken ct)
    {
        var (total, rows) = await PlanAsync(request, ct);
        return new PriceChangePreview(total, rows.Count, [.. rows.Take(PreviewRows)]);
    }

    public async Task<PriceChangeResult> ApplyAsync(PriceChangeRequest request, int userId, CancellationToken ct)
    {
        var (_, rows) = await PlanAsync(request, ct);
        if (rows.Count == 0)
            return new PriceChangeResult(0);
        var ids = rows.Select(r => r.ProductId).ToList();
        if (request.Target == TargetPrice)
            await store.SetPricesAsync(rows.Where(r => r.New is not null).ToDictionary(r => r.ProductId, r => r.New!.Value), ct);
        else if (request.Operation == Remove)
            await store.RemoveSalesAsync(ids, ct);
        else
            await store.SetSalePercentsAsync(ids, request.Amount, ct);
        foreach (var row in rows)
            audit.Record($"mass {request.Target} change: product {row.ProductId}, {row.Old} -> {row.New}, by user {userId}");
        return new PriceChangeResult(rows.Count);
    }

    private async Task<(int Total, List<PriceChangeRow> Rows)> PlanAsync(PriceChangeRequest request, CancellationToken ct)
    {
        var categoryIds = request.CategoryIds ?? [];
        var manufacturerIds = request.ManufacturerIds ?? [];
        if (request.Amount is < 0 or > MaxAmount)
            throw new DomainException("Число — від 0 до 1 000 000.");
        if (categoryIds.Count > MaxIds || manufacturerIds.Count > MaxIds)
            throw new DomainException($"Не більше {MaxIds} категорій і {MaxIds} виробників за раз.");
        if (categoryIds.Count == 0 && manufacturerIds.Count == 0 && !request.AllProducts)
            throw new DomainException("Оберіть категорії чи виробників або явно позначте «Усі товари».");
        switch (request.Target)
        {
            case TargetPrice when !PriceChange.Operations.Contains(request.Operation):
                throw new DomainException("Для ціни: додати, відняти, помножити або поділити.");
            case TargetPrice:
                PriceChange.Changed(0, request.Operation, request.Amount, request.Unit); // the unit and division by zero
                break;
            case TargetSale when request.Operation is not (SetPercent or Remove):
                throw new DomainException("Для акції: встановити відсоток або прибрати акцію.");
            case TargetSale when request.Operation == SetPercent:
                PriceChange.SalePercent(request.Amount);
                break;
            case TargetSale:
                break;
            default:
                throw new DomainException("Що змінюємо: ціну (price) або акцію (sale).");
        }

        var products = await store.ProductsAsync(categoryIds, manufacturerIds, ct);
        var rows = new List<PriceChangeRow>();
        foreach (var p in products)
        {
            var (old, @new) = request.Target == TargetPrice
                ? (p.Price, PriceChange.Changed(p.Price, request.Operation, request.Amount, request.Unit))
                : (p.SalePercent, request.Operation == Remove ? null : (decimal?)request.Amount);
            if (old != @new)
                rows.Add(new PriceChangeRow(p.ProductId, p.Name, p.Model, old, @new));
        }
        return (products.Count, rows);
    }
}

// ═══ Фільтри розмірів (пункт 52) ═══

/// <summary>CreatedValues — нові значення фільтра («XL» з'явився вперше).</summary>
public sealed record SizeFilterResult(int CreatedValues, int Linked, int Unlinked);

public interface ISizeFilterStore
{
    /// <summary>(товар, назва розміру) кожного розміру кожного товару.</summary>
    Task<IReadOnlyList<(int ProductId, string Size)>> ProductSizesAsync(CancellationToken ct);

    /// <summary>Значення групи фільтрів «Розмір»: назва → id фільтра.</summary>
    Task<Dictionary<string, int>> SizeFiltersAsync(CancellationToken ct);

    Task<int> CreateSizeFilterAsync(string name, CancellationToken ct);

    /// <summary>Зв'язки (товар, фільтр) з групою «Розмір».</summary>
    Task<HashSet<(int ProductId, int FilterId)>> SizeLinksAsync(CancellationToken ct);

    Task ChangeLinksAsync(IReadOnlyCollection<(int ProductId, int FilterId)> add, IReadOnlyCollection<(int ProductId, int FilterId)> remove, CancellationToken ct);
}

/// <summary>
/// Вітрина фільтрує за розміром через dd_product_filter, а розміри товар отримує у формі чи з виробництва, тож фільтри
/// розходяться з розмірами. Перебудова дає кожному товару рівно фільтри його розмірів (створюючи бракуюче значення), як
/// старий updateSizeFilters, що лише додавав і ніколи не знімав.
/// </summary>
public sealed class SizeFilterService(ISizeFilterStore store, IAuditLog audit)
{
    public async Task<SizeFilterResult> RebuildAsync(int userId, CancellationToken ct)
    {
        var filters = await store.SizeFiltersAsync(ct);
        var wanted = new HashSet<(int, int)>();
        var created = 0;
        foreach (var (productId, size) in await store.ProductSizesAsync(ct))
        {
            var name = size.Trim();
            if (name.Length == 0)
                continue;
            if (!filters.TryGetValue(name, out var filterId))
            {
                filters[name] = filterId = await store.CreateSizeFilterAsync(name, ct);
                created++;
            }
            wanted.Add((productId, filterId));
        }
        var current = await store.SizeLinksAsync(ct);
        var add = wanted.Except(current).ToList();
        var remove = current.Except(wanted).ToList();
        await store.ChangeLinksAsync(add, remove, ct);
        audit.Record($"size filters rebuilt: +{created} values, +{add.Count} links, -{remove.Count} links, by user {userId}");
        return new SizeFilterResult(created, add.Count, remove.Count);
    }
}
