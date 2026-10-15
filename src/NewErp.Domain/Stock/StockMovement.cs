namespace NewErp.Domain.Stock;

/// <summary>
/// Запис журналу руху розхідника (таблиця dd_stock_log старого сайту).
/// Створюється лише через <see cref="StockItem.Adjust"/> або <see cref="StockItem.TakeUpTo"/>.
/// </summary>
public class StockMovement
{
    /// <summary>Витрата. Так само пише старий модуль StockTrack.</summary>
    public const string Use = "use";

    /// <summary>Прихід.</summary>
    public const string Restock = "restock";

    private StockMovement() { }

    internal StockMovement(StockItem item, decimal delta, decimal stockBefore, string comment, int userId, string username, DateTime createdAt)
    {
        Item = item;
        ItemName = item.Name;
        Action = delta < 0 ? Use : Restock;
        Quantity = Math.Abs(delta);
        StockBefore = stockBefore;
        StockAfter = item.Quantity;
        Comment = comment;
        UserId = userId;
        Username = username;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public int ItemId { get; private set; }

    public StockItem Item { get; private set; } = null!;

    /// <summary>Назва позиції на момент запису (старий журнал зберігає її копію).</summary>
    public string ItemName { get; private set; } = "";

    /// <summary><see cref="Use"/> або <see cref="Restock"/>.</summary>
    public string Action { get; private set; } = "";

    /// <summary>Скільки списано чи прийнято, завжди додатне.</summary>
    public decimal Quantity { get; private set; }

    public decimal StockBefore { get; private set; }

    public decimal StockAfter { get; private set; }

    public string Comment { get; private set; } = "";

    public int UserId { get; private set; }

    public string Username { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }

    /// <summary>Зміна залишку зі знаком: мінус — витрата, плюс — прихід.</summary>
    public decimal Delta => Action == Use ? -Quantity : Quantity;
}
