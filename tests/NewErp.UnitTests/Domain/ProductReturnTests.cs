using NewErp.Domain.Common;
using NewErp.Domain.Returns;

namespace NewErp.UnitTests.Domain;

public class ProductReturnTests
{
    private static readonly ReturnableLine[] Lines = [new(101, "Сукня", "SL-01", 2), new(102, "Пояс", "P-01", 1), new(101, "Сукня", "SL-01", 1)];

    private static ReturnableLine Validate(int productId = 101, int quantity = 1, int reasonId = 1, int alreadyReturned = 0,
        string firstname = "Тарас", string telephone = "0671234567", string email = "") =>
        ProductReturn.Validate(firstname, "", telephone, email, Lines, productId, quantity, reasonId, null, alreadyReturned);

    [Fact]
    public void ProductFromOrder_UpToBoughtMinusReturned_AcrossSameProductLines()
    {
        Assert.Equal("Сукня", Validate(quantity: 3).Name);
        Assert.Contains("від 1 до 3", Assert.Throws<DomainException>(() => Validate(quantity: 4)).Message);
        Assert.Contains("від 1 до 1", Assert.Throws<DomainException>(() => Validate(quantity: 2, alreadyReturned: 2)).Message);
        Assert.Contains("повністю", Assert.Throws<DomainException>(() => Validate(productId: 102, alreadyReturned: 1)).Message);
        Assert.Contains("немає в замовленні", Assert.Throws<DomainException>(() => Validate(productId: 999)).Message);
        Assert.Throws<DomainException>(() => Validate(quantity: 0));
    }

    [Fact]
    public void CustomerAndReason_CheckedLikeOldForm_ButEmailOptional()
    {
        Assert.Equal("Сукня", Validate(email: "").Name);
        Assert.Contains("Ім'я", Assert.Throws<DomainException>(() => Validate(firstname: " ")).Message);
        Assert.Contains("Телефон", Assert.Throws<DomainException>(() => Validate(telephone: "12")).Message);
        Assert.Contains("причину", Assert.Throws<DomainException>(() => Validate(reasonId: 0)).Message);
        Assert.Contains("E-mail", Assert.Throws<DomainException>(() => Validate(email: new string('a', 97))).Message);
    }
}
