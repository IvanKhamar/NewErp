namespace NewErp.Domain.Competitors;

/// <summary>Рядок історії цін конкурента (erp_competitor_price): з'являється, коли ціна змінилася. Source — auto чи manual.</summary>
public class CompetitorPrice
{
    private CompetitorPrice() { }

    internal CompetitorPrice(int linkId, decimal price, string source, DateTime checkedAt)
    {
        LinkId = linkId;
        Price = price;
        Source = source;
        CheckedAt = checkedAt;
    }

    public int Id { get; private set; }

    public int LinkId { get; private set; }

    public decimal Price { get; private set; }

    public string Source { get; private set; } = "";

    public DateTime CheckedAt { get; private set; }
}
