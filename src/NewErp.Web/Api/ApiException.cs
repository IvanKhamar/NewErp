namespace NewErp.Web.Api;

/// <summary>API відмовив (400, 404, 409...). Message — пояснення з відповіді, його можна показати людині.</summary>
public sealed class ApiException(string message) : Exception(message);
