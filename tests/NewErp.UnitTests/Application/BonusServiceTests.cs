using Microsoft.Extensions.Options;

using NewErp.Application.Staff;
using NewErp.Domain.Common;
using NewErp.Domain.Staff;

namespace NewErp.UnitTests.Application;

public class BonusServiceTests
{
    [Fact]
    public async Task Calculate_UsesInclusivePeriodAndAddsNames()
    {
        var source = new FakeSource();
        var service = new BonusService(source, Options.Create(new BonusOptions { Percent = 10, CountedStatusIds = [5] }));

        var report = await service.CalculateAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None);

        Assert.Equal(new DateTime(2026, 10, 1), source.RequestedStart);
        Assert.Equal(new DateTime(2026, 11, 1), source.RequestedEnd); // 31 жовтня входить у період
        var row = Assert.Single(report.Managers);
        Assert.Equal(new ManagerBonusRow(7, "Олена Петренко", 1, 200m, 20m), row);
        Assert.Equal(20m, report.TotalBonus);
    }

    [Fact]
    public async Task Calculate_ReversedPeriod_Throws()
    {
        var service = new BonusService(new FakeSource(), Options.Create(new BonusOptions { Percent = 1, CountedStatusIds = [5] }));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CalculateAsync(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1), CancellationToken.None));
    }

    private sealed class FakeSource : IBonusDataSource
    {
        public DateTime RequestedStart { get; private set; }
        public DateTime RequestedEnd { get; private set; }

        public Task<IReadOnlyList<BonusOrder>> GetManagerOrdersAsync(DateTime start, DateTime endExclusive, CancellationToken ct)
        {
            (RequestedStart, RequestedEnd) = (start, endExclusive);
            return Task.FromResult<IReadOnlyList<BonusOrder>>([new(1, 7, 5, 200m), new(2, 7, 7, 1000m)]);
        }

        public Task<IReadOnlyDictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string> { [7] = "Олена Петренко" });
    }
}