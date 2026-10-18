using NewErp.Domain.Common;

namespace NewErp.Domain.Catalog;

/// <summary>Чи можна купити розмір на вітрині.</summary>
public enum SizeAvailability
{
    InStock,

    /// <summary>Залишку немає, але є ліміт передзамовлення: вітрина продає наперед.</summary>
    PreOrder,

    SoldOut,
}

/// <summary>
/// Передзамовлення розміру товару, як на старому сайті: коли залишку немає, вітрина продає наперед до pre_order_quantity штук,
/// і залишок іде в мінус. Потім продане наперед списується з ліміту (updatePreOrdersQuantity).
/// </summary>
public static class PreOrder
{
    public const int MaxQuota = 9999;

    /// <summary>Як у кошику старого сайту: розмір без обліку залишку завжди в наявності, передзамовлення — коли залишок ≤ 0 і ліміт є.</summary>
    public static SizeAvailability Availability(int quantity, int quota, bool subtract) =>
        !subtract || quantity > 0 ? SizeAvailability.InStock
        : quota > 0 ? SizeAvailability.PreOrder
        : SizeAvailability.SoldOut;

    /// <summary>
    /// Списує продане наперед (мінусовий залишок) з ліміту. Старий код робив ліміт += залишок, і перепродаж понад ліміт
    /// давав мінусовий ліміт, який вітрина приймала за дозвіл продавати далі. Тут ліміт не йде нижче 0, а перепродане
    /// понад нього лишається мінусом у залишку, щоб його було видно.
    /// </summary>
    public static (int Quantity, int Quota) Settle(int quantity, int quota)
    {
        if (quantity >= 0 || quota <= 0)
            return (quantity, quota);
        var covered = Math.Min(quota, -quantity);
        return (quantity + covered, quota - covered);
    }

    public static void ValidateQuota(int quota)
    {
        if (quota is < 0 or > MaxQuota)
            throw new DomainException($"Ліміт передзамовлення — від 0 до {MaxQuota} штук.");
    }
}
