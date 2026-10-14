namespace NewErp.Domain.Stock;

public class StockSupplier
{
    private StockSupplier() { }

    public StockSupplier(string name, string? phone, string? note)
    {
        Name = StockItem.RequireText(name, "Назва постачальника", 150);
        Phone = phone;
        Note = note;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    public string? Phone { get; private set; }

    public string? Note { get; private set; }
}