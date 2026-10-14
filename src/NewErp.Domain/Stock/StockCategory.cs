namespace NewErp.Domain.Stock;

public class StockCategory
{
    private StockCategory() { }

    public StockCategory(string name) => Name = StockItem.RequireText(name, "Назва категорії", 100);

    public int Id { get; private set; }

    public string Name { get; private set; } = "";
}