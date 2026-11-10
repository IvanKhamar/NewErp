namespace NewErp.Web.Pages.Sales;

/// <summary>Адреси сторінок замовлень в інтерфейсі (відносно /app/).</summary>
public static class OrderRoutes
{
    public static string Order(int id) => $"orders/{id}";

    public const string NewOrder = "orders/new";

    public const string Partners = "partners";

    public static string Partner(int id) => $"partners/{id}";

    public const string NewPartner = "partners/new";

    public const string Discrepancies = "orders/discrepancies";

    /// <param name="kind">invoice (рахунок) або shipping (пакувальний лист).</param>
    public static string Print(string kind, IEnumerable<int> ids) => $"orders/print/{kind}?" + string.Join("&", ids.Select(id => $"ids={id}"));
}
