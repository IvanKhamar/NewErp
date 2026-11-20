using System.Text.RegularExpressions;

using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

using static NewErp.Application.Catalog.RequestLimits;

namespace NewErp.Application.Catalog;

// Вміст вітрини старої адмінки (Python first, пункт 50): інформаційні сторінки, FAQ, блог, блоки «Про нас» і NFT головної
// сторінки. Усе, що вітрина показує як HTML, очищується перед записом.

public sealed record InformationDto(
    int Id = 0, string Title = "", string Description = "", string MetaTitle = "", string MetaDescription = "", string MetaKeyword = "",
    string SeoKeyword = "", int SortOrder = 0, bool Bottom = false, bool Enabled = true);

public sealed record FaqDto(int Id = 0, string Question = "", string Answer = "", string Author = "", string Email = "", int SortOrder = 0, bool Enabled = true);

public sealed record BlogCategoryDto(int Id = 0, string Name = "", bool Enabled = true, int Posts = 0);

/// <summary>DateAvailable — з якого часу допис видно; порожньо — зараз.</summary>
public sealed record BlogPostDto(
    int Id = 0, string Name = "", string Description = "", string MetaTitle = "", string MetaDescription = "", string ImageAlt = "",
    string Image = "", string BigImage = "", bool Top = false, bool Enabled = true, DateTime? DateAvailable = null, string SeoKeyword = "",
    IReadOnlyList<int>? CategoryIds = null)
{
    public IReadOnlyList<int> CategoryIds { get; init; } = CategoryIds ?? [];
}

public sealed record AboutItemDto(string ImageLeft = "", string ImageRight = "", string ImageMobile = "", int SortOrder = 0);

public sealed record AboutBlockDto(bool Enabled = false, string HeadingTitle = "", string Subtitle = "", string Description = "", IReadOnlyList<AboutItemDto>? Items = null)
{
    public IReadOnlyList<AboutItemDto> Items { get; init; } = Items ?? [];
}

public sealed record NftImageDto(string Image = "", string Link = "", int SortOrder = 0);

public sealed record NftItemDto(string Title = "", string Subtitle = "", string Description = "", int SortOrder = 0);

public sealed record NftBlockDto(
    bool Enabled = false, string HeadingTitle = "", string Subtitle = "", string Description = "", string HeadingForm = "", string SubtitleForm = "",
    string Link = "", string TextLink = "", string Image = "", IReadOnlyList<NftImageDto>? Images = null, IReadOnlyList<NftItemDto>? Items = null)
{
    public IReadOnlyList<NftImageDto> Images { get; init; } = Images ?? [];

    public IReadOnlyList<NftItemDto> Items { get; init; } = Items ?? [];
}

public interface IStorefrontContentStore
{
    Task<IReadOnlyList<InformationDto>> InformationAsync(CancellationToken ct);

    Task<int> SaveInformationAsync(InformationDto page, CancellationToken ct);

    Task<IReadOnlyList<FaqDto>> FaqsAsync(CancellationToken ct);

    Task<int> SaveFaqAsync(FaqDto faq, DateTime now, CancellationToken ct);

    Task<IReadOnlyList<BlogCategoryDto>> BlogCategoriesAsync(CancellationToken ct);

    Task<int> SaveBlogCategoryAsync(BlogCategoryDto category, DateTime now, CancellationToken ct);

    Task<IReadOnlyList<BlogPostDto>> BlogPostsAsync(CancellationToken ct);

    Task<int> SaveBlogPostAsync(BlogPostDto post, DateTime now, CancellationToken ct);

    /// <summary>Разом з назвами, зв'язками й SEO-адресою; false — запису не було.</summary>
    Task<bool> DeleteAsync(string kind, int itemId, CancellationToken ct);

    Task<string?> SeoKeywordOwnerAsync(string keyword, CancellationToken ct);

    Task<AboutBlockDto> AboutAsync(CancellationToken ct);

    Task SaveAboutAsync(AboutBlockDto block, CancellationToken ct);

    Task<NftBlockDto> NftAsync(CancellationToken ct);

    Task SaveNftAsync(NftBlockDto block, CancellationToken ct);
}

public sealed partial class StorefrontContentService(IStorefrontContentStore store, IHtmlCleaner html, TimeProvider clock, IAuditLog audit)
{
    /// <summary>Що можна видалити через DELETE /catalog/content/{kind}/{id}.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["information", "faq", "blog-categories", "blog-posts"];

