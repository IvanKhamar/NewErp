using NewErp.Application.Common;

namespace NewErp.Application.Catalog;

// Уся форма товару старої адмінки (Python first, пункти 43, 45 і 76). Ті самі поля JSON, що в Python-версії.

/// <summary>Додаткове фото товару (dd_product_image).</summary>
public sealed record ProductImageDto(string Image, int SortOrder = 0);

/// <summary>Акційна ціна: price, або відсоток знижки від звичайної ціни, коли price = 0 (власна колонка старого сайту).</summary>
public sealed record ProductSpecialDto(
    int CustomerGroupId = 1, int Priority = 1, decimal Price = 0, decimal Percent = 0, DateOnly? DateStart = null, DateOnly? DateEnd = null);

/// <summary>Значення атрибута товару: «Склад» → «Льон 100%».</summary>
public sealed record ProductAttributeDto(int AttributeId, string Text = "");

/// <summary>Розмір товару (значення опції «Розмір»). Quantity лише показуємо: залишки веде виробництво.</summary>
public sealed record ProductSizeEditDto(int OptionValueId, string Name = "", decimal Price = 0, int Quantity = 0);

/// <summary>Значення іншої опції (колір, довжина). PricePrefix: вітрина додає ("+") чи віднімає ("-") ціну.</summary>
public sealed record ProductOptionValueEditDto(int OptionValueId, string Name = "", decimal Price = 0, string PricePrefix = "+", int Quantity = 0);

/// <summary>Опція, крім розміру. Value — текст опції без списку значень (text, date…).</summary>
public sealed record ProductOptionDto
{
    public int OptionId { get; init; }

    public string Name { get; init; } = "";

    public bool Required { get; init; } = true;

    public string Value { get; init; } = "";

    public IReadOnlyList<ProductOptionValueEditDto> Values { get; init; } = [];
}

/// <summary>Опція в довіднику форми: HasValues — select, radio, checkbox чи image (решта тримає текст).</summary>
public sealed record ProductOptionLookup(int Id, string Name, string Type, bool HasValues, IReadOnlyList<LookupItem> Values);

/// <summary>Товар так, як його редагує форма. Текстові поля мають довжини колонок OpenCart, HTML очищується.</summary>
public record ProductForm
{
    public string Model { get; init; } = "";

    public string Sku { get; init; } = "";

    public decimal Price { get; init; }

    public int Minimum { get; init; } = 1;

    public bool Subtract { get; init; } = true;

    public int StockStatusId { get; init; }

    public int ManufacturerId { get; init; }

    public decimal Weight { get; init; }

    public decimal Length { get; init; }

    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public int SortOrder { get; init; }

    public bool Enabled { get; init; }

    public string Image { get; init; } = "";

    public string Name { get; init; } = "";

    public string Description { get; init; } = "";

    public string Tag { get; init; } = "";

    public string MetaTitle { get; init; } = "";

    public string MetaDescription { get; init; } = "";

    public string MetaKeyword { get; init; } = "";

    public string TableSize { get; init; } = "";

    public string Variant { get; init; } = "";

    public string Notice { get; init; } = "";

    public string ModelInfo { get; init; } = "";

    public string Characteristics { get; init; } = "";

    public string Color { get; init; } = "";

    public string SeoKeyword { get; init; } = "";

    public IReadOnlyList<int> CategoryIds { get; init; } = [];

    public IReadOnlyList<int> LabelIds { get; init; } = [];

    public IReadOnlyList<int> FilterIds { get; init; } = [];

    public IReadOnlyList<int> RelatedIds { get; init; } = [];

    public IReadOnlyList<int> RelatedColorIds { get; init; } = [];

    public IReadOnlyList<int> RelatedVariantIds { get; init; } = [];

    public IReadOnlyList<ProductImageDto> Images { get; init; } = [];

    public IReadOnlyList<ProductSpecialDto> Specials { get; init; } = [];

    public IReadOnlyList<ProductSizeEditDto> Sizes { get; init; } = [];

    public IReadOnlyList<ProductOptionDto> Options { get; init; } = [];

    public IReadOnlyList<ProductAttributeDto> Attributes { get; init; } = [];

    public IReadOnlyList<int> DownloadIds { get; init; } = [];
}

/// <summary>Товар у формі редагування. DateModified — версія, з якою форму відкрили.</summary>
public sealed record ProductEditDto : ProductForm
{
    public int Id { get; init; }

    public DateTime DateModified { get; init; }
}

/// <summary>Збереження форми. DateModified — версія, з якою форму відкрили (лише PUT); новіший товар — 409.</summary>
public sealed record SaveProductRequest : ProductForm
{
    public DateTime? DateModified { get; init; }
}

/// <summary>Довідники форми. Filters — «група: значення», Sizes — значення опції «Розмір», Options — решта опцій.</summary>
public sealed record ProductFormLookups(
    IReadOnlyList<LookupItem> Categories, IReadOnlyList<LookupItem> Labels, IReadOnlyList<LookupItem> Filters,
    IReadOnlyList<LookupItem> Sizes, IReadOnlyList<ProductOptionLookup> Options, IReadOnlyList<LookupItem> Attributes,
    IReadOnlyList<LookupItem> Downloads);

public sealed record CreatedProductDto(int ProductId);

/// <summary>«Зняти NEW» з усіх товарів принта: SKU — код принта.</summary>
public sealed record RemoveNewRequest(string? Sku);

public sealed record RemoveNewResult(IReadOnlyList<int> ProductIds, bool LabelRemoved, bool FilterRemoved);

/// <summary>Товар і пов'язані таблиці OpenCart, як їх писали addProduct / editProduct старої адмінки.</summary>
public interface IProductEditStore
{
    Task<ProductEditDto?> GetAsync(int productId, CancellationToken ct);

    Task<ProductFormLookups> LookupsAsync(CancellationToken ct);

    Task<IReadOnlySet<int>> ExistingProductsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);

    /// <summary>Запит («product_id=5», «category_id=2»…), який уже має цю адресу, або null.</summary>
    Task<string?> SeoKeywordOwnerAsync(string keyword, CancellationToken ct);

    Task<int> CreateAsync(ProductForm form, DateTime now, CancellationToken ct);

    /// <summary>Пише все, лише якщо date_modified досі дорівнює expected; інакше — ConflictException.</summary>
    Task SaveAsync(int productId, ProductForm form, DateTime expected, DateTime now, CancellationToken ct);

    Task<RemoveNewResult> RemoveNewAsync(string sku, CancellationToken ct);
}
