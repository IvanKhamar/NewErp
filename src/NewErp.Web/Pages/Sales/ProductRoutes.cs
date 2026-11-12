namespace NewErp.Web.Pages.Sales;

/// <summary>Адреси сторінок товарів магазину в інтерфейсі (відносно /app/), ті самі, що у Vaadin- і Python-версії.</summary>
public static class ProductRoutes
{
    public const string List = "catalog/products";

    public const string New = "catalog/products/new";

    public const string Import = "catalog/products/import";

    public static string Product(int id) => $"catalog/products/{id}";

    public static string Edit(int id) => $"catalog/products/{id}/edit";
}
