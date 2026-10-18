using System.Net;
using System.Text.RegularExpressions;

namespace NewErp.Domain.Partners;

/// <summary>Увімкнений товар магазину з усім, що потрібно для фіда.</summary>
/// <param name="CategoryIds">Усі категорії товару.</param>
/// <param name="Images">Шляхи фото відносно теки image/ магазину: головне, потім додаткові за порядком.</param>
/// <param name="Options">Значення опцій товару за зростанням option_value_id, як їх перебирав старий фід.</param>
public sealed record FeedProduct(
    int ProductId,
    string Model,
    string Sku,
    decimal Price,
    string Name,
    string Description,
    IReadOnlyList<int> CategoryIds,
    IReadOnlyList<string> Images,
    IReadOnlyList<FeedProductOption> Options);

/// <summary>Значення опції товару (здебільшого розмір). Price — повна ціна розміру, як її вважав старий фід.</summary>
public sealed record FeedProductOption(int OptionValueId, string Name, decimal Price);

public sealed record FeedCategory(string Id, string? ExternalId, string Name);

public sealed record FeedOffer(
    string Id,
    decimal Price,
    decimal? PriceOld,
    string CategoryId,
    IReadOnlyList<string> Pictures,
    string Model,
    string? VendorCode,
    string Name,
    string Description,
    string? Size,
    string Barcode);

public sealed record PartnerFeed(IReadOnlyList<FeedCategory> Categories, IReadOnlyList<FeedOffer> Offers);

/// <summary>
/// Складає фід партнера так само, як catalog/model/feed/eva.php старого сайту: пропозиції з категорій
/// (ціна розміру плюс націнка категорії) і окремі товари (фіксовані ціни), кожен розмір — окрема пропозиція.
/// Id пропозицій і штрихкоди збігаються зі старими, щоб партнер не втратив прив'язку своїх товарів.
/// </summary>
public static partial class PartnerFeedBuilder
{
    /// <param name="products">Увімкнені товари з відповідних категорій і з переліку окремих товарів.</param>
    /// <param name="categoryNames">Назви категорій магазину (для категорій без власної назви в партнера).</param>
    /// <param name="storeUrl">Адреса магазину з «/» в кінці, до неї додаються шляхи фото.</param>
    public static PartnerFeed Build(Partner partner, IReadOnlyCollection<FeedProduct> products, IReadOnlyDictionary<int, string> categoryNames, string storeUrl)
    {
        // Ключ «категорія|розмір»; порожній розмір — уся категорія. Як у старому коді, пізніший рядок перекриває попередній.
        var mapIndex = new Dictionary<string, PartnerCategoryMap>();
        foreach (var row in partner.CategoryMap.OrderBy(r => r.Id))
            mapIndex[Key(row.CategoryId, row.SizeName)] = row;
        var mappedCategoryIds = partner.CategoryMap.Select(r => r.CategoryId).ToHashSet();
        var productsById = products.ToDictionary(p => p.ProductId);

        var standalone = new Dictionary<string, FeedOffer>();
        var extraCategories = new SortedDictionary<int, string>();
        foreach (var row in partner.ProductMap.OrderBy(r => r.Id))
        {
            if (!productsById.TryGetValue(row.ProductId, out var product))
                continue;

            var categoryId = product.CategoryIds.Count > 0 ? product.CategoryIds.Min() : 0;
            var categoryRow = Resolve(mapIndex, categoryId, row.SizeName);
            string feedCategoryId;
            if (categoryRow is not null)
                feedCategoryId = categoryRow.Id.ToString();
            else
            {
                feedCategoryId = "p" + categoryId;
                extraCategories[categoryId] = categoryNames.GetValueOrDefault(categoryId, "");
            }

            if (product.Options.Count > 0)
            {
                for (var i = 0; i < product.Options.Count; i++)
                {
                    var option = product.Options[i];
                    if (row.SizeName is not null && option.Name.Trim() != row.SizeName)
                        continue;
                    var id = $"p{product.ProductId}_{option.OptionValueId}";
                    standalone[id] = Offer(id, product, option, row.PriceSupply ?? option.Price, row.PriceSale, feedCategoryId,
                        row.ExternalName != "" ? row.ExternalName : $"{product.Name} - {option.Name}".Trim(), row.ExternalId, i, storeUrl);
                }
            }
            else
            {
                var id = $"p{product.ProductId}";
                standalone[id] = Offer(id, product, null, row.PriceSupply ?? product.Price, row.PriceSale, feedCategoryId,
                    row.ExternalName != "" ? row.ExternalName : product.Name, row.ExternalId, 0, storeUrl);
            }
        }

        // Товар, доданий окремим рядком, не дублюється пропозицією з категорії (старий фід віддавав обидві).
        var offers = new List<FeedOffer>();
        foreach (var product in products.OrderBy(p => p.ProductId))
        {
            var categoryIds = product.CategoryIds.Where(mappedCategoryIds.Contains).ToList();
            if (categoryIds.Count == 0)
                continue;
            var categoryId = categoryIds.Min();

            if (product.Options.Count > 0)
            {
                for (var i = 0; i < product.Options.Count; i++)
                {
                    var option = product.Options[i];
                    var categoryRow = Resolve(mapIndex, categoryId, option.Name);
                    if (categoryRow is null || standalone.ContainsKey($"p{product.ProductId}_{option.OptionValueId}"))
                        continue;
                    offers.Add(CategoryOffer($"{product.ProductId}_{option.OptionValueId}", product, option, option.Price, categoryRow,
                        $"{product.Name} - {option.Name}".Trim(), i, storeUrl));
                }
            }
            else
            {
                var categoryRow = Resolve(mapIndex, categoryId, null);
                if (categoryRow is null || standalone.ContainsKey($"p{product.ProductId}"))
                    continue;
                offers.Add(CategoryOffer(product.ProductId.ToString(), product, null, product.Price, categoryRow, product.Name, 0, storeUrl));
            }
        }
        offers.AddRange(standalone.Values);

        var categories = partner.CategoryMap.OrderBy(r => r.Id)
            .Select(r => new FeedCategory(r.Id.ToString(), r.ExternalId == "" ? null : r.ExternalId,
                r.ExternalName != "" ? r.ExternalName : categoryNames.GetValueOrDefault(r.CategoryId, "")))
            .Concat(extraCategories.Select(c => new FeedCategory("p" + c.Key, null, c.Value)))
            .ToList();

        // Пропозиції без фото партнер не приймає, тож їх пропускаємо, як і старий фід.
        return new PartnerFeed(categories, offers.Where(o => o.Pictures.Count > 0).ToList());
    }

