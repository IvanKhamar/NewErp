using System.Net;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Catalog;
using NewErp.Application.Common;
using NewErp.Web.Api;
using NewErp.Web.Pages.Sales;

namespace NewErp.Web.Tests;

/// <summary>Модерація відгуків з фото (пункти 51, 83) і питань покупців з підміненим API.</summary>
public class FeedbackPagesTests : WebTestContext
{
    private static readonly ReviewDto Review = new(1, 102, "Сорочка лляна оверсайз", "Ірина", "Гарна", 5, false, new DateTime(2026, 11, 9),
        [new ReviewImageDto(10, "review_photos/r1.jpg", "pending")]);

    private static readonly QuestionDto Question = new(1, 102, "Сорочка лляна оверсайз", "Оксана", "+380671112233", "Київ", "Чи є розмір L?", "", false,
        null, new DateTime(2026, 11, 9));

    public FeedbackPagesTests()
    {
        Services.AddScoped<CatalogApi>();
        Api.Respond("/catalog/reviews", HttpStatusCode.OK, new PagedResult<ReviewDto>([Review], 1, 20, 1));
        Api.Respond("/catalog/reviews/1/images/10", HttpStatusCode.NoContent, method: HttpMethod.Put);
        Api.Respond("/catalog/questions", HttpStatusCode.OK, new PagedResult<QuestionDto>([Question], 1, 20, 1));
        Api.Respond("/catalog/questions/1", HttpStatusCode.NoContent, method: HttpMethod.Put);
    }

    [Fact]
    public void AReviewShowsItsPhotos_AndAPhotoIsPublished()
    {
        var page = Render<ContentReviews>();
        page.WaitForAssertion(() => Assert.Contains("Ірина", page.Find("[data-test=reviews]").TextContent));
        Assert.Contains("state=pending", Api.Urls[0]);

        page.Find("[data-test=reviews] tbody tr").Click();
        Assert.Contains("review_photos/r1.jpg", page.Find("[data-test=review-images]").TextContent);
        page.FindAll("[data-test=review-images] button").First(b => b.TextContent.Contains("Опублікувати")).Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/reviews/1/images/10", Api.Bodies.Keys));
        Assert.Contains("\"status\":\"published\"", Api.Bodies["/catalog/reviews/1/images/10"]);
    }

    [Fact]
    public void AQuestionIsAnswered()
    {
        var page = Render<ContentQuestions>();
        page.WaitForAssertion(() => Assert.Contains("Чи є розмір L?", page.Find("[data-test=questions]").TextContent));

        page.Find("[data-test=questions] tbody tr").Click();
        page.Find("textarea[data-test=question-answer]").Change("Так");
        page.Find("input[data-test=question-published]").Change(true);
        page.Find("button[data-test=question-save]").Click();
        page.WaitForAssertion(() => Assert.Contains("/catalog/questions/1", Api.Bodies.Keys));
        Assert.Contains("\"published\":true", Api.Bodies["/catalog/questions/1"]);
    }
}
