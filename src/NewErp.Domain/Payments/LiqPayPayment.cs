using System.Globalization;

using NewErp.Domain.Common;

namespace NewErp.Domain.Payments;

/// <summary>Стан оплати замовлення в LiqPay, зведений до того, що важливо менеджеру.</summary>
public enum LiqPayState
{
    /// <summary>Платежу з таким номером замовлення LiqPay не знає: клієнт ще не платив.</summary>
    NotFound,
    /// <summary>Платіж почато, але гроші ще не зараховано (очікує підтвердження, 3DS тощо).</summary>
    Waiting,
    Paid,
    Failed,
    Reversed,
}

/// <summary>Правила оплати замовлення через LiqPay, як у catalog/controller/liqpay/payment.php старого сайту.</summary>
public static class LiqPayPayment
{
    /// <summary>Сума посилання: типово вся сума замовлення; можна менше (передоплата), але не більше й не нуль.</summary>
    public static decimal ValidateAmount(decimal? amount, decimal orderTotal)
    {
        var value = Math.Round(amount ?? orderTotal, 2);
        if (value <= 0)
            throw new DomainException("Сума оплати має бути більшою за нуль.");
        if (value > orderTotal)
            throw new DomainException($"Сума оплати не може перевищувати суму замовлення ({orderTotal.ToString("0.00", CultureInfo.InvariantCulture)}).");
        return value;
    }

    /// <summary>Призначення платежу — той самий текст, що формував старий сайт.</summary>
    public static string Description(int orderId, string customer, string telephone) =>
        $"Оплата замовлення #{orderId}.\nПокупець: {customer.Trim()}. \nТелефон: {telephone.Trim()}";

    /// <summary>
    /// Відповідь LiqPay на запит action=status: поле status, а для незнайденого платежу — err_code payment_not_found.
    /// Тестові (sandbox) оплати рахуємо оплаченими, щоб перевірку можна було пройти в тестовому режимі.
    /// </summary>
    public static LiqPayState Interpret(string? status, string? errorCode)
    {
        if (errorCode == "payment_not_found")
            return LiqPayState.NotFound;
        return status switch
        {
            "success" or "sandbox" or "wait_accept" or "wait_compensation" => LiqPayState.Paid,
            "reversed" => LiqPayState.Reversed,
            "failure" or "error" => LiqPayState.Failed,
            null or "" when errorCode is not null => throw new DomainException($"LiqPay відповів помилкою: {errorCode}."),
            _ => LiqPayState.Waiting,
        };
    }

    public static string Label(LiqPayState state) => state switch
    {
        LiqPayState.NotFound => "Ще не оплачено",
        LiqPayState.Waiting => "Оплата в процесі",
        LiqPayState.Paid => "Оплачено",
        LiqPayState.Failed => "Оплата не вдалася",
        LiqPayState.Reversed => "Гроші повернено",
        _ => state.ToString(),
    };
}
