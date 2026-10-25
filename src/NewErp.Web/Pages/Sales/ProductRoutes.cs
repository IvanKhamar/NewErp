namespace NewErp.Web.Pages.Sales;

/// <summary>Адреси сторінок товарів магазину в інтерфейсі (відносно /app/), ті самі, що у Vaadin-версії.</summary>
public static class ProductRoutes
{
    public const string List = "catalog/products";

    public static string Product(int id) => $"catalog/products/{id}";
}
