using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Application;

public class PreOrderServiceTests
{
    private readonly FakeStore _store = new();
    private readonly PreOrderService _service;

    public PreOrderServiceTests() => _service = new PreOrderService(_store);

    [Fact]
    public async Task List_TrimsSearch_AndLimits()
    {
        await _service.ListAsync(new PreOrderFilter("  сукня ", true), CancellationToken.None);
        Assert.Equal((new PreOrderFilter("сукня", true), 200), _store.LastList);

        await _service.ListAsync(new PreOrderFilter("   "), CancellationToken.None);
        Assert.Null(_store.LastList.Filter.Search);
    }

    [Fact]
    public async Task Update_ValidatesQuota_AndUnknownSize()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.UpdateAsync(1, new UpdatePreOrderRequest(-1, null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(404, new UpdatePreOrderRequest(3, null), CancellationToken.None));

        await _service.UpdateAsync(1, new UpdatePreOrderRequest(3, new DateOnly(2026, 11, 1)), CancellationToken.None);
        Assert.Equal((1, 3, new DateOnly(2026, 11, 1)), _store.LastUpdate);
    }

    [Fact]
    public async Task Settle_CoversAllProducts() =>
        Assert.Equal(new PreOrderSettleResult(2), await _service.SettleAsync(CancellationToken.None));

    private sealed class FakeStore : IPreOrderStore
    {
        public (PreOrderFilter Filter, int Limit) LastList { get; private set; }
        public (int Id, int Quota, DateOnly? Date) LastUpdate { get; private set; }

        public Task<IReadOnlyList<PreOrderRow>> ListAsync(PreOrderFilter filter, int limit, CancellationToken ct)
        {
            LastList = (filter, limit);
            return Task.FromResult<IReadOnlyList<PreOrderRow>>([]);
        }

        public Task<bool> UpdateAsync(int productOptionValueId, int quota, DateOnly? dateAvailable, CancellationToken ct)
        {
            if (productOptionValueId == 404)
                return Task.FromResult(false);
            LastUpdate = (productOptionValueId, quota, dateAvailable);
            return Task.FromResult(true);
        }

        public Task<int> SettleAsync(IReadOnlyCollection<int>? productIds, CancellationToken ct) => Task.FromResult(productIds is null ? 2 : 0);
    }
}
