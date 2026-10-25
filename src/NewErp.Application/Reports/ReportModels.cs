using NewErp.Domain.Reports;

namespace NewErp.Application.Reports;

/// <summary>Фільтр звітів. Без статусу враховуються всі оформлені замовлення, крім скасованих.</summary>
public sealed record ReportFilter(DateOnly? From = null, DateOnly? To = null, int? StatusId = null, ReportGrouping Group = ReportGrouping.Week);

public sealed record SalesReportRow(DateOnly Start, DateOnly End, int Orders, int Products, decimal Total);

public sealed record SalesReportDto(DateOnly From, DateOnly To, IReadOnlyList<SalesReportRow> Rows, int Orders, int Products, decimal Total);

public sealed record ProductSalesRow(int ProductId, string Name, string Model, int Quantity, decimal Total);

public sealed record ProductSalesDto(DateOnly From, DateOnly To, IReadOnlyList<ProductSalesRow> Rows);

/// <summary>Замовлення одного статусу за період: кількість і сума.</summary>
public sealed record OrderStatusReportRow(int StatusId, string Name, int Orders, decimal Total);

/// <summary>Повернення одного статусу за період: скільки повернень і скільки одиниць товару.</summary>
public sealed record ReturnStatusReportRow(int StatusId, string Name, int Returns, int Quantity);

/// <summary>
/// Замовлення й повернення за статусами, як «Статистика» старого сайту. Тут кожен статус окремим рядком, тож скасовані
/// замовлення теж лічаться; незавершені (статус 0) — ні.
/// </summary>
public sealed record StatusReportDto(
    DateOnly From, DateOnly To, IReadOnlyList<OrderStatusReportRow> Orders, int OrderCount, decimal Total,
    IReadOnlyList<ReturnStatusReportRow> Returns, int ReturnCount);

/// <summary>Показник «зараз проти раніше»: ChangePercent — null, якщо раніше було 0.</summary>
public sealed record DashboardTile(decimal Current, decimal Previous, int? ChangePercent);

/// <summary>
/// Фід виручки для зовнішнього дашборда (розділ RevenueFeed). Токен — лише в user-secrets чи змінній середовища;
/// без нього фід вимкнено.
/// </summary>
public sealed class RevenueFeedOptions
{
    public const string SectionName = "RevenueFeed";

    /// <summary>Що дашборд надсилає в заголовку X-Feed-Token (або ?token=).</summary>
    public string Token { get; set; } = "";

    /// <summary>Найдовший денний ряд, який можна попросити одним запитом.</summary>
    public int MaxDays { get; set; } = 366;

    public bool Enabled => !string.IsNullOrWhiteSpace(Token);
}

public sealed record RevenueTotals(DateOnly From, DateOnly To, int Orders, decimal Revenue, decimal AverageCheck);

public sealed record RevenueDay(DateOnly Date, int Orders, int Products, decimal Revenue);

/// <summary>
/// Виручка для зовнішнього дашборда: оформлені замовлення без скасованих, як на головній. PreviousMonth — ті самі дні
/// попереднього місяця (1–24 вересня проти 1–24 жовтня). Days — від найстаршого дня до сьогодні.
/// </summary>
public sealed record RevenueFeedDto(
    DateTime GeneratedAt, string Currency, RevenueTotals Today, RevenueTotals Yesterday, RevenueTotals Month,
    RevenueTotals PreviousMonth, RevenueTotals Year, IReadOnlyList<RevenueDay> Days);

public sealed record DashboardDto(
    DashboardTile OrdersToday,
    DashboardTile SalesToday,
    DashboardTile OrdersMonth,
    DashboardTile SalesMonth,
    DashboardTile AverageCheckMonth,
    DashboardTile NewCustomersMonth,
    IReadOnlyList<SalesReportRow> Last30Days,
    IReadOnlyList<ProductSalesRow> TopProducts30Days);
