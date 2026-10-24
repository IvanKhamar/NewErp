using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;

using NewErp.Application.Reports;

namespace NewErp.IntegrationTests;

/// <summary>Фід виручки без входу: токен у заголовку або в адресі; без налаштованого токена фіда немає.</summary>
public class RevenueFeedApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task Feed_NeedsToken_ReturnsTotalsAndSeries()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/feeds/revenue.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/feeds/revenue.json?token=wrong")).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/feeds/revenue.json?days=7");
        request.Headers.Add("X-Feed-Token", ApiFactory.FeedToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var feed = (await response.Content.ReadFromJsonAsync<RevenueFeedDto>())!;
        Assert.Equal(7, feed.Days.Count);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), feed.Days[^1].Date);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/feeds/revenue.json?token={ApiFactory.FeedToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/feeds/revenue.json?token={ApiFactory.FeedToken}&days=1000")).StatusCode);
    }

    [MySqlFact]
    public async Task Feed_WithoutConfiguredToken_IsNotFound()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("RevenueFeed:Token", "")).CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/feeds/revenue.json?token=anything")).StatusCode);
    }
}
