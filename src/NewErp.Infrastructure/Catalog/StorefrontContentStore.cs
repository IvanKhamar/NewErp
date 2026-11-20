using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.Infrastructure.Catalog;

/// <summary>
/// Інформаційні сторінки, FAQ, блог і блоки головної сторінки над таблицями вітрини (пункт 50).
/// Блоки «Про нас» і NFT — налаштування модулів OpenCart: рядки dd_setting, де масиви записано JSON (serialized = 1), а
/// тексти — окремо для кожної мови. Замінюються лише тексти цієї мови; інші лишаються, як були.
/// </summary>
public sealed class StorefrontContentStore(ErpDbContext db, IOptions<ErpOptions> options) : IStorefrontContentStore
{
    private const int StoreId = 0;
    private static readonly DateTime NoDate = new(1000, 1, 2);

    private readonly int _languageId = options.Value.LanguageId;

    private string Language => _languageId.ToString(CultureInfo.InvariantCulture);

    // ═══ Інформаційні сторінки ═══

    public async Task<IReadOnlyList<InformationDto>> InformationAsync(CancellationToken ct)
    {
        var keywords = await KeywordsAsync("information_id", ct);
        var rows = await (
            from i in db.Information.AsNoTracking()
            join d in db.InformationDescriptions.Where(x => x.LanguageId == _languageId) on i.Id equals d.InformationId into ds
            from d in ds.DefaultIfEmpty()
            orderby i.SortOrder, d == null ? "" : d.Title
            select new { i, d }).ToListAsync(ct);
        return [.. rows.Select(r => new InformationDto(
            r.i.Id, r.d?.Title ?? "", r.d?.Description ?? "", r.d?.MetaTitle ?? "", r.d?.MetaDescription ?? "", r.d?.MetaKeyword ?? "",
            keywords.GetValueOrDefault(r.i.Id, ""), r.i.SortOrder, r.i.Bottom != 0, r.i.Status))];
    }

