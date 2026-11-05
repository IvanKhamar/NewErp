using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

namespace NewErp.Api.Auth;

/// <summary>
/// За проксі (nginx, Cloudflare) адреса з'єднання — це адреса проксі, тож усі користувачі ділили б один ліміт входу.
/// Адресу клієнта з X-Forwarded-For беремо лише від проксі зі списку TrustedProxies (секція в налаштуваннях або змінні
/// TrustedProxies__0, TrustedProxies__1...). Від когось іншого цей заголовок ігноруємо, інакше нападник підмінить адресу.
/// Порожній список: заголовок не читаємо зовсім.
/// </summary>
public static class TrustedProxies
{
    public const string SectionName = "TrustedProxies";

    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var addresses = configuration.GetSection(SectionName).Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(options => Apply(options, addresses));
        return services;
    }

    public static IApplicationBuilder UseTrustedProxies(this IApplicationBuilder app, IConfiguration configuration) =>
        configuration.GetSection(SectionName).GetChildren().Any() ? app.UseForwardedHeaders() : app;

    /// <summary>Хибна адреса в налаштуваннях зупиняє запуск: краще одразу, ніж мовчки довіряти не тому.</summary>
    public static void Apply(ForwardedHeadersOptions options, IReadOnlyCollection<string> addresses)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Типово довіряють localhost; тут довіряємо лише тому, що вказано.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var address in addresses)
            options.KnownProxies.Add(IPAddress.Parse(address));
        options.ForwardLimit = Math.Max(1, addresses.Count);
    }
}
