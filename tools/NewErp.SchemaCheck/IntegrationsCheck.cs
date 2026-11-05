using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Cashbox;
using NewErp.Application.Delivery;
using NewErp.Application.Payments;
using NewErp.Application.Sms;
using NewErp.Domain.Common;

namespace NewErp.SchemaCheck;

/// <summary>
/// Один запит лише для читання до кожного зовнішнього сервісу, чий ключ задано: клієнти написано за документацією, а справжні ключі
/// має лише власник. Ключі беруться зі звичайних налаштувань і не друкуються. У Checkbox і SMSClub запиту без запису немає (лише чеки,
/// зміни й SMS), тому про них кажемо тільки, чи вони налаштовані.
/// </summary>
public static class IntegrationsCheck
{
    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider services, TextWriter output, TimeProvider clock, CancellationToken ct = default)
    {
        var problems = new List<string>();
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        var novaPoshta = services.GetRequiredService<INovaPoshtaClient>();
        await ProbeAsync("Нова Пошта (трекінг вигаданої ТТН)", novaPoshta.IsConfigured,
            () => novaPoshta.TrackAsync([new TrackingRequest("20450000000000", "")], ct), output, problems);

        var bank = services.GetRequiredService<IBankStatementClient>();
        await ProbeAsync("ПриватБанк (виписка за вчора й сьогодні)", bank.IsConfigured, () => bank.FetchAsync(today.AddDays(-1), today, ct), output, problems);

        var liqPay = services.GetRequiredService<ILiqPayGateway>();
        await ProbeAsync("LiqPay (статус неіснуючого замовлення)", !liqPay.IsTest, () => liqPay.StatusAsync("0", ct), output, problems);

        Configured("Checkbox", !services.GetRequiredService<ICheckboxClient>().IsFake, output);
        Configured("SMSClub", services.GetRequiredService<ISmsSender>().IsConfigured, output);
        return problems;
    }

    /// <summary>Виняток може повторити запит із ключем, тож показуємо лише текст, написаний для людей, або сам тип помилки.</summary>
    public static async Task ProbeAsync(string name, bool configured, Func<Task> call, TextWriter output, List<string> problems)
    {
        if (!configured)
        {
            await output.WriteLineAsync($"{name}: ключ не задано, пропущено");
            return;
        }
        try
        {
            await call();
            await output.WriteLineAsync($"{name}: відповідає");
        }
        catch (Exception e) when (e is not OperationCanceledException or TaskCanceledException)
        {
            problems.Add($"{name}: {(e is DomainException ? e.Message : e.GetType().Name)}");
        }
    }

    private static void Configured(string name, bool configured, TextWriter output) =>
        output.WriteLine($"{name}: {(configured ? "налаштовано" : "ключ не задано")}; запиту без запису немає, перевірте тестовим чеком чи SMS.");
}
