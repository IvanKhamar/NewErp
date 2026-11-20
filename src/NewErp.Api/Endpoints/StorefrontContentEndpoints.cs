using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Catalog;

namespace NewErp.Api.Endpoints;

/// <summary>Вміст вітрини (пункт 50): інформаційні сторінки, FAQ, блог, блоки головної. Менеджер і адміністратор.</summary>
public static class StorefrontContentEndpoints
{
    public static void MapStorefrontContentEndpoints(this WebApplication app)
    {
        var catalog = app.MapGroup("/catalog").WithTags("Вміст вітрини").RequireAuthorization(Policies.Orders);

        catalog.MapGet("/information", (StorefrontContentService service, CancellationToken ct) => service.InformationAsync(ct))
            .WithSummary("Інформаційні сторінки вітрини");
        catalog.MapPost("/information", async (InformationDto request, StorefrontContentService service, CancellationToken ct) =>
            {
                var page = await service.SaveInformationAsync(null, request, ct);
                return Results.Created($"/catalog/information/{page.Id}", page);
            })
            .WithSummary("Нова сторінка; HTML очищується");
        catalog.MapPut("/information/{id:int}", (int id, InformationDto request, StorefrontContentService service, CancellationToken ct) =>
                service.SaveInformationAsync(id, request, ct))
            .WithSummary("Змінити сторінку");

        catalog.MapGet("/faq", (StorefrontContentService service, CancellationToken ct) => service.FaqsAsync(ct))
            .WithSummary("Питання й відповіді (FAQ)");
        catalog.MapPost("/faq", async (FaqDto request, StorefrontContentService service, CancellationToken ct) =>
            {
                var faq = await service.SaveFaqAsync(null, request, ct);
                return Results.Created($"/catalog/faq/{faq.Id}", faq);
            })
            .WithSummary("Нове питання FAQ");
        catalog.MapPut("/faq/{id:int}", (int id, FaqDto request, StorefrontContentService service, CancellationToken ct) =>
                service.SaveFaqAsync(id, request, ct))
            .WithSummary("Змінити питання FAQ");

        catalog.MapGet("/blog/categories", (StorefrontContentService service, CancellationToken ct) => service.BlogCategoriesAsync(ct))
            .WithSummary("Рубрики блогу");
        catalog.MapPost("/blog/categories", async (BlogCategoryDto request, StorefrontContentService service, CancellationToken ct) =>
            {
                var category = await service.SaveBlogCategoryAsync(null, request, ct);
                return Results.Created($"/catalog/blog/categories/{category.Id}", category);
            })
            .WithSummary("Нова рубрика блогу");
        catalog.MapPut("/blog/categories/{id:int}", (int id, BlogCategoryDto request, StorefrontContentService service, CancellationToken ct) =>
                service.SaveBlogCategoryAsync(id, request, ct))
            .WithSummary("Змінити рубрику блогу");

        catalog.MapGet("/blog/posts", (StorefrontContentService service, CancellationToken ct) => service.BlogPostsAsync(ct))
            .WithSummary("Дописи блогу");
        catalog.MapPost("/blog/posts", async (BlogPostDto request, StorefrontContentService service, CancellationToken ct) =>
            {
                var post = await service.SaveBlogPostAsync(null, request, ct);
                return Results.Created($"/catalog/blog/posts/{post.Id}", post);
            })
            .WithSummary("Новий допис; HTML очищується");
        catalog.MapPut("/blog/posts/{id:int}", (int id, BlogPostDto request, StorefrontContentService service, CancellationToken ct) =>
                service.SaveBlogPostAsync(id, request, ct))
            .WithSummary("Змінити допис");

        catalog.MapDelete("/content/{kind}/{id:int}", async (string kind, int id, ClaimsPrincipal user, StorefrontContentService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(kind, id, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити сторінку, питання FAQ, рубрику чи допис блогу (kind: information, faq, blog-categories, blog-posts)");

        catalog.MapGet("/blocks/about", (StorefrontContentService service, CancellationToken ct) => service.AboutAsync(ct))
            .WithSummary("Блок «Про нас» головної сторінки");
        catalog.MapPut("/blocks/about", (AboutBlockDto request, StorefrontContentService service, CancellationToken ct) => service.SaveAboutAsync(request, ct))
            .WithSummary("Змінити блок «Про нас» (тексти інших мов лишаються)");
        catalog.MapGet("/blocks/nft", (StorefrontContentService service, CancellationToken ct) => service.NftAsync(ct))
            .WithSummary("Блок NFT головної сторінки");
        catalog.MapPut("/blocks/nft", (NftBlockDto request, StorefrontContentService service, CancellationToken ct) => service.SaveNftAsync(request, ct))
            .WithSummary("Змінити блок NFT (посилання — свій магазин або https)");
    }
}
