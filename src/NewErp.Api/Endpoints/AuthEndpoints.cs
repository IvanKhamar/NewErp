using System.Globalization;
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

        auth.MapPost("/login", async (LoginRequest request, AuthService service, LoginThrottle throttle, HttpContext http, CancellationToken ct) =>
        {
            // За проксі тут буде адреса проксі; тоді потрібні ForwardedHeaders з переліком довірених проксі (див. README, «Безпека»).
            var address = http.Connection.RemoteIpAddress?.ToString();
            if (throttle.RetryAfter(address, request.Username) is { } wait)
            {
                var minutes = (int)Math.Ceiling(wait.TotalMinutes);
                http.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Забагато невдалих спроб входу",
                    detail: $"Забагато невдалих спроб входу. Спробуйте знову через {minutes} хв.");
            }

            var authenticated = await service.LoginAsync(request, ct);
            if (authenticated is null)
            {
                throttle.Failed(address, request.Username);
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Невірний логін або пароль");
            }

            throttle.Succeeded(request.Username);
            await http.SignInAsync(authenticated.ToPrincipal());
            return Results.Ok(authenticated.User);
        })
        .AllowAnonymous()
        .WithSummary("Вхід: видає cookie сесії. Після кількох невдалих спроб для логіна чи адреси — 429 і Retry-After");

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync();
            return Results.NoContent();
        })
        .WithSummary("Вихід: видаляє cookie сесії");

        auth.MapGet("/me", (ClaimsPrincipal user) => user.ToCurrentUser())
            .WithSummary("Хто зараз увійшов; mustChangePassword = true — пароль тимчасовий, його треба змінити");

        auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal user, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            var authenticated = await service.ChangePasswordAsync(user.GetUserId(), request, ct);
            // Нова версія облікового запису робить недійсними всі сесії, тож поточну видаємо заново.
            await http.SignInAsync(authenticated.ToPrincipal());
            return Results.NoContent();
        })
        .WithSummary("Змінити власний пароль (потрібен поточний). Інші сесії цього користувача закриваються");
    }
}
