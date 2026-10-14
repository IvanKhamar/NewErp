using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

using NewErp.Api.Auth;
using NewErp.Application.Auth;

namespace NewErp.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Вхід");

        auth.MapPost("/login", async (LoginRequest request, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            var user = await service.LoginAsync(request, ct);
            if (user is null)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Невірний логін або пароль");

            await http.SignInAsync(user.ToPrincipal());
            return Results.Ok(user);
        })
        .AllowAnonymous()
        .WithSummary("Вхід: видає cookie сесії");

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync();
            return Results.NoContent();
        })
        .WithSummary("Вихід: видаляє cookie сесії");

        auth.MapGet("/me", (ClaimsPrincipal user) => user.ToCurrentUser())
            .WithSummary("Хто зараз увійшов");
    }
}
