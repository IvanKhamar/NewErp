using NewErp.Domain.Common;

namespace NewErp.Domain.Staff;

/// <summary>Замовлення менеджера, яке може дати бонус.</summary>
public sealed record BonusOrder(int OrderId, int ManagerId, int StatusId, decimal Total);

/// <summary>Підсумок бонусу одного менеджера за період.</summary>
public sealed record ManagerBonus(int ManagerId, int OrdersCount, decimal Revenue, decimal Bonus);

/// <summary>
/// Правило нарахування бонусу: відсоток від суми замовлень у "зарахованих" статусах.
/// Це наше спрощене правило. Точну формулу старого модуля user/bonus ще треба звірити.
/// </summary>
public sealed class BonusPolicy
{
    public BonusPolicy(decimal percent, IEnumerable<int> countedStatusIds)
    {
        if (percent is < 0 or > 100)
            throw new DomainException("Відсоток бонусу має бути від 0 до 100.");

        Percent = percent;
        CountedStatusIds = countedStatusIds.ToHashSet();

        if (CountedStatusIds.Count == 0)
            throw new DomainException("Вкажіть хоча б один статус, за який нараховується бонус.");
    }

    public decimal Percent { get; }

    public IReadOnlySet<int> CountedStatusIds { get; }

    public IReadOnlyList<ManagerBonus> Calculate(IEnumerable<BonusOrder> orders) =>
        orders
            .Where(o => CountedStatusIds.Contains(o.StatusId))
            .GroupBy(o => o.ManagerId)
            .Select(g =>
            {
                var revenue = g.Sum(o => o.Total);
                var bonus = Math.Round(revenue * Percent / 100m, 2, MidpointRounding.AwayFromZero);
                return new ManagerBonus(g.Key, g.Count(), revenue, bonus);
            })
            .OrderByDescending(b => b.Bonus)
            .ToList();
}