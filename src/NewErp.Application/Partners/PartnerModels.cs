namespace NewErp.Application.Partners;

/// <summary>Розділ "PartnerFeed" в appsettings.json: шапка фіда й адреса магазину для посилань на фото.</summary>
public sealed class PartnerFeedOptions
{
    public const string SectionName = "PartnerFeed";

    public string ShopName { get; set; } = "Vidro";

    public string Company { get; set; } = "";

    /// <summary>Адреса вітрини магазину (https://…/). Фото у фіді — це {StoreUrl}image/{шлях}.</summary>
    public string StoreUrl { get; set; } = "";
}

public sealed record PartnerListItemDto(
    int Id, string Code, string Name, string VendorName, bool FeedEnabled, bool IsActive, int Categories, int Products);

public sealed record PartnerDto(
    int Id,
    string Code,
    string Name,
    bool IsActive,
    string FeedUrl,
    bool FeedEnabled,
    string VendorName,
    string Notes,
    int DefaultQuantity,
    DateTime DateModified,
    IReadOnlyList<PartnerCategoryMapDto> CategoryMap,
    IReadOnlyList<PartnerProductMapDto> ProductMap);

/// <param name="Id">Id рядка — він же id категорії у фіді.</param>
public sealed record PartnerCategoryMapDto(
    int Id, int CategoryId, string CategoryName, string? SizeName, string ExternalId, string ExternalName,
    decimal? PercentSupply, decimal? PercentSale);

public sealed record PartnerProductMapDto(
    int Id, int ProductId, string ProductName, bool ProductEnabled, string? SizeName, string ExternalId, string ExternalName,
    decimal? PriceSupply, decimal? PriceSale);

public sealed record SavePartnerRequest(
    string Code,
    string Name,
    bool IsActive = true,
    string FeedUrl = "",
    bool FeedEnabled = false,
    string VendorName = "Vidro",
    string Notes = "",
    int DefaultQuantity = 5,
    IReadOnlyList<CategoryMapRow>? CategoryMap = null,
    IReadOnlyList<ProductMapRow>? ProductMap = null);

/// <param name="SizeName">Порожній — уся категорія.</param>
/// <param name="PercentSupply">Націнка до ціни розміру, %; null — без націнки.</param>
/// <param name="PercentSale">Стара (перекреслена) ціна як ціна розміру плюс %; null — без неї.</param>
public sealed record CategoryMapRow(
    int CategoryId, string? SizeName = null, string ExternalId = "", string ExternalName = "",
    decimal? PercentSupply = null, decimal? PercentSale = null);

/// <param name="PriceSupply">Ціна для партнера; null — ціна з магазину.</param>
public sealed record ProductMapRow(
    int ProductId, string? SizeName = null, string ExternalId = "", string ExternalName = "",
    decimal? PriceSupply = null, decimal? PriceSale = null);

/// <summary>Категорія магазину та розміри її товарів (для вибору у відповідності).</summary>
public sealed record ShopCategoryDto(int Id, string Name, IReadOnlyList<string> Sizes);

/// <summary>Товар магазину для вибору окремим рядком, з цінами розмірів (або з ціною товару, якщо розмірів немає).</summary>
public sealed record PartnerProductPickDto(int Id, string Name, bool IsEnabled, IReadOnlyList<ProductSizePriceDto> Sizes);

public sealed record ProductSizePriceDto(string Name, decimal Price);
