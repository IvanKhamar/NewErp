using NewErp.Domain.Common;
using NewErp.Domain.Marketing;

namespace NewErp.UnitTests.Domain;

/// <summary>Як GiftVoucherTest у Java-версії.</summary>
public class GiftVoucherTests
{
    [Fact]
    public void Of_CleansAndChecksTheForm()
    {
        var voucher = GiftVoucher.Of(" vidro2026 ", null, null, " Олена ", "", null, " З днем народження! ", 1000.50m, null);
        Assert.Equal(new GiftVoucher("VIDRO2026", "", "", "Олена", "", 0, "З днем народження!", 1000.50m, true), voucher);

        Assert.Equal("Код сертифіката — від 1 до 10 латинських літер або цифр.",
            Assert.Throws<DomainException>(() => GiftVoucher.Of("TOO-LONG-CODE", "", "", "Олена", "", 0, "", 10m, true)).Message);
        Assert.Throws<DomainException>(() => GiftVoucher.Of("ПОДАРУНОК", "", "", "Олена", "", 0, "", 10m, true));
        Assert.Equal("Сума сертифіката — більше 0, не більше двох знаків після коми.",
            Assert.Throws<DomainException>(() => GiftVoucher.Of("ABC", "", "", "Олена", "", 0, "", 0m, true)).Message);
        Assert.Equal("Сума сертифіката — більше 0, не більше двох знаків після коми.",
            Assert.Throws<DomainException>(() => GiftVoucher.Of("ABC", "", "", "Олена", "", 0, "", 10.005m, true)).Message);
        Assert.Equal("Вкажіть, кому сертифікат.",
            Assert.Throws<DomainException>(() => GiftVoucher.Of("ABC", "Іван", "", " ", "", 0, "", 10m, true)).Message);
    }

    [Fact]
    public void NewCode_AvoidsLookAlikes() =>
        Assert.Matches("^[A-HJ-NP-Z2-9]{8}$", GiftVoucher.NewCode(new Random(42)));

    [Fact]
    public void BalanceAndUsability()
    {
        Assert.Equal(650m, GiftVoucher.Balance(1000m, -350m));
        Assert.Equal(1000m, GiftVoucher.Balance(1000m, null));
        Assert.True(GiftVoucher.Usable(true, 0, false));     // створений в адмінці, замовлення за ним немає
        Assert.False(GiftVoucher.Usable(true, 2001, false)); // куплений у замовленні, яке ще не оплачене
        Assert.True(GiftVoucher.Usable(true, 2001, true));
        Assert.False(GiftVoucher.Usable(false, 0, true));
    }
}
