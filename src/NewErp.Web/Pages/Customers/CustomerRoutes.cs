namespace NewErp.Web.Pages.Customers;

/// <summary>Адреси сторінок покупців в інтерфейсі (відносно /app/).</summary>
public static class CustomerRoutes
{
    public const string List = "customers";

    public static string Customer(int id) => $"customers/{id}";
}
