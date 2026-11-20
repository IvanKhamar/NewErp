namespace NewErp.Web.Pages.Sales;

/// <summary>Адреси сторінок довідників каталогу (відносно /app/), ті самі, що в Python-версії.</summary>
public static class CategoryRoutes
{
    public const string List = "catalog/categories";

    public const string New = "catalog/categories/new";

    public const string Labels = "catalog/labels";

    public static string Edit(int id) => $"catalog/categories/{id}";
}
