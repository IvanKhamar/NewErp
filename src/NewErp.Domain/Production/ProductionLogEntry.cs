namespace NewErp.Domain.Production;

/// <summary>
/// Запис журналу виробництва (dd_stock_production_log): зміна залишку крою чи пошиву одного розміру.
/// Значення <see cref="Stage"/> і <see cref="Direction"/> такі самі, як пише старий модуль StockTrack.
/// </summary>
public class ProductionLogEntry
{
    public const string Cut = "cut";
    public const string Sewn = "sewn";
    public const string In = "in";
    public const string Out = "out";

    private ProductionLogEntry() { }

    internal ProductionLogEntry(
        ProductionModel model, ProductionSize size, string stage, decimal delta, decimal before, decimal after,
        string comment, int userId, string username, DateTime createdAt)
    {
        ProductionId = model.Id;
        Model = model;
        SizeId = size.Id;
        Size = size;
        SizeName = size.Name;
        Stage = stage;
        Direction = delta > 0 ? In : Out;
        Quantity = Math.Abs(delta);
        StockBefore = before;
        StockAfter = after;
        Comment = comment;
        UserId = userId;
        Username = username;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public int ProductionId { get; private set; }

    /// <summary>Навігації потрібні, щоб EF підставив id нової моделі чи розміру під час збереження.</summary>
    public ProductionModel Model { get; private set; } = null!;

    public int SizeId { get; private set; }

    public ProductionSize Size { get; private set; } = null!;

    public string SizeName { get; private set; } = "";

    /// <summary><see cref="Cut"/> або <see cref="Sewn"/>.</summary>
    public string Stage { get; private set; } = "";

    /// <summary><see cref="In"/> або <see cref="Out"/>.</summary>
    public string Direction { get; private set; } = "";

    public decimal Quantity { get; private set; }

    public decimal StockBefore { get; private set; }

    public decimal StockAfter { get; private set; }

    public string Comment { get; private set; } = "";

    public int UserId { get; private set; }

    public string Username { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }
}
