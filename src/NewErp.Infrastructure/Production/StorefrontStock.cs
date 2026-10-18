using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Application.Production;
using NewErp.Domain.Production;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Production;

public sealed class StorefrontStock(ErpDbContext db, IOptions<ErpOptions> options, IPreOrderStore preOrders) : IStorefrontStock
{
    private readonly int _languageId = options.Value.LanguageId;

    public async Task<StorefrontSyncResult> PushAsync(IReadOnlyCollection<int>? productionIds, CancellationToken ct)
    {
        if (productionIds is { Count: 0 })
            return new StorefrontSyncResult(0, 0);

        var models = await db.ProductionModels.AsNoTracking().Include(m => m.Sizes)
            .Where(m => m.IsActive && (productionIds == null || productionIds.Contains(m.Id)))
            .ToListAsync(ct);
        var owners = await ProductOwnersAsync(ct);

        int modelCount = 0, productCount = 0;
        foreach (var model in models)
        {
            var quantities = model.StorefrontQuantities();
            var productIds = owners.Where(o => o.Value == model.Id).Select(o => o.Key).ToList();
            if (quantities.Count == 0 || productIds.Count == 0)
                continue;

            // Мінусовий залишок — продане наперед. Його спершу списуємо з ліміту передзамовлення, інакше новий залишок його затре,
            // а ліміт лишиться повним.
            await preOrders.SettleAsync(productIds, ct);

            // Таблиці OpenCart — MyISAM, без транзакцій, тож оновлюємо прямими UPDATE, як старий модуль.
            foreach (var (optionValueId, quantity) in quantities)
                await db.DdProductOptionValues
                    .Where(v => productIds.Contains(v.ProductId) && v.OptionValueId == optionValueId)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Quantity, quantity), ct);

            // Кількість товару — сума його зв'язаних розмірів (лише тих товарів, які мають такі розміри).
            var optionValueIds = quantities.Keys.ToList();
            var totals = await db.DdProductOptionValues.AsNoTracking()
                .Where(v => productIds.Contains(v.ProductId) && optionValueIds.Contains(v.OptionValueId))
                .GroupBy(v => v.ProductId)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(v => v.Quantity) })
                .ToListAsync(ct);
            foreach (var group in totals.GroupBy(t => t.Total))
            {
                var ids = group.Select(t => t.ProductId).ToList();
                await db.DdProducts.Where(p => ids.Contains(p.ProductId))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Quantity, group.Key), ct);
            }

            modelCount++;
            productCount += totals.Count;
        }

        return new StorefrontSyncResult(modelCount, productCount);
    }

    /// <summary>
    /// Якій моделі належить кожен товар магазину. Як і при списанні за замовленням: пряма прив'язка важливіша
    /// за категорію, а категорія з фільтром назви — за всю категорію. Через категорію беруться лише ввімкнені товари.
    /// </summary>
    private async Task<Dictionary<int, int>> ProductOwnersAsync(CancellationToken ct)
    {
        var direct = await (
            from l in db.Set<ProductionProductLink>()
            join m in db.ProductionModels on l.ProductionId equals m.Id
            where m.IsActive
            select new { l.ProductId, l.ProductionId }
        ).ToListAsync(ct);

        var byCategory = await (
            from c in db.Set<ProductionCategoryLink>()
            join m in db.ProductionModels on c.ProductionId equals m.Id
            join pc in db.DdProductToCategories on c.CategoryId equals pc.CategoryId
            join p in db.DdProducts on pc.ProductId equals p.ProductId
            join pd in db.DdProductDescriptions on pc.ProductId equals pd.ProductId
            where m.IsActive && p.Status && pd.LanguageId == _languageId
                && (c.NameFilter == ""
                    || (c.MatchExact && pd.Name == c.NameFilter)
                    || (!c.MatchExact && pd.Name.Contains(c.NameFilter)))
            select new { pc.ProductId, c.ProductionId, HasFilter = c.NameFilter != "" }
        ).ToListAsync(ct);

        var owners = byCategory
            .GroupBy(c => c.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.HasFilter).ThenBy(c => c.ProductionId).First().ProductionId);
        foreach (var group in direct.GroupBy(d => d.ProductId))
            owners[group.Key] = group.Min(d => d.ProductionId);
        return owners;
    }
}
