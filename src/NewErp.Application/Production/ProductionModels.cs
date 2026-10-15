namespace NewErp.Application.Production;

// ── Запити ──

public sealed record SizeRequest(string Name, decimal Percent = 0);

/// <summary>Type: "clothing" (власний пошив) або "ready_made" (готовий товар від постачальника).</summary>
public sealed record SaveProductionModelRequest(
    string Name, IReadOnlyList<SizeRequest> Sizes, string Type = "clothing", int MinTotal = 50, int SortOrder = 0);

public sealed record CategoryLinkRequest(int CategoryId, string? NameFilter = null, bool MatchExact = false);

public sealed record SizeOptionRequest(int SizeId, int? OptionValueId);

/// <summary>Повністю замінює прив'язку моделі до товарів. SizeOptions змінює лише перелічені розміри.</summary>
public sealed record SetProductionLinksRequest(
    IReadOnlyList<int>? ProductIds, IReadOnlyList<CategoryLinkRequest>? Categories, IReadOnlyList<SizeOptionRequest>? SizeOptions);

public sealed record SaveComponentRequest(string Name, int SortOrder = 0);

/// <summary>SizeId = null — для всіх розмірів; рядок із SizeId перекриває його для цього розміру.</summary>
public sealed record ComponentBomRowRequest(int? SizeId, int ItemId, decimal QtyPerUnit);

public sealed record SetComponentBomRequest(IReadOnlyList<ComponentBomRowRequest> Rows);

/// <summary>Або ComponentId (деталь крою), або ItemId (розхідник напряму).</summary>
public sealed record SewnBomRowRequest(int? SizeId, int? ComponentId, int? ItemId, decimal QtyPerUnit);

public sealed record SetSewnBomRequest(IReadOnlyList<SewnBomRowRequest> Rows);

public sealed record ProductionModelFilter(string? Type = null, bool BelowMinimumOnly = false);

// ── Відповіді ──

/// <summary>Deficit: скільки бракує пошитих до MinTotal (0, якщо вистачає).</summary>
public sealed record ProductionModelSummaryDto(
    int Id, string Name, string Type, int MinTotal, decimal TotalCut, decimal TotalSewn, decimal Deficit);

public sealed record ProductionSizeDto(int Id, string Name, decimal Percent, decimal CutStock, decimal SewnStock, int? OptionValueId);

public sealed record CategoryLinkDto(int CategoryId, string NameFilter, bool MatchExact);

public sealed record ComponentBomLineDto(int? SizeId, int ItemId, string? ItemName, string? Unit, decimal QtyPerUnit);

/// <summary>Stock: запас розкроєних деталей за id розміру.</summary>
public sealed record ComponentDto(int Id, string Name, int SortOrder, IReadOnlyDictionary<int, decimal> Stock, IReadOnlyList<ComponentBomLineDto> Bom);

public sealed record SewnBomLineDto(int? SizeId, int? ComponentId, string? ComponentName, int? ItemId, string? ItemName, string? Unit, decimal QtyPerUnit);

public sealed record ProductionModelDto(
    int Id,
    string Name,
    string Type,
    int MinTotal,
    int SortOrder,
    IReadOnlyList<ProductionSizeDto> Sizes,
    IReadOnlyList<int> ProductIds,
    IReadOnlyList<CategoryLinkDto> Categories,
    IReadOnlyList<ComponentDto> Components,
    IReadOnlyList<SewnBomLineDto> SewnBom);
