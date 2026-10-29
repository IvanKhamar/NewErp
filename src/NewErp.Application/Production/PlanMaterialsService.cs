using NewErp.Application.Stock;
using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>
/// Розхідники для плану пошиву: скільки знадобиться, щоб розкроїти й пошити все, чого бракує активним замовленням, і чого
/// не вистачить навіть після вже замовленого в постачальників. Старий модуль цього не рахував: нестачу видно було лише тоді,
/// коли операція крою чи пошиву вже списувала «що є».
/// </summary>
public sealed class PlanMaterialsService(SewingPlanService plan, IProductionRepository repository, IPurchaseOrderRepository purchaseOrders)
{
    public async Task<PlanMaterialsDto> GetAsync(CancellationToken ct)
    {
        var rows = (await plan.GetAsync(onlyToSew: true, ct)).Rows;

        var needs = new List<(MaterialNeed Need, SewingPlanRowDto Row)>();
        foreach (var group in rows.GroupBy(r => r.ProductionId))
        {
            // Повна модель (FindModelsAsync не вантажить специфікацію пошиву).
            if (await repository.FindModelAsync(group.Key, ct) is not { } model)
                continue;
            var components = await repository.FindComponentsAsync(model.Id, ct);
            foreach (var row in group)
                needs.AddRange(MaterialNeeds.ForSize(model, components, row.SizeId, row.ToCut, row.ToSew).Select(n => (n, row)));
        }

        var items = (await repository.FindStockItemsAsync(needs.Select(n => n.Need.ItemId).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
        var onOrder = await purchaseOrders.OnOrderAsync(ct);

        var warnings = needs
            .Where(n => !items.ContainsKey(n.Need.ItemId))
            .Select(n => $"Розхідника {n.Need.ItemId} зі специфікації моделі «{n.Row.ModelName}» немає на складі, його не враховано.")
            .Distinct()
            .ToList();

        var materials = needs
            .Where(n => items.ContainsKey(n.Need.ItemId))
            .GroupBy(n => n.Need.ItemId)
            .Select(g =>
            {
                var item = items[g.Key];
                var needed = g.Sum(n => n.Need.Quantity);
                var ordered = onOrder.GetValueOrDefault(g.Key);
                var uses = g
                    .GroupBy(n => (n.Row.ProductionId, n.Row.ModelName, n.Row.SizeName, n.Need.Stage))
                    .Select(u => new PlanMaterialUseDto(u.Key.ProductionId, u.Key.ModelName, u.Key.SizeName, u.Key.Stage, u.Sum(n => n.Need.Quantity)))
                    .ToList();
                return new PlanMaterialDto(item.Id, item.Name, item.Unit, needed, item.Quantity, ordered,
                    Math.Max(0, needed - Math.Max(0, item.Quantity) - ordered), uses);
            })
            .OrderByDescending(m => m.Shortage > 0)
            .ThenBy(m => m.Name, StringComparer.CurrentCulture)
            .ToList();

        return new PlanMaterialsDto(materials, warnings);
    }
}
