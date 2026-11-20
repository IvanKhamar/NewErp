using NewErp.Application.Common;
using NewErp.Domain.Common;

using static NewErp.Application.Catalog.RequestLimits;

namespace NewErp.Application.Catalog;

// Відгуки й питання покупців з вітрини: модерація старої адмінки (Python first, пункт 51). Вітрина показує обидва як
// звичайний текст (екранований), тож і зберігаємо їх текстом, не HTML. Відповідь на питання — колонка comment старого
// сайту; питання з відповіддю й увімкненим статусом видно на сторінці товару.
// Покупці можуть додати до відгуку фото (dd_review_image, пункт 83). Кожне фото модерується окремо; вітрина показує лише
// опубліковані. Видалення фото чи відгуку видаляє й файли, які вітрина зберегла в review_photos/.

/// <summary>Path — від папки image/ вітрини; Status — pending, published чи rejected.</summary>
public sealed record ReviewImageDto(int Id, string Path, string Status);

public sealed record ReviewDto(
    int Id, int ProductId, string ProductName, string Author, string Text, int Rating, bool Published, DateTime DateAdded, IReadOnlyList<ReviewImageDto> Images);

public sealed record SetReviewImageStatusRequest(string Status);

public sealed record UpdateReviewRequest(string Author = "", string Text = "", int Rating = 5, bool Published = false);

public sealed record QuestionDto(
    int Id, int ProductId, string ProductName, string Name, string Telephone, string City, string Question, string Answer, bool Published,
    DateOnly? DateQuestion, DateTime? DateAdded);

public sealed record UpdateQuestionRequest(string Question = "", string Answer = "", bool Published = false);

public interface IFeedbackStore
{
    /// <summary>state: all, pending чи published.</summary>
    Task<PagedResult<ReviewDto>> ReviewsAsync(string state, int page, int pageSize, CancellationToken ct);

    Task<bool> UpdateReviewAsync(int reviewId, UpdateReviewRequest request, DateTime now, CancellationToken ct);

    /// <summary>Шляхи фото відгуку, які видаляються разом із ним; null — такого відгуку немає.</summary>
    Task<IReadOnlyList<string>?> DeleteReviewAsync(int reviewId, CancellationToken ct);

    Task<bool> SetReviewImageStatusAsync(int reviewId, int imageId, string status, CancellationToken ct);

    /// <summary>Шлях видаленого фото; null — у відгуку такого фото немає.</summary>
    Task<string?> DeleteReviewImageAsync(int reviewId, int imageId, CancellationToken ct);

    Task<PagedResult<QuestionDto>> QuestionsAsync(string state, int page, int pageSize, CancellationToken ct);

    Task<bool> UpdateQuestionAsync(int questionId, UpdateQuestionRequest request, CancellationToken ct);

    Task<bool> DeleteQuestionAsync(int questionId, CancellationToken ct);
}

public sealed class FeedbackService(IFeedbackStore store, IPictureFolder photos, TimeProvider clock, IAuditLog audit)
{
    public const string All = "all";
    public const string Pending = "pending";
    public const string Published = "published";
    public const string Rejected = "rejected";

    /// <summary>Куди вітрина зберігає фото відгуків (catalog/controller/product/product.php). Видаляються лише файли звідти.</summary>
    public const string ReviewPhotoFolder = "review_photos/";

    public static readonly IReadOnlyList<string> ImageStatuses = [Pending, Published, Rejected];

    /// <summary>
    /// Файл самої папки review_photos/: без «..» і без зворотної косої, тож «review_photos/../catalog/x.jpg» — не воно.
    /// Рядок може вказувати й на картинку товару, а її видаляти не можна ніколи.
    /// </summary>
    public static bool IsReviewPhoto(string path)
    {
        if (!path.StartsWith(ReviewPhotoFolder, StringComparison.Ordinal) || path.Contains('\\'))
            return false;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && !parts.Contains("..");
    }

    public Task<PagedResult<ReviewDto>> ReviewsAsync(string? state, int page, int pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        return store.ReviewsAsync(State(state), p, size, ct);
    }

