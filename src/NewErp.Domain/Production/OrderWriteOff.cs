using NewErp.Domain.Stock;

namespace NewErp.Domain.Production;

/// <summary>
/// Списання розхідників за замовленням. Для кожного замовлення буває лише одне,
/// тому повторний перехід у той самий статус нічого не списує вдруге.
/// </summary>
public class OrderWriteOff
{
    private readonly List<OrderWriteOffLine> _lines = [];

    private OrderWriteOff() { }

    public int OrderId { get; private set; }

    public int UserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<OrderWriteOffLine> Lines => _lines;

    /// <summary>
    /// Списує зі складу потрібну кількість кожного розхідника. Якщо залишку не вистачає,
    /// списує те, що є, а нестачу видно в рядку (<see cref="OrderWriteOffLine.Shortage"/>).
    /// </summary>
    /// <returns>Списання і записи журналу руху, які треба зберегти разом із ним.</returns>
    public static (OrderWriteOff WriteOff, IReadOnlyList<StockMovement> Movements) Perform(
        int orderId, IEnumerable<(StockItem Item, decimal Required)> needs, int userId, DateTime now)
    {
        var writeOff = new OrderWriteOff { OrderId = orderId, UserId = userId, CreatedAt = now };
        var movements = new List<StockMovement>();

        foreach (var (item, required) in needs)
        {
            var amount = Math.Min(required, item.Quantity);
            if (amount > 0)
                movements.Add(item.Adjust(-amount, $"Списання за замовленням №{orderId}", userId, now));
            writeOff._lines.Add(new OrderWriteOffLine(orderId, item.Id, required, amount));
        }

        return (writeOff, movements);
    }
}
