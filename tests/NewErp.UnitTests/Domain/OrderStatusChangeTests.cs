using NewErp.Domain.Common;
using NewErp.Domain.Orders;

namespace NewErp.UnitTests.Domain;

public class OrderStatusChangeTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0);

    [Fact]
    public void Create_RemembersOldAndNewStatus()
    {
        var change = OrderStatusChange.Create(orderId: 10, currentStatusId: 1, newStatusId: 5, userId: 3, Now);

        Assert.Equal(new OrderStatusChange(10, 1, 5, 3, Now), change);
    }

    [Fact]
    public void Create_SameStatus_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => OrderStatusChange.Create(10, 5, 5, 3, Now));
        Assert.Contains("вже має цей статус", ex.Message);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    [InlineData(5, 0)]
    public void Create_InvalidStatusOrUser_Throws(int newStatusId, int userId) =>
        Assert.Throws<DomainException>(() => OrderStatusChange.Create(10, 1, newStatusId, userId, Now));
}