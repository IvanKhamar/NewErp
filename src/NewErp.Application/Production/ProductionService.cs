using System.Globalization;

using Microsoft.Extensions.Options;

using NewErp.Application.Common;
using NewErp.Application.Orders;
using NewErp.Domain.Common;
using NewErp.Domain.Orders;
using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>Сценарії виробництва: специфікації виробів, потреба в розхідниках і їх списання за замовленням.</summary>
public sealed class ProductionService(IProductionRepository repository, TimeProvider clock, IOptions<ProductionOptions> options)
    : IOrderStatusHandler
{
    /// <summary>Створює специфікацію товару або повністю замінює наявну.</summary>
    public async Task SetBomAsync(int productId, SetBomRequest request, int userId, CancellationToken ct)
    {
        var lines = (request.Lines ?? []).Select(l => (l.StockItemId, l.Quantity)).ToList();
        var missing = await repository.MissingStockItemIdsAsync(lines.Select(l => l.StockItemId).Distinct().ToList(), ct);
        if (missing.Count > 0)
            throw new DomainException($"Розхідників {string.Join(", ", missing)} немає на складі.");

        var bom = await repository.FindAsync(productId, ct);
        if (bom is null)
        {
            bom = new Bom(productId);
            repository.Add(bom);
        }

        bom.SetLines(lines, userId, clock.GetLocalNow().DateTime);
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteBomAsync(int productId, CancellationToken ct)
    {
        var bom = await repository.FindAsync(productId, ct)
            ?? throw new NotFoundException($"Специфікації товару {productId} немає.");

        repository.Remove(bom);
        await repository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Скільки розхідників потрібно, щоб пошити все із замовлення: кількість товару × норма зі специфікації,
    /// підсумовано по кожному розхіднику й порівняно із залишком.
    /// </summary>
    public async Task<OrderRequirementsDto> GetOrderRequirementsAsync(int orderId, CancellationToken ct)
    {
        var products = await repository.GetOrderProductsAsync(orderId, ct)
            ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        var components = await repository.GetComponentsAsync(products.Select(p => p.ProductId).Distinct().ToList(), ct);
        var withBom = components.Select(c => c.ProductId).ToHashSet();

        var materials = (
            from p in products
            join c in components on p.ProductId equals c.ProductId
            group (c, p.Quantity) by c.StockItemId into g
            let first = g.First().c
            let required = g.Sum(x => x.c.QuantityPerUnit * x.Quantity)
            orderby first.ItemName
            select new MaterialRequirementDto(
                first.StockItemId, first.ItemName, first.Unit, required, first.InStock, Math.Max(0, required - first.InStock))
        ).ToList();

        return new OrderRequirementsDto(orderId, materials, products.Where(p => !withBom.Contains(p.ProductId)).ToList());
    }

    /// <summary>Коли замовлення переходить у статус із налаштування Production:WriteOffStatusId, списує розхідники.</summary>
    public async Task<IReadOnlyList<string>> OnStatusChangingAsync(OrderStatusChange change, CancellationToken ct) =>
        options.Value.WriteOffStatusId == change.NewStatusId
            ? await WriteOffAsync(change.OrderId, change.UserId, ct)
            : [];

    /// <summary>
    /// Списує розхідники за специфікаціями товарів замовлення. Якщо залишку не вистачає, списує те, що є.
    /// Замовлення списується лише раз: повторний виклик нічого не робить.
    /// </summary>
    /// <returns>Попередження про нестачу й товари без специфікації.</returns>
    public async Task<IReadOnlyList<string>> WriteOffAsync(int orderId, int userId, CancellationToken ct)
    {
        if (await repository.WriteOffExistsAsync(orderId, ct))
            return [];

        var requirements = await GetOrderRequirementsAsync(orderId, ct);
        var items = (await repository.FindStockItemsAsync(requirements.Materials.Select(m => m.StockItemId).ToList(), ct))
            .ToDictionary(i => i.Id);

        var (writeOff, movements) = OrderWriteOff.Perform(
            orderId, requirements.Materials.Select(m => (items[m.StockItemId], m.Required)), userId, clock.GetLocalNow().DateTime);
        repository.Add(writeOff);
        foreach (var movement in movements)
            repository.Add(movement);
        await repository.SaveChangesAsync(ct);

        var warnings = writeOff.Lines
            .Where(l => l.Shortage > 0)
            .Select(l =>
            {
                var item = items[l.StockItemId];
                return string.Create(CultureInfo.InvariantCulture,
                    $"Бракує {l.Shortage:0.###} {item.Unit}: {item.Name}. Списано {l.WrittenOff:0.###} з {l.Required:0.###}.");
            })
            .ToList();
        if (requirements.ProductsWithoutBom.Count > 0)
            warnings.Add("Без специфікації, нічого не списано: " +
                string.Join(", ", requirements.ProductsWithoutBom.Select(p => p.Name).Distinct()) + ".");
        return warnings;
    }
}