    private const int MaxText = 500_000;
    private const int MaxListItems = 50;

    // ═══ Інформаційні сторінки ═══

    public Task<IReadOnlyList<InformationDto>> InformationAsync(CancellationToken ct) => store.InformationAsync(ct);

    public async Task<InformationDto> SaveInformationAsync(int? pageId, InformationDto page, CancellationToken ct)
    {
        Length("Заголовок", page.Title, 64);
        Length("Текст", page.Description, MaxText);
        MetaLimits(page.MetaTitle, page.MetaDescription, page.MetaKeyword, page.SeoKeyword);
        Exists(pageId, (await store.InformationAsync(ct)).Select(p => p.Id));
        var keyword = await UniqueKeywordAsync(page.SeoKeyword, $"information_id={pageId}", ct);
        var clean = page with { Id = pageId ?? 0, Title = Required(page.Title, "заголовок"), Description = html.Clean(page.Description), SeoKeyword = keyword };
        var saved = await store.SaveInformationAsync(clean, ct);
        return (await store.InformationAsync(ct)).First(p => p.Id == saved);
    }

    // ═══ FAQ ═══

    public Task<IReadOnlyList<FaqDto>> FaqsAsync(CancellationToken ct) => store.FaqsAsync(ct);

    public async Task<FaqDto> SaveFaqAsync(int? faqId, FaqDto faq, CancellationToken ct)
    {
        Length("Питання", faq.Question, 255);
        Length("Відповідь", faq.Answer, 65_000);
        Length("Автор", faq.Author, 64);
        Length("E-mail", faq.Email, 96);
        Exists(faqId, (await store.FaqsAsync(ct)).Select(f => f.Id));
        var email = faq.Email.Trim();
        if (email.Length > 0 && !Email().IsMatch(email))
            throw new DomainException($"E-mail «{email}» записано з помилкою.");
        var clean = faq with
        {
            Id = faqId ?? 0, Question = Required(faq.Question, "питання"), Answer = html.Clean(faq.Answer), Author = faq.Author.Trim(), Email = email,
        };
        var saved = await store.SaveFaqAsync(clean, Now(), ct);
        return (await store.FaqsAsync(ct)).First(f => f.Id == saved);
    }

    // ═══ Блог ═══

    public Task<IReadOnlyList<BlogCategoryDto>> BlogCategoriesAsync(CancellationToken ct) => store.BlogCategoriesAsync(ct);

    public async Task<BlogCategoryDto> SaveBlogCategoryAsync(int? categoryId, BlogCategoryDto category, CancellationToken ct)
    {
        Length("Назва", category.Name, 255);
        Exists(categoryId, (await store.BlogCategoriesAsync(ct)).Select(c => c.Id));
        var saved = await store.SaveBlogCategoryAsync(category with { Id = categoryId ?? 0, Name = Required(category.Name, "назву") }, Now(), ct);
        return (await store.BlogCategoriesAsync(ct)).First(c => c.Id == saved);
    }

    public Task<IReadOnlyList<BlogPostDto>> BlogPostsAsync(CancellationToken ct) => store.BlogPostsAsync(ct);

    public async Task<BlogPostDto> SaveBlogPostAsync(int? postId, BlogPostDto post, CancellationToken ct)
    {
        Length("Назва", post.Name, 255);
        Length("Текст", post.Description, MaxText);
        Length("Підпис картинки", post.ImageAlt, 255);
        MetaLimits(post.MetaTitle, post.MetaDescription, "", post.SeoKeyword);
        Count("рубрик", post.CategoryIds, MaxListItems);
        Exists(postId, (await store.BlogPostsAsync(ct)).Select(p => p.Id));
        var (image, bigImage) = (CatalogText.ImagePath(post.Image), CatalogText.ImagePath(post.BigImage));
        var unknown = post.CategoryIds.Except((await store.BlogCategoriesAsync(ct)).Select(c => c.Id)).Order().ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Невідомі рубрики блогу: {string.Join(", ", unknown)}.");
        var keyword = await UniqueKeywordAsync(post.SeoKeyword, $"post_id={postId}", ct);
        var now = Now();
        var clean = post with
        {
            Id = postId ?? 0, Name = Required(post.Name, "назву"), Description = html.Clean(post.Description), Image = image, BigImage = bigImage,
            SeoKeyword = keyword, DateAvailable = post.DateAvailable ?? now, CategoryIds = [.. post.CategoryIds.Distinct()],
        };
        var saved = await store.SaveBlogPostAsync(clean, now, ct);
        return (await store.BlogPostsAsync(ct)).First(p => p.Id == saved);
    }

