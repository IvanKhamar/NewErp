using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.Application.Catalog;

/// <summary>
/// Порядок товарів на вітрині за продажами, як модуль «Порядок товарів» старого сайту: у кожній групі (категорія, колір,
/// тема, мітка, панель допродажу) товари стають за спаданням продажів за період, закріплені вручну — на свої позиції.
/// Перед кожною зміною sort_order зберігається знімок, щоб можна було повернути попередній порядок.
/// </summary>
public sealed class ProductSortService(IProductSortStore store, IOptions<ProductSortOptions> options, TimeProvider clock)
{
    private const int SearchLimit = 15;
    private const int SnapshotLimit = 50;

    public async Task<IReadOnlyList<SortGroupDto>> ListGroupsAsync(CancellationToken ct) =>
        (await store.ListGroupsAsync(ct)).Select(g => new SortGroupDto(g.Type, g.Key, g.Name)).ToList();

    /// <summary>Новий порядок групи без запису: його можна переглянути, змінити вручну й записати через <see cref="ApplyAsync"/>.</summary>
    public async Task<IReadOnlyList<SortPreviewRow>> PreviewAsync(SortGroupType type, string key, SalesPeriod? period, CancellationToken ct)
    {
        var group = await FindGroupAsync(type, key, ct);
        var (order, sales, pinned) = await CalculateAsync(group, ToRange(period), ct);
        if (order.Count == 0)
            return [];

        var products = (await store.ProductsAsync(order, group.Type == SortGroupType.CrossSell, ct)).ToDictionary(p => p.ProductId);
        // Поточна позиція — місце товару серед товарів цієї ж групи за їхнім теперішнім sort_order, а не сам sort_order.
        var current = order
            .OrderBy(id => products.TryGetValue(id, out var p) ? p.SortOrder : 0).ThenBy(id => id)
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index + 1);

