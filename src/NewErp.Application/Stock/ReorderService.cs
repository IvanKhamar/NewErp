using System.Globalization;
using System.Text;

using Microsoft.Extensions.Options;

namespace NewErp.Application.Stock;

/// <summary>Звіт «що дозамовити»: позиції нижче мінімуму, згруповані за постачальниками.</summary>
public sealed class ReorderService(IStockRepository repository, IPurchaseOrderRepository purchaseOrders, IOptions<StockOptions> options)
{
    public const string NoSupplierName = "Без постачальника";

    /// <summary>Уже замовлене й ще не отримане віднімається, тож повторний звіт не пропонує те саме вдруге.</summary>
    public async Task<ReorderReportDto> GetReportAsync(CancellationToken ct)
    {
        var items = await repository.ListItemsAsync(new StockItemFilter(BelowMinimumOnly: true), ct);
        var suppliers = (await repository.ListSuppliersAsync(ct)).ToDictionary(s => s.Id);
        var onOrder = await purchaseOrders.OnOrderAsync(ct);
        return Build(items, suppliers, onOrder, options.Value.ReorderTargetFactor);
    }

    /// <summary>
    /// Розрахунок без бази: пропонована кількість = factor × мінімум − залишок − уже замовлене, округлено вгору.
    /// Позиції, яким нічого не треба (усе вже замовлено), у звіт не потрапляють.
    /// </summary>
    public static ReorderReportDto Build(
        IEnumerable<StockItemDto> items,
        IReadOnlyDictionary<int, StockSupplierDto> suppliers,
        IReadOnlyDictionary<int, decimal> onOrder,
        decimal targetFactor)
    {
        var groups = items
            .Where(i => i.IsBelowMinimum)
            .Select(i =>
            {
                var ordered = onOrder.GetValueOrDefault(i.Id);
                var suggested = Math.Ceiling(Math.Max(0, targetFactor * i.MinQuantity - i.Quantity - ordered));
                return (Item: i, Line: new ReorderLineDto(
                    i.Id, i.Name, i.Unit, i.Quantity, i.MinQuantity, ordered, suggested, i.Price, suggested * i.Price));
            })
            .Where(x => x.Line.Suggested > 0)
            .GroupBy(x => x.Item.SupplierId)
            .Select(g =>
            {
                var supplier = g.Key is { } id ? suppliers.GetValueOrDefault(id) : null;
                var lines = g.Select(x => x.Line).OrderBy(l => l.Name).ToList();
                return new SupplierReorderDto(
                    g.Key, supplier?.Name ?? NoSupplierName, supplier?.Phone ?? "", supplier?.Email ?? "",
                    lines, lines.Sum(l => l.Sum), MessageText(lines));
            })
            // Постачальники за абеткою, позиції без постачальника — останніми.
            .OrderBy(s => s.SupplierId is null).ThenBy(s => s.SupplierName)
            .ToList();

        return new ReorderReportDto(groups, groups.Sum(g => g.Total));
    }

    /// <summary>Текст, який можна одразу скопіювати постачальнику.</summary>
    public static string MessageText(IEnumerable<ReorderLineDto> lines)
    {
        var text = new StringBuilder("Добрий день! Хочемо замовити:");
        foreach (var line in lines)
            text.Append(CultureInfo.InvariantCulture, $"\n— {line.Name}: {line.Suggested:0.###} {line.Unit}");
        return text.Append("\nДякуємо!").ToString();
    }
}
