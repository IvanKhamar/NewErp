using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.Application.Catalog;

/// <summary>
/// Уся форма товару старої адмінки (Python first, пункти 43, 45 і 76): створити, змінити, копіювати і «зняти NEW» з принта.
/// Залишки лишаються за виробництвом: форма змінює список розмірів і їхні ціни, але не кількість. Копія вимкнена, без
/// залишку й без SEO-адреси, яка має бути унікальною.
/// </summary>
public sealed class ProductEditService(IProductEditStore store, IHtmlCleaner html, TimeProvider clock, IAuditLog audit)
{
    private const int MaxLinks = 200;
    private const decimal MaxPrice = 10_000_000m;

    /// <summary>Опції зі списком значень; решта (text, date, file…) тримає текст.</summary>
    public static readonly IReadOnlySet<string> OptionTypesWithValues = new HashSet<string> { "select", "radio", "checkbox", "image" };

    public async Task<ProductEditDto> GetAsync(int productId, CancellationToken ct) =>
        await store.GetAsync(productId, ct) ?? throw new NotFoundException($"Товар {productId} не знайдено.");

    public Task<ProductFormLookups> LookupsAsync(CancellationToken ct) => store.LookupsAsync(ct);

    public async Task<CreatedProductDto> CreateAsync(SaveProductRequest request, int userId, CancellationToken ct)
    {
        var form = await CheckedAsync(request, null, null, ct);
        var productId = await store.CreateAsync(form, Now(), ct);
        audit.Record($"product created: product {productId}, by user {userId}");
        return new CreatedProductDto(productId);
    }

    public async Task<ProductEditDto> SaveAsync(int productId, SaveProductRequest request, int userId, CancellationToken ct)
    {
        var current = await GetAsync(productId, ct);
        if (request.DateModified is not { } expected)
            throw new DomainException("Не вказано dateModified: з якою версією товару ви працювали.");
        var form = await CheckedAsync(request, productId, current, ct);
        await store.SaveAsync(productId, form, expected, Now(), ct);
        if (current.Price != form.Price)
            audit.Record($"product price changed: product {productId}, {current.Price} -> {form.Price}, by user {userId}");
        return await GetAsync(productId, ct);
    }

    /// <summary>Як копія в OpenCart, але вимкнена, без залишку й без SEO-адреси.</summary>
    public async Task<CreatedProductDto> CopyAsync(int productId, int userId, CancellationToken ct)
    {
        ProductForm source = await GetAsync(productId, ct);
        var copy = source with
        {
            Enabled = false,
            SeoKeyword = "",
            Sizes = source.Sizes.Select(s => s with { Quantity = 0 }).ToList(),
            Options = source.Options.Select(o => o with { Values = o.Values.Select(v => v with { Quantity = 0 }).ToList() }).ToList(),
        };
        var newId = await store.CreateAsync(copy, Now(), ct);
        audit.Record($"product copied: product {newId} from {productId}, by user {userId}");
        return new CreatedProductDto(newId);
    }

    public async Task<RemoveNewResult> RemoveNewAsync(RemoveNewRequest request, CancellationToken ct)
    {
        var sku = (request.Sku ?? "").Trim();
        if (sku.Length == 0)
            throw new DomainException("Вкажіть код принта (SKU).");
        if (sku.Length > 64)
            throw new DomainException("SKU не довший за 64 символи.");
        var result = await store.RemoveNewAsync(sku, ct);
        return result.ProductIds.Count > 0 ? result : throw new NotFoundException($"Товарів з SKU «{sku}» не знайдено.");
    }

