using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Competitors;

namespace NewErp.IntegrationTests;

/// <summary>Ціни конкурентів: конкурент, сторінка, перевірка (підроблена сторінка), ціна вручну, порівняння, історія.</summary>
public class CompetitorApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task CompetitorLinkCheckAndReport()
    {
        var pages = new FakePages();
        var client = await LoginAsync(factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IPageFetcher>(pages))));

        var created = await client.PostAsJsonAsync("/competitors", new SaveCompetitorRequest("Конкурент А", "https://www.shop-a.ua"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var competitor = (await created.Content.ReadFromJsonAsync<CompetitorDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/competitors/links", new AddCompetitorLinkRequest(competitor.Id, ApiFactory.ProductId, "https://other.ua/x"))).StatusCode);

        var linkResponse = await client.PostAsJsonAsync("/competitors/links",
            new AddCompetitorLinkRequest(competitor.Id, ApiFactory.ProductId, "https://shop-a.ua/dress"));
        Assert.Equal(HttpStatusCode.Created, linkResponse.StatusCode);
        var link = (await linkResponse.Content.ReadFromJsonAsync<CompetitorLinkDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/competitors/links", new AddCompetitorLinkRequest(competitor.Id, ApiFactory.ProductId, "https://shop-a.ua/dress-2"))).StatusCode);

        pages.Html = """<meta property="product:price:amount" content="800.00">""";
        var all = (await (await client.PostAsync("/competitors/check", null)).Content.ReadFromJsonAsync<CompetitorCheckResult>())!;
        Assert.True(all.Checked >= 1);
        var checkedLink = (await client.GetFromJsonAsync<List<CompetitorLinkDto>>($"/competitors/links?productId={ApiFactory.ProductId}"))!
            .Single(l => l.Id == link.Id);
        Assert.Equal((800m, ""), (checkedLink.LastPrice, checkedLink.LastError));

        pages.Html = "<p>Немає в наявності</p>";
        var failed = (await (await client.PostAsync($"/competitors/links/{link.Id}/check", null)).Content.ReadFromJsonAsync<CompetitorLinkDto>())!;
        Assert.Equal((800m, "Ціну на сторінці не знайдено."), (failed.LastPrice, failed.LastError));

        (await client.PostAsJsonAsync($"/competitors/links/{link.Id}/price", new CompetitorPriceRequest(750))).EnsureSuccessStatusCode();
        var history = await client.GetFromJsonAsync<List<CompetitorPriceDto>>($"/competitors/links/{link.Id}/history");
        Assert.Equal([(750m, "manual"), (800m, "auto")], history!.Select(h => (h.Price, h.Source)));

        var report = (await client.GetFromJsonAsync<CompetitorReportDto>("/competitors/report"))!;
        var column = report.Competitors.ToList().FindIndex(c => c.Id == competitor.Id);
        var row = report.Rows.Single(r => r.ProductId == ApiFactory.ProductId);
        Assert.Equal(750m, row.Prices[column]);
        Assert.NotNull(row.DifferencePercent);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/competitors/links/{link.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/competitors/links/{link.Id}/history")).StatusCode);
    }

    [MySqlFact]
    public async Task Storekeeper_HasNoAccess()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/competitors")).StatusCode);
    }

    private static async Task<HttpClient> LoginAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Manager, ApiFactory.Password))).EnsureSuccessStatusCode();
        return client;
    }

    private sealed class FakePages : IPageFetcher
    {
        public string Html { get; set; } = "";

        public Task<string> FetchAsync(Uri page, string siteHost, CancellationToken ct) => Task.FromResult(Html);
    }
}
