using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Production;
using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.UnitTests.Application;

public class ProductionServiceTests
{
    private readonly FakeBomRepository _repo = new();
    private readonly ProductionService _service;

    public ProductionServiceTests() => _service = new ProductionService(_repo, new FakeTimeProvider());

    [Fact]
    public async Task SetBom_New_AddsAndSaves()
    {
        _repo.StockItemIds.UnionWith([1, 2]);

        await _service.SetBomAsync(101, new SetBomRequest([new(1, 2.5m), new(2, 6)]), userId: 3, CancellationToken.None);

        var bom = Assert.Single(_repo.Boms.Values);
        Assert.Equal(2, bom.Lines.Count);
        Assert.Equal(3, bom.UpdatedByUserId);
        Assert.Equal(1, _repo.SaveCount);
    }

    [Fact]
    public async Task SetBom_UnknownStockItem_RejectedWithoutSaving()
    {
        _repo.StockItemIds.Add(1);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _service.SetBomAsync(101, new SetBomRequest([new(1, 1), new(99, 1)]), 1, CancellationToken.None));

        Assert.Contains("99", error.Message);
        Assert.Equal(0, _repo.SaveCount);
    }

    [Fact]
    public async Task DeleteBom_Missing_NotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteBomAsync(101, CancellationToken.None));

    [Fact]
    public async Task OrderRequirements_SumsAcrossProductsAndShowsShortage()
    {
        _repo.OrderProducts[1] =
        [
            new OrderProductLine(101, "Сукня", 2),
            new OrderProductLine(102, "Сорочка", 1),
            new OrderProductLine(103, "Без специфікації", 1),
        ];
        _repo.Components.AddRange(
        [
            new BomComponent(101, 1, "Льон", "м", 2.5m, InStock: 4),
            new BomComponent(101, 2, "Ґудзики", "шт", 6, InStock: 100),
            new BomComponent(102, 1, "Льон", "м", 1.5m, InStock: 4),
        ]);

        var result = await _service.GetOrderRequirementsAsync(1, CancellationToken.None);

        var linen = result.Materials.Single(m => m.StockItemId == 1);
        Assert.Equal(6.5m, linen.Required); // 2 × 2.5 + 1 × 1.5
        Assert.Equal(2.5m, linen.Shortage);
        var buttons = result.Materials.Single(m => m.StockItemId == 2);
        Assert.Equal(12, buttons.Required);
        Assert.Equal(0, buttons.Shortage);
        Assert.Equal(103, Assert.Single(result.ProductsWithoutBom).ProductId);
    }

    [Fact]
    public async Task OrderRequirements_UnknownOrder_NotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetOrderRequirementsAsync(42, CancellationToken.None));

    private sealed class FakeBomRepository : IBomRepository
    {
        public Dictionary<int, Bom> Boms { get; } = [];

        public HashSet<int> StockItemIds { get; } = [];

        public Dictionary<int, IReadOnlyList<OrderProductLine>> OrderProducts { get; } = [];

        public List<BomComponent> Components { get; } = [];

        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<BomSummaryDto>> ListAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<BomDto?> GetDtoAsync(int productId, CancellationToken ct) => throw new NotSupportedException();

        public Task<Bom?> FindAsync(int productId, CancellationToken ct) => Task.FromResult(Boms.GetValueOrDefault(productId));

        public Task<IReadOnlyList<int>> MissingStockItemIdsAsync(IReadOnlyCollection<int> stockItemIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<int>>(stockItemIds.Where(id => !StockItemIds.Contains(id)).ToList());

        public Task<IReadOnlyList<OrderProductLine>?> GetOrderProductsAsync(int orderId, CancellationToken ct) =>
            Task.FromResult(OrderProducts.GetValueOrDefault(orderId));

        public Task<IReadOnlyList<BomComponent>> GetComponentsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<BomComponent>>(Components.Where(c => productIds.Contains(c.ProductId)).ToList());

        public void Add(Bom bom) => Boms[bom.ProductId] = bom;

        public void Remove(Bom bom) => Boms.Remove(bom.ProductId);

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
