namespace NewErp.Web.Pages.Sales;

/// <summary>Адреси сторінок замовлень в інтерфейсі (відносно /app/).</summary>
public static class OrderRoutes
{
    public static string Order(int id) => $"orders/{id}";

    public const string NewOrder = "orders/new";

    public const string Partners = "partners";

    public static string Partner(int id) => $"partners/{id}";

    public const string NewPartner = "partners/new";
}
