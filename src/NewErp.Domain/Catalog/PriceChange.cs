using NewErp.Domain.Common;

namespace NewErp.Domain.Catalog;

/// <summary>
/// Масова зміна цін (Python first, пункт 46). Старий модуль price_control будував SQL зі значень запиту; тут дія й число
/// перевіряються до будь-якого запису.
/// </summary>
public static class PriceChange
{
    public const string Add = "add";
    public const string Subtract = "subtract";
    public const string Multiply = "multiply";
    public const string Divide = "divide";

    /// <summary>Гривні.</summary>
    public const string Amount = "amount";

    /// <summary>Відсоток від поточної ціни.</summary>
    public const string Percent = "percent";

    public static readonly IReadOnlyList<string> Operations = [Add, Subtract, Multiply, Divide];

    /// <summary>Нова ціна, округлена до цілих гривень, як ціни магазину, і не нижча за нуль.</summary>
    public static decimal Changed(decimal price, string operation, decimal amount, string unit)
    {
        if (amount < 0)
            throw new DomainException("Число має бути додатним.");
        if (unit is not (Amount or Percent))
            throw new DomainException("Одиниця: гривні (amount) або відсоток (percent).");
        var step = unit == Percent ? price * amount / 100 : amount;
        var result = operation switch
        {
            Add => price + step,
            Subtract => price - step,
            Multiply => price * amount,
            Divide when amount == 0 => throw new DomainException("На нуль ділити не можна."),
            Divide => price / amount,
            _ => throw new DomainException("Для ціни: додати, відняти, помножити або поділити."),
        };
        return Math.Round(Math.Max(result, 0), MidpointRounding.AwayFromZero);
    }

    public static decimal SalePercent(decimal percent) =>
        percent is > 0 and < 100 ? percent : throw new DomainException("Знижка має бути від 1 до 99 %.");
}
