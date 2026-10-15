namespace NewErp.Web.Pages.Inventory;

/// <summary>Адреси сторінок складу в інтерфейсі (відносно /app/).</summary>
public static class StockRoutes
{
    public static string Item(int id) => $"stock/items/{id}";

    public static string PurchaseOrder(int id) => $"stock/purchase-orders/{id}";
}
