namespace NewErp.Domain.Production;

/// <summary>Товар OpenCart, прив'язаний до моделі напряму (dd_stock_production_products).</summary>
public class ProductionProductLink
{
    private ProductionProductLink() { }

    internal ProductionProductLink(int productId) => ProductId = productId;

    public int ProductionId { get; private set; }

    public int ProductId { get; private set; }
}

/// <summary>
/// Прив'язка через категорію OpenCart (dd_stock_production_categories): товари категорії,
/// назва яких містить <see cref="NameFilter"/> (або дорівнює йому, якщо <see cref="MatchExact"/>).
/// </summary>
public class ProductionCategoryLink
{
    private ProductionCategoryLink() { }

    internal ProductionCategoryLink(int categoryId, string nameFilter, bool matchExact)
    {
        CategoryId = categoryId;
        NameFilter = nameFilter;
        MatchExact = matchExact;
    }

    public int ProductionId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Порожній — уся категорія.</summary>
    public string NameFilter { get; internal set; } = "";

    public bool MatchExact { get; internal set; }
}