    private async Task<ProductForm> CheckedAsync(SaveProductRequest request, int? productId, ProductEditDto? current, CancellationToken ct)
    {
        // Спершу формати, щоб погане значення давало 400 ще до запитів у базу.
        CheckLimits(request);
        var image = CatalogText.ImagePath(request.Image);
        var images = Safe(request.Images).Where(i => !string.IsNullOrWhiteSpace(i.Image))
            .Select(i => i with { Image = CatalogText.ImagePath(i.Image) }).ToList();
        var keyword = CatalogText.SeoKeyword(request.SeoKeyword);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Вкажіть назву товару.");
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new DomainException("Вкажіть модель товару.");

        var lookups = await store.LookupsAsync(ct);
        // Категорія, мітка чи фільтр, які потім вимкнули, можуть лишитися в товарі, але обрати їх наново не можна.
        Known("категорії", Safe(request.CategoryIds), lookups.Categories, current?.CategoryIds);
        Known("мітки", Safe(request.LabelIds), lookups.Labels, current?.LabelIds);
        Known("фільтри", Safe(request.FilterIds), lookups.Filters, current?.FilterIds);
        var sizeIds = Safe(request.Sizes).Select(s => s.OptionValueId).ToList();
        Known("розміри", sizeIds, lookups.Sizes, null);
        Known("атрибути", Safe(request.Attributes).Select(a => a.AttributeId).ToList(), lookups.Attributes, null);
        Known("завантаження", Safe(request.DownloadIds), lookups.Downloads, current?.DownloadIds);
        if (sizeIds.Distinct().Count() != sizeIds.Count)
            throw new DomainException("Розмір у списку двічі.");
        if (current is not null)
        {
            var kept = sizeIds.ToHashSet();
            foreach (var size in current.Sizes.Where(s => !kept.Contains(s.OptionValueId) && s.Quantity > 0))
                throw new DomainException($"Розмір {size.Name} не можна прибрати: на вітрині {size.Quantity} шт.");
        }
        var options = CheckedOptions(Safe(request.Options), lookups.Options, current);

        var related = Safe(request.RelatedIds).Concat(Safe(request.RelatedColorIds)).Concat(Safe(request.RelatedVariantIds))
            .Where(id => id != productId).ToHashSet();
        var existing = await store.ExistingProductsAsync(related, ct);
        var missing = related.Where(id => !existing.Contains(id)).Order().ToList();
        if (missing.Count > 0)
            throw new DomainException($"Пов'язаних товарів немає: {string.Join(", ", missing)}.");
        if (Safe(request.Specials).Any(s => s.DateStart is { } start && s.DateEnd is { } end && end < start))
            throw new DomainException("Акційна ціна: кінець раніше за початок.");
        if (keyword.Length > 0 && await store.SeoKeywordOwnerAsync(keyword, ct) is { } owner && owner != $"product_id={productId}")
            throw new ConflictException($"SEO-адресу «{keyword}» вже має {owner}.");

        return request with
        {
            Name = request.Name.Trim(),
            Model = request.Model.Trim(),
            Sku = request.Sku ?? "",
            Tag = request.Tag ?? "",
            MetaTitle = request.MetaTitle ?? "",
            MetaDescription = request.MetaDescription ?? "",
            MetaKeyword = request.MetaKeyword ?? "",
            Variant = request.Variant ?? "",
            Color = request.Color ?? "",
            Description = html.Clean(request.Description),
            TableSize = html.Clean(request.TableSize),
            Notice = html.Clean(request.Notice),
            ModelInfo = html.Clean(request.ModelInfo),
            Characteristics = html.Clean(request.Characteristics),
            Image = image,
            Images = images,
            SeoKeyword = keyword,
            CategoryIds = Safe(request.CategoryIds).Distinct().ToList(),
            LabelIds = Safe(request.LabelIds).Distinct().ToList(),
            FilterIds = Safe(request.FilterIds).Distinct().ToList(),
            Specials = Safe(request.Specials),
            Sizes = Safe(request.Sizes),
            Options = options,
            RelatedIds = Unique(request.RelatedIds, productId),
            RelatedColorIds = Unique(request.RelatedColorIds, productId),
            RelatedVariantIds = Unique(request.RelatedVariantIds, productId),
            // Порожній текст атрибута — атрибута немає; той самий атрибут двічі — лишається останній.
            Attributes = Safe(request.Attributes).Where(a => !string.IsNullOrWhiteSpace(a.Text))
                .GroupBy(a => a.AttributeId).Select(g => g.Last()).ToList(),
            DownloadIds = Safe(request.DownloadIds).Distinct().ToList(),
        };
    }

    /// <summary>Межі, які Python перевіряє моделлю запиту: довжини колонок OpenCart, діапазони чисел, розміри списків.</summary>
    private static void CheckLimits(SaveProductRequest r)
    {
        Length("Модель", r.Model, 64);
        Length("SKU", r.Sku, 64);
        Length("Картинка", r.Image, 255);
        Length("Назва", r.Name, 255);
        Length("Опис", r.Description, 65_000);
        Length("Теги", r.Tag, 10_000);
        Length("Meta title", r.MetaTitle, 255);
        Length("Meta description", r.MetaDescription, 255);
        Length("Meta keywords", r.MetaKeyword, 255);
        Length("Таблиця розмірів", r.TableSize, 65_000);
        Length("Варіант", r.Variant, 10_000);
        Length("Примітка", r.Notice, 65_000);
        Length("Опис фото", r.ModelInfo, 255); // varchar(255): звірено зі скриптом сайту (пункт 72)
        Length("Характеристики", r.Characteristics, 65_000);
        Length("Колір", r.Color, 255);
        Length("SEO-адреса", r.SeoKeyword, 255);
        Range("Ціна", r.Price, 0, MaxPrice);
        Range("Мінімальна кількість", r.Minimum, 1, 10_000);
        Range("Вага", r.Weight, 0, 100_000);
        Range("Довжина", r.Length, 0, 10_000);
        Range("Ширина", r.Width, 0, 10_000);
        Range("Висота", r.Height, 0, 10_000);
        Count("категорій", r.CategoryIds, MaxLinks);
        Count("міток", r.LabelIds, MaxLinks);
        Count("фільтрів", r.FilterIds, MaxLinks);
        Count("пов'язаних товарів", r.RelatedIds, MaxLinks);
        Count("інших кольорів", r.RelatedColorIds, MaxLinks);
        Count("варіантів", r.RelatedVariantIds, MaxLinks);
        Count("фото", r.Images, 50);
        Count("акційних цін", r.Specials, 50);
        Count("розмірів", r.Sizes, 100);
        Count("опцій", r.Options, 50);
        Count("атрибутів", r.Attributes, 100);
        Count("завантажень", r.DownloadIds, MaxLinks);
        foreach (var image in Safe(r.Images))
            Length("Шлях фото", image.Image, 255);
        foreach (var special in Safe(r.Specials))
        {
            Range("Акційна ціна", special.Price, 0, MaxPrice);
            Range("Відсоток акції", special.Percent, 0, 100);
        }
        foreach (var size in Safe(r.Sizes))
            Range("Ціна розміру", size.Price, 0, MaxPrice);
        foreach (var attribute in Safe(r.Attributes))
            Length("Текст атрибута", attribute.Text, 10_000);
        foreach (var option in Safe(r.Options))
        {
            Length("Текст опції", option.Value, 1000);
            Count("значень опції", option.Values, 200);
            foreach (var value in option.Values ?? [])
            {
                Range("Ціна значення опції", value.Price, 0, MaxPrice);
                if (value.PricePrefix is not ("+" or "-"))
                    throw new DomainException("Знак ціни значення опції: + або -.");
            }
        }
    }

