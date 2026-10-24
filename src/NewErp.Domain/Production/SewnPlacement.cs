using NewErp.Domain.Common;

namespace NewErp.Domain.Production;

/// <summary>
/// Де лежать пошиті вироби моделі. Як і з розхідниками: пошитий залишок розміру (dd_stock_production_sizes.sewn_stock)
/// лишається загальним — з ним працюють старий сайт, замовлення й вітрина; склади мають свої залишки, а основний
/// склад — це решта. warehouseId = null — основний склад.
/// </summary>
public sealed class SewnPlacement
{
    private readonly Dictionary<(int SizeId, int WarehouseId), SewnBalance> _balances;
    private readonly List<SewnBalance> _created = [];

    public SewnPlacement(ProductionModel model, IEnumerable<SewnBalance> balances)
    {
        Model = model;
        _balances = balances.ToDictionary(b => (b.SizeId, b.WarehouseId));
    }

    public ProductionModel Model { get; }

    /// <summary>Баланси для складів, де розміру ще не було: їх треба додати до збереження.</summary>
    public IReadOnlyList<SewnBalance> Created => _created;

    /// <summary>Баланси розміру, також щойно створені.</summary>
    public IEnumerable<SewnBalance> Balances(int sizeId) => _balances.Values.Where(b => b.SizeId == sizeId);

    /// <summary>Пошиті вироби розміру на складі або, для null, на основному.</summary>
    public decimal At(int sizeId, int? warehouseId)
    {
        var size = Model.RequireSize(sizeId);
        return warehouseId is { } id
            ? _balances.GetValueOrDefault((sizeId, id))?.Quantity ?? 0
            : size.SewnStock - Balances(sizeId).Sum(b => b.Quantity);
    }

    /// <summary>Переміщує пошиті вироби розміру між місцями; пошитий залишок той самий. Не більше, ніж є в джерелі.</summary>
    public SewnTransfer Move(int sizeId, int? from, int? to, decimal quantity, string? comment, int userId, string username, DateTime now)
    {
        if (from == to)
            throw new DomainException("Звідки й куди переміщують — має бути різне місце.");
        if (quantity <= 0 || decimal.Round(quantity, 3) != quantity)
            throw new DomainException("Кількість має бути більшою за нуль, не більше 3 знаків після коми.");
        var text = comment?.Trim() ?? "";
        if (text.Length > 255)
            throw new DomainException("Коментар: не довше 255 символів.");
        RequireAvailable(sizeId, from, quantity);

        Change(sizeId, from, -quantity);
        Change(sizeId, to, quantity);
        return new SewnTransfer(Model.Id, sizeId, from, to, quantity, text, userId, username, now);
    }

    /// <summary>Готові вироби надійшли на склад (чи на основний): пошитий залишок росте, у журналі запис.</summary>
    public ProductionLogEntry SewnAt(int sizeId, int? warehouseId, decimal quantity, string? comment, int userId, string username, DateTime now)
    {
        var entry = Model.RecordSewn(sizeId, quantity, comment, userId, username, now);
        Change(sizeId, warehouseId, quantity);
        return entry;
    }

    /// <summary>Вироби пішли з місця не через замовлення. Не більше, ніж там є.</summary>
    public ProductionLogEntry ShipFrom(int sizeId, int? warehouseId, decimal quantity, string? comment, int userId, string username, DateTime now)
    {
        // Коли на складах розміру немає, основний склад і є пошитим залишком: діє звична перевірка моделі.
        if (quantity > 0 && (warehouseId is not null || Balances(sizeId).Any(b => b.Quantity != 0)))
            RequireAvailable(sizeId, warehouseId, quantity);
        var entry = Model.Ship(sizeId, quantity, comment, userId, username, now);
        Change(sizeId, warehouseId, -quantity);
        return entry;
    }

    private void Change(int sizeId, int? warehouseId, decimal delta)
    {
        if (warehouseId is not { } id)
            return; // основний склад — це решта, він змінюється сам

        if (!_balances.TryGetValue((sizeId, id), out var balance))
        {
            balance = new SewnBalance(sizeId, id);
            _balances[(sizeId, id)] = balance;
            _created.Add(balance);
        }
        balance.Add(delta);
    }

    private void RequireAvailable(int sizeId, int? warehouseId, decimal quantity)
    {
        var available = Math.Max(At(sizeId, warehouseId), 0);
        if (available < quantity)
        {
            var place = warehouseId is null ? "на основному складі" : "на цьому складі";
            throw new DomainException($"Розмір {Model.RequireSize(sizeId).Name}: {place} пошито {available:0.###}, потрібно {quantity:0.###}.");
        }
    }
}
