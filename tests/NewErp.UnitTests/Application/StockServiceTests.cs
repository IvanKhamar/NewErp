using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Stock;
using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Application;

public class StockServiceTests
{
    private readonly FakeStockRepository _repo = new();
    private readonly StockService _service;

    public StockServiceTests() => _service = new StockService(_repo, new FakeTimeProvider());

    [Fact]
    public async Task Adjust_ChangesQuantityAndRecordsMovement()
    {
        var item = new StockItem(1, null, "Льон", "м", 5);
        _repo.Items[1] = item;

        var result = await _service.AdjustAsync(1, new AdjustStockRequest(12, "Прихід"), userId: 2, "sklad", CancellationToken.None);

        Assert.Equal(12, item.Quantity);
        Assert.Equal(12, result.QuantityAfter);
        Assert.Single(_repo.Movements);
        Assert.Equal(1, _repo.SaveCount);
    }

    [Fact]
    public async Task Adjust_UnknownItem_NotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AdjustAsync(42, new AdjustStockRequest(1, "Прихід"), userId: 1, "sklad", CancellationToken.None));

    [Fact]
    public async Task Adjust_RuleViolation_DoesNotSave()
    {
        _repo.Items[1] = new StockItem(1, null, "Льон", "м", 5);

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.AdjustAsync(1, new AdjustStockRequest(-1, "Пошиття"), userId: 1, "sklad", CancellationToken.None));
        Assert.Equal(0, _repo.SaveCount);
    }

    [Fact]
    public async Task CreateItem_UnknownCategory_Rejected() =>
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateItemAsync(new CreateStockItemRequest(99, null, "Нитки", "шт", 0), CancellationToken.None));

    [Fact]
    public async Task CreateCategory_DuplicateName_Rejected()
    {
        _repo.CategoryNames.Add("Тканини");

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateCategoryAsync(new CreateStockCategoryRequest(" Тканини "), CancellationToken.None));
    }

    private sealed class FakeStockRepository : IStockRepository
    {
        public Dictionary<int, StockItem> Items { get; } = [];
        public HashSet<string> CategoryNames { get; } = [];
        public List<StockMovement> Movements { get; } = [];
        public int SaveCount { get; private set; }

        public Task<StockItem?> FindItemAsync(int itemId, CancellationToken ct) => Task.FromResult(Items.GetValueOrDefault(itemId));
        public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CategoryNameTakenAsync(string name, CancellationToken ct) => Task.FromResult(CategoryNames.Contains(name));
        public Task<bool> SupplierExistsAsync(int supplierId, CancellationToken ct) => Task.FromResult(false);
        public void Add(StockMovement movement) => Movements.Add(movement);
        public void Add(StockCategory category) { }
        public void Add(StockSupplier supplier) { }
        public void Add(StockItem item) { }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LookupItem>> ListCategoriesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<StockSupplierDto>> ListSuppliersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<StockItemDto>> ListItemsAsync(StockItemFilter filter, CancellationToken ct) => throw new NotSupportedException();
        public Task<StockItemDto?> GetItemDtoAsync(int itemId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(int itemId, CancellationToken ct) => throw new NotSupportedException();
    }
}