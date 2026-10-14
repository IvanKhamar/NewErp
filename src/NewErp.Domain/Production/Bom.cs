using NewErp.Domain.Common;

namespace NewErp.Domain.Production;

/// <summary>
/// Специфікація виробу (BOM, bill of materials): скільки якого розхідника йде на одну одиницю товару.
/// Виріб — це товар OpenCart (product_id). Рядки змінюються лише через <see cref="SetLines"/>.
/// </summary>
public class Bom
{
    private readonly List<BomLine> _lines = [];

    private Bom() { }

    public Bom(int productId)
    {
        if (productId <= 0)
            throw new DomainException("Id товару має бути додатним.");
        ProductId = productId;
    }

    public int ProductId { get; private set; }

    public IReadOnlyList<BomLine> Lines => _lines;

    public int UpdatedByUserId { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Замінює склад специфікації. Наявні рядки оновлюються на місці, зайві видаляються, нові додаються.
    /// </summary>
    public void SetLines(IReadOnlyCollection<(int StockItemId, decimal Quantity)> lines, int userId, DateTime now)
    {
        if (lines.Count == 0)
            throw new DomainException("Специфікація має містити хоча б один розхідник.");
        if (lines.GroupBy(l => l.StockItemId).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
            throw new DomainException($"Розхідник {duplicate.Key} указано двічі.");
        if (lines.Where(l => l.Quantity <= 0).Select(l => (int?)l.StockItemId).FirstOrDefault() is { } invalidId)
            throw new DomainException($"Кількість розхідника {invalidId} має бути більшою за нуль.");

        _lines.RemoveAll(existing => lines.All(l => l.StockItemId != existing.StockItemId));
        foreach (var (stockItemId, quantity) in lines)
        {
            if (_lines.Find(l => l.StockItemId == stockItemId) is { } existing)
                existing.Quantity = quantity;
            else
                _lines.Add(new BomLine(ProductId, stockItemId, quantity));
        }

        UpdatedByUserId = userId;
        UpdatedAt = now;
    }
}
