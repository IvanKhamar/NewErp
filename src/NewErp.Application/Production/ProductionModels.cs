namespace NewErp.Application.Production;

// ── Запити ──

public sealed record SizeRequest(string Name, decimal Percent = 0);

/// <summary>Type: "clothing" (власний пошив) або "ready_made" (готовий товар від постачальника).</summary>
public sealed record SaveProductionModelRequest(
    string Name, IReadOnlyList<SizeRequest> Sizes, string Type = "clothing", int MinTotal = 50, int SortOrder = 0);

public sealed record CategoryLinkRequest(int CategoryId, string? NameFilter = null, bool MatchExact = false);

public sealed record SizeOptionRequest(int SizeId, int? OptionValueId);

/// <summary>Повністю замінює прив'язку моделі до товарів. SizeOptions змінює лише перелічені розміри.</summary>
public sealed record SetProductionLinksRequest(
    IReadOnlyList<int>? ProductIds, IReadOnlyList<CategoryLinkRequest>? Categories, IReadOnlyList<SizeOptionRequest>? SizeOptions);

public sealed record SaveComponentRequest(string Name, int SortOrder = 0);

/// <summary>SizeId = null — для всіх розмірів; рядок із SizeId перекриває його для цього розміру.</summary>
public sealed record ComponentBomRowRequest(int? SizeId, int ItemId, decimal QtyPerUnit);

public sealed record SetComponentBomRequest(IReadOnlyList<ComponentBomRowRequest> Rows);

/// <summary>Або ComponentId (деталь крою), або ItemId (розхідник напряму).</summary>
public sealed record SewnBomRowRequest(int? SizeId, int? ComponentId, int? ItemId, decimal QtyPerUnit);

public sealed record SetSewnBomRequest(IReadOnlyList<SewnBomRowRequest> Rows);

public sealed record ProductionModelFilter(string? Type = null, bool BelowMinimumOnly = false);

// ── Відповіді ──

/// <summary>Deficit: скільки бракує пошитих до MinTotal (0, якщо вистачає).</summary>
public sealed record ProductionModelSummaryDto(
    int Id, string Name, string Type, int MinTotal, decimal TotalCut, decimal TotalSewn, decimal Deficit);

public sealed record ProductionSizeDto(int Id, string Name, decimal Percent, decimal CutStock, decimal SewnStock, int? OptionValueId);

public sealed record CategoryLinkDto(int CategoryId, string NameFilter, bool MatchExact);

public sealed record ComponentBomLineDto(int? SizeId, int ItemId, string? ItemName, string? Unit, decimal QtyPerUnit);

/// <summary>Stock: запас розкроєних деталей за id розміру.</summary>
public sealed record ComponentDto(int Id, string Name, int SortOrder, IReadOnlyDictionary<int, decimal> Stock, IReadOnlyList<ComponentBomLineDto> Bom);

public sealed record SewnBomLineDto(int? SizeId, int? ComponentId, string? ComponentName, int? ItemId, string? ItemName, string? Unit, decimal QtyPerUnit);

public sealed record ProductionModelDto(
    int Id,
    string Name,
    string Type,
    int MinTotal,
    int SortOrder,
    IReadOnlyList<ProductionSizeDto> Sizes,
    IReadOnlyList<int> ProductIds,
    IReadOnlyList<CategoryLinkDto> Categories,
    IReadOnlyList<ComponentDto> Components,
    IReadOnlyList<SewnBomLineDto> SewnBom);

// ── Операції крою й пошиву ──

/// <summary>Операції з залишками моделі (у старому модулі — кнопки «+Покроєно», «В пошив», «+Пошито», «Відвантаж.»).</summary>
public enum ProductionOperation
{
    /// <summary>+Покроєно: крій зростає, розхідники списуються за специфікацією крою, запас деталей зростає.</summary>
    Cut,

    /// <summary>В пошив: розкроєні комплекти передано швачкам, крій зменшується.</summary>
    SendToSewing,

    /// <summary>+Пошито: пошиті вироби зростають, деталі й розхідники списуються за специфікацією пошиву.</summary>
    Sewn,

    /// <summary>Відвантажено не через замовлення: пошиті вироби зменшуються.</summary>
    Ship,
}

public sealed record SizeQuantity(int SizeId, decimal Quantity);

/// <summary>WarehouseId — лише для «пошито» й «відвантажено»: куди прийшли чи звідки пішли вироби; null — основний склад.</summary>
public sealed record ProductionOperationRequest(IReadOnlyList<SizeQuantity> Quantities, string? Comment = null, int? WarehouseId = null);

public sealed record SizeChangeDto(int SizeId, string SizeName, decimal Before, decimal After);

/// <summary>Warnings: чого не вистачило при списанні (списано те, що було).</summary>
public sealed record ProductionOperationResult(IReadOnlyList<SizeChangeDto> Sizes, IReadOnlyList<string> Warnings);

