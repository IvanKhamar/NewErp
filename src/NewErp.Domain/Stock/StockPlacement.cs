using NewErp.Domain.Common;

namespace NewErp.Domain.Stock;

/// <summary>
/// Де лежить розхідник. dd_stock_items.stock лишається загальним залишком: з ним працюють старий сайт, замовлення
/// й виробництво. Склади мають свої залишки, а основний склад — це решта: загальний мінус усе на складах.
/// Тож усе, що не називає складу, бере з основного або додає до нього. warehouseId = null — основний склад.
/// </summary>
public sealed class StockPlacement
{
    private readonly Dictionary<int, StockBalance> _balances;
    private readonly List<StockBalance> _created = [];

    public StockPlacement(StockItem item, IEnumerable<StockBalance> balances)
    {
        Item = item;
        _balances = balances.ToDictionary(b => b.WarehouseId);
    }

    public StockItem Item { get; }

    /// <summary>Основний склад: загальний залишок мінус склади. Від'ємний, якщо загальний зменшили повз нього.</summary>
    public decimal Main => Item.Quantity - Located;

    /// <summary>Баланси складів, також щойно створені.</summary>
    public IReadOnlyCollection<StockBalance> Balances => _balances.Values;

    /// <summary>Баланси для складів, де позиції ще не було: їх треба додати до збереження.</summary>
    public IReadOnlyList<StockBalance> Created => _created;

    private decimal Located => _balances.Values.Sum(b => b.Quantity);

    /// <summary>Залишок на складі або, для null, на основному.</summary>
    public decimal At(int? warehouseId) =>
        warehouseId is { } id ? _balances.GetValueOrDefault(id)?.Quantity ?? 0 : Main;

    /// <summary>Переміщує кількість між двома місцями; загальний залишок не змінюється. Не більше, ніж є в джерелі.</summary>
    public StockTransfer Move(int? from, int? to, decimal quantity, string? comment, int userId, string username, DateTime now)
    {
        if (from == to)
            throw new DomainException("Звідки й куди переміщують — має бути різне місце.");
        RequireQuantity(quantity);
        var text = comment?.Trim() ?? "";
        if (text.Length > 255)
            throw new DomainException("Коментар: не довше 255 символів.");
        RequireAvailable(from, quantity);

        Change(from, -quantity);
        Change(to, quantity);
        return new StockTransfer(Item.Id, from, to, quantity, text, userId, username, now);
    }

    /// <summary>
    /// Прихід (+) або витрата (−) в одному місці. Загальний залишок теж змінюється, тому це запис у журнал руху,
    /// як будь-яке коригування. Витратити в місці можна не більше, ніж там є.
    /// </summary>
    public StockMovement AdjustAt(int? warehouseId, decimal delta, string comment, int userId, string username, DateTime now)
    {
        // Коли на складах нічого немає, основний склад і є загальним залишком: діє звична перевірка позиції.
        if (delta < 0 && (warehouseId is not null || Located != 0))
            RequireAvailable(warehouseId, -delta);
        var movement = Item.Adjust(delta, comment, userId, username, now);
        Change(warehouseId, delta);
        return movement;
    }

    /// <summary>
    /// Інвентаризація одного місця: там стає пораховане, а загальний залишок змінюється на ту саму різницю
    /// (запис у журнал). Повертає null, якщо збіглося.
    /// </summary>
    public StockMovement? RecountAt(int? warehouseId, decimal actual, string comment, int userId, string username, DateTime now)
    {
        Stocktaking.RequireValid(actual);
        var delta = actual - At(warehouseId);
        if (delta == 0)
            return null;
        var movement = Item.Recount(Item.Quantity + delta, comment, userId, username, now);
        Change(warehouseId, delta);
        return movement;
    }

    /// <summary>Прихід, що вже потрапив у загальний залишок (поставка), лежить на цьому складі, а не на основному.</summary>
    public void ReceivedAt(int warehouseId, decimal quantity)
    {
        RequireQuantity(quantity);
        Change(warehouseId, quantity);
    }

    private void Change(int? warehouseId, decimal delta)
    {
        if (warehouseId is not { } id)
            return; // основний склад — це решта, він змінюється сам

        if (!_balances.TryGetValue(id, out var balance))
        {
            balance = new StockBalance(Item.Id, id);
            _balances[id] = balance;
            _created.Add(balance);
        }
        balance.Add(delta);
    }

    private void RequireAvailable(int? warehouseId, decimal quantity)
    {
        var available = Math.Max(At(warehouseId), 0);
        if (available < quantity)
        {
            var place = warehouseId is null ? "на основному складі" : "на цьому складі";
            throw new DomainException(
                $"Недостатньо «{Item.Name}»: {place} є {available:0.###} {Item.Unit}, потрібно {quantity:0.###} {Item.Unit}.");
        }
    }

    internal static void RequireQuantity(decimal quantity)
    {
        if (quantity <= 0 || decimal.Round(quantity, 3) != quantity)
            throw new DomainException("Кількість має бути більшою за нуль, не більше 3 знаків після коми.");
    }
}
