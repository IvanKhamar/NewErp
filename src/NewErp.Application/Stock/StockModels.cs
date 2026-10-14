namespace NewErp.Application.Stock;

public sealed record StockItemDto(
    int Id,
    string Name,
    string Unit,
    int CategoryId,
    string CategoryName,
    int? SupplierId,
    string? SupplierName,
    decimal Quantity,
    decimal MinQuantity,
    bool IsBelowMinimum);

public sealed record StockMovementDto(long Id, int ItemId, decimal Delta, decimal QuantityAfter, string Reason, int UserId, DateTime CreatedAt);

public sealed record StockSupplierDto(int Id, string Name, string? Phone, string? Note);

public sealed record CreateStockCategoryRequest(string Name);

public sealed record CreateStockSupplierRequest(string Name, string? Phone, string? Note);

public sealed record CreateStockItemRequest(int CategoryId, int? SupplierId, string Name, string Unit, decimal MinQuantity);

/// <summary>Delta: плюс = прихід, мінус = витрата.</summary>
public sealed record AdjustStockRequest(decimal Delta, string Reason);

public sealed record StockItemFilter(int? CategoryId = null, bool BelowMinimumOnly = false);