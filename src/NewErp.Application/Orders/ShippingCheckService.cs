using NewErp.Application.Common;
using NewErp.Domain.Orders;

namespace NewErp.Application.Orders;

public sealed record OrderForCheck(string Customer, string Telephone, string ShippingCode, string ShippingMethod, DateTime DateAdded);

/// <summary>Що читає перевірка відправки: замовлення, його остання ТТН, журнал SMS і журнал статусів.</summary>
public interface IShippingCheckStore
{
    Task<OrderForCheck?> OrderForCheckAsync(int orderId, CancellationToken ct);

    /// <summary>Номер (лише цифри) останньої ТТН Нової Пошти замовлення або "".</summary>
    Task<string> LastWaybillAsync(int orderId, CancellationToken ct);

    /// <summary>Телефони, на які журнал SMS надіслав текст із цим номером ТТН (пробіли в тексті не враховуються).</summary>
    Task<IReadOnlyList<string>> SmsPhonesWithAsync(string waybill, DateTime since, CancellationToken ct);

    /// <summary>(id замовлення, клієнт, час останньої ТТН) замовлень у цих статусах із ТТН, створеною до того часу.</summary>
    Task<IReadOnlyList<(int OrderId, string Customer, DateTime Created)>> StaleWaybillsAsync(
        IReadOnlyList<int> statusIds, IReadOnlyList<string> shippingCodes, DateTime createdBefore, CancellationToken ct);

    /// <summary>Замовлення, які зараз у цьому статусі й увійшли в нього відтоді (за журналом статусів).</summary>
    Task<IReadOnlyList<int>> OrdersMovedIntoAsync(int statusId, DateTime since, IReadOnlyList<string>? shippingCodes, CancellationToken ct);
}

/// <summary>Перевірка перед «Відправлено» / «Чекає на відправку» і список розбіжностей (checkShipping і getDiscrepancies старої адмінки).</summary>
public sealed class ShippingCheckService(IShippingCheckStore store, TimeProvider clock)
{
    public async Task<ShippingCheckDto> CheckAsync(int orderId, int statusId, CancellationToken ct)
    {
        var order = await store.OrderForCheckAsync(orderId, ct)
            ?? throw new NotFoundException($"Замовлення {orderId} не знайдено.");
        var (waybill, issues) = await IssuesAsync(orderId, order, statusId, ct);
        return new ShippingCheckDto(orderId, statusId, order.Customer, order.Telephone, waybill,
            issues.Select(i => new ShippingIssueDto(i.Level, i.Text)).ToList());
    }

    /// <summary>Три групи, як у старій адмінці; days рахує й сьогодні, від 1 до 30.</summary>
    public async Task<IReadOnlyList<DiscrepancyGroupDto>> DiscrepanciesAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, ShippingChecks.DiscrepancyMaxDays);
        var now = clock.GetLocalNow().DateTime;
        var since = now.Date.AddDays(-(days - 1));

        var stale = await store.StaleWaybillsAsync(
            [ShippingChecks.AcceptedStatusId, ShippingChecks.UrgentStatusId],
            ShippingChecks.NovaPoshtaShippingCodes,
            now.AddHours(-ShippingChecks.WaybillStaleHours), ct);
        return
        [
            new($"ТТН створена понад {ShippingChecks.WaybillStaleHours} год тому, а статус досі «Прийнято» / «Терміново»",
                stale.Select(s => new DiscrepancyDto(s.OrderId, s.Customer, IssueLevel.Warning, $"ТТН створена {s.Created:dd.MM HH:mm}")).ToList()),
            new("«Відправлено», але є проблема з ТТН або SMS",
                await MovedWithIssuesAsync(ShippingChecks.ShippedStatusId, since, ShippingChecks.NovaPoshtaShippingCodes, ct)),
            new("«Чекає на відправку», але схоже на доставку Новою Поштою",
                await MovedWithIssuesAsync(ShippingChecks.AwaitingShipmentStatusId, since, null, ct)),
        ];
    }

    private async Task<IReadOnlyList<DiscrepancyDto>> MovedWithIssuesAsync(int statusId, DateTime since, IReadOnlyList<string>? shippingCodes, CancellationToken ct)
    {
        var items = new List<DiscrepancyDto>();
        foreach (var orderId in await store.OrdersMovedIntoAsync(statusId, since, shippingCodes, ct))
        {
            if (await store.OrderForCheckAsync(orderId, ct) is not { } order)
                continue;
            var (_, issues) = await IssuesAsync(orderId, order, statusId, ct);
            items.AddRange(issues.Select(i => new DiscrepancyDto(orderId, order.Customer, i.Level, i.Text)));
        }

        return items;
    }

    private async Task<(string Waybill, IReadOnlyList<ShippingIssue> Issues)> IssuesAsync(int orderId, OrderForCheck order, int statusId, CancellationToken ct)
    {
        var waybill = await store.LastWaybillAsync(orderId, ct);
        if (statusId == ShippingChecks.ShippedStatusId)
        {
            var phones = waybill.Length > 0 ? await store.SmsPhonesWithAsync(waybill, order.DateAdded, ct) : [];
            return (waybill, ShippingChecks.ShippedIssues(order.ShippingCode, order.Telephone, waybill, phones));
        }

        if (statusId == ShippingChecks.AwaitingShipmentStatusId)
            return (waybill, ShippingChecks.PickupIssues(order.ShippingCode, order.ShippingMethod, waybill));
        return (waybill, []);
    }
}