        return order.Select((id, index) =>
        {
            var product = products.GetValueOrDefault(id);
            return new SortPreviewRow(id, product?.Name ?? "(товар видалено)", product?.Image ?? "", sales.GetValueOrDefault(id),
                pinned.Contains(id), current[id], index + 1);
        }).ToList();
    }

    /// <summary>Записує готовий порядок групи (наприклад, з прев'ю, зміненого вручну).</summary>
    public async Task<SortResult> ApplyAsync(ApplySortRequest request, CancellationToken ct)
    {
        var group = await FindGroupAsync(request.Type, request.Key, ct);
        var ids = request.ProductIds ?? [];
        if (ids.Count == 0)
            throw new DomainException("Немає товарів для запису — спершу переглянь новий порядок.");
        if (ids.Distinct().Count() != ids.Count)
            throw new DomainException("Товар у порядку повторюється.");

        var snapshot = await SnapshotUnlessCrossSellAsync(group, $"Порядок групи: {group.Name}", ct);
        return new SortResult(snapshot, await store.ApplyAsync(group.Type == SortGroupType.CrossSell, ids, ct));
    }

    public async Task<SortResult> RecalculateGroupAsync(RecalculateGroupRequest request, CancellationToken ct)
    {
        var group = await FindGroupAsync(request.Type, request.Key, ct);
        var range = ToRange(request.Period);
        var snapshot = await SnapshotUnlessCrossSellAsync(group, $"Перерахунок групи: {group.Name}", ct);
        var (order, _, _) = await CalculateAsync(group, range, ct);
        return new SortResult(snapshot, order.Count == 0 ? 0 : await store.ApplyAsync(group.Type == SortGroupType.CrossSell, order, ct));
    }

    /// <summary>
    /// Перераховує всі групи по черзі (теми, категорії, мітки, панель допродажу), один знімок перед початком.
    /// sort_order у товару один, тож товар з кількох груп отримує позицію з останньої з них, як і на старому сайті.
    /// </summary>
    public async Task<SortResult> RecalculateAllAsync(SalesPeriod? period, CancellationToken ct)
    {
        var range = ToRange(period);
        var snapshot = await store.CreateSnapshotAsync("Перед перерахунком усіх груп", Now(), ct);
        var updated = 0;
        foreach (var group in await store.ListGroupsAsync(ct))
        {
            var (order, _, _) = await CalculateAsync(group, range, ct);
            if (order.Count > 0)
                updated += await store.ApplyAsync(group.Type == SortGroupType.CrossSell, order, ct);
        }
        return new SortResult(snapshot, updated);
    }

    public Task<IReadOnlyList<SortSnapshotDto>> ListSnapshotsAsync(CancellationToken ct) => store.ListSnapshotsAsync(SnapshotLimit, ct);

    public async Task RestoreSnapshotAsync(int id, CancellationToken ct)
    {
        if (!await store.RestoreSnapshotAsync(id, ct))
            throw new NotFoundException($"Знімка {id} не знайдено.");
    }

    public Task<IReadOnlyList<SortPinDto>> ListPinsAsync(CancellationToken ct) => store.ListPinsAsync(ct);

    public async Task SetPinAsync(SetPinRequest request, CancellationToken ct)
    {
        if (request.Position < 1)
            throw new DomainException("Позиція рахується з 1.");
        var group = await FindGroupAsync(request.Type, request.Key, ct);
        if ((await store.ProductsAsync([request.ProductId], false, ct)).Count == 0)
            throw new DomainException($"Товару {request.ProductId} не знайдено.");
        await store.SetPinAsync(group, request.Position, request.ProductId, Now(), ct);
    }

    public async Task RemovePinAsync(int id, CancellationToken ct)
    {
        if (!await store.RemovePinAsync(id, ct))
            throw new NotFoundException($"Закріплення {id} не знайдено.");
    }

    public async Task<CrossSellMetricDto> GetCrossSellMetricAsync(CancellationToken ct) => new(await store.CrossSellPanelOnlyAsync(ct));

    public async Task<CrossSellMetricDto> SetCrossSellMetricAsync(CrossSellMetricDto metric, CancellationToken ct)
    {
        await store.SetCrossSellPanelOnlyAsync(metric.PanelOnly, ct);
        return metric;
    }

    public Task<IReadOnlyList<SortProductDto>> SearchProductsAsync(string? term, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2
            ? Task.FromResult<IReadOnlyList<SortProductDto>>([])
            : store.SearchProductsAsync(term.Trim(), SearchLimit, ct);

    private async Task<(IReadOnlyList<int> Order, IReadOnlyDictionary<int, int> Sales, HashSet<int> Pinned)> CalculateAsync(
        SortGroup group, (DateTime From, DateTime To) range, CancellationToken ct)
    {
        var productIds = await store.GroupProductIdsAsync(group, ct);
        if (productIds.Count == 0)
            return ([], new Dictionary<int, int>(), []);

        var crossSellOnly = group.Type == SortGroupType.CrossSell && await store.CrossSellPanelOnlyAsync(ct);
        var sales = await store.SalesAsync(productIds, range.From, range.To, crossSellOnly, ct);
        // Мітка «Новинка» лишається від найновішого товару, без продажів.
        var isNew = group.Type == SortGroupType.Label && string.Equals(group.Key, options.Value.NewLabelName, StringComparison.CurrentCultureIgnoreCase);
        var queue = isNew ? productIds : ProductSorter.BySales(productIds, sales);

        var pins = await store.PinsAsync(group, ct);
        return (ProductSorter.Merge(queue, pins), sales, pins.Select(p => p.ProductId).ToHashSet());
    }

    private async Task<SortGroup> FindGroupAsync(SortGroupType type, string key, CancellationToken ct) =>
        (await store.ListGroupsAsync(ct)).FirstOrDefault(g => g.Type == type && g.Key == key)
            ?? throw new NotFoundException($"Групи «{key}» немає (у ній мають бути увімкнені товари).");

    private async Task<int?> SnapshotUnlessCrossSellAsync(SortGroup group, string note, CancellationToken ct) =>
        group.Type == SortGroupType.CrossSell ? null : await store.CreateSnapshotAsync(note, Now(), ct);

    private (DateTime From, DateTime To) ToRange(SalesPeriod? period)
    {
        var now = Now();
        if (period is { From: { } from, To: { } to })
        {
            if (from > to)
                throw new DomainException("Початок періоду пізніший за кінець.");
            return (from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MaxValue));
        }
        if (period is { From: not null } or { To: not null })
            throw new DomainException("Вкажи обидві дати періоду.");
        var days = period?.Days is { } d && d > 0 ? d : options.Value.DefaultDays;
        return (now.AddDays(-days), now);
    }

    private DateTime Now() => clock.GetLocalNow().DateTime;
}
