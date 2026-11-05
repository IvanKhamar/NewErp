using NewErp.Domain.Common;
using NewErp.Domain.Marketing;

namespace NewErp.UnitTests.Domain;

public class CouponTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateOnly End = new(2026, 11, 30);

    private static Coupon Make(
        string? name = "Осінь", string? code = "autumn10", string? type = "P", decimal? discount = 10, decimal? total = 0,
        DateOnly? start = null, DateOnly? end = null, bool noStart = false, int usesTotal = 0, int[]? products = null, int[]? categories = null,
        int[]? excludedProducts = null, int[]? excludedCategories = null) =>
        Coupon.Of(name, code, type, discount, total, false, false, noStart ? null : start ?? Start, end ?? End, usesTotal, 1, true,
            products, categories, excludedProducts, excludedCategories);

    [Fact]
    public void Code_IsUppercase_AndIdsLoseRepeatsAndZeros()
    {
        var coupon = Make(products: [5, 5, 0, 3]);

        Assert.Equal("AUTUMN10", coupon.Code);
        Assert.Equal([5, 3], coupon.ProductIds);
    }

    [Fact]
    public void FixedSum_MayBeAbove100() => Assert.Equal(500m, Make(type: "F", discount: 500).Discount);

    [Fact]
    public void BadValues_AreRefused()
    {
        void Refused(string message, Func<Coupon> make) =>
            Assert.Contains(message, Assert.Throws<DomainException>(() => make()).Message);

        Refused("Назва купона", () => Make(name: "Оу"));
        Refused("Код купона", () => Make(code: "a-1"));
        Refused("Тип знижки", () => Make(type: "X"));
        Refused("не більше 100", () => Make(discount: 101));
        Refused("Знижка", () => Make(discount: 0));
        Refused("Знижка", () => Make(discount: 10.555m));
        Refused("Мінімальна сума", () => Make(total: -1));
        Refused("закінчитися раніше", () => Make(end: new DateOnly(2026, 10, 1)));
        Refused("з якого й до якого", () => Make(noStart: true));
        Refused("від'ємною", () => Make(usesTotal: -1));
        Refused("водночас", () => Make(categories: [5], excludedCategories: [5]));
        Refused("водночас", () => Make(products: [7], excludedProducts: [7]));
    }
}
