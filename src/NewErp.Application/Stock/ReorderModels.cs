namespace NewErp.Application.Stock;

/// <summary>Налаштування складу з розділу "Stock" в appsettings.json.</summary>
public sealed class StockOptions
{
    public const string SectionName = "Stock";

    /// <summary>
    /// До скількох мінімумів дозамовляти: 2 означає «довести залишок до 2 × мінімум».
    /// Запас із подушкою, щоб позиція не ставала «нижче мінімуму» одразу після приходу.
    /// </summary>
    public decimal ReorderTargetFactor { get; set; } = 2;
}

/// <summary>
/// Позиція, яку треба дозамовити. OnOrder — уже замовлено й ще не отримано (віднімається від пропозиції).
/// Suggested округлено вгору до цілого, бо постачальники продають цілими метрами й штуками.
/// </summary>
public sealed record ReorderLineDto(
    int ItemId, string Name, string Unit, decimal Quantity, decimal MinQuantity, decimal OnOrder, decimal Suggested, decimal Price, decimal Sum);

/// <summary>SupplierId = null — позиції без постачальника. MessageText — готовий текст замовлення для месенджера чи листа.</summary>
public sealed record SupplierReorderDto(
    int? SupplierId, string SupplierName, string Phone, string Email, IReadOnlyList<ReorderLineDto> Lines, decimal Total, string MessageText);

public sealed record ReorderReportDto(IReadOnlyList<SupplierReorderDto> Suppliers, decimal Total);
