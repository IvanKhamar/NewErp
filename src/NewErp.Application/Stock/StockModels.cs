namespace NewErp.Application.Stock;

public sealed record StockItemDto(
    int Id,
    string Name,
    string Unit,
    int? CategoryId,
    string? CategoryName,
    int? SupplierId,
    string? SupplierName,
    decimal Quantity,
    decimal MinQuantity,
    bool IsBelowMinimum,
    string UsedFor,
    decimal Price);

/// <summary>Delta: мінус — витрата, плюс — прихід. Reason — коментар із журналу.</summary>
public sealed record StockMovementDto(
    int Id, int ItemId, decimal Delta, decimal QuantityBefore, decimal QuantityAfter, string Reason, int UserId, string Username, DateTime CreatedAt);

public sealed record StockSupplierDto(int Id, string Name, string Phone, string Email, string Categories, string Notes);

public sealed record CreateStockCategoryRequest(string Name, int SortOrder = 0);

public sealed record CreateStockSupplierRequest(string Name, string? Phone, string? Email = null, string? Categories = null, string? Notes = null);

public sealed record SaveStockItemRequest(
    int? CategoryId, int? SupplierId, string Name, string Unit, decimal MinQuantity, string? UsedFor = null, decimal Price = 0);

/// <summary>Delta: плюс = прихід, мінус = витрата.</summary>
public sealed record AdjustStockRequest(decimal Delta, string Reason);

public sealed record StockItemFilter(int? CategoryId = null, bool BelowMinimumOnly = false);
