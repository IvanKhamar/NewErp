using System.Globalization;

using Microsoft.Extensions.Options;

using NewErp.Application.Competitors;

namespace NewErp.Api;

/// <summary>
/// Читає ціни конкурентів раз на день, зранку до початку роботи менеджерів (Competitors:CheckTime, «-» — вимкнено).
/// Будова — як у <see cref="CashboxAutoCloseWorker"/>.
/// </summary>
public sealed class CompetitorPriceWorker(
    IServiceScopeFactory scopes, IOptions<CompetitorOptions> options, TimeProvider clock, ILogger<CompetitorPriceWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var setting = options.Value.CheckTime.Trim();
        if (setting == "-")
            return;
        if (!TimeOnly.TryParseExact(setting, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
        {
            logger.LogError("Competitors:CheckTime «{Setting}» — не час HH:mm; перевірку цін вимкнено", setting);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(UntilNext(at), clock, stoppingToken);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<CompetitorService>().CheckAllAsync(stoppingToken);
                if (result.Checked > 0)
                    logger.LogInformation("Ціни конкурентів: перевірено {Checked}, змінилося {Changed}, не вдалося {Failed}",
                        result.Checked, result.Changed, result.Failed);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Перевірка цін конкурентів не вдалася");
            }
        }
    }

    /// <summary>Скільки чекати до найближчого <paramref name="at"/> за місцевим часом сервера (сьогодні чи завтра).</summary>
    private TimeSpan UntilNext(TimeOnly at)
    {
        var now = clock.GetLocalNow().DateTime;
        var next = now.Date + at.ToTimeSpan();
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }
}
