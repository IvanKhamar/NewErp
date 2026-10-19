using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Production;
using NewErp.Domain.Production;

namespace NewErp.UnitTests.Application;

public class SewingPlanServiceTests
{
    private const int SizeS = 6;
    private const int SizeM = 7;
    private const int OptionS = 46;
    private const int OptionM = 47;
    private const int Product = 101;

    private readonly FakeSource _source = new();
    private readonly SewingPlanService _service;

    public SewingPlanServiceTests()
    {
        var model = new ProductionModel("Сукня", ProductionModel.Clothing, 0, 0);
        model.SetSizes([("S", 50m), ("M", 50m)]);
        typeof(ProductionModel).GetProperty(nameof(ProductionModel.Id))!.SetValue(model, 1);
        typeof(ProductionSize).GetProperty(nameof(ProductionSize.Id))!.SetValue(model.Sizes[0], SizeS);
        typeof(ProductionSize).GetProperty(nameof(ProductionSize.Id))!.SetValue(model.Sizes[1], SizeM);
        model.SetSizeOptions([(SizeS, OptionS), (SizeM, OptionM)]);
        model.RecordSewn(SizeS, 1, null, 1, "sklad", DateTime.Now);
        _source.Models.Add(model);
        _source.Days[Product] = 10;

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new SewingPlanService(_source, Options.Create(new ProductionOptions { PlanStatusIds = [24] }), clock);
    }

    [Fact]
    public async Task Rows_PerSize_SortedByDeadline_OverdueMarked()
    {
        _source.Orders =
        [
            Order(1, new DateTime(2026, 10, 15), Line(OptionM, 2)),
            Order(2, new DateTime(2026, 10, 1), Line(OptionS, 2)),
        ];

        var plan = await _service.GetAsync(onlyToSew: true, default);

        Assert.Equal(2, plan.OrderCount);
        Assert.Equal(["S", "M"], plan.Rows.Select(r => r.SizeName));
        var s = plan.Rows[0];
        Assert.Equal((2m, 1m, 1m, new DateOnly(2026, 10, 11), true), (s.Ordered, s.SewnStock, s.ToSew, s.Deadline, s.Overdue));
        Assert.False(plan.Rows[1].Overdue);
        Assert.Equal([24], _source.RequestedStatuses);
    }

    [Fact]
    public async Task HeldBySize_IsSpreadOverOrderLines_SoTwoLinesOfSameSizeAreNotDoubleCovered()
    {
        _source.Orders = [Order(1, new DateTime(2026, 10, 15), Line(OptionM, 1), Line(OptionM, 2)) with { Held = new Dictionary<int, decimal> { [SizeM] = 2 } }];

        var row = Assert.Single((await _service.GetAsync(true, default)).Rows);

        Assert.Equal((3m, 1m, 1m), (row.Ordered, row.Missing, row.ToSew));
    }

    [Fact]
    public async Task UnlinkedProductAndUnmatchedSize_AreIssues_NotRows()
    {
        _source.Orders =
        [
            Order(1, new DateTime(2026, 10, 15),
                new OrderSizeLine(200, "Шарф", 1, null, []),
                new OrderSizeLine(Product, "Сукня", 1, 1, [999])),
            Order(2, new DateTime(2026, 10, 15), new OrderSizeLine(200, "Шарф", 3, null, [])),
        ];

        var plan = await _service.GetAsync(false, default);

        Assert.Empty(plan.Rows);
        Assert.Equal(["Сукня", "Шарф"], plan.Issues.Select(i => i.ProductName));
        Assert.Contains("«Сукня»", plan.Issues[0].Reason);
        Assert.Equal([1, 2], plan.Issues[1].OrderIds);
    }

    [Fact]
    public async Task EnoughSewn_ShownOnlyWhenAskedForAll()
    {
        _source.Orders = [Order(1, new DateTime(2026, 10, 15), Line(OptionS, 1))];

        Assert.Empty((await _service.GetAsync(true, default)).Rows);
        var row = Assert.Single((await _service.GetAsync(false, default)).Rows);
        Assert.Equal((0m, false), (row.ToSew, row.Overdue));
    }

    private static PlanOrder Order(int id, DateTime date, params OrderSizeLine[] lines) =>
        new(id, date, lines, new Dictionary<int, decimal>());

    private static OrderSizeLine Line(int option, int quantity) => new(Product, "Сукня", quantity, 1, [option]);

    private sealed class FakeSource : ISewingPlanSource
    {
        public List<ProductionModel> Models { get; } = [];
        public Dictionary<int, int> Days { get; } = [];
        public IReadOnlyList<PlanOrder> Orders { get; set; } = [];
        public IReadOnlyCollection<int>? RequestedStatuses { get; private set; }

        public Task<IReadOnlyList<PlanOrder>> GetOrdersAsync(IReadOnlyCollection<int> statusIds, CancellationToken ct)
        {
            RequestedStatuses = statusIds;
            return Task.FromResult(Orders);
        }

        public Task<IReadOnlyDictionary<int, int>> GetProductionDaysAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, int>>(Days);

        public Task<IReadOnlyList<ProductionModel>> GetModelsAsync(IReadOnlyCollection<int> productionIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProductionModel>>(Models.Where(m => productionIds.Contains(m.Id)).ToList());
    }
}
