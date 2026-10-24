using NewErp.Domain.Common;

namespace NewErp.Domain.Competitors;

/// <summary>Конкурент, за цінами якого стежимо (наша таблиця erp_competitor). SiteUrl — головна сторінка сайту.</summary>
public class Competitor
{
    private Competitor() { }

    public Competitor(string? name, string? siteUrl, string? priceRegex, bool isActive, DateTime now)
    {
        Update(name, siteUrl, priceRegex, isActive);
        CreatedAt = now;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    public string SiteUrl { get; private set; } = "";

    /// <summary>Власний вираз для ціни (перша група), коли сторінка не має розмітки schema.org чи og-тегів.</summary>
    public string PriceRegex { get; private set; } = "";

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Хост сайту без «www.»: сторінки товарів мають бути на ньому.</summary>
    public string Host => CompetitorSite.Host(new Uri(SiteUrl));

    public void Update(string? name, string? siteUrl, string? priceRegex, bool isActive)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Length > 100)
            throw new DomainException("Назва конкурента: від 1 до 100 символів.");
        Name = trimmed;
        SiteUrl = CompetitorSite.RequireUrl(siteUrl, "Сайт", 255).ToString();
        PriceRegex = PriceExtractor.RequireRegex(priceRegex);
        IsActive = isActive;
    }
}
