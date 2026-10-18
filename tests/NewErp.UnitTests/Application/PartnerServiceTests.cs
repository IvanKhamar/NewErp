using System.Xml.Linq;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Partners;
using NewErp.Domain.Common;
using NewErp.Domain.Partners;

namespace NewErp.UnitTests.Application;

public class PartnerServiceTests
{
    private readonly FakePartnerRepository _repository = new();
    private readonly PartnerService _service;

    public PartnerServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 9, 30, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new PartnerService(_repository,
            Options.Create(new PartnerFeedOptions { ShopName = "Vidro", Company = "ФОП Тест", StoreUrl = "https://shop.test" }), clock);
    }

    [Fact]
    public async Task Create_RejectsTakenCode()
    {
        await _service.CreateAsync(new SavePartnerRequest("eva", "EVA"), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(new SavePartnerRequest("eva", "Інший"), CancellationToken.None));
    }

    [Fact]
    public async Task Feed_IsNullWhenDisabledOrUnknown()
    {
        await _service.CreateAsync(new SavePartnerRequest("eva", "EVA", FeedEnabled: false), CancellationToken.None);

        Assert.Null(await _service.BuildFeedXmlAsync("eva", CancellationToken.None));
        Assert.Null(await _service.BuildFeedXmlAsync("rozetka", CancellationToken.None));
    }

    [Fact]
    public async Task Feed_IsYmlCatalogWithOffers()
    {
        await _service.CreateAsync(new SavePartnerRequest("eva", "EVA", FeedEnabled: true, VendorName: "Vidro", DefaultQuantity: 7,
            CategoryMap: [new CategoryMapRow(5, ExternalId: "77", PercentSupply: 10)]), CancellationToken.None);
        _repository.Products.Add(new FeedProduct(12, "T-12", "", 1000, "Сорочка", "<p>Льон & бавовна</p>", [5], ["catalog/1.jpg"],
            [new FeedProductOption(47, "M", 1000)]));

        var xml = XDocument.Parse((await _service.BuildFeedXmlAsync("eva", CancellationToken.None))!);

        var shop = xml.Root!.Element("shop")!;
        Assert.Equal("2026-10-18 09:30", xml.Root.Attribute("date")!.Value);
        Assert.Equal(("Vidro", "ФОП Тест", "https://shop.test/"), (shop.Element("name")!.Value, shop.Element("company")!.Value, shop.Element("url")!.Value));
        var category = shop.Element("categories")!.Element("category")!;
        Assert.Equal(("77", "Сорочки"), (category.Attribute("eva_id")!.Value, category.Value));
        var offer = shop.Element("offers")!.Element("offer")!;
        Assert.Equal("12_47", offer.Attribute("id")!.Value);
        Assert.Equal(("1100", "7", "Vidro", "T-12"), (offer.Element("price")!.Value, offer.Element("stock_quantity")!.Value, offer.Element("vendor")!.Value, offer.Element("article")!.Value));
        Assert.Null(offer.Element("price_old"));
        Assert.Equal("https://shop.test/image/catalog/1.jpg", offer.Element("picture")!.Value);
        Assert.Equal("Льон & бавовна", offer.Element("description_ua")!.Value);
        Assert.Equal(["M", PartnerFeedBuilder.Barcode("", 12, 0)], offer.Elements("param").Select(p => p.Value));
    }

    private sealed class FakePartnerRepository : IPartnerRepository
    {
        private readonly List<Partner> _partners = [];

        public List<FeedProduct> Products { get; } = [];

        public Task<IReadOnlyList<PartnerListItemDto>> ListAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<PartnerDto?> GetDtoAsync(int id, CancellationToken ct) => throw new NotSupportedException();

        public Task<Partner?> GetAsync(int id, CancellationToken ct) => Task.FromResult(_partners.FirstOrDefault(p => p.Id == id));

        public Task<Partner?> GetByCodeAsync(string code, CancellationToken ct) => Task.FromResult(_partners.FirstOrDefault(p => p.Code == code));

        public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct) =>
            Task.FromResult(_partners.Any(p => p.Code == code && p.Id != exceptId));

        public void Add(Partner partner) => _partners.Add(partner);

        public void Remove(Partner partner) => _partners.Remove(partner);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ShopCategoryDto>> ListCategoriesAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<PartnerProductPickDto>> SearchProductsAsync(string term, int limit, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FeedProduct>> GetFeedProductsAsync(IReadOnlyCollection<int> categoryIds, IReadOnlyCollection<int> productIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FeedProduct>>(Products);

        public Task<IReadOnlyDictionary<int, string>> GetCategoryNamesAsync(IReadOnlyCollection<int> categoryIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string> { [5] = "Сорочки" });
    }
}
