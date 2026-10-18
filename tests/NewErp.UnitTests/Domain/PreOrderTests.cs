using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Domain;

public class PreOrderTests
{
    [Theory]
    [InlineData(3, 0, true, SizeAvailability.InStock)]
    [InlineData(0, 5, true, SizeAvailability.PreOrder)]
    [InlineData(-2, 5, true, SizeAvailability.PreOrder)]
    [InlineData(0, 0, true, SizeAvailability.SoldOut)]
    [InlineData(0, 0, false, SizeAvailability.InStock)]
    public void Availability_FollowsStockThenQuota(int quantity, int quota, bool subtract, SizeAvailability expected) =>
        Assert.Equal(expected, PreOrder.Availability(quantity, quota, subtract));

    [Theory]
    [InlineData(-2, 5, 0, 3)]  // продано наперед 2 з 5
    [InlineData(-7, 5, -2, 0)] // перепродано понад ліміт: ліміт 0, решта лишається мінусом
    [InlineData(4, 5, 4, 5)]   // залишок є — нічого не списується
    [InlineData(-3, 0, -3, 0)]
    public void Settle_MovesSoldAheadFromQuota(int quantity, int quota, int expectedQuantity, int expectedQuota) =>
        Assert.Equal((expectedQuantity, expectedQuota), PreOrder.Settle(quantity, quota));

    [Fact]
    public void ValidateQuota_RejectsNegativeAndHuge()
    {
        PreOrder.ValidateQuota(0);
        Assert.Throws<DomainException>(() => PreOrder.ValidateQuota(-1));
        Assert.Throws<DomainException>(() => PreOrder.ValidateQuota(PreOrder.MaxQuota + 1));
    }
}
