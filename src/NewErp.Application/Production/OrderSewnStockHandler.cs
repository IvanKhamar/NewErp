using Microsoft.Extensions.Options;

using NewErp.Application.Orders;
using NewErp.Domain.Orders;
using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>
/// Пошиті вироби й замовлення (на основі події StockTrack старого сайту). Коли замовлення входить у статуси
/// з Production:OrderSubtractStatusIds, вироби потрібного розміру списуються; коли виходить — повертаються.
/// Розмір береться з опції товару в замовленні, зв'язаної з розміром моделі.
/// </summary>
public sealed class OrderSewnStockHandler(
    IProductionRepository repository, IStorefrontStock storefront, IOptions<ProductionOptions> options, TimeProvider clock)
    : IOrderStatusHandler
{
    /// <summary>Як у старому сайті: автоматичні зміни журналу підписані "system", а user_id — того, хто змінив статус.</summary>
    public const string SystemUsername = "system";

    public async Task<IReadOnlyList<string>> OnStatusChangingAsync(OrderStatusChange change, CancellationToken ct)
    {
        var statuses = options.Value.OrderSubtractStatusIds;
        var wasIn = statuses.Contains(change.OldStatusId);
        var isIn = statuses.Contains(change.NewStatusId);
        if (wasIn == isIn)
            return [];

        var lines = (await repository.GetOrderSizeLinesAsync(change.OrderId, ct)).Where(l => l.ProductionId is not null).ToList();
        if (lines.Count == 0)
            return [];

        var models = (await repository.FindModelsAsync(lines.Select(l => l.ProductionId!.Value).Distinct().ToList(), ct))
            .ToDictionary(m => m.Id);
        var warnings = new List<string>();
        var bySize = new Dictionary<(ProductionModel Model, ProductionSize Size), decimal>();

        foreach (var line in lines)
        {
            if (!models.TryGetValue(line.ProductionId!.Value, out var model))
                continue;
            var size = model.Sizes.FirstOrDefault(s => s.OptionValueId is { } ov && line.OptionValueIds.Contains(ov));
            if (size is null)
            {
                warnings.Add($"«{line.ProductName}»: розмір із замовлення не зв'язаний з розмірами моделі «{model.Name}», склад не змінено.");
                continue;
            }
            bySize[(model, size)] = bySize.GetValueOrDefault((model, size)) + line.Quantity;
        }

        // Скільки замовлення вже тримає за журналом. Завдяки цьому повторний виклик (збій після списання, але до
        // збереження статусу, або списання, яке вже зробив старий сайт) не списує й не повертає вдруге.
        var held = await repository.GetSewnHeldByOrderAsync(change.OrderId, ct);
        var now = clock.GetLocalNow().DateTime;
        if (isIn)
            Take(change, bySize, held, warnings, now);
        else
            Return(change, bySize, held, now);

        await repository.SaveChangesAsync(ct);
        // Сайт сам віднімає залишок лише при оформленні замовлення на вітрині, тож після зміни в NewErp передаємо його.
        await storefront.PushAsync(bySize.Keys.Select(k => k.Model.Id).Distinct().ToList(), ct);
        return warnings;
    }

    /// <summary>Списує те, чого замовлення ще не тримає. Якщо пошитих менше, бере те, що є, і попереджає.</summary>
    private void Take(
        OrderStatusChange change, Dictionary<(ProductionModel Model, ProductionSize Size), decimal> bySize,
        IReadOnlyDictionary<int, decimal> held, List<string> warnings, DateTime now)
    {
        foreach (var ((model, size), quantity) in bySize)
        {
            var missing = quantity - held.GetValueOrDefault(size.Id);
            if (missing <= 0)
                continue;
            var entry = model.TakeSewnForOrder(size.Id, missing, change.OrderId, change.UserId, SystemUsername, now);
            if (entry is not null)
                repository.Add(entry);
            var taken = entry?.Quantity ?? 0;
            if (taken < missing)
                warnings.Add(FormattableString.Invariant(
                    $"Пошитих «{model.Name}» розміру {size.Name} бракує {missing - taken:0.###} шт.: списано {taken:0.###} з {missing:0.###}."));
        }
    }

    /// <summary>
    /// Повертає не більше, ніж замовлення справді тримає за журналом. Старий модуль повертав повну кількість,
    /// навіть якщо при списанні виробів бракувало, і так «створював» вироби, яких не було.
    /// </summary>
    private void Return(
        OrderStatusChange change, Dictionary<(ProductionModel Model, ProductionSize Size), decimal> bySize,
        IReadOnlyDictionary<int, decimal> held, DateTime now)
    {
        foreach (var ((model, size), quantity) in bySize)
        {
            var toReturn = Math.Min(quantity, held.GetValueOrDefault(size.Id));
            if (toReturn > 0)
                repository.Add(model.ReturnSewnFromOrder(size.Id, toReturn, change.OrderId, change.UserId, SystemUsername, now));
        }
    }
}
