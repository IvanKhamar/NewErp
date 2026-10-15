namespace NewErp.Domain.Stock;

public class StockCategory
{
    private StockCategory() { }

    public StockCategory(string name, int sortOrder = 0)
    {
        Name = StockItem.RequireText(name, "Назва категорії", 100);
        SortOrder = sortOrder;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    public int SortOrder { get; private set; }
}
