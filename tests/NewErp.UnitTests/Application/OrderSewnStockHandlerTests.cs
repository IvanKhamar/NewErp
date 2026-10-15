using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Production;
using NewErp.Domain.Orders;
using NewErp.Domain.Production;
using NewErp.Domain.Stock;

namespace NewErp.UnitTests.Application;

public class OrderSewnStockHandlerTests
{
    private const int OrderId = 100;
    private const int SizeM = 7;
    private const int OptionM = 47;
    private const int Accepted = 18;
    private const int Manufacturing = 25;
    private const int Pending = 24;
    private const int Cancelled = 19;

    private readonly FakeRepository _repo = new();
    private readonly OrderSewnStockHandler _handler;
    private readonly ProductionModel _model;

    public OrderSewnStockHandlerTests()
    {
        _model = new ProductionModel("Сукня", ProductionModel.Clothing, 0, 0);
        _model.SetSizes([("M", 100m)]);
        typeof(ProductionModel).GetProperty(nameof(ProductionModel.Id))!.SetValue(_model, 1);
        typeof(ProductionSize).GetProperty(nameof(ProductionSize.Id))!.SetValue(_model.Sizes[0], SizeM);
        _model.SetSizeOptions([(SizeM, OptionM)]);
        _model.RecordSewn(SizeM, 5, null, 1, "sklad", DateTime.Now);

        _repo.Models.Add(_model);
        _repo.Lines = [new OrderSizeLine(101, "Сукня", 2, ProductionId: 1, OptionValueIds: [OptionM])];
        _handler = new OrderSewnStockHandler(
            _repo, Options.Create(new ProductionOptions { OrderSubtractStatusIds = [Accepted, Manufacturing] }), new FakeTimeProvider());
    }

    private decimal Sewn => _model.Sizes[0].SewnStock;

    private Task<IReadOnlyList<string>> Change(int from, int to) =>
        _handler.OnStatusChangingAsync(new OrderStatusChange(OrderId, from, to, 3, DateTime.Now), CancellationToken.None);

    [Fact]
    public async Task EnteringSet_TakesOrderedQuantityOfThatSize()
    {
        await Change(Pending, Accepted);

        Assert.Equal(3, Sewn);
        Assert.Equal(ProductionModel.OrderComment(OrderId), Assert.Single(_repo.Log).Comment);
    }

    [Fact]
    public async Task EnteringAgain_WhenOrderAlreadyHoldsItems_DoesNotTakeTwice()
    {
        await Change(Pending, Accepted);
        await Change(Pending, Accepted); // повтор після збою, коли статус не встиг зберегтися

        Assert.Equal(3, Sewn);
    }

    [Fact]
    public async Task MovingInsideSet_ChangesNothing()
    {
        await Change(Pending, Accepted);
        await Change(Accepted, Manufacturing);

        Assert.Equal(3, Sewn);
        Assert.Single(_repo.Log);
    }

    [Fact]
    public async Task Leaving_ReturnsOnlyWhatWasTaken()
    {
        _model.Ship(SizeM, 4, null, 1, "sklad", DateTime.Now); // лишився 1 виріб, а замовлено 2

        var warnings = await Change(Pending, Accepted);
        Assert.Single(warnings);
        await Change(Accepted, Cancelled);

        Assert.Equal(1, Sewn); // повернули 1, а не 2
    }

    [Fact]
    public async Task SizeNotLinkedToOption_WarnsAndChangesNothing()
    {
        _repo.Lines = [new OrderSizeLine(101, "Сукня", 2, ProductionId: 1, OptionValueIds: [999])];

        var warnings = await Change(Pending, Accepted);

        Assert.Contains("не зв'язаний", Assert.Single(warnings));
        Assert.Equal(5, Sewn);
    }

    private sealed class FakeRepository : IProductionRepository
    {
        public List<ProductionModel> Models { get; } = [];

        public IReadOnlyList<OrderSizeLine> Lines { get; set; } = [];

        public List<ProductionLogEntry> Log { get; } = [];

        public Task<IReadOnlyList<OrderSizeLine>> GetOrderSizeLinesAsync(int orderId, CancellationToken ct) => Task.FromResult(Lines);

        public Task<IReadOnlyList<ProductionModel>> FindModelsAsync(IReadOnlyCollection<int> productionIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProductionModel>>(Models.Where(m => productionIds.Contains(m.Id)).ToList());

        // Так само, як справжнє сховище: списане з «Замовлення #id» мінус повернене зі «Скасування #id».
        public Task<IReadOnlyDictionary<int, decimal>> GetSewnHeldByOrderAsync(int orderId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, decimal>>(Log
                .GroupBy(l => l.SizeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Where(l => l.Comment == ProductionModel.OrderComment(orderId)).Sum(l => l.Quantity)
                       - g.Where(l => l.Comment == ProductionModel.CancelComment(orderId)).Sum(l => l.Quantity)));

        public void Add(ProductionLogEntry entry) => Log.Add(entry);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ProductionModelSummaryDto>> ListAsync(ProductionModelFilter filter, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProductionModelDto?> GetDtoAsync(int productionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProductionModel?> FindModelAsync(int productionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProductionComponent?> FindComponentAsync(int componentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> ComponentIdsAsync(int productionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> MissingStockItemIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> SizesInUseAsync(IReadOnlyCollection<int> sizeIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ComponentStock>> FindComponentStockAsync(int componentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductionComponent>> FindComponentsAsync(int productionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ComponentStock>> FindComponentStocksAsync(IReadOnlyCollection<int> componentIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<StockItem>> FindStockItemsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductionLogDto>> ListLogAsync(ProductionLogFilter filter, CancellationToken ct) => throw new NotSupportedException();
        public void Add(StockMovement movement) => throw new NotSupportedException();
        public void Add(ComponentStock stock) => throw new NotSupportedException();
        public void Add(ProductionModel model) => throw new NotSupportedException();
        public void Add(ProductionComponent component) => throw new NotSupportedException();
        public void Remove(ProductionComponent component) => throw new NotSupportedException();
        public void Remove(ComponentStock stock) => throw new NotSupportedException();
    }
}
