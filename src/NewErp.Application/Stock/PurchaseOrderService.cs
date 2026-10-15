using NewErp.Application.Common;
using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

/// <summary>Замовлення постачальникам: створення (вручну або зі звіту «що дозамовити»), надсилання, приймання.</summary>
public sealed class PurchaseOrderService(
    IPurchaseOrderRepository orders, IStockRepository stock, ReorderService reorder, TimeProvider clock)
{
    /// <summary>Чернетка для постачальника з тим, що пропонує звіт «що дозамовити» (кількість і ціна з позиції).</summary>
    public async Task<int> CreateFromReorderAsync(int supplierId, int userId, CancellationToken ct)
    {
        var report = await reorder.GetReportAsync(ct);
        var group = report.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId)
            ?? throw new DomainException($"У постачальника {supplierId} зараз нічого дозамовляти.");

        var order = new PurchaseOrder(
            supplierId, group.Lines.Select(l => (l.ItemId, l.Suggested, l.Price)).ToList(), "Зі звіту «що дозамовити»", userId, Now());
        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task<int> CreateAsync(CreatePurchaseOrderRequest request, int userId, CancellationToken ct)
    {
        if (!await stock.SupplierExistsAsync(request.SupplierId, ct))
            throw new DomainException($"Постачальника {request.SupplierId} не існує.");
        var lines = await ValidLinesAsync(request.Lines, ct);

        var order = new PurchaseOrder(request.SupplierId, lines, request.Comment, userId, Now());
        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task SetLinesAsync(int id, SetPurchaseOrderLinesRequest request, CancellationToken ct)
    {
        var order = await RequireAsync(id, ct);
        order.SetLines(await ValidLinesAsync(request.Lines, ct));
        await orders.SaveChangesAsync(ct);
    }

    public async Task MarkOrderedAsync(int id, CancellationToken ct)
    {
        (await RequireAsync(id, ct)).MarkOrdered(Now());
        await orders.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int id, CancellationToken ct)
    {
        (await RequireAsync(id, ct)).Cancel(Now());
        await orders.SaveChangesAsync(ct);
    }

    /// <summary>Приймання товару: залишки зростають, у журналі складу — приходи. Усе одним записом.</summary>
    public async Task ReceiveAsync(int id, ReceivePurchaseOrderRequest request, int userId, string username, CancellationToken ct)
    {
        var order = await RequireAsync(id, ct);
        var items = (await orders.FindItemsAsync(order.Lines.Select(l => l.ItemId).ToList(), ct)).ToDictionary(i => i.Id);

        var movements = order.Receive(
            (request.Lines ?? []).Select(l => (l.LineId, l.Quantity)).ToList(), items, userId, username, Now());
        foreach (var movement in movements)
            orders.Add(movement);
        await orders.SaveChangesAsync(ct);
    }

    private async Task<List<(int, decimal, decimal)>> ValidLinesAsync(IReadOnlyList<PurchaseOrderLineRequest>? lines, CancellationToken ct)
    {
        var result = (lines ?? []).Select(l => (l.ItemId, l.Quantity, l.Price)).ToList();
        var ids = result.Select(l => l.ItemId).Distinct().ToList();
        var found = (await orders.FindItemsAsync(ids, ct)).Select(i => i.Id).ToHashSet();
        var missing = ids.Where(i => !found.Contains(i)).ToList();
        if (missing.Count > 0)
            throw new DomainException($"Розхідників {string.Join(", ", missing)} немає на складі.");
        return result;
    }

    private async Task<PurchaseOrder> RequireAsync(int id, CancellationToken ct) =>
        await orders.FindAsync(id, ct) ?? throw new NotFoundException($"Замовлення постачальнику №{id} не знайдено.");

    private DateTime Now() => clock.GetLocalNow().DateTime;
}
