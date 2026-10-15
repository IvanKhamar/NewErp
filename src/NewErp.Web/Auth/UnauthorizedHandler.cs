using System.Net;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace NewErp.Web.Auth;

/// <summary>
/// Пропускає кожен запит до API. Якщо відповідь 401 (сесія скінчилась, адмін змінив роль чи забрав доступ),
/// скидає стан входу й веде на сторінку входу. Запити самого входу не чіпає: там 401 означає «невірний пароль».
/// </summary>
public sealed class UnauthorizedHandler(IServiceProvider services) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        var path = request.RequestUri?.AbsolutePath ?? "";
        if (response.StatusCode == HttpStatusCode.Unauthorized && path is not ("/auth/login" or "/auth/me"))
        {
            // Провайдер і навігацію беремо тут, а не в конструкторі: провайдер сам залежить від HttpClient з цим обробником.
            services.GetRequiredService<ApiAuthenticationStateProvider>().SessionEnded();
            services.GetRequiredService<NavigationManager>().NavigateTo("login");
        }
        return response;
    }
}
