namespace NewErp.Application.Production;

public sealed record BomLineRequest(int StockItemId, decimal Quantity);

public sealed record SetBomRequest(IReadOnlyList<BomLineRequest> Lines);

public sealed record BomLineDto(int StockItemId, string ItemName, string Unit, decimal Quantity);

public sealed record BomDto(int ProductId, IReadOnlyList<BomLineDto> Lines, int UpdatedByUserId, DateTime UpdatedAt);

public sealed record BomSummaryDto(int ProductId, int LineCount, DateTime UpdatedAt);

/// <summary>Рядок специфікації разом із поточним залишком розхідника.</summary>
public sealed record BomComponent(int ProductId, int StockItemId, string ItemName, string Unit, decimal QuantityPerUnit, decimal InStock);

public sealed record OrderProductLine(int ProductId, string Name, int Quantity);

/// <summary>Shortage: скільки бракує на складі (0, якщо вистачає).</summary>
public sealed record MaterialRequirementDto(int StockItemId, string ItemName, string Unit, decimal Required, decimal InStock, decimal Shortage);

/// <summary>Потреба в розхідниках для замовлення. ProductsWithoutBom: товари, для яких специфікацію ще не задано.</summary>
public sealed record OrderRequirementsDto(
    int OrderId,
    IReadOnlyList<MaterialRequirementDto> Materials,
    IReadOnlyList<OrderProductLine> ProductsWithoutBom);
