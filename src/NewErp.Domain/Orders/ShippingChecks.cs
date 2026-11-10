namespace NewErp.Domain.Orders;

/// <summary>Рівень проблеми перевірки відправки, у JSON рядком.</summary>
public static class IssueLevel
{
    /// <summary>Так замовлення відправляти не можна.</summary>
    public const string Danger = "danger";

    /// <summary>Варто глянути.</summary>
    public const string Warning = "warning";
}

public sealed record ShippingIssue(string Level, string Text);

/// <summary>
/// Перевірка перед «Відправлено» й «Чекає на відправку» та кнопка «Розбіжності» старої адмінки
/// (checkShipping і getDiscrepancies). Перенесено з Python-версії (пункти 36–37).
/// </summary>
public static class ShippingChecks
{
    public const int AcceptedStatusId = 18;          // «Прийнято»
    public const int AwaitingShipmentStatusId = 21;  // «Чекає на відправку»: лише самовивози, передані в шоурум
    public const int ShippedStatusId = 22;           // «Відправлено»
    public const int UrgentStatusId = 23;            // «Терміново»

    /// <summary>Нова Пошта по Україні: відділення, поштомат, кур'єр. Самовивозу, закордону й партнерам ТТН не потрібна.</summary>
    public static readonly IReadOnlyList<string> NovaPoshtaShippingCodes =
    [
        "novaposhta_warehouse.novaposhta_warehouse",
        "novaposhta_postomat.novaposhta_postomat",
        NovaPoshtaDoorCode,
    ];

    public const string NovaPoshtaDoorCode = "novaposhta_address.novaposhta_address";

    /// <summary>ТТН, старша за стільки годин, коли замовлення досі «Прийнято» чи «Терміново», — розбіжність.</summary>
    public const int WaybillStaleHours = 4;

    public const int DiscrepancyDays = 3;
    public const int DiscrepancyMaxDays = 30;

    public static string WaybillDigits(string? number) => new((number ?? "").Where(char.IsAsciiDigit).ToArray());

    /// <summary>
    /// Перед «Відправлено»: замовленню Новою Поштою потрібна ТТН, а SMS з її номером має піти на телефон замовлення.
    /// smsPhones — куди пішла SMS з цією ТТН відтоді, як оформили замовлення.
    /// </summary>
    public static IReadOnlyList<ShippingIssue> ShippedIssues(string shippingCode, string telephone, string waybill, IReadOnlyList<string> smsPhones)
    {
        if (!NovaPoshtaShippingCodes.Contains(shippingCode))
            return [];
        if (waybill.Length == 0)
            return [new(IssueLevel.Danger, "ТТН для цього замовлення не створена")];
        if (smsPhones.Count == 0)
            return [new(IssueLevel.Warning, "SMS з номером ТТН клієнту не надсилалась")];

        var orderPhone = CustomerRisk.PhoneKey(telephone);
        if (smsPhones.Any(phone => CustomerRisk.PhoneKey(phone) == orderPhone))
            return [];
        var others = string.Join(", ", smsPhones.Distinct());
        return [new(IssueLevel.Danger, $"SMS з цією ТТН пішла на номер {others}, а в замовленні {telephone}")];
    }

    /// <summary>Перед «Чекає на відправку», який ставиться лише самовивозам, переданим у шоурум.</summary>
    public static IReadOnlyList<ShippingIssue> PickupIssues(string shippingCode, string shippingMethod, string waybill)
    {
        if (waybill.Length > 0)
            return [new(IssueLevel.Danger, "У замовлення є ТТН Нової Пошти. «Чекає на відправку» ставиться лише самовивозам, переданим у шоурум")];
        if (NovaPoshtaShippingCodes.Contains(shippingCode))
            return [new(IssueLevel.Warning, $"Спосіб доставки в замовленні: {shippingMethod}. Перевірте, чи це справді самовивіз")];
        return [];
    }
}

/// <summary>Товар замовлення з вагою (кг) і розмірами (см) з каталогу, разом із кількістю.</summary>
public sealed record ParcelItem(int Quantity, decimal Weight, decimal Length, decimal Width, decimal Height);

/// <summary>Вага й об'єм посилки для тарифу Нової Пошти (кнопка швидкого замовлення старого сайту, пункт 41).</summary>
public static class Parcel
{
    /// <summary>У кг, не менше 0,1 (мінімум Нової Пошти). Старий сайт рахував кожен товар один раз; тут — на кількість.</summary>
    public static decimal Weight(IEnumerable<ParcelItem> items) =>
        Math.Max(items.Sum(i => i.Weight * i.Quantity), 0.1m);

    /// <summary>У кубометрах, кожен розмір округлено вгору до цілого сантиметра, як на старому сайті.</summary>
    public static decimal Volume(IEnumerable<ParcelItem> items) =>
        items.Sum(i => i.Quantity * Math.Ceiling(i.Length) * Math.Ceiling(i.Width) * Math.Ceiling(i.Height)) / 1_000_000m;
}
