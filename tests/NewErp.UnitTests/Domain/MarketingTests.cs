using NewErp.Domain.Common;
using NewErp.Domain.Marketing;

namespace NewErp.UnitTests.Domain;

public class MarketingTests
{
    private static readonly DateOnly Today = new(2026, 12, 28);

    [Theory]
    [InlineData(28, 12, BirthdayPeriod.Today, true)]
    [InlineData(29, 12, BirthdayPeriod.Tomorrow, true)]
    [InlineData(28, 12, BirthdayPeriod.Week, true)]   // сьогоднішні теж у тижні
    [InlineData(3, 1, BirthdayPeriod.Week, true)]     // тиждень переходить через Новий рік
    [InlineData(5, 1, BirthdayPeriod.Week, false)]
    [InlineData(1, 12, BirthdayPeriod.Month, true)]
    [InlineData(31, 4, BirthdayPeriod.All, false)]    // неможлива дата
    public void InPeriod(int day, int month, BirthdayPeriod period, bool expected) =>
        Assert.Equal(expected, Birthdays.InPeriod(day, month, period, Today));

    [Fact]
    public void Next_MovesFebruary29ToTheLastDayInCommonYear()
    {
        Assert.Equal(new DateOnly(2027, 2, 28), Birthdays.Next(29, 2, Today));
        Assert.Equal(new DateOnly(2027, 1, 3), Birthdays.Next(3, 1, Today));
    }

    [Fact]
    public void CouponCode_HasPrefixAndHex_AndIsValidated()
    {
        var code = BirthdayCoupon.NewCode(new Random(1));
        Assert.Matches("^DN[0-9A-F]{8}$", code);
        Assert.Equal("DNABC123", BirthdayCoupon.RequireCode(" dnabc123 "));
        Assert.Throws<DomainException>(() => BirthdayCoupon.RequireCode("DN-1"));
    }

    [Fact]
    public void Build_DeduplicatesCleanValues_AndTakesNameFromLatestOrder()
    {
        var orders = new[]
        {
            new OrderContact("+38 (067) 111-22-33", "old@test.ua", "Оля", "Стара", "Київ", new DateTime(2025, 1, 1)),
            new OrderContact("0671112233", "OLGA@test.ua", " Ольга ", "Нова", "Львів", new DateTime(2026, 1, 1)),
        };

        var rows = ContactExport.Build(ContactField.Phone, ["0671112233", "+380671112233", "12345", "380501234567"], orders);

        Assert.Equal(
            [new ContactRow("Ольга", "Нова", "Львів", "380671112233", "olga@test.ua"), new ContactRow("", "", "", "380501234567", "")],
            rows.OrderByDescending(r => r.FirstName).ToList());
        Assert.Equal(["380501234567", "380671112233"], rows.Select(r => r.Phone));
    }

    [Fact]
    public void Build_ByEmail_SkipsInvalid()
    {
        var rows = ContactExport.Build(ContactField.Email, ["a@b.ua", "A@B.ua", "not-an-email", "x@localhost"], []);

        Assert.Equal(["a@b.ua"], rows.Select(r => r.Email));
    }

    [Fact]
    public void Csv_HasHeaderAndEscapesFormulasAndSeparators()
    {
        var csv = ContactExport.ToCsv(ContactField.Email, [new ContactRow("=HYPERLINK(1)", "Ко;ва", "", "380671112233", "a@b.ua")]);

        Assert.Equal("Ім'я;Прізвище;Місто;E-mail;Телефон\r\n'=HYPERLINK(1);\"Ко;ва\";;a@b.ua;380671112233\r\n", csv);
    }
}
