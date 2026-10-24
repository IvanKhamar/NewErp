using System.Net;
using System.Text;

using NewErp.Application.Competitors;
using NewErp.Infrastructure.Competitors;

namespace NewErp.IntegrationTests;

/// <summary>Завантаження сторінок конкурентів на підроблених відповідях: справжні сайти тести не відкривають.</summary>
public class HttpPageFetcherTests
{
    [Fact]
    public async Task FollowsRedirectWithinSite_ReadsCharsetFromHeader()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath == "/old"
            ? Redirect("/new")
            : Html("Ціна 1 299 грн", "windows-1251"));
        using var fetcher = new HttpPageFetcher(handler);

        var html = await fetcher.FetchAsync(new Uri("https://shop.ua/old"), "shop.ua", default);

        Assert.Equal("Ціна 1 299 грн", html);
        Assert.Equal(["/old", "/new"], handler.Paths);
    }

    [Fact]
    public async Task RedirectToOtherSite_TooManyRedirects_AndErrors_AreRefused()
    {
        using var away = new HttpPageFetcher(new FakeHandler(_ => Redirect("https://evil.example/page")));
        Assert.Contains("інший сайт", (await Assert.ThrowsAsync<PageFetchException>(() =>
            away.FetchAsync(new Uri("https://shop.ua/a"), "shop.ua", default))).Message);

        using var loop = new HttpPageFetcher(new FakeHandler(_ => Redirect("/again")));
        Assert.Equal("Забагато переадресацій.", (await Assert.ThrowsAsync<PageFetchException>(() =>
            loop.FetchAsync(new Uri("https://shop.ua/a"), "shop.ua", default))).Message);

        using var down = new HttpPageFetcher(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        Assert.Equal("Сайт відповів HTTP 503.", (await Assert.ThrowsAsync<PageFetchException>(() =>
            down.FetchAsync(new Uri("https://shop.ua/a"), "shop.ua", default))).Message);
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.1", false)]
    [InlineData("192.168.1.10", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:192.168.0.1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2001:4860:4860::8888", true)]
    public void IsPublic_RefusesLocalNetworks(string address, bool expected) =>
        Assert.Equal(expected, HttpPageFetcher.IsPublic(IPAddress.Parse(address)));

    [Fact]
    public async Task RealHandler_RefusesLocalhost()
    {
        using var fetcher = new HttpPageFetcher();

        var error = await Assert.ThrowsAsync<PageFetchException>(() => fetcher.FetchAsync(new Uri("http://localhost:9/"), "localhost", default));

        Assert.Equal("Адреса сайту веде в локальну мережу.", error.Message);
    }

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    private static HttpResponseMessage Html(string text, string charset)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var content = new ByteArrayContent(Encoding.GetEncoding(charset).GetBytes(text));
        content.Headers.TryAddWithoutValidation("Content-Type", $"text/html; charset={charset}");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(respond(request));
        }
    }
}
