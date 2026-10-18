using NewErp.Domain.Common;
using NewErp.Domain.Partners;

namespace NewErp.UnitTests.Domain;

public class PartnerTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0);

    internal static Partner Partner(string code = "eva", bool feedEnabled = true, int defaultQuantity = 5) =>
        new(new PartnerSettings(code, "EVA", true, "", feedEnabled, "Vidro", "", defaultQuantity), Now);

    [Theory]
    [InlineData("")]
    [InlineData("ева")]
    [InlineData("eva feed")]
    [InlineData("a23456789012345678901234567890123")]
    public void Code_MustBeShortLatin(string code) =>
        Assert.Throws<DomainException>(() => Partner(code));

    [Fact]
    public void NegativeQuantity_IsRejected() =>
        Assert.Throws<DomainException>(() => Partner(defaultQuantity: -1));

    [Fact]
    public void SetCategoryMap_KeepsExistingRows_SoFeedCategoryIdsStayTheSame()
    {
        var partner = Partner();
        partner.SetCategoryMap([new CategoryMapping(5, null, "100", "", 10, null), new CategoryMapping(5, "M", "", "", null, null)]);
        var wholeCategory = partner.CategoryMap[0];

        partner.SetCategoryMap([new CategoryMapping(5, " ", "200", "Сорочки", 15, 30), new CategoryMapping(6, null, "", "", null, null)]);

        Assert.Equal(2, partner.CategoryMap.Count);
        Assert.Same(wholeCategory, partner.CategoryMap[0]); // порожній розмір = усі розміри, той самий рядок
        Assert.Equal(("200", "Сорочки", 15m, 30m), (wholeCategory.ExternalId, wholeCategory.ExternalName, wholeCategory.PercentSupply!.Value, wholeCategory.PercentSale!.Value));
        Assert.Equal(6, partner.CategoryMap[1].CategoryId);
    }

    [Fact]
    public void SetCategoryMap_RejectsDuplicatesAndBadPercents()
    {
        var partner = Partner();

        Assert.Throws<DomainException>(() => partner.SetCategoryMap([new(5, "M", "", "", null, null), new(5, "M ", "", "", null, null)]));
        Assert.Throws<DomainException>(() => partner.SetCategoryMap([new(5, null, "", "", -100, null)]));
        Assert.Throws<DomainException>(() => partner.SetCategoryMap([new(0, null, "", "", null, null)]));
    }

    [Fact]
    public void SetProductMap_RejectsZeroPrice() =>
        Assert.Throws<DomainException>(() => Partner().SetProductMap([new ProductMapping(10, null, "", "", 0, null)]));
}
