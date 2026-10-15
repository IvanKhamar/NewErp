using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

using NewErp.Application.Auth;

namespace NewErp.Api.Auth;

/// <summary>Назви політик доступу: яким ролям відкрито який модуль.</summary>
public static class Policies
{
    public const string Orders = nameof(Orders);
    public const string Stock = nameof(Stock);
    public const string Bonuses = nameof(Bonuses);
    public const string Users = nameof(Users);
}

public static class AuthSetup
{
    public static IServiceCollection AddErpAuth(this IServiceCollection services)
    {
        // Після POST /auth/login сервер видає зашифровану cookie, і браузер сам надсилає її з кожним запитом.
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "newerp.auth";
                options.Cookie.HttpOnly = true; // JavaScript на сторінці не може прочитати cookie
                options.Cookie.SameSite = SameSiteMode.Strict; // чужі сайти не надішлють запит від імені користувача (захист від CSRF)
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true; // поки користувач працює, сесія подовжується

                // Типово cookie-схема переадресовує на сторінку входу. В API замість цього повертаємо коди 401 і 403.
                options.Events.OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden);

                // На кожному запиті: чи не змінилася версія облікового запису (роль, пароль, відкликаний доступ)
                // і чи не вимкнули користувача на старому сайті. Якщо так — cookie недійсна, відповідь 401.
                options.Events.OnValidatePrincipal = async context =>
                {
                    var principal = context.Principal!;
                    var store = context.HttpContext.RequestServices.GetRequiredService<IUserLoginStore>();
                    var current = await store.GetActiveSecurityStampAsync(principal.GetUserId(), context.HttpContext.RequestAborted);
                    if (current is null || current != principal.GetSecurityStamp())
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            });

        services.AddAuthorizationBuilder()
            // Усе, що не позначено AllowAnonymous, вимагає входу. Так новий ендпоінт не залишиться відкритим випадково.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Orders, p => p.RequireRole(Roles.Admin, Roles.Manager))
            .AddPolicy(Policies.Stock, p => p.RequireRole(Roles.Admin, Roles.Storekeeper))
            .AddPolicy(Policies.Bonuses, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.Users, p => p.RequireRole(Roles.Admin));

        return services;
    }

    private static Task SetStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
