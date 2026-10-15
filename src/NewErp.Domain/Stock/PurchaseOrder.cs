using NewErp.Domain.Common;

namespace NewErp.Domain.Stock;

/// <summary>
/// Замовлення постачальнику розхідників: чернетка → замовлено → отримано (або скасовано).
/// Приймати можна частинами; кожне приймання — прихід у журнал складу.
/// </summary>
public class PurchaseOrder
{
    public const string Draft = "draft";
    public const string Ordered = "ordered";
    public const string Received = "received";
    public const string Cancelled = "cancelled";

    private readonly List<PurchaseOrderLine> _lines = [];

    private PurchaseOrder() { }

    public PurchaseOrder(int supplierId, IReadOnlyCollection<(int ItemId, decimal Quantity, decimal Price)> lines, string? comment, int userId, DateTime now)
    {
        SupplierId = supplierId;
        Comment = CommentText(comment);
        CreatedByUserId = userId;
        CreatedAt = now;
        SetLines(lines);
    }

    public int Id { get; private set; }

    public int SupplierId { get; private set; }

    public string Status { get; private set; } = Draft;

    public string Comment { get; private set; } = "";

    public int CreatedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? OrderedAt { get; private set; }

    /// <summary>Коли замовлення повністю отримано або скасовано.</summary>
    public DateTime? ClosedAt { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    /// <summary>Чи ще чекаємо товар за цим замовленням (чернетка теж: її скоро надішлють).</summary>
    public bool IsOpen => Status is Draft or Ordered;

    /// <summary>Замінює рядки. Лише в чернетці: надіслане постачальнику замовлення вже не змінюють.</summary>
    public void SetLines(IReadOnlyCollection<(int ItemId, decimal Quantity, decimal Price)> lines)
    {
        RequireStatus(Draft, "змінювати рядки");
        if (lines.Count == 0)
            throw new DomainException("Замовлення має містити хоча б одну позицію.");
        if (lines.Any(l => l.Quantity <= 0))
            throw new DomainException("Кількість у замовленні має бути більшою за нуль.");
        if (lines.Any(l => l.Price < 0))
            throw new DomainException("Ціна не може бути від'ємною.");
        if (lines.GroupBy(l => l.ItemId).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new DomainException($"Позицію {dup.Key} указано двічі.");

        _lines.Clear();
        _lines.AddRange(lines.Select(l => new PurchaseOrderLine(l.ItemId, l.Quantity, l.Price)));
    }

    /// <summary>Замовлення надіслано постачальнику.</summary>
    public void MarkOrdered(DateTime now)
    {
        RequireStatus(Draft, "позначити як замовлене");
        Status = Ordered;
        OrderedAt = now;
    }

    /// <summary>Скасувати можна чернетку або замовлене (уже отримане при цьому лишається на складі).</summary>
    public void Cancel(DateTime now)
    {
        if (!IsOpen)
            throw new DomainException($"Замовлення №{Id} вже закрите.");
        Status = Cancelled;
        ClosedAt = now;
    }

    /// <summary>
    /// Приймання товару: кожен рядок приймання збільшує залишок розхідника (запис «Прихід за замовленням…» у журналі складу).
    /// Можна прийняти більше, ніж замовлено: постачальник іноді докладає. Коли все отримано, замовлення закривається.
    /// </summary>
    public IReadOnlyList<StockMovement> Receive(
        IReadOnlyCollection<(int LineId, decimal Quantity)> receipts,
        IReadOnlyDictionary<int, StockItem> items,
        int userId, string username, DateTime now)
    {
        RequireStatus(Ordered, "приймати товар");
        if (receipts.Count == 0)
            throw new DomainException("Вкажіть, що саме отримано.");
        if (receipts.GroupBy(r => r.LineId).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new DomainException($"Рядок {dup.Key} указано двічі.");

        var movements = new List<StockMovement>();
        foreach (var (lineId, quantity) in receipts)
        {
            var line = _lines.Find(l => l.Id == lineId) ?? throw new DomainException($"Рядок {lineId} не належить замовленню №{Id}.");
            if (quantity <= 0)
                throw new DomainException("Отримана кількість має бути більшою за нуль.");
            var item = items.GetValueOrDefault(line.ItemId) ?? throw new DomainException($"Розхідника {line.ItemId} немає на складі.");

            line.ReceivedQuantity += quantity;
            movements.Add(item.Adjust(quantity, $"Прихід за замовленням постачальнику №{Id}", userId, username, now));
        }

        if (_lines.All(l => l.Remaining == 0))
        {
            Status = Received;
            ClosedAt = now;
        }
        return movements;
    }

    private void RequireStatus(string status, string action)
    {
        if (Status != status)
            throw new DomainException($"Замовлення №{Id} у статусі {Status}: {action} не можна.");
    }

    private static string CommentText(string? comment)
    {
        var text = comment?.Trim() ?? "";
        return text.Length <= 500 ? text : throw new DomainException("Коментар: не довше 500 символів.");
    }
}

public class PurchaseOrderLine
{
    private PurchaseOrderLine() { }

    internal PurchaseOrderLine(int itemId, decimal quantity, decimal price)
    {
        ItemId = itemId;
        Quantity = quantity;
        Price = price;
    }

    public int Id { get; private set; }

    public int OrderId { get; private set; }

    public int ItemId { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal Price { get; private set; }

    public decimal ReceivedQuantity { get; internal set; }

    /// <summary>Скільки ще чекаємо (0, якщо отримано все або більше).</summary>
    public decimal Remaining => Math.Max(0, Quantity - ReceivedQuantity);
}
