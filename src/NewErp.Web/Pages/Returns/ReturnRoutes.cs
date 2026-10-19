namespace NewErp.Web.Pages.Returns;

/// <summary>Адреси сторінок повернень в інтерфейсі (відносно /app/).</summary>
public static class ReturnRoutes
{
    public const string List = "returns";

    public static string Return(int id) => $"returns/{id}";
}
