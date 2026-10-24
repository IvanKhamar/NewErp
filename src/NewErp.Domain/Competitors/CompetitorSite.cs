using NewErp.Domain.Common;

namespace NewErp.Domain.Competitors;

/// <summary>Адреси конкурентів: лише http(s), а сторінка товару має бути на власному сайті конкурента.</summary>
public static class CompetitorSite
{
    /// <summary>Обрізана абсолютна адреса http(s) з хостом, не довша за maxLength.</summary>
    public static Uri RequireUrl(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
            throw new DomainException($"{field}: вкажіть адресу до {maxLength} символів.");
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new DomainException($"{field}: це не схоже на адресу сторінки.");
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
            throw new DomainException($"{field}: адреса має починатися з http:// або https://.");
        return uri;
    }

    /// <summary>Хост без «www.» у нижньому регістрі: shop.ua для https://www.Shop.ua/catalog.</summary>
    public static string Host(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    /// <summary>Чи сторінка на сайті: той самий хост або його піддомен (m.shop.ua на shop.ua).</summary>
    public static bool IsOnSite(Uri page, string siteHost)
    {
        var host = Host(page);
        return host == siteHost || host.EndsWith("." + siteHost, StringComparison.Ordinal);
    }
}
