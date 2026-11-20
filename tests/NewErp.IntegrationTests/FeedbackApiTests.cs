using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using NewErp.Application.Auth;
using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>
/// Модерація відгуків і питань (пункт 51) та фото у відгуках (пункт 83). Перенесено з Python tests/test_storefront_content.py
/// (відгуки й питання) і tests/test_review_photos.py.
/// </summary>
public sealed class FeedbackApiTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 9, 10, 0, 0);

    private readonly string _folder = Directory.CreateTempSubdirectory("newerp-review-photos-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Відгуки 1 (на модерації), 2 (опублікований), 3 (на модерації) з фото й питання 1; база спільна для класу.</summary>
    private Task SeedAsync() => ProductFormApiTests.WithDbAsync(factory, async db =>
    {
        if (await db.Reviews.AnyAsync())
            return;
        foreach (var (id, published) in new[] { (1, false), (2, true), (3, false) })
            db.Reviews.Add(new Review
            {
                Id = id, ProductId = ApiFactory.CategoryProductId, Author = id == 2 ? "Петро" : "Ірина", Text = "Гарна", Rating = 5,
                Status = published, DateAdded = Now, DateModified = Now,
            });
        foreach (var (id, review, path, order, status) in new[]
        {
            (10, 1, "review_photos/r1-b.jpg", 1, 0), (11, 1, "review_photos/r1-a.jpg", 0, 1), (20, 2, "review_photos/r2.jpg", 0, 2),
            (30, 3, "review_photos/r3.jpg", 0, 0), (31, 3, "catalog/products/shared.jpg", 1, 1),
        })
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO dd_review_image (image_id, review_id, image, sort_order, status, date_added) VALUES ({id}, {review}, {path}, {order}, {status}, {Now})");
        db.DdQuestions.Add(new DdQuestion
        {
            QuestionId = 1, ProductId = ApiFactory.CategoryProductId, Name = "Оксана", Telephone = "+380671112233", Question = "Чи є розмір L?", DateAdded = Now,
        });
        await db.SaveChangesAsync();
    });

    private WebApplicationFactory<Program> WithFolder()
    {
        foreach (var path in new[] { "review_photos/r1-b.jpg", "review_photos/r3.jpg", "catalog/products/shared.jpg" })
        {
            var file = Path.Combine(_folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, "jpeg"u8.ToArray());
        }
        return factory.WithWebHostBuilder(b => b.UseSetting("Pictures:ImageDir", _folder));
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string username)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/auth/login", new LoginRequest(username, ApiFactory.Password))).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<List<(int Id, string Path, string Status)>> PhotosAsync(HttpClient client, int reviewId)
    {
        var reviews = (await client.GetFromJsonAsync<PagedResult<ReviewDto>>("/catalog/reviews?state=all"))!;
        return [.. reviews.Items.Single(r => r.Id == reviewId).Images.Select(i => (i.Id, i.Path, i.Status))];
    }

    [MySqlFact]
    public async Task Reviews_AreModerated()
    {
        await SeedAsync();
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var pending = (await client.GetFromJsonAsync<PagedResult<ReviewDto>>("/catalog/reviews?state=pending"))!;
        Assert.Contains(pending.Items, r => r.Id == 1 && r.ProductName == "Сорочка лляна оверсайз");
        Assert.DoesNotContain(pending.Items, r => r.Published);

        var body = new UpdateReviewRequest("Ірина", "Чудова сорочка!", 5, true);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/catalog/reviews/1", body)).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<PagedResult<ReviewDto>>("/catalog/reviews?state=pending"))!.Items, r => r.Id == 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/catalog/reviews/1", body with { Rating = 6 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/catalog/reviews/999", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/catalog/reviews?state=everything")).StatusCode);
    }

    [MySqlFact]
    public async Task Questions_AreAnsweredBeforePublishing()
    {
        await SeedAsync();
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var question = Assert.Single((await client.GetFromJsonAsync<PagedResult<QuestionDto>>("/catalog/questions?state=pending"))!.Items);
        Assert.Equal(("Оксана", "Чи є розмір L?", ""), (question.Name, question.Question, question.Answer));

        var noAnswer = new UpdateQuestionRequest(question.Question, "", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/catalog/questions/1", noAnswer)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/catalog/questions/1", noAnswer with { Answer = "Так, з'явиться за тиждень." })).StatusCode);
        var published = Assert.Single((await client.GetFromJsonAsync<PagedResult<QuestionDto>>("/catalog/questions?state=published"))!.Items);
        Assert.Equal("Так, з'явиться за тиждень.", published.Answer);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/catalog/questions/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/catalog/questions/1")).StatusCode);
    }

    [MySqlFact]
    public async Task ReviewsListTheirPhotos_InTheStorefrontOrder()
    {
        await SeedAsync();
        var client = await factory.LoginAsync(ApiFactory.Manager);
        Assert.Equal([(11, "review_photos/r1-a.jpg", "published"), (10, "review_photos/r1-b.jpg", "pending")], await PhotosAsync(client, 1));
        Assert.Equal([(20, "review_photos/r2.jpg", "rejected")], await PhotosAsync(client, 2));
    }

    [MySqlFact]
    public async Task APhoto_IsPublishedRejectedAndDeletedWithItsFile()
    {
        await SeedAsync();
        var client = await LoginAsync(WithFolder(), ApiFactory.Manager);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/catalog/reviews/1/images/10", new SetReviewImageStatusRequest("published"))).StatusCode);
        Assert.Contains(("review_photos/r1-b.jpg", "published"), (await PhotosAsync(client, 1)).Select(p => (p.Path, p.Status)));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/catalog/reviews/1/images/10", new SetReviewImageStatusRequest("hidden"))).StatusCode);
        var otherReview = await client.PutAsJsonAsync("/catalog/reviews/2/images/10", new SetReviewImageStatusRequest("rejected"));
        Assert.Equal(HttpStatusCode.NotFound, otherReview.StatusCode);
        Assert.Contains("Фото 10 у відгуку 2 немає.", await otherReview.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/catalog/reviews/1/images/10")).StatusCode);
        Assert.False(File.Exists(Path.Combine(_folder, "review_photos/r1-b.jpg")));
        Assert.Equal([11], (await PhotosAsync(client, 1)).Select(p => p.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/catalog/reviews/1/images/10")).StatusCode);
    }

    [MySqlFact]
    public async Task DeletingAReview_TakesItsPhotos_ButNeverASharedPicture()
    {
        await SeedAsync();
        var client = await LoginAsync(WithFolder(), ApiFactory.Manager);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/catalog/reviews/3")).StatusCode);

        Assert.Equal(0, await ProductFormApiTests.QueryAsync(factory, db => db.ReviewImages.CountAsync(i => i.ReviewId == 3)));
        Assert.False(File.Exists(Path.Combine(_folder, "review_photos/r3.jpg")));
        Assert.True(File.Exists(Path.Combine(_folder, "catalog/products/shared.jpg")));
    }

    [MySqlFact]
    public async Task Photos_AreClosedToOtherRoles()
    {
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.PutAsJsonAsync("/catalog/reviews/1/images/11", new SetReviewImageStatusRequest("rejected"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.DeleteAsync("/catalog/reviews/1/images/11")).StatusCode);
    }
}
