namespace NewErp.Application.Orders;

public sealed record OrderFilter(
    int? StatusId = null,
    int? SourceId = null,
    int? ManagerId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record OrderListItem(
    int Id,
    DateTime DateAdded,
    string Customer,
    string Telephone,
    decimal Total,
    string CurrencyCode,
    int StatusId,
    string? StatusName,
    string? SourceName,
    int? ManagerId,
    string? ManagerName);

public sealed record OrderProductLine(int ProductId, string Name, string Model, int Quantity, decimal Price, decimal Total);

public sealed record OrderStatusHistoryEntry(DateTime ChangedAt, int OldStatusId, int NewStatusId, string? NewStatusName, int UserId, string? UserName);

public sealed record OrderDetails(
    int Id,
    DateTime DateAdded,
    string Customer,
    string Telephone,
    string Email,
    string ShippingMethod,
    string ShippingCity,
    string ShippingAddress,
    string PaymentMethod,
    string Comment,
    decimal Total,
    string CurrencyCode,
    int StatusId,
    string? StatusName,
    int SourceId,
    string? SourceName,
    int? ManagerId,
    string? ManagerName,
    IReadOnlyList<OrderProductLine> Products,
    IReadOnlyList<OrderStatusHistoryEntry> StatusHistory);

/// <summary>Хто змінює статус, сервер бере з сесії, а не з тіла запиту, тому підробити його не можна.</summary>
public sealed record ChangeOrderStatusRequest(int StatusId);

/// <summary>Warnings: попередження від інших модулів, наприклад про нестачу розхідників при списанні.</summary>
public sealed record ChangeOrderStatusResult(IReadOnlyList<string> Warnings);

public sealed record AssignManagerRequest(int ManagerId);