    public async Task UpdateReviewAsync(int reviewId, UpdateReviewRequest request, int userId, CancellationToken ct)
    {
        Length("Автор", request.Author, 64);
        Length("Текст", request.Text, 10_000);
        Range("Оцінка", request.Rating, 1, 5);
        if (request.Author.Trim().Length == 0 || request.Text.Trim().Length == 0)
            throw new DomainException("Відгук має мати автора й текст.");
        var clean = request with { Author = request.Author.Trim(), Text = request.Text.Trim() };
        if (!await store.UpdateReviewAsync(reviewId, clean, Now(), ct))
            throw new NotFoundException($"Відгуку {reviewId} немає.");
        audit.Record($"review moderated: {reviewId}, published {request.Published}, by user {userId}");
    }

    public async Task DeleteReviewAsync(int reviewId, int userId, CancellationToken ct)
    {
        var paths = await store.DeleteReviewAsync(reviewId, ct) ?? throw new NotFoundException($"Відгуку {reviewId} немає.");
        foreach (var path in paths)
            await DeleteFileAsync(path, ct);
        audit.Record($"review deleted: {reviewId} with {paths.Count} photos, by user {userId}");
    }

    public async Task SetReviewImageStatusAsync(int reviewId, int imageId, SetReviewImageStatusRequest request, int userId, CancellationToken ct)
    {
        if (!ImageStatuses.Contains(request.Status))
            throw new DomainException($"Статус фото: {string.Join(", ", ImageStatuses)}.");
        if (!await store.SetReviewImageStatusAsync(reviewId, imageId, request.Status, ct))
            throw new NotFoundException($"Фото {imageId} у відгуку {reviewId} немає.");
        audit.Record($"review photo moderated: {imageId} of review {reviewId}, {request.Status}, by user {userId}");
    }

    public async Task DeleteReviewImageAsync(int reviewId, int imageId, int userId, CancellationToken ct)
    {
        var path = await store.DeleteReviewImageAsync(reviewId, imageId, ct) ?? throw new NotFoundException($"Фото {imageId} у відгуку {reviewId} немає.");
        await DeleteFileAsync(path, ct);
        audit.Record($"review photo deleted: {imageId} of review {reviewId}, by user {userId}");
    }

    /// <summary>Як у старій адмінці: спершу рядок; файл, який не вдалося видалити, лише лишає попередження в журналі (це робить папка).</summary>
    private async Task DeleteFileAsync(string path, CancellationToken ct)
    {
        if (photos.Enabled && IsReviewPhoto(path))
            await photos.DeleteAsync(path, ct);
    }

    public Task<PagedResult<QuestionDto>> QuestionsAsync(string? state, int page, int pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        return store.QuestionsAsync(State(state), p, size, ct);
    }

    public async Task UpdateQuestionAsync(int questionId, UpdateQuestionRequest request, int userId, CancellationToken ct)
    {
        Length("Питання", request.Question, 10_000);
        Length("Відповідь", request.Answer, 10_000);
        if (request.Question.Trim().Length == 0)
            throw new DomainException("Питання не може бути порожнім.");
        if (request.Published && request.Answer.Trim().Length == 0)
            throw new DomainException("Опублікувати можна лише питання з відповіддю.");
        var clean = request with { Question = request.Question.Trim(), Answer = request.Answer.Trim() };
        if (!await store.UpdateQuestionAsync(questionId, clean, ct))
            throw new NotFoundException($"Питання {questionId} немає.");
        audit.Record($"question answered: {questionId}, published {request.Published}, by user {userId}");
    }

    public async Task DeleteQuestionAsync(int questionId, int userId, CancellationToken ct)
    {
        if (!await store.DeleteQuestionAsync(questionId, ct))
            throw new NotFoundException($"Питання {questionId} немає.");
        audit.Record($"question deleted: {questionId}, by user {userId}");
    }

    private static string State(string? state) => (state ?? All) switch
    {
        All or Pending or Published => state ?? All,
        _ => throw new DomainException("Стан: all, pending або published."),
    };

    private DateTime Now()
    {
        var now = clock.GetLocalNow().DateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
