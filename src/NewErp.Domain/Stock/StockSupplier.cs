namespace NewErp.Domain.Stock;

public class StockSupplier
{
    private StockSupplier() { }

    public StockSupplier(string name, string? phone, string? email, string? categories, string? notes)
    {
        Name = StockItem.RequireText(name, "Назва постачальника", 255);
        Phone = phone?.Trim() ?? "";
        Email = email?.Trim() ?? "";
        Categories = categories?.Trim() ?? "";
        Notes = notes?.Trim() ?? "";
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    // Старий модуль зберігає порожній рядок, а не NULL.
    private string? _phone; // NULL в старій таблиці читається як ""

    public string Phone { get => _phone ?? ""; private set => _phone = value; }

    private string? _email; // NULL в старій таблиці читається як ""

    public string Email { get => _email ?? ""; private set => _email = value; }

    /// <summary>Вільний текст: що саме постачає (тканини, фурнітура...).</summary>
    private string? _categories; // NULL в старій таблиці читається як ""

    public string Categories { get => _categories ?? ""; private set => _categories = value; }

    private string? _notes; // NULL в старій таблиці читається як ""

    public string Notes { get => _notes ?? ""; private set => _notes = value; }
}
