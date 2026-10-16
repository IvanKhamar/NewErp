using NewErp.Application.Common;

namespace NewErp.Application.Delivery;

/// <summary>Доставка Новою Поштою: трекінг ТТН і нагадування забрати посилку (на основі модуля старого сайту).</summary>
public sealed class DeliveryService(IShipmentRepository repository, INovaPoshtaClient client, TimeProvider clock)
{
    /// <summary>API Нової Пошти приймає до 100 ТТН за один запит трекінгу.</summary>
    public const int TrackingBatchSize = 100;

    /// <summary>За скільки днів до платного зберігання нагадувати (старий сайт — рівно за 3 дні).</summary>
    public const int RemindDaysBefore = 3;

    /// <summary>Оновлює статуси всіх незакритих ТТН. Змінені зберігаються одним записом.</summary>
    public async Task<TrackingRefreshResult> RefreshTrackingAsync(CancellationToken ct)
    {
        if (!client.IsConfigured)
            return new TrackingRefreshResult(false, 0, 0);

        var open = await repository.ListOpenForTrackingAsync(ct);
        var byNumber = open.GroupBy(s => s.Document.Number).ToDictionary(g => g.Key, g => g.First().Document);
        var updated = 0;

        foreach (var batch in open.Chunk(TrackingBatchSize))
        {
            var infos = await client.TrackAsync(batch.Select(s => new TrackingRequest(s.Document.Number, s.Phone)).ToList(), ct);
            foreach (var info in infos)
            {
                if (byNumber.TryGetValue(info.Number, out var document) && document.ApplyTracking(info))
                    updated++;
            }
        }

        await repository.SaveChangesAsync(ct);
        return new TrackingRefreshResult(true, open.Count, updated);
    }

    /// <summary>
    /// Кому нагадати забрати посилку: платне зберігання починається від сьогодні до трьох днів наперед.
    /// Старий сайт показував лише тих, у кого рівно через 3 дні: якщо день пропустили, людина випадала зі списку.
    /// </summary>
    public Task<IReadOnlyList<ReminderDto>> RemindersAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        return repository.ListRemindersAsync(today, today.AddDays(RemindDaysBefore), ct);
    }

    public async Task MarkRemindedAsync(int orderId, CancellationToken ct)
    {
        if (!await repository.MarkRemindedAsync(orderId, clock.GetLocalNow().DateTime, ct))
            throw new NotFoundException($"Замовлення {orderId} не знайдено.");
    }
}
