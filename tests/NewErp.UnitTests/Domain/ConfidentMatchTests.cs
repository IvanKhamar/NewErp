using NewErp.Domain.Payments;

namespace NewErp.UnitTests.Domain;

public class ConfidentMatchTests
{
    private static ScoredCandidate Candidate(int orderId, MatchReason reasons, int? linked = null) =>
        new(new OrderCandidate(orderId, "Тарас Клієнт", "0670000002", 2000, 0, new DateTime(2026, 10, 1), "Нове", linked), reasons, 0);

    [Fact]
    public void Confident_MeansTheOrderIsNamed_AndTheSumFits()
    {
        Assert.True(PaymentMatcher.IsConfident(Candidate(5, MatchReason.OrderNumber | MatchReason.Total)));
        Assert.True(PaymentMatcher.IsConfident(Candidate(5, MatchReason.Phone | MatchReason.Prepayment)));
        Assert.False(PaymentMatcher.IsConfident(Candidate(5, MatchReason.Name | MatchReason.Total))); // тезка з тією самою сумою
        Assert.False(PaymentMatcher.IsConfident(Candidate(5, MatchReason.OrderNumber))); // сума інша
        Assert.False(PaymentMatcher.IsConfident(Candidate(5, MatchReason.Phone | MatchReason.Total, linked: 7))); // уже оплачено
    }

    [Fact]
    public void OnlyExactlyOneConfidentOrder_IsLinked()
    {
        var sure = Candidate(5, MatchReason.OrderNumber | MatchReason.Total);
        var other = Candidate(6, MatchReason.Phone | MatchReason.Total);
        var weak = Candidate(7, MatchReason.Name);

        Assert.Equal(5, PaymentMatcher.ConfidentMatch([sure, weak])!.OrderId);
        Assert.Null(PaymentMatcher.ConfidentMatch([sure, other])); // двоє — питання для людини
        Assert.Null(PaymentMatcher.ConfidentMatch([weak]));
    }
}