    private static IReadOnlyList<ProductOptionDto> CheckedOptions(
        IReadOnlyList<ProductOptionDto> options, IReadOnlyList<ProductOptionLookup> lookups, ProductEditDto? current)
    {
        var known = lookups.ToDictionary(l => l.Id);
        var checkedOptions = new Dictionary<int, ProductOptionDto>();
        foreach (var option in options)
        {
            if (!known.TryGetValue(option.OptionId, out var lookup))
                throw new DomainException($"Опції №{option.OptionId} немає (розміри задаються окремо).");
            if (checkedOptions.ContainsKey(option.OptionId))
                throw new DomainException($"Опція «{lookup.Name}» у списку двічі.");
            var values = option.Values ?? [];
            var ids = values.Select(v => v.OptionValueId).ToList();
            if (ids.Distinct().Count() != ids.Count)
                throw new DomainException($"Опція «{lookup.Name}»: значення у списку двічі.");
            var allowed = lookup.Values.Select(v => v.Id).ToHashSet();
            if (!ids.All(allowed.Contains))
                throw new DomainException($"Опція «{lookup.Name}» не має такого значення.");
            // Обов'язковий список без жодного значення зупиняє покупку на вітрині.
            if (lookup.HasValues && ids.Count == 0)
                throw new DomainException($"Опція «{lookup.Name}»: оберіть хоча б одне значення.");
            var text = lookup.HasValues ? "" : (option.Value ?? "").Trim();
            if (text.Contains('<') || text.Contains('>'))
                throw new DomainException($"Опція «{lookup.Name}»: текст без знаків < і >.");
            checkedOptions[option.OptionId] = option with { Value = text, Values = values };
        }
        if (current is not null)
        {
            var kept = options.SelectMany(o => o.Values ?? []).Select(v => v.OptionValueId).ToHashSet();
            foreach (var option in current.Options)
                foreach (var value in option.Values.Where(v => !kept.Contains(v.OptionValueId) && v.Quantity > 0))
                    throw new DomainException(
                        $"Значення {value.Name} опції «{option.Name}» не можна прибрати: на вітрині {value.Quantity} шт.");
        }
        return [.. checkedOptions.Values];
    }

    private static void Known(string what, IReadOnlyCollection<int> chosen, IReadOnlyList<LookupItem> allowed, IReadOnlyList<int>? kept)
    {
        var ok = allowed.Select(a => a.Id).Concat(kept ?? []).ToHashSet();
        var unknown = chosen.Where(id => !ok.Contains(id)).Distinct().Order().ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Невідомі {what}: {string.Join(", ", unknown)}.");
    }

    /// <summary>У своєму порядку, кожен раз, без самого товару.</summary>
    private static IReadOnlyList<int> Unique(IReadOnlyList<int>? ids, int? ownId) => Safe(ids).Distinct().Where(id => id != ownId).ToList();

    private static IReadOnlyList<T> Safe<T>(IReadOnlyList<T>? items) => items ?? [];

    private static void Length(string what, string? value, int max)
    {
        if (value is not null && value.Length > max)
            throw new DomainException($"{what}: не довше за {max} символів.");
    }

    private static void Range(string what, decimal value, decimal min, decimal max)
    {
        if (value < min || value > max)
            throw new DomainException($"{what}: від {min} до {max}.");
    }

    private static void Count<T>(string what, IReadOnlyList<T>? items, int max)
    {
        if (items is not null && items.Count > max)
            throw new DomainException($"Не більше {max} {what}.");
    }

    // date_modified — datetime без часток секунди, як його пише OpenCart.
    private DateTime Now()
    {
        var now = clock.GetLocalNow().DateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
