using NewErp.Application.Common;
using NewErp.Domain.Catalog;

namespace NewErp.Application.Catalog;

/// <summary>
/// Передзамовлення розмірів, як на старому сайті: ліміт і очікувана дата задаються для кожного розміру товару,
/// вітрина продає наперед, а продане наперед регулярно списується з ліміту.
/// </summary>
public sealed class PreOrderService(IPreOrderStore store)
{
    private const int ListLimit = 200;

    public Task<IReadOnlyList<PreOrderRow>> ListAsync(PreOrderFilter filter, CancellationToken ct) =>
        store.ListAsync(filter with { Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim() }, ListLimit, ct);

    public async Task UpdateAsync(int productOptionValueId, UpdatePreOrderRequest request, CancellationToken ct)
    {
        PreOrder.ValidateQuota(request.Quota);
        if (!await store.UpdateAsync(productOptionValueId, request.Quota, request.DateAvailable, ct))
            throw new NotFoundException($"Розміру {productOptionValueId} не знайдено.");
    }

    public async Task<PreOrderSettleResult> SettleAsync(CancellationToken ct) => new(await store.SettleAsync(null, ct));
}
