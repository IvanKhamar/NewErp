namespace NewErp.Domain.Stock;

public class StockSupplier
{
    private StockSupplier() { }

    public StockSupplier(string name, string? phone, string? email, string? categories, string? notes)
    {
        Name = StockItem.RequireText(name, "Назва постачальника", 150);
        Phone = phone?.Trim() ?? "";
        Email = email?.Trim() ?? "";
        Categories = categories?.Trim() ?? "";
        Notes = notes?.Trim() ?? "";
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    // Старий модуль зберігає порожній рядок, а не NULL.
    public string Phone { get; private set; } = "";

    public string Email { get; private set; } = "";

    /// <summary>Вільний текст: що саме постачає (тканини, фурнітура...).</summary>
    public string Categories { get; private set; } = "";

    public string Notes { get; private set; } = "";
}
