using NewErp.Domain.Payments;

namespace NewErp.UnitTests.Domain;

public class BankPaymentTests
{
    private static BankPayment Payment(decimal amount, string description, string payer = "") =>
        new("tx-1", "UA00", amount, 10_000m, description, payer, new DateOnly(2026, 10, 10), new TimeOnly(14, 30));

    private static OrderCandidate Order(int id, decimal total, string customer = "Тарас Клієнт", string phone = "+380670000000", decimal prepayment = 0, int? linked = null) =>
        new(id, customer, phone, total, prepayment, new DateTime(2026, 10, 1).AddDays(id % 10), "2) Прийнято", linked);

    [Theory]
    [InlineData("1500.00 UAH", 1500)]
    [InlineData("1 234,50 UAH", 1234.5)]
    [InlineData("99", 99)]
    [InlineData("—", 0)]
    public void ParseMoney_ReadsOldSiteText(string text, decimal expected) => Assert.Equal(expected, BankPayment.ParseMoney(text));

    [Fact]
    public void NewPayment_StoresMoneyAsOldSiteDoes()
    {
        var payment = Payment(1500m, "");

        Assert.Equal(("1500.00 UAH", "1500.00 UAH", "10000.00 UAH"), (payment.AmountText, payment.CardAmountText, payment.RestText));
        Assert.Equal(1500m, payment.Amount);
        Assert.Equal(new DateTime(2026, 10, 10, 14, 30, 0), payment.Timestamp);
    }

    [Fact]
    public void OrderNumbersIn_FindsNumbers_ButNotAmounts()
    {
        Assert.Equal([12345, 777], PaymentMatcher.OrderNumbersIn("Оплата зам.№12345, 777 шт; сума 1500.00 грн, 2,50"));
    }

    [Fact]
    public void Rank_OrderNumberAndPhoneBeatSameAmount()
    {
        var payment = Payment(1500m, "Оплата замовлення 2045 від Коваленко, тел 067 000 00 99");
        var sameAmount = Order(2001, 1500m);
        var byNumber = Order(2045, 1450m, "Ірина Коваленко", "+380670000099");
        var paidAlready = Order(2002, 1500m, linked: 5);

        var ranked = PaymentMatcher.Rank(payment, [sameAmount, paidAlready, byNumber]);

        Assert.Equal([2045, 2001, 2002], ranked.Select(r => r.Order.OrderId));
        Assert.Equal(MatchReason.OrderNumber | MatchReason.Phone | MatchReason.Name, ranked[0].Reasons);
        Assert.Equal(MatchReason.Total, ranked[1].Reasons);
    }

    [Fact]
    public void Rank_RecognisesPrepaymentAndRest()
    {
        var payment = Payment(300m, "Передоплата");

        var ranked = PaymentMatcher.Rank(payment, [Order(1, 1500m, prepayment: 300m), Order(2, 1500m, prepayment: 1200m)]);

        Assert.All(ranked, r => Assert.Equal(MatchReason.Prepayment, r.Reasons));
    }
}