/// <summary>Пораховане (не різниця) одного розміру; null — цей залишок не рахували.</summary>
public sealed record SizeCountRequest(int SizeId, decimal? Cut = null, decimal? Sewn = null);

public sealed record ComponentCountRequest(int ComponentId, int SizeId, decimal Actual);

/// <summary>
/// Пораховані залишки розмірів і деталей крою моделі. WarehouseId — склад, де рахували лише пошите;
/// null — основний склад, де рахують усе.
/// </summary>
public sealed record ProductionStocktakingRequest(
    IReadOnlyList<SizeCountRequest>? Sizes, IReadOnlyList<ComponentCountRequest>? Components = null, string? Comment = null, int? WarehouseId = null);

/// <summary>Cut і Sewn — розміри, чий залишок змінився; Components — скільки запасів деталей змінилося.</summary>
public sealed record ProductionStocktakingResult(IReadOnlyList<SizeChangeDto> Cut, IReadOnlyList<SizeChangeDto> Sewn, int Components);

public sealed record ProductionLogDto(
    int Id, int ProductionId, string? ProductionName, int SizeId, string SizeName, string Stage, string Direction,
    decimal Quantity, decimal StockBefore, decimal StockAfter, string Comment, int UserId, string Username, DateTime CreatedAt);

/// <summary>Stage: "cut" або "sewn".</summary>
public sealed record ProductionLogFilter(int? ProductionId = null, string? Stage = null, int Limit = 100);

// ── Замовлення ──

/// <summary>Налаштування виробництва з розділу "Production" в appsettings.json.</summary>
public sealed class ProductionOptions
{
    public const string SectionName = "Production";

    /// <summary>
    /// Статуси, у яких замовлення забирає пошиті вироби зі складу. Вхід у цей набір списує, вихід із нього повертає,
    /// перехід усередині набору нічого не змінює (як у старому модулі StockTrack).
    /// </summary>
    public int[] OrderSubtractStatusIds { get; set; } = [];

    /// <summary>Статуси активних замовлень, для яких рахуємо план пошиву (ще не готові до відправки).</summary>
    public int[] PlanStatusIds { get; set; } = [];
}

/// <summary>
/// Рядок замовлення з тим, що потрібно для складу: модель виробництва, до якої прив'язано товар
/// (напряму чи через категорію), і значення опцій OpenCart, серед яких шукаємо розмір.
/// </summary>
public sealed record OrderSizeLine(int ProductId, string ProductName, int Quantity, int? ProductionId, IReadOnlyList<int> OptionValueIds);

// ── План пошиву ──

/// <summary>Активне замовлення для плану пошиву: рядки з моделлю й розміром і те, що воно вже тримає (за id розміру).</summary>
public sealed record PlanOrder(int OrderId, DateTime DateAdded, IReadOnlyList<OrderSizeLine> Lines, IReadOnlyDictionary<int, decimal> Held);

/// <summary>Missing — скільки бракує активним замовленням; ToSew — скільки пошити після вільного запасу; ToCut — скільки ще розкроїти.</summary>
public sealed record SewingPlanRowDto(
    int ProductionId, string ModelName, int SizeId, string SizeName, decimal Ordered, decimal Missing, decimal SewnStock,
    decimal CutStock, decimal ToSew, decimal FromCut, decimal ToCut, DateOnly? Deadline, bool Overdue, IReadOnlyList<int> OrderIds);

/// <summary>Товар активних замовлень, який не вдалося врахувати в плані (без моделі або розмір без зв'язку), і в яких замовленнях він є.</summary>
public sealed record SewingPlanIssueDto(string ProductName, string Reason, IReadOnlyList<int> OrderIds);

public sealed record SewingPlanDto(int OrderCount, IReadOnlyList<SewingPlanRowDto> Rows, IReadOnlyList<SewingPlanIssueDto> Issues);

/// <summary>
/// Розхідник для плану пошиву. Needed — скільки знадобиться на крій і пошив плану; InStock — залишок; OnOrder — замовлено в
/// постачальників і ще не отримано; Shortage — скільки бракує й після замовленого (0 — вистачає).
/// </summary>
public sealed record PlanMaterialDto(
    int ItemId, string Name, string Unit, decimal Needed, decimal InStock, decimal OnOrder, decimal Shortage, IReadOnlyList<PlanMaterialUseDto> Uses);

/// <summary>На що саме йде розхідник: модель, розмір, етап («Крій» чи «Пошив») і кількість.</summary>
public sealed record PlanMaterialUseDto(int ProductionId, string ModelName, string SizeName, string Stage, decimal Quantity);

/// <summary>Розхідники плану пошиву: спершу ті, яких бракує. Warnings — розхідники зі специфікацій, яких немає на складі.</summary>
public sealed record PlanMaterialsDto(IReadOnlyList<PlanMaterialDto> Materials, IReadOnlyList<string> Warnings);
