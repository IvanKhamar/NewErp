namespace NewErp.Application.Staff;

/// <summary>Налаштування бонусів із розділу "Bonus" в appsettings.json.</summary>
public sealed class BonusOptions
{
    public const string SectionName = "Bonus";

    public decimal Percent { get; set; }

    public int[] CountedStatusIds { get; set; } = [];
}

public sealed record ManagerBonusRow(int ManagerId, string? ManagerName, int OrdersCount, decimal Revenue, decimal Bonus);

public sealed record BonusReport(DateOnly From, DateOnly To, decimal Percent, IReadOnlyList<ManagerBonusRow> Managers)
{
    public decimal TotalBonus => Managers.Sum(m => m.Bonus);
}