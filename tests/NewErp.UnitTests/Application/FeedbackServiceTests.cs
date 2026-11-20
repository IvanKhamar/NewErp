using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Catalog;
using NewErp.Application.Common;

namespace NewErp.UnitTests.Application;

/// <summary>Пункт 83: які файли фото відгуків можна видаляти. Перенесено з Python tests/test_review_photos.py.</summary>
public class FeedbackServiceTests
{
    [Theory]
    [InlineData("review_photos/a1b2.jpg", true)]
    [InlineData("review_photos/2026/a1b2.jpg", true)]
    [InlineData("catalog/products/a1b2.jpg", false)] // рядок може вказувати на картинку товару: її не видаляємо ніколи
    [InlineData("review_photos/../catalog/x.jpg", false)]
    [InlineData("review_photos\\..\\catalog\\x.jpg", false)]
    [InlineData("review_photos/", false)]
    [InlineData("/etc/passwd", false)]
    public void OnlyFilesOfTheReviewPhotoFolder_AreDeleted(string path, bool deleted) =>
        Assert.Equal(deleted, FeedbackService.IsReviewPhoto(path));

    [Fact]
    public async Task DeletingAReview_DeletesItsPhotoFiles_ButNotSharedPictures()
    {
        var folder = new FakeFolder();
        await Service(["review_photos/a.jpg", "catalog/x.jpg"], folder).DeleteReviewAsync(1, userId: 1, CancellationToken.None);
        Assert.Equal(["review_photos/a.jpg"], folder.Deleted);
    }

    [Fact]
    public async Task WithoutThePictureFolder_OnlyTheRowsGo()
    {
        var folder = new FakeFolder { Enabled = false };
        await Service(["review_photos/a.jpg"], folder).DeleteReviewImageAsync(1, 10, userId: 1, CancellationToken.None);
        Assert.Empty(folder.Deleted);
    }

    [Fact]
    public async Task UnknownPhotoOrReview_IsNotFound()
    {
        var service = Service([], new FakeFolder());
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteReviewImageAsync(1, 11, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteReviewAsync(2, 1, CancellationToken.None));
        await service.DeleteReviewAsync(1, 1, CancellationToken.None); // відгук без фото теж знаходиться
    }

    private static FeedbackService Service(IReadOnlyList<string> paths, FakeFolder folder) =>
        new(new FakeStore(paths), folder, new FakeTimeProvider(), new RecordingAudit());

    private sealed class FakeFolder : IPictureFolder
    {
        public bool Enabled { get; set; } = true;

        public List<string> Deleted { get; } = [];

        public Task SaveAsync(string path, byte[] content, CancellationToken ct) => Task.CompletedTask;

        public Task DeleteAsync(string path, CancellationToken ct)
        {
            Deleted.Add(path);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStore(IReadOnlyList<string> paths) : IFeedbackStore
    {
        public Task<IReadOnlyList<string>?> DeleteReviewAsync(int reviewId, CancellationToken ct) =>
            Task.FromResult(reviewId == 1 ? paths : null);

        public Task<string?> DeleteReviewImageAsync(int reviewId, int imageId, CancellationToken ct) =>
            Task.FromResult((reviewId, imageId) == (1, 10) ? paths[0] : null);

        public Task<bool> SetReviewImageStatusAsync(int reviewId, int imageId, string status, CancellationToken ct) =>
            Task.FromResult((reviewId, imageId) == (1, 10));

        public Task<PagedResult<ReviewDto>> ReviewsAsync(string state, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> UpdateReviewAsync(int reviewId, UpdateReviewRequest request, DateTime now, CancellationToken ct) => throw new NotSupportedException();

        public Task<PagedResult<QuestionDto>> QuestionsAsync(string state, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> UpdateQuestionAsync(int questionId, UpdateQuestionRequest request, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> DeleteQuestionAsync(int questionId, CancellationToken ct) => throw new NotSupportedException();
    }
}
