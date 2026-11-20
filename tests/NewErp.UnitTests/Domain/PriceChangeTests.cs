using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Domain;

public class PriceChangeTests
{
    [Theory]
    [InlineData(PriceChange.Add, 10, PriceChange.Percent, 1490, 1639)] // 1639.0 — цілі гривні
    [InlineData(PriceChange.Add, 50, PriceChange.Amount, 1000, 1050)]
    [InlineData(PriceChange.Subtract, 2000, PriceChange.Amount, 1000, 0)] // не нижче нуля
    [InlineData(PriceChange.Multiply, 1.5, PriceChange.Amount, 999, 1499)] // 1498.5 округлюється вгору
    [InlineData(PriceChange.Divide, 3, PriceChange.Amount, 1000, 333)]
    public void Changed_RoundsToWholeHryvnias(string operation, double amount, string unit, int price, int expected) =>
        Assert.Equal(expected, PriceChange.Changed(price, operation, (decimal)amount, unit));

    [Fact]
    public void Changed_RefusesWhatCannotBeComputed()
    {
        Assert.Equal("На нуль ділити не можна.", Assert.Throws<DomainException>(() => PriceChange.Changed(100, PriceChange.Divide, 0, PriceChange.Amount)).Message);
        Assert.Throws<DomainException>(() => PriceChange.Changed(100, "set_percent", 10, PriceChange.Amount));
        Assert.Throws<DomainException>(() => PriceChange.Changed(100, PriceChange.Add, -1, PriceChange.Amount));
        Assert.Throws<DomainException>(() => PriceChange.Changed(100, PriceChange.Add, 1, "kopecks"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void SalePercent_IsFrom1To99(int percent) =>
        Assert.Equal("Знижка має бути від 1 до 99 %.", Assert.Throws<DomainException>(() => PriceChange.SalePercent(percent)).Message);
}
