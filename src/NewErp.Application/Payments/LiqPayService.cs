using NewErp.Application.Common;
using NewErp.Application.Orders;
using NewErp.Domain.Payments;

namespace NewErp.Application.Payments;

/// <summary>
/// Посилання на оплату замовлення карткою через LiqPay і перевірка, чи його оплачено.
/// Старий сайт давав клієнту ту саму форму на сторінці оформлення; тут менеджер може надіслати посилання будь-коли.
/// </summary>
public sealed class LiqPayService(IOrderQueries orders, ILiqPayGateway gateway)
{
    public async Task<PaymentLinkDto> CreateLinkAsync(int orderId, CreatePaymentLinkRequest request, CancellationToken ct)
    {
        var order = await orders.GetAsync(orderId, ct) ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        var amount = LiqPayPayment.ValidateAmount(request.Amount, order.Total);
        var currency = string.IsNullOrWhiteSpace(order.CurrencyCode) ? "UAH" : order.CurrencyCode;
        var checkout = new LiqPayCheckout(orderId.ToString(), amount, currency,
            LiqPayPayment.Description(orderId, order.Customer, order.Telephone));
        return new PaymentLinkDto(gateway.CheckoutUrl(checkout), amount, currency, gateway.IsTest);
    }

    public async Task<LiqPayStatusDto> StatusAsync(int orderId, CancellationToken ct)
    {
        _ = await orders.GetAsync(orderId, ct) ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        var reply = await gateway.StatusAsync(orderId.ToString(), ct);
        var state = LiqPayPayment.Interpret(reply.Status, reply.ErrorCode);
        return new LiqPayStatusDto(state, LiqPayPayment.Label(state), reply.Amount,
            state == LiqPayState.Paid ? reply.EndDate : null, gateway.IsTest);
    }
}
