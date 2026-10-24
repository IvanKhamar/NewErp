using NewErp.Domain.Common;
using NewErp.Domain.Competitors;

namespace NewErp.UnitTests.Domain;

public class CompetitorTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 7, 0, 0);

    [Theory]
    [InlineData("1 299,00", 1299)]
    [InlineData("1,299.00", 1299)]
    [InlineData("1299.5", 1299.5)]
    [InlineData("1 299 грн", 1299)]
    [InlineData("1.299", 1299)]
    [InlineData("12,50", 12.5)]
    [InlineData("1 250", 1250)]
    public void Parse_ReadsShopPriceFormats(string text, double expected) =>
        Assert.Equal((decimal)expected, PriceExtractor.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("ціна за запитом")]
    [InlineData("0,00")]
    public void Parse_NoPrice(string text) => Assert.Null(PriceExtractor.Parse(text));

    [Theory]
    [InlineData("""<meta property="product:price:amount" content="1499.00">""")]
    [InlineData("""<meta content="1499" property="product:price:amount" />""")]
    [InlineData("""<span itemprop="price" content="1499">1 499 грн</span>""")]
    [InlineData("""<script type="application/ld+json">{"@type":"Offer","price": "1499.00"}</script>""")]
    public void Extract_StandardMarkup(string html) => Assert.Equal(1499m, PriceExtractor.Extract(html, null));

    [Fact]
    public void Extract_CustomRegexFirst_ThenStandard()
    {
        const string html = """<div class="price">1 199 грн</div><meta property="product:price:amount" content="1499">""";

        Assert.Equal(1199m, PriceExtractor.Extract(html, """class="price">([0-9 ]+)"""));
        Assert.Equal(1499m, PriceExtractor.Extract(html, """class="nothing">([0-9]+)"""));
        Assert.Null(PriceExtractor.Extract("<p>Немає в наявності</p>", null));
    }

    [Fact]
    public void RequireRegex_NeedsGroupAndValidSyntax()
    {
        Assert.Equal("", PriceExtractor.RequireRegex("  "));
        Assert.Contains("групу", Assert.Throws<DomainException>(() => PriceExtractor.RequireRegex("price: [0-9]+")).Message);
        Assert.Throws<DomainException>(() => PriceExtractor.RequireRegex("(unclosed"));
    }

    [Fact]
    public void Link_MustBeOnCompetitorSite_SubdomainAllowed()
    {
        var competitor = new Competitor("Магазин", "https://www.Shop.ua/", "", true, Now);

        Assert.Equal("shop.ua", competitor.Host);
        Assert.Equal(0, new CompetitorLink(competitor, 101, "https://m.shop.ua/dress-1", Now).CompetitorId);
        Assert.Throws<DomainException>(() => new CompetitorLink(competitor, 101, "https://evilshop.ua/dress-1", Now));
        Assert.Throws<DomainException>(() => new CompetitorLink(competitor, 101, "ftp://shop.ua/dress-1", Now));
        Assert.Throws<DomainException>(() => new Competitor(" ", "https://shop.ua", "", true, Now));
    }

    [Fact]
    public void RecordPrice_HistoryOnlyWhenChanged_FailureKeepsLastPrice()
    {
        var link = new CompetitorLink(new Competitor("Магазин", "https://shop.ua", "", true, Now), 101, "https://shop.ua/a", Now);

        Assert.NotNull(link.RecordPrice(1000, CompetitorLink.Auto, Now));
        Assert.Null(link.RecordPrice(1000, CompetitorLink.Auto, Now.AddDays(1)));
        link.RecordFailure("Сайт відповів HTTP 503.", Now.AddDays(2));
        Assert.Equal((1000m, "Сайт відповів HTTP 503.", Now.AddDays(2)), (link.LastPrice!.Value, link.LastError, link.LastCheckedAt!.Value));
        Assert.Equal(CompetitorLink.Manual, link.RecordPrice(950, CompetitorLink.Manual, Now.AddDays(3))!.Source);
        Assert.Equal("", link.LastError);
        Assert.Throws<DomainException>(() => link.RecordPrice(9.999m, CompetitorLink.Manual, Now));
    }
}
