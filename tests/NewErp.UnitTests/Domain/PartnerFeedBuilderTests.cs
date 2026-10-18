using NewErp.Domain.Partners;

namespace NewErp.UnitTests.Domain;

public class PartnerFeedBuilderTests
{
    private const string Store = "https://shop.test/";
    private static readonly Dictionary<int, string> CategoryNames = new() { [5] = "Сорочки", [6] = "Сукні", [9] = "Аксесуари" };

    private static FeedProduct Product(int id, int category, params FeedProductOption[] options) =>
        new(id, $"T-{id}", "", 1000, $"Товар {id}", "<p>Льон</p>", [category], ["catalog/фото 1.jpg"], options);

    private static FeedProductOption Size(int optionValueId, string name, decimal price) => new(optionValueId, name, price);

    [Fact]
    public void CategoryOffers_HaveMarkupAndOldPrice_PerSize()
    {
        var partner = PartnerTests.Partner();
        partner.SetCategoryMap([new CategoryMapping(5, null, "77", "", 10, 25)]);

        var feed = PartnerFeedBuilder.Build(partner, [Product(12, 5, Size(46, "S", 900), Size(47, "M", 1000))], CategoryNames, Store);

        Assert.Equal(["12_46", "12_47"], feed.Offers.Select(o => o.Id));
        var m = feed.Offers[1];
        Assert.Equal((1100m, 1250m), (m.Price, m.PriceOld!.Value));
        Assert.Equal(("Товар 12 - M", "M", "Льон"), (m.Name, m.Size, m.Description));
        Assert.Equal(PartnerFeedBuilder.Barcode("", 12, 1), m.Barcode); // другий розмір — цифра варіанта 1
        Assert.Equal("https://shop.test/image/catalog/%D1%84%D0%BE%D1%82%D0%BE%201.jpg", m.Pictures.Single());
        Assert.Equal(new FeedCategory("0", "77", "Сорочки"), feed.Categories.Single());
    }

    [Fact]
    public void SizeRow_WinsOverWholeCategory_AndUnmappedSizesAreSkipped()
    {
        var partner = PartnerTests.Partner();
        partner.SetCategoryMap([new CategoryMapping(5, "M", "", "", 50, null), new CategoryMapping(6, "L", "", "", null, null)]);

        var feed = PartnerFeedBuilder.Build(partner,
            [Product(12, 5, Size(46, "S", 900), Size(47, "M", 1000)), Product(13, 6, Size(47, "M", 1000))], CategoryNames, Store);

        var offer = Assert.Single(feed.Offers);
        Assert.Equal(("12_47", 1500m, (decimal?)null), (offer.Id, offer.Price, offer.PriceOld));
    }

    [Fact]
    public void ProductRow_ReplacesCategoryOffer_AndAddsOwnCategoryWhenUnmapped()
    {
        var partner = PartnerTests.Partner();
        partner.SetCategoryMap([new CategoryMapping(5, null, "", "", null, null)]);
        partner.SetProductMap([
            new ProductMapping(12, "M", "EVA-12", "Сорочка для EVA", 1300, 1600),
            new ProductMapping(20, null, "", "", null, null),
        ]);
        var noSizes = Product(20, 9) with { Price = 450 };

        var feed = PartnerFeedBuilder.Build(partner, [Product(12, 5, Size(46, "S", 900), Size(47, "M", 1000)), noSizes], CategoryNames, Store);

        Assert.Equal(["12_46", "p12_47", "p20"], feed.Offers.Select(o => o.Id));
        var mapped = feed.Offers[1];
        Assert.Equal((1300m, 1600m, "EVA-12", "Сорочка для EVA", "0"), (mapped.Price, mapped.PriceOld!.Value, mapped.VendorCode!, mapped.Name, mapped.CategoryId));
        Assert.Equal(("p9", 450m, "Товар 20"), (feed.Offers[2].CategoryId, feed.Offers[2].Price, feed.Offers[2].Name));
        Assert.Contains(new FeedCategory("p9", null, "Аксесуари"), feed.Categories);
    }

    [Fact]
    public void OffersWithoutPictures_AreSkipped()
    {
        var partner = PartnerTests.Partner();
        partner.SetCategoryMap([new CategoryMapping(5, null, "", "", null, null)]);

        var feed = PartnerFeedBuilder.Build(partner, [Product(12, 5) with { Images = [] }], CategoryNames, Store);

        Assert.Empty(feed.Offers);
    }

    [Theory]
    [InlineData("123", 12345, 2, "4820123123426")]
    [InlineData("", 102, 0, "4820000010207")]
    [InlineData("AB1", 7, 11, "4820000000710")]
    public void Barcode_IsTheSameEan13AsOnTheOldSite(string sku, int productId, int variant, string expected) =>
        Assert.Equal(expected, PartnerFeedBuilder.Barcode(sku, productId, variant));

    [Fact]
    public void PlainDescription_DecodesHtmlAndKeepsLineBreaks() =>
        Assert.Equal("Льон 100%\nПрати при 30°", PartnerFeedBuilder.PlainDescription("&lt;p&gt;Льон 100%&lt;/p&gt;&lt;b&gt;Прати&lt;/b&gt; при 30&amp;deg;"));
}
