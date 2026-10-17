using Microsoft.Extensions.Options;

using NewErp.Application.Cashbox;

namespace NewErp.Api;

public sealed class CashboxScheduleOptions
{
    public const string SectionName = "Checkbox";

    /// <summary>Як часто перевіряти відкриті зміни (хвилини). 0 — не перевіряти (можна викликати POST /cashbox/shifts/auto-close).</summary>
    public int AutoCloseIntervalMinutes { get; set; } = 15;
}

/// <summary>
/// Фонова задача замість cron старого сайту (cashbox/work_shift/cron): що кілька хвилин звіряє відкриті зміни з Checkbox
/// і закриває ті, у яких настав час автозакриття касира. Із заглушкою Checkbox не запускається.
/// BackgroundService — служба, яку ASP.NET Core запускає разом із застосунком і зупиняє разом з ним.
/// </summary>
public sealed class CashboxAutoCloseWorker(
    IServiceScopeFactory scopes, ICheckboxClient client, IOptions<CashboxScheduleOptions> options, ILogger<CashboxAutoCloseWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = options.Value.AutoCloseIntervalMinutes;
        if (client.IsFake || minutes <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // CashboxService і DbContext живуть у межах запиту (scoped), тож для кожного проходу — свій scope.
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<CashboxService>().AutoCloseAsync(stoppingToken);
                if (result.ClosedByCheckbox + result.ClosedBySchedule > 0 || result.Errors.Count > 0)
                    logger.LogInformation("Автозакриття змін: {Result}", result);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Автозакриття змін не вдалося");
            }
        }
    }
}
