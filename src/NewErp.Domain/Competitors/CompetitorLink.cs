using NewErp.Domain.Common;

namespace NewErp.Domain.Competitors;

/// <summary>
/// Сторінка нашого товару (dd_product) на сайті конкурента з останньою знайденою ціною (erp_competitor_link).
/// LastError — чому остання перевірка не вдалася, порожньо — вдалася.
/// </summary>
public class CompetitorLink
{
    /// <summary>Ціну прочитано зі сторінки.</summary>
    public const string Auto = "auto";

    /// <summary>Ціну вписано вручну (сайт не дає її прочитати).</summary>
    public const string Manual = "manual";

    private CompetitorLink() { }

    public CompetitorLink(Competitor competitor, int productId, string? url, DateTime now)
    {
        var page = CompetitorSite.RequireUrl(url, "Сторінка товару", 500);
        if (!CompetitorSite.IsOnSite(page, competitor.Host))
            throw new DomainException($"Сторінка має бути на сайті «{competitor.Name}» ({competitor.Host}).");
        CompetitorId = competitor.Id;
        ProductId = productId;
        Url = page.ToString();
        CreatedAt = now;
    }

    public int Id { get; private set; }

    public int CompetitorId { get; private set; }

    public int ProductId { get; private set; }

    public string Url { get; private set; } = "";

    public decimal? LastPrice { get; private set; }

    public DateTime? LastCheckedAt { get; private set; }

    public string LastError { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Знайдена чи вписана ціна. Історія отримує рядок, лише коли ціна змінилася (або знайдена вперше): тоді повертається
    /// цей рядок, інакше null.
    /// </summary>
    public CompetitorPrice? RecordPrice(decimal price, string source, DateTime now)
    {
        if (price <= 0 || decimal.Round(price, 2) != price)
            throw new DomainException("Ціна має бути більшою за нуль, не більше двох знаків після коми.");
        var changed = LastPrice != price;
        LastPrice = price;
        LastCheckedAt = now;
        LastError = "";
        return changed ? new CompetitorPrice(Id, price, source, now) : null;
    }

    /// <summary>Перевірка не вдалася: остання ціна лишається, причина — в LastError.</summary>
    public void RecordFailure(string error, DateTime now)
    {
        LastCheckedAt = now;
        LastError = error.Length > 255 ? error[..255] : error;
    }
}
