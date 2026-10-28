namespace NewErp.Domain.Delivery;

/// <summary>Як виглядає статус ТТН у списку: так само розфарбовував старий сайт.</summary>
public enum ShipmentTone
{
    /// <summary>Ще в дорозі чи на відділенні.</summary>
    Info,

    /// <summary>Отримано (а з грошовим переказом — коли гроші вже повернулися відправнику).</summary>
    Success,

    /// <summary>Повернення чи переадресація.</summary>
    Warning,

    /// <summary>Відмова чи видалено.</summary>
    Error,
}

/// <summary>Що повідомив трекінг Нової Пошти про одну ТТН.</summary>
public sealed record TrackingInfo(
    string Number,
    string Status,
    int StatusCode,
    string RecipientAddress,
    bool Redelivery,
    DateTime? DateCreated,
    DateOnly? DateScheduled,
    DateOnly? DatePayedKeeping);

/// <summary>ТТН замовлення (dd_novaposhta_document старого сайту).</summary>
public class ShipmentDocument
{
    /// <summary>Коди, після яких ТТН більше не змінюється: отримано, відмова, повернення. Такі трекінг не перевіряє.</summary>
    public static readonly IReadOnlySet<int> FinalStatusCodes = new HashSet<int> { 9, 11, 102, 103, 104, 105, 107, 108 };

    /// <summary>Посилка чекає на відділенні: саме для них рахується початок платного зберігання.</summary>
    public static readonly IReadOnlySet<int> WaitingAtBranchCodes = new HashSet<int> { 7, 8 };

    public const string NovaPoshta = "novaposhta";

    /// <summary>Nova Post International (за кордон); внутрішній API їх не відстежує.</summary>
    public const string International = "international";

    private ShipmentDocument() { }

    /// <param name="documentRef">Ref документа в API Нової Пошти (потрібен для друку й видалення); якщо невідомий — номер ТТН.</param>
    public ShipmentDocument(int orderId, string number, decimal cost, string? documentRef = null, string carrier = NovaPoshta)
    {
        OrderId = orderId;
        Number = number;
        Ref = documentRef ?? number;
        Cost = cost;
        Carrier = carrier;
    }

    public int Id { get; private set; }

    public int OrderId { get; private set; }

    /// <summary>Порожньо або novaposhta — Нова Пошта; international — Nova Post International.</summary>
    public string Carrier { get; private set; } = "";

    public string Ref { get; private set; } = "";

    /// <summary>Номер ТТН (14 цифр у Нової Пошти).</summary>
    public string Number { get; private set; } = "";

    public decimal Cost { get; private set; }

    public string Status { get; private set; } = "";

    public int StatusCode { get; private set; }

    public string RecipientAddress { get; private set; } = "";

    /// <summary>Є зворотна доставка (наприклад, грошовий переказ за накладеним платежем).</summary>
    public bool Redelivery { get; private set; }

    public DateTime? DateCreated { get; private set; }

    public DateOnly? DateScheduled { get; private set; }

    public DateOnly? DatePayedKeeping { get; private set; }

    public bool IsFinal => FinalStatusCodes.Contains(StatusCode);

    public ShipmentTone Tone => StatusCode switch
    {
        9 when !Redelivery => ShipmentTone.Success,
        11 when Redelivery => ShipmentTone.Success,
        106 => ShipmentTone.Success,
        3 or 107 => ShipmentTone.Error,
        102 or 103 or 105 or 108 => ShipmentTone.Warning,
        _ => ShipmentTone.Info,
    };

    /// <summary>
    /// Застосовує відповідь трекінгу. Як і старий сайт, оновлює запис лише тоді, коли змінився код статусу.
    /// </summary>
    /// <returns>true, якщо статус змінився.</returns>
    public bool ApplyTracking(TrackingInfo info)
    {
        if (info.StatusCode == StatusCode)
            return false;

        Status = info.Status.Length > 255 ? info.Status[..255] : info.Status;
        StatusCode = info.StatusCode;
        RecipientAddress = info.RecipientAddress.Length > 255 ? info.RecipientAddress[..255] : info.RecipientAddress;
        Redelivery = info.Redelivery;
        DateCreated = info.DateCreated ?? DateCreated;
        DateScheduled = info.DateScheduled;
        DatePayedKeeping = info.DatePayedKeeping;
        return true;
    }
}
