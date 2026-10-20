using NewErp.Domain.Reports;

namespace NewErp.Application.Reports;

/// <summary>Фільтр звітів. Без статусу враховуються всі оформлені замовлення, крім скасованих.</summary>
public sealed record ReportFilter(DateOnly? From = null, DateOnly? To = null, int? StatusId = null, ReportGrouping Group = ReportGrouping.Week);

public sealed record SalesReportRow(DateOnly Start, DateOnly End, int Orders, int Products, decimal Total);

public sealed record SalesReportDto(DateOnly From, DateOnly To, IReadOnlyList<SalesReportRow> Rows, int Orders, int Products, decimal Total);

public sealed record ProductSalesRow(int ProductId, string Name, string Model, int Quantity, decimal Total);

public sealed record ProductSalesDto(DateOnly From, DateOnly To, IReadOnlyList<ProductSalesRow> Rows);

/// <summary>Показник «зараз проти раніше»: ChangePercent — null, якщо раніше було 0.</summary>
public sealed record DashboardTile(decimal Current, decimal Previous, int? ChangePercent);

public sealed record DashboardDto(
    DashboardTile OrdersToday,
    DashboardTile SalesToday,
    DashboardTile OrdersMonth,
    DashboardTile SalesMonth,
    DashboardTile AverageCheckMonth,
    DashboardTile NewCustomersMonth,
    IReadOnlyList<SalesReportRow> Last30Days,
    IReadOnlyList<ProductSalesRow> TopProducts30Days);