    public async Task<int> SaveInformationAsync(InformationDto page, CancellationToken ct)
    {
        var row = page.Id == 0 ? db.Information.Add(new Information()).Entity : await db.Information.SingleAsync(i => i.Id == page.Id, ct);
        (row.Bottom, row.SortOrder, row.Status) = (page.Bottom ? 1 : 0, page.SortOrder, page.Enabled);
        await db.SaveChangesAsync(ct);
        if (page.Id == 0)
            db.InformationToStores.Add(new InformationToStore { InformationId = row.Id, StoreId = StoreId });
        await db.InformationDescriptions.Where(d => d.InformationId == row.Id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        db.InformationDescriptions.Add(new InformationDescription
        {
            InformationId = row.Id, LanguageId = _languageId, Title = page.Title, Description = page.Description,
            MetaTitle = page.MetaTitle, MetaDescription = page.MetaDescription, MetaKeyword = page.MetaKeyword,
        });
        await SetKeywordAsync("information_id", row.Id, page.SeoKeyword, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return row.Id;
    }

    // ═══ FAQ ═══

    public async Task<IReadOnlyList<FaqDto>> FaqsAsync(CancellationToken ct) => await (
        from f in db.Faqs.AsNoTracking()
        join d in db.FaqDescriptions.Where(x => x.LanguageId == _languageId) on f.Id equals d.FaqId into ds
        from d in ds.DefaultIfEmpty()
        orderby f.SortOrder, f.Id
        select new FaqDto(f.Id, d == null ? "" : d.Name, d == null ? "" : d.Description, f.Author, f.Email, f.SortOrder, f.Status)).ToListAsync(ct);

    public async Task<int> SaveFaqAsync(FaqDto faq, DateTime now, CancellationToken ct)
    {
        var row = faq.Id == 0 ? db.Faqs.Add(new Faq { Comment = "", DateAdded = now }).Entity : await db.Faqs.SingleAsync(f => f.Id == faq.Id, ct);
        (row.Author, row.Email, row.SortOrder, row.Status) = (faq.Author, faq.Email, faq.SortOrder, faq.Enabled);
        await db.SaveChangesAsync(ct);
        await db.FaqDescriptions.Where(d => d.FaqId == row.Id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        db.FaqDescriptions.Add(new FaqDescription { FaqId = row.Id, LanguageId = _languageId, Name = faq.Question, Description = faq.Answer });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return row.Id;
    }

    // ═══ Блог ═══

    public async Task<IReadOnlyList<BlogCategoryDto>> BlogCategoriesAsync(CancellationToken ct) => await (
        from c in db.PostCategories.AsNoTracking()
        join d in db.PostCategoryDescriptions.Where(x => x.LanguageId == _languageId) on c.Id equals d.PostCategoryId into ds
        from d in ds.DefaultIfEmpty()
        orderby d == null ? "" : d.Name
        select new BlogCategoryDto(c.Id, d == null ? "" : d.Name, c.Status, db.PostToCategories.Count(p => p.PostCategoryId == c.Id))).ToListAsync(ct);

    public async Task<int> SaveBlogCategoryAsync(BlogCategoryDto category, DateTime now, CancellationToken ct)
    {
        var row = category.Id == 0 ? db.PostCategories.Add(new PostCategory { DateAdded = now }).Entity : await db.PostCategories.SingleAsync(c => c.Id == category.Id, ct);
        row.Status = category.Enabled;
        await db.SaveChangesAsync(ct);
        await db.PostCategoryDescriptions.Where(d => d.PostCategoryId == row.Id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        db.PostCategoryDescriptions.Add(new PostCategoryDescription { PostCategoryId = row.Id, LanguageId = _languageId, Name = category.Name });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return row.Id;
    }

    public async Task<IReadOnlyList<BlogPostDto>> BlogPostsAsync(CancellationToken ct)
    {
        var keywords = await KeywordsAsync("post_id", ct);
        var links = (await db.PostToCategories.AsNoTracking().ToListAsync(ct)).ToLookup(l => l.PostId, l => l.PostCategoryId);
        // Нульову дату старого сайту прочитати не можна: вона повертається порожньою.
        var rows = await (
            from p in db.Posts.AsNoTracking()
            join d in db.PostDescriptions.Where(x => x.LanguageId == _languageId) on p.Id equals d.PostId into ds
            from d in ds.DefaultIfEmpty()
            orderby p.DateAvailable descending, p.Id descending
            select new
            {
                p.Id, p.Image, p.BigImage, p.IsTop, p.Status, DateAvailable = p.DateAvailable < NoDate ? (DateTime?)null : p.DateAvailable,
                Name = d == null ? "" : d.Name, Description = d == null ? "" : d.Description, MetaTitle = d == null ? "" : d.MetaTitle,
                MetaDescription = d == null ? "" : d.MetaDescription, ImageAlt = d == null ? "" : d.ImageAlt,
            }).ToListAsync(ct);
        return [.. rows.Select(r => new BlogPostDto(
            r.Id, r.Name, r.Description, r.MetaTitle, r.MetaDescription, r.ImageAlt, r.Image, r.BigImage, r.IsTop, r.Status, r.DateAvailable,
            keywords.GetValueOrDefault(r.Id, ""), [.. links[r.Id]]))];
    }

    public async Task<int> SaveBlogPostAsync(BlogPostDto post, DateTime now, CancellationToken ct)
    {
        var row = post.Id == 0 ? db.Posts.Add(new Post { DateAdded = now }).Entity : await db.Posts.SingleAsync(p => p.Id == post.Id, ct);
        (row.Image, row.BigImage, row.IsTop, row.Status, row.DateAvailable) = (post.Image, post.BigImage, post.Top, post.Enabled, post.DateAvailable ?? now);
        await db.SaveChangesAsync(ct);
        await db.PostDescriptions.Where(d => d.PostId == row.Id && d.LanguageId == _languageId).ExecuteDeleteAsync(ct);
        db.PostDescriptions.Add(new PostDescription
        {
            PostId = row.Id, LanguageId = _languageId, Name = post.Name, Description = post.Description, MetaTitle = post.MetaTitle,
            MetaDescription = post.MetaDescription, ImageAlt = post.ImageAlt,
        });
        await db.PostToCategories.Where(l => l.PostId == row.Id).ExecuteDeleteAsync(ct);
        db.PostToCategories.AddRange(post.CategoryIds.Select(c => new PostToCategory { PostId = row.Id, PostCategoryId = c }));
        await SetKeywordAsync("post_id", row.Id, post.SeoKeyword, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return row.Id;
    }

    // ═══ Видалення: свій рядок, усе, що на нього вказує, і SEO-адреса ═══

    public async Task<bool> DeleteAsync(string kind, int itemId, CancellationToken ct)
    {
        switch (kind)
        {
            case "information":
                if (!await db.Information.AnyAsync(i => i.Id == itemId, ct))
                    return false;
                await db.InformationDescriptions.Where(x => x.InformationId == itemId).ExecuteDeleteAsync(ct);
                await db.InformationToStores.Where(x => x.InformationId == itemId).ExecuteDeleteAsync(ct);
                await db.Information.Where(x => x.Id == itemId).ExecuteDeleteAsync(ct);
                await DeleteKeywordAsync("information_id", itemId, ct);
                return true;
            case "faq":
                if (!await db.Faqs.AnyAsync(f => f.Id == itemId, ct))
                    return false;
                await db.FaqDescriptions.Where(x => x.FaqId == itemId).ExecuteDeleteAsync(ct);
                await db.Faqs.Where(x => x.Id == itemId).ExecuteDeleteAsync(ct);
                return true;
            case "blog-categories":
                if (!await db.PostCategories.AnyAsync(c => c.Id == itemId, ct))
                    return false;
                await db.PostCategoryDescriptions.Where(x => x.PostCategoryId == itemId).ExecuteDeleteAsync(ct);
                await db.PostToCategories.Where(x => x.PostCategoryId == itemId).ExecuteDeleteAsync(ct);
                await db.PostCategories.Where(x => x.Id == itemId).ExecuteDeleteAsync(ct);
                return true;
            default:
                if (!await db.Posts.AnyAsync(p => p.Id == itemId, ct))
                    return false;
                await db.PostDescriptions.Where(x => x.PostId == itemId).ExecuteDeleteAsync(ct);
                await db.PostToCategories.Where(x => x.PostId == itemId).ExecuteDeleteAsync(ct);
                await db.Posts.Where(x => x.Id == itemId).ExecuteDeleteAsync(ct);
                await DeleteKeywordAsync("post_id", itemId, ct);
                return true;
        }
    }

    public Task<string?> SeoKeywordOwnerAsync(string keyword, CancellationToken ct) =>
        db.SeoUrls.Where(s => s.Keyword == keyword && s.StoreId == StoreId).Select(s => (string?)s.Query).FirstOrDefaultAsync(ct);

    private async Task<Dictionary<int, string>> KeywordsAsync(string key, CancellationToken ct)
    {
        var prefix = key + "=";
        var rows = await db.SeoUrls.AsNoTracking().Where(s => s.Query.StartsWith(prefix) && s.StoreId == StoreId)
            .Select(s => new { s.Query, s.Keyword }).ToListAsync(ct);
        var result = new Dictionary<int, string>();
        foreach (var row in rows)
            if (int.TryParse(row.Query[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                result[id] = row.Keyword;
        return result;
    }

    private async Task SetKeywordAsync(string key, int id, string keyword, CancellationToken ct)
    {
        var query = $"{key}={id}";
        await db.SeoUrls.Where(s => s.Query == query && s.StoreId == StoreId).ExecuteDeleteAsync(ct);
        if (keyword.Length > 0)
            db.SeoUrls.Add(new SeoUrl { StoreId = StoreId, LanguageId = _languageId, Query = query, Keyword = keyword });
    }

    private Task DeleteKeywordAsync(string key, int id, CancellationToken ct)
    {
        var query = $"{key}={id}";
        return db.SeoUrls.Where(s => s.Query == query).ExecuteDeleteAsync(ct);
    }

    // ═══ Блоки головної сторінки (налаштування модулів) ═══

    public async Task<AboutBlockDto> AboutAsync(CancellationToken ct)
    {
        var s = await SettingsAsync("module_about", ct);
        var texts = Texts(s.GetValueOrDefault("module_about_description"));
        return new AboutBlockDto(
            Text(s.GetValueOrDefault("module_about_status")) == "1", Text(texts?["heading_title"]), Text(texts?["subtitle"]), Text(texts?["description"]),
            [.. Items(s.GetValueOrDefault("module_about_item")).Select(i => new AboutItemDto(
                Text(i["image_left"]), Text(i["image_right"]), Text(i["image_mobile"]), Number(i["sort_order"])))]);
    }

    public async Task SaveAboutAsync(AboutBlockDto block, CancellationToken ct)
    {
        var s = await SettingsAsync("module_about", ct);
        var texts = new JsonObject { ["heading_title"] = block.HeadingTitle, ["subtitle"] = block.Subtitle, ["description"] = block.Description };
        await SaveSettingsAsync("module_about", new Dictionary<string, JsonNode?>
        {
            ["module_about_status"] = block.Enabled ? "1" : "0",
            ["module_about_description"] = WithTexts(s.GetValueOrDefault("module_about_description"), texts),
            ["module_about_item"] = new JsonArray([.. block.Items.Select(i => (JsonNode)new JsonObject
            {
                ["image_left"] = i.ImageLeft, ["image_right"] = i.ImageRight, ["image_mobile"] = i.ImageMobile, ["sort_order"] = i.SortOrder,
            })]),
        }, ct);
    }

    public async Task<NftBlockDto> NftAsync(CancellationToken ct)
    {
        var s = await SettingsAsync("module_nft", ct);
        var texts = Texts(s.GetValueOrDefault("module_nft_description"));
        return new NftBlockDto(
            Text(s.GetValueOrDefault("module_nft_status")) == "1", Text(texts?["heading_title"]), Text(texts?["subtitle"]), Text(texts?["description"]),
            Text(texts?["heading_form"]), Text(texts?["subtitle_form"]), Text(texts?["link"]), Text(texts?["text_link"]), Text(s.GetValueOrDefault("module_nft_image")),
            [.. Items(s.GetValueOrDefault("module_nft_images")).Select(i => new NftImageDto(
                Text(i["image"]), Text(Texts(i["descriptions"])?["link"]), Number(i["sort_order"])))],
            [.. Items(s.GetValueOrDefault("module_nft_item")).Select(i =>
            {
                var t = Texts(i["descriptions"]);
                return new NftItemDto(Text(t?["title"]), Text(t?["subtitle"]), Text(t?["description"]), Number(i["sort_order"]));
            })]);
    }

    public async Task SaveNftAsync(NftBlockDto block, CancellationToken ct)
    {
        var s = await SettingsAsync("module_nft", ct);
        var texts = new JsonObject
        {
            ["heading_title"] = block.HeadingTitle, ["subtitle"] = block.Subtitle, ["description"] = block.Description, ["heading_form"] = block.HeadingForm,
            ["subtitle_form"] = block.SubtitleForm, ["link"] = block.Link, ["text_link"] = block.TextLink,
        };
        await SaveSettingsAsync("module_nft", new Dictionary<string, JsonNode?>
        {
            ["module_nft_status"] = block.Enabled ? "1" : "0",
            ["module_nft_image"] = block.Image,
            ["module_nft_description"] = WithTexts(s.GetValueOrDefault("module_nft_description"), texts),
            ["module_nft_images"] = new JsonArray([.. block.Images.Select(i => (JsonNode)new JsonObject
            {
                ["image"] = i.Image, ["sort_order"] = i.SortOrder, ["descriptions"] = new JsonObject { [Language] = new JsonObject { ["link"] = i.Link } },
            })]),
            ["module_nft_item"] = new JsonArray([.. block.Items.Select(i => (JsonNode)new JsonObject
            {
                ["sort_order"] = i.SortOrder,
                ["descriptions"] = new JsonObject
                {
                    [Language] = new JsonObject { ["title"] = i.Title, ["subtitle"] = i.Subtitle, ["description"] = i.Description },
                },
            })]),
        }, ct);
    }

    /// <summary>Налаштування модуля: ключ → значення (JSON для serialized, рядок для решти). Зламаний JSON старого сайту читається порожнім.</summary>
    private async Task<Dictionary<string, JsonNode?>> SettingsAsync(string code, CancellationToken ct)
    {
        var rows = await db.DdSettings.AsNoTracking().Where(s => s.StoreId == StoreId && s.Code == code).ToListAsync(ct);
        var result = new Dictionary<string, JsonNode?>();
        foreach (var row in rows)
        {
            try
            {
                result[row.Key] = row.Serialized ? JsonNode.Parse(row.Value) : JsonValue.Create(row.Value);
            }
            catch (JsonException)
            {
                result[row.Key] = new JsonObject();
            }
        }
        return result;
    }

    private async Task SaveSettingsAsync(string code, Dictionary<string, JsonNode?> values, CancellationToken ct)
    {
        await db.DdSettings.Where(s => s.StoreId == StoreId && s.Code == code).ExecuteDeleteAsync(ct);
        db.DdSettings.AddRange(values.Select(v =>
        {
            var text = v.Value is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;
            return new DdSetting
            {
                StoreId = StoreId, Code = code, Key = v.Key, Value = text ?? v.Value?.ToJsonString(Json) ?? "", Serialized = text is null,
            };
        }));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Тексти цієї мови з об'єкта «мова → тексти».</summary>
    private JsonObject? Texts(JsonNode? byLanguage) => byLanguage is JsonObject all && all[Language] is JsonObject texts ? texts : null;

    private JsonObject WithTexts(JsonNode? byLanguage, JsonObject texts)
    {
        var merged = byLanguage is JsonObject all ? (JsonObject)all.DeepClone() : new JsonObject();
        merged[Language] = texts;
        return merged;
    }

    /// <summary>Масив або об'єкт «ключ → елемент» (так PHP серіалізує масиви з дірками) — як список об'єктів.</summary>
    private static IEnumerable<JsonObject> Items(JsonNode? node) => node switch
    {
        JsonArray array => array.OfType<JsonObject>(),
        JsonObject map => map.Select(p => p.Value).OfType<JsonObject>(),
        _ => [],
    };

    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var s) ? s : node?.ToString() ?? "";

    private static int Number(JsonNode? node) => int.TryParse(Text(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
