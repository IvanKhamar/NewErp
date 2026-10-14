using NewErp.Domain.Common;

namespace NewErp.Domain.Stock;

/// <summary>
/// Розхідник на складі (тканина, фурнітура, пакування...).
/// Залишок змінюється лише через <see cref="Adjust"/>, тому кожна зміна потрапляє в журнал.
/// </summary>
public class StockItem
{
    private StockItem() { }

    public StockItem(int categoryId, int? supplierId, string name, string unit, decimal minQuantity)
    {
        CategoryId = categoryId;
        SupplierId = supplierId;
        Name = RequireText(name, "Назва позиції", 150);
        Unit = RequireText(unit, "Одиниця виміру", 16);
        SetMinQuantity(minQuantity);
    }

    public int Id { get; private set; }

    public int CategoryId { get; private set; }

    public int? SupplierId { get; private set; }

    public string Name { get; private set; } = "";

    public string Unit { get; private set; } = "";

    public decimal Quantity { get; private set; }

    public decimal MinQuantity { get; private set; }

    public bool IsBelowMinimum => Quantity < MinQuantity;

    /// <summary>Змінює залишок на <paramref name="delta"/> (плюс = прихід, мінус = витрата).</summary>
    public StockMovement Adjust(decimal delta, string reason, int userId, DateTime now)
    {
        if (delta == 0)
            throw new DomainException("Зміна залишку не може дорівнювати нулю.");
        if (Quantity + delta < 0)
            throw new DomainException($"Недостатньо залишку: є {Quantity} {Unit}, списується {-delta} {Unit}.");

        Quantity += delta;
        return new StockMovement(this, delta, RequireText(reason, "Причина", 255), userId, now);
    }

    public void SetMinQuantity(decimal minQuantity)
    {
        if (minQuantity < 0)
            throw new DomainException("Мінімальний залишок не може бути від'ємним.");
        MinQuantity = minQuantity;
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