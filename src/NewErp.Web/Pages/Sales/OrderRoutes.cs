namespace NewErp.Web.Pages.Sales;

/// <summary>Адреси сторінок замовлень в інтерфейсі (відносно /app/).</summary>
public static class OrderRoutes
{
    public static string Order(int id) => $"orders/{id}";

    public const string NewOrder = "orders/new";
}