    // ═══ Видалення ═══

    public async Task DeleteAsync(string kind, int itemId, int userId, CancellationToken ct)
    {
        if (!Kinds.Contains(kind))
            throw new DomainException($"Що видаляємо: {string.Join(", ", Kinds)}.");
        if (!await store.DeleteAsync(kind, itemId, ct))
            throw new NotFoundException($"Запису {itemId} немає.");
        audit.Record($"{kind} deleted: {itemId}, by user {userId}");
    }

    // ═══ Блоки головної сторінки ═══

    public Task<AboutBlockDto> AboutAsync(CancellationToken ct) => store.AboutAsync(ct);

    public async Task<AboutBlockDto> SaveAboutAsync(AboutBlockDto block, CancellationToken ct)
    {
        Length("Заголовок", block.HeadingTitle, 255);
        Length("Підзаголовок", block.Subtitle, 255);
        Length("Текст", block.Description, 65_000);
        Count("картинок", block.Items, MaxListItems);
        var items = block.Items.Select(i => i with
        {
            ImageLeft = CatalogText.ImagePath(i.ImageLeft), ImageRight = CatalogText.ImagePath(i.ImageRight), ImageMobile = CatalogText.ImagePath(i.ImageMobile),
        }).ToList();
        await store.SaveAboutAsync(block with { Description = html.Clean(block.Description), Items = items }, ct);
        return await store.AboutAsync(ct);
    }

    public Task<NftBlockDto> NftAsync(CancellationToken ct) => store.NftAsync(ct);

    public async Task<NftBlockDto> SaveNftAsync(NftBlockDto block, CancellationToken ct)
    {
        foreach (var (what, value) in new[]
        {
            ("Заголовок", block.HeadingTitle), ("Підзаголовок", block.Subtitle), ("Заголовок форми", block.HeadingForm),
            ("Підзаголовок форми", block.SubtitleForm), ("Посилання", block.Link), ("Текст посилання", block.TextLink),
        })
            Length(what, value, 255);
        Length("Текст", block.Description, 65_000);
        Count("картинок", block.Images, MaxListItems);
        Count("кроків", block.Items, MaxListItems);
        var clean = block with
        {
            Description = html.Clean(block.Description),
            Image = CatalogText.ImagePath(block.Image),
            Link = Link(block.Link),
            Images = [.. block.Images.Select(i => i with { Image = CatalogText.ImagePath(i.Image), Link = Link(i.Link) })],
            Items = [.. block.Items.Select(i => i with { Description = html.Clean(i.Description) })],
        };
        await store.SaveNftAsync(clean, ct);
        return await store.NftAsync(ct);
    }

    // ═══ Спільне ═══

    /// <summary>Посилання блоку: як адреса пресета (свій магазин або https) або порожньо.</summary>
    private static string Link(string value) => value.Trim().Length > 0 ? CatalogDictionaryService.PresetUrl(value) : "";

    private async Task<string> UniqueKeywordAsync(string value, string ownQuery, CancellationToken ct)
    {
        var keyword = CatalogText.SeoKeyword(value);
        if (keyword.Length > 0 && await store.SeoKeywordOwnerAsync(keyword, ct) is { } owner && owner != ownQuery)
            throw new ConflictException($"SEO-адресу «{keyword}» вже має {owner}.");
        return keyword;
    }

    private static void Exists(int? id, IEnumerable<int> ids)
    {
        if (id is { } value && !ids.Contains(value))
            throw new NotFoundException($"Запису {value} немає.");
    }

    private static void MetaLimits(string metaTitle, string metaDescription, string metaKeyword, string seoKeyword)
    {
        Length("Meta title", metaTitle, 255);
        Length("Meta description", metaDescription, 255);
        Length("Meta keywords", metaKeyword, 255);
        Length("SEO-адреса", seoKeyword, 255);
    }

    private static string Required(string value, string what)
    {
        var text = value.Trim();
        return text.Length > 0 ? text : throw new DomainException($"Вкажіть {what}.");
    }

    private DateTime Now()
    {
        var now = clock.GetLocalNow().DateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Email();
}
