using System.Text.RegularExpressions;

using NewErp.Domain.Common;

namespace NewErp.Domain.Partners;

/// <summary>Налаштування партнера, які задає менеджер (усе, крім відповідностей категорій і товарів).</summary>
public sealed record PartnerSettings(
    string Code,
    string Name,
    bool IsActive,
    string FeedUrl,
    bool FeedEnabled,
    string VendorName,
    string Notes,
    int DefaultQuantity);

/// <summary>Рядок відповідності категорії: категорія магазину (за потреби лише один розмір) → категорія партнера.</summary>
public sealed record CategoryMapping(
    int CategoryId,
    string? SizeName,
    string ExternalId,
    string ExternalName,
    decimal? PercentSupply,
    decimal? PercentSale);

/// <summary>Рядок окремого товару у фіді: товар (за потреби лише один розмір) з артикулом, назвою й цінами для партнера.</summary>
public sealed record ProductMapping(
    int ProductId,
    string? SizeName,
    string ExternalId,
    string ExternalName,
    decimal? PriceSupply,
    decimal? PriceSale);

/// <summary>
/// Партнер-перепродавець (dd_partner): отримує XML-фід наших товарів з його категоріями й цінами.
/// Товари потрапляють у фід через відповідність категорій (з націнкою у відсотках) або окремими рядками (з фіксованими цінами).
/// </summary>
public partial class Partner
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 128;

    private readonly List<PartnerCategoryMap> _categoryMap = [];
    private readonly List<PartnerProductMap> _productMap = [];

    private Partner() { }

    public Partner(PartnerSettings settings, DateTime now)
    {
        Update(settings, now);
        DateAdded = now;
    }

    public int Id { get; private set; }

    /// <summary>Короткий латинський код, з ним формується адреса фіда (/feeds/{code}.xml).</summary>
    public string Code { get; private set; } = "";

    public string Name { get; private set; } = "";

    public bool IsActive { get; private set; } = true;

    /// <summary>Довідкова адреса фіда для партнера, як на старому сайті. Сам фід віддає NewErp.</summary>
    public string FeedUrl { get; private set; } = "";

    public bool FeedEnabled { get; private set; }

    /// <summary>Бренд (vendor) у фіді.</summary>
    public string VendorName { get; private set; } = "";

    public string Notes { get; private set; } = "";

    /// <summary>Кількість, яку фід показує для кожної пропозиції.</summary>
    public int DefaultQuantity { get; private set; }

    public DateTime DateAdded { get; private set; }

    public DateTime DateModified { get; private set; }

    public IReadOnlyList<PartnerCategoryMap> CategoryMap => _categoryMap;

    public IReadOnlyList<PartnerProductMap> ProductMap => _productMap;

    public void Update(PartnerSettings settings, DateTime now)
    {
        var code = settings.Code?.Trim() ?? "";
        if (code.Length is 0 or > CodeMaxLength || !CodePattern().IsMatch(code))
            throw new DomainException($"Код партнера — від 1 до {CodeMaxLength} латинських літер, цифр, «-» чи «_».");
        var name = settings.Name?.Trim() ?? "";
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException($"Назва партнера — від 1 до {NameMaxLength} символів.");
        if (settings.DefaultQuantity < 0)
            throw new DomainException("Кількість у фіді не може бути від'ємною.");

        Code = code;
        Name = name;
        IsActive = settings.IsActive;
        FeedUrl = Limit(settings.FeedUrl, 255, "Адреса фіда");
        FeedEnabled = settings.FeedEnabled;
        VendorName = Limit(settings.VendorName, 64, "Бренд");
        Notes = settings.Notes?.Trim() ?? "";
        DefaultQuantity = settings.DefaultQuantity;
        DateModified = now;
    }

    /// <summary>
    /// Замінює відповідність категорій. Рядки з тією самою категорією й розміром оновлюються на місці, тож їхні id
    /// (це id категорій у фіді) не змінюються. Старий сайт видаляв і створював усі рядки заново.
    /// </summary>
    public void SetCategoryMap(IEnumerable<CategoryMapping> rows)
    {
        var mappings = rows.Select(r => r with
        {
            SizeName = NormalizeSize(r.SizeName),
            ExternalId = Limit(r.ExternalId, 64, "Id категорії партнера"),
            ExternalName = Limit(r.ExternalName, 255, "Назва категорії партнера"),
        }).ToList();
        foreach (var m in mappings)
        {
            if (m.CategoryId <= 0)
                throw new DomainException("Оберіть категорію в кожному рядку відповідності.");
            CheckPercent(m.PercentSupply);
            CheckPercent(m.PercentSale);
        }
        CheckUnique(mappings.Select(m => (m.CategoryId, m.SizeName)), "Категорія з таким розміром");

        Merge(_categoryMap, mappings, (row, m) => row.CategoryId == m.CategoryId && row.SizeName == m.SizeName,
            m => new PartnerCategoryMap(m.CategoryId, m.SizeName), (row, m) => row.Apply(m));
    }

    /// <summary>Замінює перелік окремих товарів. Як і з категоріями, наявні рядки оновлюються на місці.</summary>
    public void SetProductMap(IEnumerable<ProductMapping> rows)
    {
        var mappings = rows.Select(r => r with
        {
            SizeName = NormalizeSize(r.SizeName),
            ExternalId = Limit(r.ExternalId, 64, "Артикул партнера"),
            ExternalName = Limit(r.ExternalName, 255, "Назва в партнера"),
        }).ToList();
        foreach (var m in mappings)
        {
            if (m.ProductId <= 0)
                throw new DomainException("Оберіть товар у кожному рядку.");
            if (m.PriceSupply is <= 0 || m.PriceSale is <= 0)
                throw new DomainException("Ціна для партнера має бути більшою за нуль.");
        }
        CheckUnique(mappings.Select(m => (m.ProductId, m.SizeName)), "Товар з таким розміром");

        Merge(_productMap, mappings, (row, m) => row.ProductId == m.ProductId && row.SizeName == m.SizeName,
            m => new PartnerProductMap(m.ProductId, m.SizeName), (row, m) => row.Apply(m));
    }

    private static void Merge<TRow, TMapping>(
        List<TRow> rows, List<TMapping> mappings, Func<TRow, TMapping, bool> same, Func<TMapping, TRow> create, Action<TRow, TMapping> apply)
    {
        rows.RemoveAll(row => !mappings.Any(m => same(row, m)));
        foreach (var mapping in mappings)
        {
            var row = rows.FirstOrDefault(r => same(r, mapping));
            if (row is null)
            {
                row = create(mapping);
                rows.Add(row);
            }
            apply(row, mapping);
        }
    }

    private static void CheckUnique<TKey>(IEnumerable<(int Id, TKey Size)> keys, string what)
    {
        var duplicate = keys.GroupBy(k => k).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new DomainException($"{what} ({duplicate.Key.Id}{(duplicate.Key.Size is null ? "" : ", " + duplicate.Key.Size)}) вказано двічі.");
    }

    private static void CheckPercent(decimal? percent)
    {
        if (percent is <= -100)
            throw new DomainException("Відсоток не може зменшувати ціну до нуля чи нижче (має бути більшим за −100).");
    }

    /// <summary>Порожній розмір означає «усі розміри» і зберігається як NULL, як на старому сайті.</summary>
    private static string? NormalizeSize(string? size) => string.IsNullOrWhiteSpace(size) ? null : size.Trim();

    private static string Limit(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length <= maxLength ? trimmed : throw new DomainException($"{field} — не довше {maxLength} символів.");
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex CodePattern();
}

