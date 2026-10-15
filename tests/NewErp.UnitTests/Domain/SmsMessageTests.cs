using NewErp.Domain.Common;
using NewErp.Domain.Sms;

namespace NewErp.UnitTests.Domain;

public class SmsMessageTests
{
    [Theory]
    [InlineData("+38 (067) 123-45-67")]
    [InlineData("0671234567")]
    [InlineData("380671234567")]
    public void NormalizePhone_BringsUkrainianFormatsTo380(string phone) =>
        Assert.Equal("380671234567", SmsMessage.NormalizePhone(phone));

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("+48 601 234 567")]
    public void NormalizePhone_RejectsNonUkrainianOrBroken(string phone) =>
        Assert.Throws<DomainException>(() => SmsMessage.NormalizePhone(phone));

    [Fact]
    public void RequireText_TrimsAndRejectsEmptyOrTooLong()
    {
        Assert.Equal("Ваше замовлення відправлено", SmsMessage.RequireText("  Ваше замовлення відправлено "));
        Assert.Throws<DomainException>(() => SmsMessage.RequireText("   "));
        Assert.Throws<DomainException>(() => SmsMessage.RequireText(new string('x', SmsMessage.MaxLength + 1)));
    }
}
