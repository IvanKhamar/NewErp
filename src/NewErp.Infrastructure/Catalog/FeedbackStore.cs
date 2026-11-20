using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Infrastructure.Persistence;

namespace NewErp.Infrastructure.Catalog;

/// <summary>Відгуки (dd_review) з фото (dd_review_image) і питання покупців (dd_question) з назвами товарів (пункти 51, 83).</summary>
public sealed class FeedbackStore(ErpDbContext db, IOptions<ErpOptions> options) : IFeedbackStore
{
    private static readonly DateTime NoDate = new(1000, 1, 2);
    private static readonly DateTime OldDate = new(2000, 1, 1);

    /// <summary>Старий сайт тримає статус фото числом.</summary>
    private static readonly Dictionary<int, string> ImageStatuses = new()
    {
        [0] = FeedbackService.Pending, [1] = FeedbackService.Published, [2] = FeedbackService.Rejected,
    };

    private readonly int _languageId = options.Value.LanguageId;

    public async Task<PagedResult<ReviewDto>> ReviewsAsync(string state, int page, int pageSize, CancellationToken ct)
    {
        var reviews = db.Reviews.AsNoTracking();
        if (state != FeedbackService.All)
            reviews = reviews.Where(r => r.Status == (state == FeedbackService.Published));
        var total = await reviews.CountAsync(ct);
        var rows = await reviews.OrderByDescending(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.ProductId, r.Author, r.Text, r.Rating, r.Status,
                ProductName = db.DdProductDescriptions.Where(d => d.ProductId == r.ProductId && d.LanguageId == _languageId).Select(d => d.Name).FirstOrDefault() ?? "",
                DateAdded = r.DateAdded < NoDate ? (DateTime?)null : r.DateAdded, // нульова дата старого сайту
            }).ToListAsync(ct);
        var images = await ImagesAsync([.. rows.Select(r => r.Id)], ct);
        return new PagedResult<ReviewDto>(
            [.. rows.Select(r => new ReviewDto(r.Id, r.ProductId, r.ProductName, r.Author, r.Text, r.Rating, r.Status, r.DateAdded ?? OldDate,
                images[r.Id].ToList()))],
            page, pageSize, total);
    }

    /// <summary>Усі фото відгуків сторінки, з будь-яким статусом, у порядку вітрини.</summary>
    private async Task<ILookup<int, ReviewImageDto>> ImagesAsync(List<int> reviewIds, CancellationToken ct)
    {
        // Статус — tinyint(1) зі значеннями 0–2: провайдер прочитав би його як bool, тож тут він число.
        var ids = string.Join(",", reviewIds);
        var rows = await db.Database.SqlQuery<ImageRow>($"""
            SELECT image_id AS Id, review_id AS ReviewId, image AS Image, CAST(status AS UNSIGNED) AS Status
            FROM dd_review_image WHERE FIND_IN_SET(review_id, {ids}) > 0
            ORDER BY sort_order, image_id
            """).ToListAsync(ct);
        return rows.ToLookup(i => i.ReviewId, i => new ReviewImageDto(i.Id, i.Image, ImageStatuses.GetValueOrDefault((int)i.Status, FeedbackService.Pending)));
    }

    private sealed class ImageRow
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }

        public string Image { get; set; } = "";

        public ulong Status { get; set; }
    }

    public async Task<bool> UpdateReviewAsync(int reviewId, UpdateReviewRequest request, DateTime now, CancellationToken ct) =>
        await db.Reviews.Where(r => r.Id == reviewId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Author, request.Author).SetProperty(r => r.Text, request.Text).SetProperty(r => r.Rating, request.Rating)
            .SetProperty(r => r.Status, request.Published).SetProperty(r => r.DateModified, now), ct) > 0;

    /// <summary>Стара адмінка лишала фото; тут вони йдуть разом із відгуком, в одній транзакції.</summary>
    public async Task<IReadOnlyList<string>?> DeleteReviewAsync(int reviewId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Reviews.Where(r => r.Id == reviewId).ExecuteDeleteAsync(ct) == 0)
            return null;
        var paths = await db.ReviewImages.Where(i => i.ReviewId == reviewId).Select(i => i.Image).ToListAsync(ct);
        await db.ReviewImages.Where(i => i.ReviewId == reviewId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return paths;
    }

    public async Task<bool> SetReviewImageStatusAsync(int reviewId, int imageId, string status, CancellationToken ct)
    {
        var code = ImageStatuses.Single(s => s.Value == status).Key;
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dd_review_image SET status = {code} WHERE image_id = {imageId} AND review_id = {reviewId}", ct) > 0;
    }

    public async Task<string?> DeleteReviewImageAsync(int reviewId, int imageId, CancellationToken ct)
    {
        var path = await db.ReviewImages.Where(i => i.Id == imageId && i.ReviewId == reviewId).Select(i => i.Image).FirstOrDefaultAsync(ct);
        if (path is null)
            return null;
        await db.ReviewImages.Where(i => i.Id == imageId).ExecuteDeleteAsync(ct);
        return path;
    }

    // ═══ Питання: власні колонки старого сайту (місто, відповідь, статус, дата питання) — поза згенерованим класом ═══

    public async Task<PagedResult<QuestionDto>> QuestionsAsync(string state, int page, int pageSize, CancellationToken ct)
    {
        var all = state == FeedbackService.All;
        var published = state == FeedbackService.Published;
        var total = await db.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM dd_question WHERE {all} OR status = {published}").SingleAsync(ct);
        var offset = (page - 1) * pageSize;
        var rows = await db.Database.SqlQuery<QuestionRow>($"""
            SELECT q.question_id AS Id, q.product_id AS ProductId,
                   COALESCE((SELECT d.name FROM dd_product_description d WHERE d.product_id = q.product_id AND d.language_id = {_languageId} LIMIT 1), '') AS ProductName,
                   q.name AS Name, q.telephone AS Telephone, COALESCE(q.city, '') AS City, q.question AS Question, COALESCE(q.comment, '') AS Answer,
                   q.status AS Published,
                   CASE WHEN q.date_question < '1000-01-02' THEN NULL ELSE q.date_question END AS DateQuestion,
                   CASE WHEN q.date_added < '1000-01-02' THEN NULL ELSE q.date_added END AS DateAdded
            FROM dd_question q
            WHERE {all} OR q.status = {published}
            ORDER BY q.question_id DESC
            LIMIT {pageSize} OFFSET {offset}
            """).ToListAsync(ct);
        return new PagedResult<QuestionDto>(
            [.. rows.Select(r => new QuestionDto(r.Id, r.ProductId, r.ProductName, r.Name, r.Telephone, r.City, r.Question, r.Answer, r.Published,
                r.DateQuestion is { } day ? DateOnly.FromDateTime(day) : null, r.DateAdded))],
            page, pageSize, total);
    }

    public async Task<bool> UpdateQuestionAsync(int questionId, UpdateQuestionRequest request, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dd_question SET question = {request.Question}, comment = {request.Answer}, status = {request.Published} WHERE question_id = {questionId}",
            ct) > 0;

    public async Task<bool> DeleteQuestionAsync(int questionId, CancellationToken ct) =>
        await db.DdQuestions.Where(q => q.QuestionId == questionId).ExecuteDeleteAsync(ct) > 0;

    private sealed class QuestionRow
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = "";

        public string Name { get; set; } = "";

        public string Telephone { get; set; } = "";

        public string City { get; set; } = "";

        public string Question { get; set; } = "";

        public string Answer { get; set; } = "";

        public bool Published { get; set; }

        public DateTime? DateQuestion { get; set; }

        public DateTime? DateAdded { get; set; }
    }
}