/// <summary>Рядок dd_partner_category_map. Його id — це id категорії у фіді.</summary>
public class PartnerCategoryMap
{
    private PartnerCategoryMap() { }

    internal PartnerCategoryMap(int categoryId, string? sizeName)
    {
        CategoryId = categoryId;
        SizeName = sizeName;
    }

    public int Id { get; private set; }

    public int PartnerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>null — усі розміри категорії.</summary>
    public string? SizeName { get; private set; }

    public string ExternalId { get; private set; } = "";

    public string ExternalName { get; private set; } = "";

    /// <summary>Націнка до ціни розміру, %. null — без націнки.</summary>
    public decimal? PercentSupply { get; private set; }

    /// <summary>Перекреслена «стара» ціна як ціна розміру плюс цей відсоток. null — без старої ціни.</summary>
    public decimal? PercentSale { get; private set; }

    internal void Apply(CategoryMapping mapping)
    {
        ExternalId = mapping.ExternalId;
        ExternalName = mapping.ExternalName;
        PercentSupply = mapping.PercentSupply;
        PercentSale = mapping.PercentSale;
    }
}

/// <summary>Рядок dd_partner_product_map: окремий товар у фіді партнера.</summary>
public class PartnerProductMap
{
    private PartnerProductMap() { }

    internal PartnerProductMap(int productId, string? sizeName)
    {
        ProductId = productId;
        SizeName = sizeName;
    }

    public int Id { get; private set; }

    public int PartnerId { get; private set; }

    public int ProductId { get; private set; }

    /// <summary>null — усі розміри товару.</summary>
    public string? SizeName { get; private set; }

    /// <summary>Артикул товару в партнера (vendorCode у фіді).</summary>
    public string ExternalId { get; private set; } = "";

    public string ExternalName { get; private set; } = "";

    /// <summary>Ціна для партнера. null — ціна розміру (чи товару) з магазину.</summary>
    public decimal? PriceSupply { get; private set; }

    /// <summary>Перекреслена «стара» ціна. null — без неї.</summary>
    public decimal? PriceSale { get; private set; }

    internal void Apply(ProductMapping mapping)
    {
        ExternalId = mapping.ExternalId;
        ExternalName = mapping.ExternalName;
        PriceSupply = mapping.PriceSupply;
        PriceSale = mapping.PriceSale;
    }
}
