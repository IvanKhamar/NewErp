using NewErp.Domain.Catalog;

namespace NewErp.UnitTests.Domain;

public class SpecialPriceTests
{
    [Theory]
    [InlineData(900, 15, 770)] // 765 → 770
    [InlineData(900, 10, 810)]
    [InlineData(1250, 33, 840)] // 837.5 → 840
    [InlineData(95, 50, 50)] // 47.5 → половина від нуля
    [InlineData(100, 100, 0)]
    public void Percent_IsRoundedToTensLikeTheOldSite(int regular, int percent, int expected) =>
        Assert.Equal(expected, new SpecialPrice(0, percent).Apply(regular));

    [Fact]
    public void FixedPrice_IsUsedWithoutAPercent() =>
        Assert.Equal(799m, new SpecialPrice(799, 0).Apply(900));
}
