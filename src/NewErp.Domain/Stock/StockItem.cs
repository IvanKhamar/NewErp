using NewErp.Domain.Common;

namespace NewErp.Domain.Stock;

/// <summary>
/// Розхідник на складі (тканина, фурнітура, пакування...), таблиця dd_stock_items старого сайту.
/// Залишок змінюється лише через <see cref="Adjust"/> і <see cref="TakeUpTo"/>, тому кожна зміна потрапляє в журнал.
/// </summary>
public class StockItem
{
    private StockItem() { }

    public StockItem(int? categoryId, int? supplierId, string name, string unit, decimal minQuantity, string? usedFor = null, decimal price = 0) =>
        Update(categoryId, supplierId, name, unit, minQuantity, usedFor, price);

    /// <summary>Змінює опис позиції. Залишок тут не змінюється: для цього лише Adjust і TakeUpTo з записом у журнал.</summary>
    public void Update(int? categoryId, int? supplierId, string name, string unit, decimal minQuantity, string? usedFor, decimal price)
    {
        if (price < 0)
            throw new DomainException("Ціна не може бути від'ємною.");
        CategoryId = categoryId;
        SupplierId = supplierId;
        Name = RequireText(name, "Назва позиції", 150);
        Unit = RequireText(unit, "Одиниця виміру", 16);
        UsedFor = usedFor?.Trim() ?? "";
        Price = price;
        SetMinQuantity(minQuantity);
    }

    public int Id { get; private set; }

    public int? CategoryId { get; private set; }

    public int? SupplierId { get; private set; }

    public string Name { get; private set; } = "";

    public string Unit { get; private set; } = "";

    public decimal Quantity { get; private set; }

    public decimal MinQuantity { get; private set; }

    /// <summary>Для чого використовується (вільний текст зі старого модуля).</summary>
    public string UsedFor { get; private set; } = "";

    /// <summary>Ціна за одиницю.</summary>
    public decimal Price { get; private set; }

    /// <summary>false — позицію «видалено»: старий модуль не видаляє рядки, а ставить status = 0.</summary>
    public bool IsActive { get; private set; } = true;

    public bool IsBelowMinimum => Quantity < MinQuantity;

    /// <summary>
    /// Ручна зміна залишку на <paramref name="delta"/> (плюс = прихід, мінус = витрата).
    /// Строгіше за старий модуль: списати більше, ніж є, не можна.
    /// </summary>
    public StockMovement Adjust(decimal delta, string comment, int userId, string username, DateTime now)
    {
        if (delta == 0)
            throw new DomainException("Зміна залишку не може дорівнювати нулю.");
        if (Quantity + delta < 0)
            throw new DomainException($"Недостатньо залишку: є {Quantity} {Unit}, списується {-delta} {Unit}.");

        return Change(delta, RequireText(comment, "Причина", 255), userId, username, now);
    }

    /// <summary>
    /// Списання для виробництва, як у старому модулі: якщо залишку не вистачає, списується те, що є,
    /// і залишок стає нулем. Повертає null, якщо списувати нічого.
    /// </summary>
    public StockMovement? TakeUpTo(decimal wanted, string comment, int userId, string username, DateTime now)
    {
        if (wanted <= 0)
            throw new DomainException("Кількість для списання має бути більшою за нуль.");

        var amount = Math.Min(wanted, Quantity);
        return amount > 0 ? Change(-amount, RequireText(comment, "Причина", 255), userId, username, now) : null;
    }

    /// <summary>Інвентаризація: залишок стає порахованим, різниця йде в журнал. Повертає null, якщо збіглося з обліком.</summary>
    public StockMovement? Recount(decimal actual, string comment, int userId, string username, DateTime now)
    {
        Stocktaking.RequireValid(actual);
        var delta = actual - Quantity;
        return delta == 0 ? null : Change(delta, RequireText(comment, "Причина", 255), userId, username, now);
    }

    public void SetMinQuantity(decimal minQuantity)
    {
        if (minQuantity < 0)
            throw new DomainException("Мінімальний залишок не може бути від'ємним.");
        MinQuantity = minQuantity;
    }

    private StockMovement Change(decimal delta, string comment, int userId, string username, DateTime now)
    {
        var before = Quantity;
        Quantity += delta;
        return new StockMovement(this, delta, before, comment, userId, username, now);
    }

    internal static string RequireText(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException($"{field}: значення обов'язкове.");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field}: не довше {maxLength} символів.");
        return trimmed;
    }
}
