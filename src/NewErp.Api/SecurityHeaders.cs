namespace NewErp.Api;

/// <summary>
/// Заголовки безпеки на кожній відповіді. Нову зовнішню адресу (скрипт, шрифт, API для браузера) спершу дописуємо в політику,
/// інакше браузер її заблокує.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Скрипти, стилі й запити — лише з цього сервера. Blazor WebAssembly потребує 'wasm-unsafe-eval'; import map в index.html —
    /// вбудований скрипт, який збірка щоразу переписує (хеш не зафіксуєш), тож скриптам дозволено 'unsafe-inline'. MudBlazor задає
    /// вбудовані стилі. Картинки товарів можуть бути з сайту магазину, тож img-src дозволяє https:. Сторінку не можна вбудувати
    /// в чужий сайт (frame-ancestors), а форми надсилаються лише сюди.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; " +
        "form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        // Документація /scalar (лише в Development) тягне власні скрипти, тож політику вмісту на неї не ставимо.
        if (!context.Request.Path.StartsWithSegments("/scalar"))
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
        return next(context);
    });
}
