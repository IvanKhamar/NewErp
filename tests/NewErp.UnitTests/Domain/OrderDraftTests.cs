using NewErp.Domain.Common;
using NewErp.Domain.Orders;

namespace NewErp.UnitTests.Domain;

public class OrderDraftTests
{
    private static OrderDraft Draft(decimal shipping = 0, decimal discount = 0, decimal prepayment = 0, params OrderDraftLine[] lines) =>
        OrderDraft.Create("Тарас", "", "Нова Пошта", "Накладений платіж",
            lines.Length > 0 ? lines : [new OrderDraftLine(1, 10, 2, 500)], shipping, discount, prepayment);

    [Fact]
    public void Totals_AreGoodsPlusShippingMinusDiscount()
    {
        var draft = Draft(shipping: 80, discount: 100, lines: [new(1, 10, 2, 500), new(2, null, 1, 300)]);

        Assert.Equal(1300, draft.SubTotal);
        Assert.Equal(1280, draft.Total);
    }

    [Theory]
    [InlineData(0, 0, 1001, "завдатку")]
    [InlineData(0, 1001, 0, "Знижка")]
    [InlineData(-1, 0, 0, "від'ємними")]
    public void Create_RejectsWrongAmounts(decimal shipping, decimal discount, decimal prepayment, string message)
    {
        var error = Assert.Throws<DomainException>(() => Draft(shipping, discount, prepayment));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void Create_PrepaymentEqualToTotal_IsAllowed() => Assert.Equal(1000, Draft(prepayment: 1000).Prepayment);

    [Fact]
    public void Create_RequiresCartMethodsAndContact()
    {
        Assert.Contains("Кошик", Assert.Throws<DomainException>(() =>
            OrderDraft.Create("Тарас", "", "НП", "Готівка", [], 0, 0, 0)).Message);
        Assert.Contains("доставки", Assert.Throws<DomainException>(() =>
            OrderDraft.Create("Тарас", "", " ", "Готівка", [new(1, null, 1, 1)], 0, 0, 0)).Message);
        Assert.Contains("оплати", Assert.Throws<DomainException>(() =>
            OrderDraft.Create("Тарас", "", "НП", "", [new(1, null, 1, 1)], 0, 0, 0)).Message);
        Assert.Contains("телефон", Assert.Throws<DomainException>(() =>
            OrderDraft.Create("", "", "НП", "Готівка", [new(1, null, 1, 1)], 0, 0, 0)).Message);
        Assert.Contains("Кількість", Assert.Throws<DomainException>(() =>
            OrderDraft.Create("", "0671234567", "НП", "Готівка", [new(1, null, 0, 1)], 0, 0, 0)).Message);
    }
}

public class CustomerRiskTests
{
    [Theory]
    [InlineData("+38 (067) 123-45-67", "671234567")]
    [InlineData("0671234567", "671234567")]
    [InlineData("12345", null)]
    [InlineData(null, null)]
    public void PhoneKey_IsLastNineDigits(string? phone, string? key) => Assert.Equal(key, CustomerRisk.PhoneKey(phone));

    [Fact]
    public void From_NoOrders_IsNewCustomer() => Assert.Equal(CustomerRiskLevel.New, CustomerRisk.From([]).Level);

    [Theory]
    [InlineData(new[] { 22, 22 }, CustomerRiskLevel.Green, 0)]
    [InlineData(new[] { 22, 22, 19 }, CustomerRiskLevel.Yellow, 33.33)]
    [InlineData(new[] { 22, 19 }, CustomerRiskLevel.Red, 50)]
    public void From_LevelDependsOnCancelledShare(int[] statuses, CustomerRiskLevel level, decimal rate)
    {
        var risk = CustomerRisk.From(statuses.Select((s, i) => (OrderId: 100 + i, StatusId: s)).ToList());

        Assert.Equal((level, rate, statuses.Length), (risk.Level, risk.SuspicionRate, risk.TotalOrders));
        Assert.All(risk.CancelledOrderIds, id => Assert.Equal(CustomerRisk.CancelledStatusId, statuses[id - 100]));
    }
}
