using NewErp.Domain.Cashbox;
using NewErp.Domain.Common;

namespace NewErp.UnitTests.Domain;

public class FiscalReceiptTests
{
    private static readonly ReceiptSourceLine Dress = new(101, " Сукня лляна ", 750m, 2);
    private static readonly ReceiptSourceLine Gift = new(102, "Подарунок", 0m, 1);

    [Fact]
    public void Build_KopecksAndThousandths_SkipsFreeItems_LikeOldSite()
    {
        var receipt = FiscalReceipt.Build(42, "Олена", [Dress, Gift], 1500m, "liqpay", "LiqPay", " a@b.ua ");

        var good = Assert.Single(receipt.Goods);
        Assert.Equal(new ReceiptGood("101", "Сукня лляна", 75000, 2000), good);
        Assert.Equal(new ReceiptPayment(ReceiptPaymentType.Cashless, "LiqPay", 150000), receipt.Payment);
        Assert.Equal((0L, "Замовлення № 42", "a@b.ua"), (receipt.DiscountKopecks, receipt.Footer, receipt.Email));
    }

    [Fact]
    public void Build_SumBelowGoods_AddsDiscountForDifference()
    {
        // Завдаток 300 уже отримано: чек на 1200, різниця — знижкою, як на старому сайті.
        var receipt = FiscalReceipt.Build(42, "Олена", [Dress], 1200m, "bank_transfer", "", null);

        Assert.Equal(30000, receipt.DiscountKopecks);
        Assert.Equal(120000, receipt.Payment.ValueKopecks);
        Assert.Null(receipt.Email);
    }

    [Theory]
    [InlineData("cod", "<b>Оплата</b> при отриманні", ReceiptPaymentType.Cashless, "Оплата в Nova pay")]
    [InlineData("cash", "Готівкою &quot;в магазині&quot; <img src=x>", ReceiptPaymentType.Cash, "Готівкою \"в магазині\"")]
    [InlineData("free_checkout", "", ReceiptPaymentType.Cash, "Готівка")]
    public void Build_PaymentTypeAndLabel(string code, string method, ReceiptPaymentType type, string label)
    {
        var receipt = FiscalReceipt.Build(1, "Олена", [Dress], 1500m, code, method, null);

        Assert.Equal((type, label), (receipt.Payment.Type, receipt.Payment.Label));
    }

    [Fact]
    public void Build_RejectsWhatCheckboxWouldReject()
    {
        Assert.Throws<DomainException>(() => FiscalReceipt.Build(1, "О", [Gift], 100m, "cod", "", null));   // нема товарів з ціною
        Assert.Throws<DomainException>(() => FiscalReceipt.Build(1, "О", [Dress], 0m, "cod", "", null));    // усе оплачено завдатком
        Assert.Throws<DomainException>(() => FiscalReceipt.Build(1, "О", [Dress], 1570m, "cod", "", null)); // платна доставка зверху
    }
}
