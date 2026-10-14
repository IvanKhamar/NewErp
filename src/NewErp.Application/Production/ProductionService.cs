using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Production;

namespace NewErp.Application.Production;

/// <summary>Сценарії виробництва: специфікації виробів і потреба в розхідниках.</summary>
public sealed class ProductionService(IBomRepository repository, TimeProvider clock)
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
}
