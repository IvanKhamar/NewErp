using System.Globalization;
using System.Text.RegularExpressions;

namespace NewErp.Domain.Payments;

/// <summary>
/// Надходження на рахунок чи картку ПриватБанку (dd_privatbank старого сайту).
/// Суми старий сайт зберігає текстом із валютою («1500.00 UAH»), тому числове значення розбирається з тексту.
/// </summary>
public class BankPayment
{
    private BankPayment() { }

    public BankPayment(
        string appCode, string card, decimal amount, decimal? balance, string description, string terminal, DateOnly date, TimeOnly time)
    {
        AppCode = appCode;
        Card = card;
        AmountText = Money(amount);
        CardAmountText = AmountText;
        RestText = Money(balance ?? 0);
        Description = description;
        Terminal = terminal.Length > 255 ? terminal[..255] : terminal;
        TranDate = date;
        TranTime = time;
    }

    public int Id { get; private set; }

    /// <summary>Замовлення, яке оплачено цим платежем; 0 — ще не зіставлено.</summary>
    public int OrderId { get; private set; }

    /// <summary>Id транзакції в банку: за ним імпорт не додає той самий платіж двічі.</summary>
    public string AppCode { get; private set; } = "";

    /// <summary>Рахунок чи картка, на яку надійшли гроші.</summary>
    public string Card { get; private set; } = "";

    public string AmountText { get; private set; } = "";

    public string CardAmountText { get; private set; } = "";

    /// <summary>Залишок на рахунку після операції.</summary>
    public string RestText { get; private set; } = "";

    /// <summary>Призначення платежу й реквізити платника.</summary>
    public string Description { get; private set; } = "";

    /// <summary>Платник (назва контрагента).</summary>
    public string Terminal { get; private set; } = "";

    public DateOnly TranDate { get; private set; }

    public TimeOnly TranTime { get; private set; }

    /// <summary>false — платіж приховано (не стосується замовлень: повернення, переказ між своїми рахунками).</summary>
    public bool IsVisible { get; private set; } = true;

    public decimal Amount => ParseMoney(AmountText);

    public DateTime Timestamp => TranDate.ToDateTime(TranTime);

    public void LinkTo(int orderId) => OrderId = orderId;

    public void Unlink() => OrderId = 0;

    public void Hide() => IsVisible = false;

    public void Show() => IsVisible = true;

    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " UAH";

    /// <summary>«1 500,00 UAH», «1500.00 UAH», «1500» → 1500. Нерозбірний текст — 0.</summary>
    public static decimal ParseMoney(string text)
    {
        var number = Regex.Replace(text ?? "", @"[^\d,.\-]", "").Replace(',', '.');
        return decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
