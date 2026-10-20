using NewErp.Domain.Common;
using NewErp.Domain.Payments;

namespace NewErp.UnitTests.Domain;

public class LiqPayPaymentTests
{
    [Fact]
    public void ValidateAmount_DefaultsToOrderTotal_AllowsPrepayment()
    {
        Assert.Equal(1500m, LiqPayPayment.ValidateAmount(null, 1500m));
        Assert.Equal(300.13m, LiqPayPayment.ValidateAmount(300.126m, 1500m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1500.01)]
    public void ValidateAmount_RejectsZeroAndMoreThanTotal(decimal amount) =>
        Assert.Throws<DomainException>(() => LiqPayPayment.ValidateAmount(amount, 1500m));

    [Fact]
    public void Description_IsOldSiteText() =>
        Assert.Equal("Оплата замовлення #2045.\nПокупець: Ірина Іваненко. \nТелефон: +380671112233",
            LiqPayPayment.Description(2045, " Ірина Іваненко ", "+380671112233"));

    [Theory]
    [InlineData(null, "payment_not_found", LiqPayState.NotFound)]
    [InlineData("success", null, LiqPayState.Paid)]
    [InlineData("sandbox", null, LiqPayState.Paid)]
    [InlineData("wait_accept", null, LiqPayState.Paid)]
    [InlineData("reversed", null, LiqPayState.Reversed)]
    [InlineData("failure", null, LiqPayState.Failed)]
    [InlineData("3ds_verify", null, LiqPayState.Waiting)]
    public void Interpret_MapsLiqPayStatus(string? status, string? error, LiqPayState expected) =>
        Assert.Equal(expected, LiqPayPayment.Interpret(status, error));

    [Fact]
    public void Interpret_UnknownError_IsDomainError() =>
        Assert.Throws<DomainException>(() => LiqPayPayment.Interpret(null, "invalid_signature"));
}
