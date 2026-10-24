namespace NewErp.Application.Stock;

public sealed record PurchaseOrderLineRequest(int ItemId, decimal Quantity, decimal Price = 0);

public sealed record CreatePurchaseOrderRequest(int SupplierId, IReadOnlyList<PurchaseOrderLineRequest> Lines, string? Comment = null);

public sealed record SetPurchaseOrderLinesRequest(IReadOnlyList<PurchaseOrderLineRequest> Lines);

public sealed record ReceiptLineRequest(int LineId, decimal Quantity);

/// <summary>WarehouseId: куди прийшла поставка; null — основний склад.</summary>
public sealed record ReceivePurchaseOrderRequest(IReadOnlyList<ReceiptLineRequest> Lines, int? WarehouseId = null);

/// <summary>Status: draft, ordered, received, cancelled.</summary>
public sealed record PurchaseOrderFilter(string? Status = null);

public sealed record PurchaseOrderLineDto(
    int Id, int ItemId, string? ItemName, string? Unit, decimal Quantity, decimal Price, decimal Sum, decimal Received, decimal Remaining);

public sealed record PurchaseOrderDto(
    int Id, int SupplierId, string? SupplierName, string? SupplierPhone, string Status, string Comment,
    DateTime CreatedAt, DateTime? OrderedAt, DateTime? ClosedAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines, decimal Total, string MessageText);

public sealed record PurchaseOrderSummaryDto(int Id, int SupplierId, string? SupplierName, string Status, DateTime CreatedAt, int LineCount, decimal Total);
