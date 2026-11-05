using System.Globalization;

using Microsoft.Extensions.Options;

using NewErp.Application.Stock;

namespace NewErp.Api;

/// <summary>
/// Щодня о Stock:AlertTime надсилає SMS про розхідники, що за добу впали нижче мінімуму. Без телефонів (Stock:AlertPhones) нікому
/// казати, тож задача не працює; «-» замість часу теж вимикає її. Будова — як у <see cref="CompetitorPriceWorker"/>.
/// </summary>
public sealed class LowStockAlertWorker(
    IServiceScopeFactory scopes, IOptions<StockOptions> options, TimeProvider clock, ILogger<LowStockAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var setting = options.Value.AlertTime.Trim();
        if (options.Value.AlertPhones.Count == 0 || setting == "-")
            return;
        if (!TimeOnly.TryParseExact(setting, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
        {
            logger.LogError("Stock:AlertTime «{Setting}» — не час HH:mm; сповіщення про мало на складі вимкнено", setting);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(UntilNext(at), clock, stoppingToken);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<LowStockAlertService>().SendAsync(stoppingToken);
                if (result.Items > 0)
                    logger.LogInformation("Мало на складі: позицій {Items}, SMS надіслано {Sent} з {Phones}", result.Items, result.Sent, result.Phones);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Сповіщення про мало на складі не вдалося");
            }
        }
    }

    private TimeSpan UntilNext(TimeOnly at)
    {
        var now = clock.GetLocalNow().DateTime;
        var next = now.Date + at.ToTimeSpan();
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }
}
