using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Catalog;

namespace NewErp.Api.Endpoints;

/// <summary>Модерація відгуків з фото й питань покупців (пункти 51, 83): менеджер і адміністратор.</summary>
public static class FeedbackEndpoints
{
    public static void MapFeedbackEndpoints(this WebApplication app)
    {
        var catalog = app.MapGroup("/catalog").WithTags("Відгуки й питання").RequireAuthorization(Policies.Orders);

        catalog.MapGet("/reviews", (string? state, int? page, int? pageSize, FeedbackService service, CancellationToken ct) =>
                service.ReviewsAsync(state, page ?? 1, pageSize ?? 20, ct))
            .WithSummary("Відгуки: all, pending (на модерації) або published; кожен з усіма фото");
        catalog.MapPut("/reviews/{id:int}", async (int id, UpdateReviewRequest request, ClaimsPrincipal user, FeedbackService service, CancellationToken ct) =>
            {
                await service.UpdateReviewAsync(id, request, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Виправити й опублікувати чи сховати відгук");
        catalog.MapDelete("/reviews/{id:int}", async (int id, ClaimsPrincipal user, FeedbackService service, CancellationToken ct) =>
            {
                await service.DeleteReviewAsync(id, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити відгук разом з його фото");
        catalog.MapPut("/reviews/{id:int}/images/{imageId:int}", async (int id, int imageId, SetReviewImageStatusRequest request, ClaimsPrincipal user,
                FeedbackService service, CancellationToken ct) =>
            {
                await service.SetReviewImageStatusAsync(id, imageId, request, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Фото відгуку: pending, published чи rejected");
        catalog.MapDelete("/reviews/{id:int}/images/{imageId:int}", async (int id, int imageId, ClaimsPrincipal user, FeedbackService service, CancellationToken ct) =>
            {
                await service.DeleteReviewImageAsync(id, imageId, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити фото відгуку (і файл у review_photos/)");

        catalog.MapGet("/questions", (string? state, int? page, int? pageSize, FeedbackService service, CancellationToken ct) =>
                service.QuestionsAsync(state, page ?? 1, pageSize ?? 20, ct))
            .WithSummary("Питання покупців: all, pending або published");
        catalog.MapPut("/questions/{id:int}", async (int id, UpdateQuestionRequest request, ClaimsPrincipal user, FeedbackService service, CancellationToken ct) =>
            {
                await service.UpdateQuestionAsync(id, request, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Відповісти на питання й опублікувати (лише з відповіддю)");
        catalog.MapDelete("/questions/{id:int}", async (int id, ClaimsPrincipal user, FeedbackService service, CancellationToken ct) =>
            {
                await service.DeleteQuestionAsync(id, user.GetUserId(), ct);
                return Results.NoContent();
            })
            .WithSummary("Видалити питання");
    }
}
