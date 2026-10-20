using NewErp.Application.Common;
using NewErp.Application.Orders;
using NewErp.Application.Payments;
using NewErp.Domain.Common;
using NewErp.Domain.Payments;

namespace NewErp.UnitTests.Application;

public class LiqPayServiceTests
{
    private readonly FakeGateway _gateway = new();
    private readonly LiqPayService _service;

    public LiqPayServiceTests() => _service = new LiqPayService(new FakeOrders(), _gateway);

    [Fact]
    public async Task CreateLink_PassesShopOrderIdAndOldDescription()
    {
        var link = await _service.CreateLinkAsync(7, new CreatePaymentLinkRequest(null), CancellationToken.None);

        Assert.Equal(("https://pay/7", 1200m, "UAH", true), (link.Url, link.Amount, link.Currency, link.IsTest));
        Assert.Equal(new LiqPayCheckout("7", 1200m, "UAH", "Оплата замовлення #7.\nПокупець: Ірина Іваненко. \nТелефон: +380671112233"),
            _gateway.Checkout);
    }

    [Fact]
    public async Task CreateLink_UnknownOrderOrTooMuch_Fails()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateLinkAsync(99, new CreatePaymentLinkRequest(null), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateLinkAsync(7, new CreatePaymentLinkRequest(5000m), CancellationToken.None));
    }

    [Fact]
    public async Task Status_PaidShowsDate_NotFoundDoesNot()
    {
        var paidAt = new DateTime(2026, 10, 20, 10, 0, 0);
        _gateway.Reply = new LiqPayStatusReply("success", null, 1200m, paidAt);
        var paid = await _service.StatusAsync(7, CancellationToken.None);
        Assert.Equal((LiqPayState.Paid, "Оплачено", 1200m, paidAt), (paid.State, paid.Label, paid.Amount, paid.PaidAt));

        _gateway.Reply = new LiqPayStatusReply(null, "payment_not_found", null, paidAt);
        var none = await _service.StatusAsync(7, CancellationToken.None);
        Assert.Equal((LiqPayState.NotFound, (DateTime?)null), (none.State, none.PaidAt));
    }

    private sealed class FakeGateway : ILiqPayGateway
    {
        public LiqPayCheckout? Checkout { get; private set; }

        public LiqPayStatusReply Reply { get; set; } = new(null, "payment_not_found", null, null);

        public bool IsTest => true;

        public string CheckoutUrl(LiqPayCheckout checkout)
        {
            Checkout = checkout;
            return $"https://pay/{checkout.OrderId}";
        }

        public Task<LiqPayStatusReply> StatusAsync(string orderId, CancellationToken ct) => Task.FromResult(Reply);
    }

    private sealed class FakeOrders : IOrderQueries
    {
        public Task<OrderDetails?> GetAsync(int orderId, CancellationToken ct) => Task.FromResult(orderId != 7 ? null : new OrderDetails(
            7, new DateTime(2026, 10, 19), "Ірина Іваненко", "+380671112233", "", "", "", "", "", "", 1200m, "UAH", 1, null, 1, null, null, null, [], []));

        public Task<PagedResult<OrderListItem>> ListAsync(OrderFilter filter, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<OrderStatusDto>> ListStatusesAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<LookupItem>> ListSourcesAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<LookupItem>> ListManagersAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
