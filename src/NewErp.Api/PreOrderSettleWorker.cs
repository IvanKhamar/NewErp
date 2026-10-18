using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;

namespace NewErp.Api;

/// <summary>
/// Фонова задача замість cron старого сайту (tool/console updatePreOrdersQuantity): регулярно списує продане наперед
/// з лімітів передзамовлення. Будова — як у <see cref="CashboxAutoCloseWorker"/>.
/// </summary>
public sealed class PreOrderSettleWorker(IServiceScopeFactory scopes, IOptions<PreOrderOptions> options, ILogger<PreOrderSettleWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = options.Value.SettleIntervalMinutes;
        if (minutes <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<PreOrderService>().SettleAsync(stoppingToken);
                if (result.Sizes > 0)
                    logger.LogInformation("Передзамовлення: списано з лімітів у {Sizes} розмірах", result.Sizes);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Списати передзамовлення не вдалося");
            }
        }
    }
}
