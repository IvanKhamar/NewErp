namespace NewErp.Application.Common;

/// <summary>Зміна суперечить тому, що вже є (зайнята SEO-адреса, товар змінили тим часом). Api перетворює її на 409.</summary>
public class ConflictException(string message) : Exception(message);
