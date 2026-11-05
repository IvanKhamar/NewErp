using NewErp.Application.Stock;
using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>Що рахувати: те, чого бракує замовленням, дошив моделей до мінімального запасу, чи обидва.</summary>
public enum MaterialScope
{
    Orders,
    Minimum,
    All,
}

/// <summary>
/// Розхідники для плану пошиву: скільки знадобиться, щоб розкроїти й пошити все, чого бракує активним замовленням, і чого
/// не вистачить навіть після вже замовленого в постачальників. Scope «minimum» рахує інше: дошив кожної моделі до мінімального
/// запасу, де бракуючі штуки розкладено за частками розмірів у партії; «all» — обидва. Старий модуль цього не рахував:
/// нестача ставала видно лише тоді, коли операція крою чи пошиву вже списувала «що є».
/// </summary>
public sealed class PlanMaterialsService(SewingPlanService plan, IProductionRepository repository, IPurchaseOrderRepository purchaseOrders)
{
    private sealed record SizeTask(int ProductionId, string ModelName, int SizeId, string SizeName, decimal ToCut, decimal ToSew, bool ForMinimum);

    /// <summary>orders, minimum або all; без значення — orders.</summary>
    public static MaterialScope ParseScope(string? scope) => scope switch
    {
        null or "" or "orders" => MaterialScope.Orders,
        "minimum" => MaterialScope.Minimum,
        "all" => MaterialScope.All,
        _ => throw new DomainException("Scope — orders, minimum або all."),
    };

    public async Task<PlanMaterialsDto> GetAsync(MaterialScope scope, CancellationToken ct)
    {
        var rows = (await plan.GetAsync(onlyToSew: false, ct)).Rows;
        var tasks = new List<SizeTask>();
        if (scope is MaterialScope.Orders or MaterialScope.All)
            tasks.AddRange(rows.Where(r => r.ToSew > 0).Select(r => new SizeTask(r.ProductionId, r.ModelName, r.SizeId, r.SizeName, r.ToCut, r.ToSew, false)));
        if (scope is MaterialScope.Minimum or MaterialScope.All)
            tasks.AddRange(await UpToMinimumAsync(rows.ToDictionary(r => r.SizeId), ct));

        var needs = new List<(MaterialNeed Need, SizeTask Task)>();
        foreach (var group in tasks.GroupBy(t => t.ProductionId))
        {
            // Повна модель (FindModelsAsync не вантажить специфікацію пошиву).
            if (await repository.FindModelAsync(group.Key, ct) is not { } model)
                continue;
            var components = await repository.FindComponentsAsync(model.Id, ct);
            foreach (var task in group)
                needs.AddRange(MaterialNeeds.ForSize(model, components, task.SizeId, task.ToCut, task.ToSew).Select(n => (n, task)));
        }

        var items = (await repository.FindStockItemsAsync(needs.Select(n => n.Need.ItemId).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
        var onOrder = await purchaseOrders.OnOrderAsync(ct);

        var warnings = needs
            .Where(n => !items.ContainsKey(n.Need.ItemId))
            .Select(n => $"Розхідника {n.Need.ItemId} зі специфікації моделі «{n.Task.ModelName}» немає на складі, його не враховано.")
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
                    .GroupBy(n => (n.Task.ProductionId, n.Task.ModelName, n.Task.SizeName, n.Need.Stage, n.Task.ForMinimum))
                    .Select(u => new PlanMaterialUseDto(u.Key.ProductionId, u.Key.ModelName, u.Key.SizeName, u.Key.Stage, u.Sum(n => n.Need.Quantity), u.Key.ForMinimum))
                    .ToList();
                return new PlanMaterialDto(item.Id, item.Name, item.Unit, needed, item.Quantity, ordered,
                    Math.Max(0, needed - Math.Max(0, item.Quantity) - ordered), uses);
            })
            .OrderByDescending(m => m.Shortage > 0)
            .ThenBy(m => m.Name, StringComparer.CurrentCulture)
            .ToList();

        return new PlanMaterialsDto(materials, warnings);
    }

    /// <summary>Чого кожній шитій моделі бракує до мінімального запасу після того, як замовлення взяли своє, за розмірами.</summary>
    private async Task<List<SizeTask>> UpToMinimumAsync(Dictionary<int, SewingPlanRowDto> rows, CancellationToken ct)
    {
        var tasks = new List<SizeTask>();
        foreach (var summary in await repository.ListAsync(new ProductionModelFilter(), ct))
        {
            if (summary.Type == ProductionModel.ReadyMade || summary.MinTotal <= 0)
                continue;
            if (await repository.FindModelAsync(summary.Id, ct) is not { } model)
                continue;

            var sewnAfter = new Dictionary<int, decimal>();
            var cutAfter = new Dictionary<int, decimal>();
            foreach (var size in model.Sizes)
            {
                var row = rows.GetValueOrDefault(size.Id);
                // Замовлення беруть із пошитого те, чого їм бракує, мінус те, що для них ще треба пошити.
                var taken = row is null ? 0 : row.Missing - row.ToSew;
                sewnAfter[size.Id] = Math.Max(0, size.SewnStock - taken);
                cutAfter[size.Id] = Math.Max(0, size.CutStock - (row?.FromCut ?? 0));
            }

            var lacking = model.MinTotal - sewnAfter.Values.Sum();
            var pieces = MaterialNeeds.SpreadByShare(lacking, model.Sizes.ToDictionary(s => s.Id, s => s.Percent));
            foreach (var size in model.Sizes)
            {
                var toSew = pieces.GetValueOrDefault(size.Id);
                if (toSew > 0)
                    tasks.Add(new SizeTask(model.Id, model.Name, size.Id, size.Name, Math.Max(0, toSew - cutAfter[size.Id]), toSew, true));
            }
        }
        return tasks;
    }
}
