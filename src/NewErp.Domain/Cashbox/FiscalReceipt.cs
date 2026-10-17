using System.Net;
using System.Text.RegularExpressions;

using NewErp.Domain.Common;

namespace NewErp.Domain.Cashbox;

/// <summary>Як покупець платив: Checkbox розрізняє лише готівку й безготівку.</summary>
public enum ReceiptPaymentType
{
    Cash,
    Cashless,
}

/// <summary>Рядок замовлення, з якого будується чек.</summary>
public sealed record ReceiptSourceLine(int ProductId, string Name, decimal Price, int Quantity);

/// <summary>Товар у чеку. Гроші — у копійках, кількість — у тисячних (так їх приймає API Checkbox).</summary>
public sealed record ReceiptGood(string Code, string Name, long PriceKopecks, long QuantityThousandths);

public sealed record ReceiptPayment(ReceiptPaymentType Type, string Label, long ValueKopecks);

/// <summary>Чек продажу для Checkbox (POST /api/v1/receipts/sell).</summary>
public sealed record FiscalReceipt(
    string CashierName,
    IReadOnlyList<ReceiptGood> Goods,
    ReceiptPayment Payment,
    long DiscountKopecks,
    string Footer,
    string? Email)
{
    /// <summary>Коди оплати OpenCart, які Checkbox має бачити як безготівку. Решта — готівка (як на старому сайті).</summary>
    public static readonly IReadOnlySet<string> CashlessPaymentCodes =
        new HashSet<string> { "wayforpay", "liqpay", "mono", "bank_transfer", "paid", "paid_shipping", "cod" };

    public const string CashOnDeliveryCode = "cod";

    /// <summary>
    /// Будує чек так само, як старий сайт: товари з ціною (нульові пропускаються), одна оплата на суму до сплати,
    /// і знижка на різницю, якщо товари дорожчі (купон, завдаток).
    /// </summary>
    public static FiscalReceipt Build(
        int orderId, string cashierName, IReadOnlyList<ReceiptSourceLine> lines, decimal sum,
        string paymentCode, string paymentMethod, string? email)
    {
        var goods = lines
            .Where(l => l.Price != 0)
            .Select(l => new ReceiptGood(l.ProductId.ToString(), l.Name.Trim(), Kopecks(l.Price), l.Quantity * 1000L))
            .ToList();
        if (goods.Count == 0)
            throw new DomainException("У замовленні немає товарів з ціною, чек видати нема з чого.");

        var total = Kopecks(sum);
        if (total <= 0)
            throw new DomainException("Сума до оплати нульова (можливо, замовлення повністю оплачене завдатком), чек не потрібен.");

        var goodsTotal = goods.Sum(g => g.PriceKopecks * g.QuantityThousandths / 1000);
        // Старий сайт у такому разі надсилав чек як є, і Checkbox його відхиляв. Пояснюємо одразу.
        if (total > goodsTotal)
            throw new DomainException(
                $"Сума до оплати ({sum:0.00}) більша за суму товарів ({goodsTotal / 100m:0.00}): мабуть, у замовленні є платна доставка. " +
                "Checkbox такий чек не прийме.");

        var type = CashlessPaymentCodes.Contains(paymentCode) ? ReceiptPaymentType.Cashless : ReceiptPaymentType.Cash;
        var label = paymentCode == CashOnDeliveryCode ? "Оплата в Nova pay" : PlainText(paymentMethod);
        if (label == "")
            label = type == ReceiptPaymentType.Cash ? "Готівка" : "Безготівкова оплата";

        return new FiscalReceipt(
            cashierName, goods, new ReceiptPayment(type, label, total), goodsTotal - total,
            $"Замовлення № {orderId}", string.IsNullOrWhiteSpace(email) ? null : email.Trim());
    }

    private static long Kopecks(decimal value) => (long)Math.Round(value * 100, MidpointRounding.AwayFromZero);

    /// <summary>Назва способу оплати в OpenCart може містити HTML (іконки, &amp;quot;): у чек іде лише текст.</summary>
    private static string PlainText(string html) =>
        Regex.Replace(Regex.Replace(WebUtility.HtmlDecode(html ?? ""), "<[^>]*>", ""), @"\s+", " ").Trim();
}
