using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.Domain.Production;

/// <summary>Деталь крою моделі (dd_stock_production_components) зі своєю специфікацією розхідників.</summary>
public class ProductionComponent
{
    private readonly List<ComponentBomLine> _bom = [];

    private ProductionComponent() { }

    public ProductionComponent(int productionId, string name, int sortOrder)
    {
        ProductionId = productionId;
        Rename(name, sortOrder);
    }

    public int Id { get; private set; }

    public int ProductionId { get; private set; }

    public string Name { get; private set; } = "";

    public int SortOrder { get; private set; }

    public IReadOnlyList<ComponentBomLine> Bom => _bom;

    public void Rename(string name, int sortOrder)
    {
        Name = StockItem.RequireText(name, "Назва деталі", 100);
        SortOrder = sortOrder;
    }

    /// <summary>Замінює специфікацію крою: скільки розхідника йде на одну деталь (за потреби — окремо для розміру).</summary>
    public void SetBom(IReadOnlyCollection<(int? SizeId, int ItemId, decimal QtyPerUnit)> rows, IReadOnlyCollection<int> sizeIdsOfModel)
    {
        foreach (var row in rows)
        {
            if (row.QtyPerUnit <= 0)
                throw new DomainException("Кількість розхідника на одну деталь має бути більшою за нуль.");
            if (row.SizeId is { } sizeId && !sizeIdsOfModel.Contains(sizeId))
                throw new DomainException($"Розмір {sizeId} не належить моделі цієї деталі.");
        }
        if (rows.GroupBy(r => (r.SizeId, r.ItemId)).Any(g => g.Count() > 1))
            throw new DomainException("Специфікація крою містить повторювані рядки.");

        _bom.Clear();
        _bom.AddRange(rows.Select(r => new ComponentBomLine(r.SizeId, r.ItemId, r.QtyPerUnit)));
    }
}