    /// <summary>
    /// Штрихкод EAN-13 з префіксом України 482: 4 цифри артикулу (sku, якщо він числовий), 4 цифри id товару,
    /// цифра варіанта (порядковий номер розміру) і контрольна цифра.
    /// </summary>
    public static string Barcode(string sku, int productId, int variant)
    {
        var skuPart = sku.Length > 0 && sku.All(char.IsAsciiDigit) ? Left(sku, 4).PadLeft(4, '0') : "0000";
        var idPart = Left(productId.ToString(), 4).PadLeft(4, '0');
        var payload = "482" + skuPart + idPart + (variant % 10);

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (payload[i] - '0') * (i % 2 == 0 ? 1 : 3);
        var check = (10 - sum % 10) % 10;
        return payload + check;
    }

    /// <summary>Опис для партнера: HTML з бази перетворено на звичайний текст з переносами рядків.</summary>
    public static string PlainDescription(string html)
    {
        // У базі OpenCart опис зберігається з екранованими сутностями (&lt;p&gt;), тому розкодовуємо двічі, як старий фід.
        var text = WebUtility.HtmlDecode(WebUtility.HtmlDecode(html));
        text = LineBreakTags().Replace(text, "\n");
        return Tags().Replace(text, "").Trim();
    }

    private static FeedOffer CategoryOffer(string id, FeedProduct product, FeedProductOption? option, decimal basePrice,
        PartnerCategoryMap categoryRow, string name, int variant, string storeUrl)
    {
        var price = basePrice * (1 + (categoryRow.PercentSupply ?? 0) / 100);
        var priceOld = categoryRow.PercentSale is { } sale ? basePrice * (1 + sale / 100) : (decimal?)null;
        return Offer(id, product, option, price, priceOld, categoryRow.Id.ToString(), name, null, variant, storeUrl);
    }

    private static FeedOffer Offer(string id, FeedProduct product, FeedProductOption? option, decimal price, decimal? priceOld,
        string categoryId, string name, string? vendorCode, int variant, string storeUrl) =>
        new(id,
            Round(price),
            priceOld is { } old ? Round(old) : null,
            categoryId,
            product.Images.Where(i => i != "").Select(i => PictureUrl(storeUrl, i)).ToList(),
            product.Model,
            string.IsNullOrEmpty(vendorCode) ? null : vendorCode,
            name,
            PlainDescription(product.Description),
            option is null || option.Name == "" ? null : option.Name,
            Barcode(product.Sku, product.ProductId, variant));

    private static PartnerCategoryMap? Resolve(Dictionary<string, PartnerCategoryMap> mapIndex, int categoryId, string? sizeName)
    {
        if (sizeName is not null && mapIndex.TryGetValue(Key(categoryId, sizeName), out var bySize))
            return bySize;
        return mapIndex.GetValueOrDefault(Key(categoryId, null));
    }

    private static string Key(int categoryId, string? sizeName) => $"{categoryId}|{sizeName?.Trim() ?? ""}";

    private static string PictureUrl(string storeUrl, string path) =>
        storeUrl + "image/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    // Як round() у PHP: половина округлюється від нуля.
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Left(string value, int length) => value.Length <= length ? value : value[..length];

    [GeneratedRegex(@"</p>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTags();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();
}
