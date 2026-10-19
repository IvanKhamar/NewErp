using Microsoft.Extensions.Options;

using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>План пошиву: скільки кожного розміру моделі пошити й розкроїти для активних замовлень.</summary>
public sealed class SewingPlanService(ISewingPlanSource source, IOptions<ProductionOptions> options, TimeProvider clock)
{
    public async Task<SewingPlanDto> GetAsync(bool onlyToSew, CancellationToken ct)
    {
        var orders = await source.GetOrdersAsync(options.Value.PlanStatusIds, ct);
        var lines = orders.SelectMany(o => o.Lines.Select(l => (Order: o, Line: l))).ToList();
        var issues = new List<(string Product, string Reason, int OrderId)>();

        var models = (await source.GetModelsAsync(
                lines.Where(x => x.Line.ProductionId is not null).Select(x => x.Line.ProductionId!.Value).Distinct().ToList(), ct))
            .ToDictionary(m => m.Id);
        var days = await source.GetProductionDaysAsync(lines.Select(x => x.Line.ProductId).Distinct().ToList(), ct);

        // Як в обробнику складу: розмір рядка — той розмір моделі, з яким зв'язана одна з опцій товару в замовленні.
        var demands = new List<SewingDemand>();
        foreach (var (order, line) in lines)
        {
            if (line.ProductionId is not { } productionId || !models.TryGetValue(productionId, out var model))
            {
                issues.Add((line.ProductName, "Не прив'язано до моделі виробництва.", order.OrderId));
                continue;
            }
            var size = model.Sizes.FirstOrDefault(s => s.OptionValueId is { } ov && line.OptionValueIds.Contains(ov));
            if (size is null)
            {
                issues.Add((line.ProductName, $"Розмір із замовлення не зв'язаний з розмірами моделі «{model.Name}».", order.OrderId));
                continue;
            }
            var deadline = SewingPlan.Deadline(order.DateAdded, days.GetValueOrDefault(line.ProductId));
            demands.Add(new SewingDemand(order.OrderId, size.Id, line.Quantity, 0, deadline));
        }

        // Тримане замовлення лічиться на розмір, а не на рядок: розподіляємо його по рядках цього розміру по черзі.
        demands = SpreadHeld(demands, orders.ToDictionary(o => o.OrderId, o => o.Held));

        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var rows = new List<SewingPlanRowDto>();
        foreach (var model in models.Values)
        {
            foreach (var size in model.Sizes.OrderBy(s => s.SortOrder).ThenBy(s => s.Id))
            {
                if (!demands.Any(d => d.SizeId == size.Id))
                    continue;
                var line = SewingPlan.ForSize(size.Id, size.SewnStock, size.CutStock, demands);
                if (onlyToSew && line.ToSew <= 0)
                    continue;
                rows.Add(new SewingPlanRowDto(
                    model.Id, model.Name, size.Id, size.Name, line.Ordered, line.Missing, size.SewnStock, size.CutStock,
                    line.ToSew, line.FromCut, line.ToCut, line.Deadline, line.ToSew > 0 && line.Deadline < today, line.OrderIds));
            }
        }

        var sorted = rows
            .OrderBy(r => r.Deadline is null)
            .ThenBy(r => r.Deadline)
            .ThenBy(r => models[r.ProductionId].SortOrder)
            .ThenBy(r => r.ModelName, StringComparer.CurrentCulture)
            .ToList();
        // Товарів, які не шиємо (без моделі), в активних замовленнях багато, тож групуємо їх за товаром.
        var grouped = issues
            .GroupBy(i => (i.Product, i.Reason))
            .Select(g => new SewingPlanIssueDto(g.Key.Product, g.Key.Reason, g.Select(i => i.OrderId).Distinct().Order().ToList()))
            .OrderBy(i => i.ProductName, StringComparer.CurrentCulture)
            .ToList();
        return new SewingPlanDto(orders.Count, sorted, grouped);
    }

    private static List<SewingDemand> SpreadHeld(List<SewingDemand> demands, IReadOnlyDictionary<int, IReadOnlyDictionary<int, decimal>> held)
    {
        var left = held.ToDictionary(h => h.Key, h => h.Value.ToDictionary(x => x.Key, x => x.Value));
        var result = new List<SewingDemand>(demands.Count);
        foreach (var d in demands)
        {
            var sizes = left.GetValueOrDefault(d.OrderId);
            var available = sizes?.GetValueOrDefault(d.SizeId) ?? 0;
            var take = Math.Clamp(available, 0, d.Ordered);
            if (sizes is not null && take > 0)
                sizes[d.SizeId] = available - take;
            result.Add(d with { Held = take });
        }
        return result;
    }
}
