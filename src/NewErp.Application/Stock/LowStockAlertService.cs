using Microsoft.Extensions.Options;

using NewErp.Application.Sms;
using NewErp.Domain.Sms;
using NewErp.Domain.Stock;

namespace NewErp.Application.Stock;

/// <summary>Items — розхідники, що за добу впали нижче мінімуму; Sent — SMS, які сервіс прийняв.</summary>
public sealed record LowStockAlertResult(int Items, int Sent, int Phones);

/// <summary>
/// Раз на день SMS про розхідники, що впали нижче мінімуму з минулої перевірки. Називаємо лише ті, що перетнули мінімум за цей
/// час (за журналом рухів), тож та сама позиція не приходить щодня; усі показують фільтр «нижче мінімуму» і звіт дозамовлення.
/// </summary>
public sealed class LowStockAlertService(IStockRepository repository, ISmsSender sender, IOptions<StockOptions> options, TimeProvider clock)
{
    /// <summary>Як далеко читати журнал рухів: час між двома запусками.</summary>
    private const int PeriodHours = 24;

    public async Task<LowStockAlertResult> SendAsync(CancellationToken ct)
    {
        var since = clock.GetLocalNow().DateTime.AddHours(-PeriodHours);
        var fell = (await repository.FellBelowMinimumAsync(since, ct)).ToHashSet();
        var phones = options.Value.AlertPhones.Select(SmsMessage.NormalizePhone).ToList();
        if (fell.Count == 0 || phones.Count == 0)
            return new LowStockAlertResult(fell.Count, 0, phones.Count);

        var items = (await repository.ListItemsAsync(new StockItemFilter(BelowMinimumOnly: true), ct)).Where(i => fell.Contains(i.Id)).ToList();
        var text = LowStockAlert.Text(items.Select(i => new LowStockLine(i.Name, i.Quantity, i.MinQuantity, i.Unit)).ToList(), SmsMessage.MaxLength);
        var sent = 0;
        foreach (var phone in phones)
            if ((await sender.SendAsync(phone, text, ct)).Sent)
                sent++;
        return new LowStockAlertResult(items.Count, sent, phones.Count);
    }
}
