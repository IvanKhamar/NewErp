using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Application;

public class ProductSortServiceTests
{
    private static readonly SortGroup Shirts = new(SortGroupType.Category, "5", "Сорочки", CategoryId: 5);
    private static readonly SortGroup New = new(SortGroupType.Label, "Новинка", "Мітка: Новинка", LabelId: 1);
    private static readonly SortGroup Panel = new(SortGroupType.CrossSell, SortGroup.CrossSellKey, "Допродаж");

    private readonly FakeStore _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly ProductSortService _service;

    public ProductSortServiceTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _store.Groups.AddRange([Shirts, New, Panel]);
        _store.Members[Shirts] = [1, 2, 3];
        _store.Members[New] = [3, 1, 2];
        _store.Members[Panel] = [2, 3];
        _store.Sales = new Dictionary<int, int> { [2] = 10, [3] = 4 };
        _service = new ProductSortService(_store, Options.Create(new ProductSortOptions { DefaultDays = 30 }), _clock);
    }

    [Fact]
    public async Task Preview_SortsBySales_WithPinsAndCurrentPositions()
    {
        _store.Pins[Shirts] = [new SortPin(1, 1)];
        _store.SortOrders = new Dictionary<int, int> { [1] = 3, [2] = 1, [3] = 2 };

        var rows = await _service.PreviewAsync(SortGroupType.Category, "5", null, CancellationToken.None);

        Assert.Equal([1, 2, 3], rows.Select(r => r.ProductId));
        Assert.Equal([(true, 0, 3), (false, 10, 1), (false, 4, 2)], rows.Select(r => (r.Pinned, r.Sales, r.CurrentPosition)));
        Assert.Equal((new DateTime(2026, 9, 18, 12, 0, 0), new DateTime(2026, 10, 18, 12, 0, 0)), _store.LastRange);
    }

    [Fact]
    public async Task Preview_UsesDateRangeInclusive()
    {
        await _service.PreviewAsync(SortGroupType.Category, "5", new SalesPeriod(null, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10)), CancellationToken.None);

        Assert.Equal(new DateTime(2026, 10, 1), _store.LastRange.From);
        Assert.Equal(new DateOnly(2026, 10, 10), DateOnly.FromDateTime(_store.LastRange.To));
        Assert.True(_store.LastRange.To.TimeOfDay > TimeSpan.FromHours(23));
    }

    [Fact]
    public async Task Preview_RejectsHalfOrReversedRange()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.PreviewAsync(SortGroupType.Category, "5", new SalesPeriod(null, new DateOnly(2026, 10, 1)), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.PreviewAsync(SortGroupType.Category, "5", new SalesPeriod(null, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 1)), CancellationToken.None));
    }

    [Fact]
    public async Task NewLabel_KeepsNewestFirst_NotSales()
    {
        var rows = await _service.PreviewAsync(SortGroupType.Label, "Новинка", null, CancellationToken.None);

        Assert.Equal([3, 1, 2], rows.Select(r => r.ProductId));
    }

    [Fact]
    public async Task UnknownGroup_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.PreviewAsync(SortGroupType.Category, "999", null, CancellationToken.None));

    [Fact]
    public async Task Apply_SnapshotsAndWrites_ButNotForCrossSell()
    {
        var result = await _service.ApplyAsync(new ApplySortRequest(SortGroupType.Category, "5", [3, 2, 1]), CancellationToken.None);
        var panel = await _service.ApplyAsync(new ApplySortRequest(SortGroupType.CrossSell, SortGroup.CrossSellKey, [3, 2]), CancellationToken.None);

        Assert.Equal(new SortResult(1, 3), result);
        Assert.Equal(new SortResult(null, 2), panel);
        Assert.Equal([(false, new[] { 3, 2, 1 }), (true, new[] { 3, 2 })], _store.Applied.Select(a => (a.CrossSell, a.Ids.ToArray())));
    }

    [Fact]
    public async Task Apply_RejectsEmptyOrDuplicates()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.ApplyAsync(new ApplySortRequest(SortGroupType.Category, "5", []), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => _service.ApplyAsync(new ApplySortRequest(SortGroupType.Category, "5", [1, 1]), CancellationToken.None));
        Assert.Empty(_store.Applied);
    }

    [Fact]
    public async Task RecalculateAll_TakesOneSnapshot_AndAppliesEveryGroup()
    {
        var result = await _service.RecalculateAllAsync(new SalesPeriod(7), CancellationToken.None);

        Assert.Equal(new SortResult(1, 8), result);
        Assert.Single(_store.Snapshots);
        Assert.Equal(3, _store.Applied.Count);
        Assert.Equal(_clock.GetLocalNow().DateTime.AddDays(-7), _store.LastRange.From);
    }

    [Fact]
    public async Task CrossSell_PanelOnlyMetric_PassesFlagToSales()
    {
        await _service.SetCrossSellMetricAsync(new CrossSellMetricDto(true), CancellationToken.None);

        await _service.PreviewAsync(SortGroupType.CrossSell, SortGroup.CrossSellKey, null, CancellationToken.None);
        Assert.True(_store.LastCrossSellOnly);
        await _service.PreviewAsync(SortGroupType.Category, "5", null, CancellationToken.None);
        Assert.False(_store.LastCrossSellOnly);
    }

    [Fact]
    public async Task SetPin_ValidatesPositionAndProduct()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.SetPinAsync(new SetPinRequest(SortGroupType.Category, "5", 0, 1), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => _service.SetPinAsync(new SetPinRequest(SortGroupType.Category, "5", 1, 404), CancellationToken.None));

        await _service.SetPinAsync(new SetPinRequest(SortGroupType.Category, "5", 2, 3), CancellationToken.None);
        Assert.Equal([new SortPin(2, 3)], _store.Pins[Shirts]);
    }

    [Fact]
    public async Task Search_NeedsTwoCharacters()
    {
        Assert.Empty(await _service.SearchProductsAsync(" a ", CancellationToken.None));
        Assert.Single(await _service.SearchProductsAsync("Тов", CancellationToken.None));
    }

    private sealed class FakeStore : IProductSortStore
    {
        public List<SortGroup> Groups { get; } = [];
        public Dictionary<SortGroup, List<int>> Members { get; } = [];
        public Dictionary<SortGroup, List<SortPin>> Pins { get; } = [];
        public Dictionary<int, int> Sales { get; set; } = [];
        public Dictionary<int, int> SortOrders { get; set; } = [];
        public List<(bool CrossSell, IReadOnlyList<int> Ids)> Applied { get; } = [];
        public List<string> Snapshots { get; } = [];
        public (DateTime From, DateTime To) LastRange { get; private set; }
        public bool LastCrossSellOnly { get; private set; }
        private bool _panelOnly;

        public Task<IReadOnlyList<SortGroup>> ListGroupsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SortGroup>>(Groups);

        public Task<IReadOnlyList<int>> GroupProductIdsAsync(SortGroup group, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<int>>(Members.GetValueOrDefault(group) ?? []);

        public Task<IReadOnlyDictionary<int, int>> SalesAsync(IReadOnlyCollection<int> productIds, DateTime from, DateTime to, bool crossSellOnly, CancellationToken ct)
        {
            LastRange = (from, to);
            LastCrossSellOnly = crossSellOnly;
            return Task.FromResult<IReadOnlyDictionary<int, int>>(Sales.Where(s => productIds.Contains(s.Key)).ToDictionary());
        }

        public Task<IReadOnlyList<SortProductDto>> ProductsAsync(IReadOnlyCollection<int> productIds, bool crossSell, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SortProductDto>>(productIds.Where(id => id < 100)
                .Select(id => new SortProductDto(id, $"Товар {id}", "", SortOrders.GetValueOrDefault(id))).ToList());

        public Task<int> ApplyAsync(bool crossSell, IReadOnlyList<int> orderedProductIds, CancellationToken ct)
        {
            Applied.Add((crossSell, orderedProductIds));
            return Task.FromResult(orderedProductIds.Count);
        }

        public Task<IReadOnlyList<SortPin>> PinsAsync(SortGroup group, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SortPin>>(Pins.GetValueOrDefault(group) ?? []);

        public Task<IReadOnlyList<SortPinDto>> ListPinsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SortPinDto>>([]);

        public Task SetPinAsync(SortGroup group, int position, int productId, DateTime now, CancellationToken ct)
        {
            Pins[group] = [.. (Pins.GetValueOrDefault(group) ?? []).Where(p => p.ProductId != productId && p.Position != position), new SortPin(position, productId)];
            return Task.CompletedTask;
        }

        public Task<bool> RemovePinAsync(int id, CancellationToken ct) => Task.FromResult(false);

        public Task<int> CreateSnapshotAsync(string note, DateTime now, CancellationToken ct)
        {
            Snapshots.Add(note);
            return Task.FromResult(Snapshots.Count);
        }

        public Task<IReadOnlyList<SortSnapshotDto>> ListSnapshotsAsync(int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<SortSnapshotDto>>([]);

        public Task<bool> RestoreSnapshotAsync(int id, CancellationToken ct) => Task.FromResult(false);

        public Task<bool> CrossSellPanelOnlyAsync(CancellationToken ct) => Task.FromResult(_panelOnly);

        public Task SetCrossSellPanelOnlyAsync(bool panelOnly, CancellationToken ct)
        {
            _panelOnly = panelOnly;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SortProductDto>> SearchProductsAsync(string term, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SortProductDto>>([new SortProductDto(1, term, "", 0)]);
    }
}
