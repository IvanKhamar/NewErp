using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace NewErp.SchemaCheck;

/// <summary>
/// Налаштування NewErp, які залежать від даних магазину: мова довідників і id статусів замовлень. Id без назви — помилка
/// (такого статусу в магазині немає); з назвою — для людини, щоб підтвердити, що це той самий статус.
/// </summary>
public static partial class SettingsCheck
{
    public const string Section = "Налаштування";

    /// <summary>Списки id статусів у appsettings NewErp.Api.</summary>
    public static readonly string[] StatusListKeys =
    [
        "Bonus:CountedStatusIds",
        "Production:OrderSubtractStatusIds",
        "Production:PlanStatusIds",
        "ProductSort:CountedStatusIds",
        "Vouchers:PaidStatusIds",
    ];

    public static IReadOnlyList<Finding> Check(IConfiguration settings, DatabaseSnapshot snapshot)
    {
        var findings = new List<Finding>();
        if (!int.TryParse(settings["Erp:LanguageId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var languageId))
        {
            findings.Add(new Finding(Severity.Info, Section, "Erp:LanguageId не задано, налаштування не перевіряємо."));
            return findings;
        }

        var languages = snapshot.Statuses.Select(s => s.LanguageId).Distinct().Order().ToList();
        if (!languages.Contains(languageId))
        {
            findings.Add(new Finding(Severity.Error, Section,
                $"Erp:LanguageId = {languageId}, а статусів цією мовою в dd_order_status немає (є мови: {List(languages)})."));
            return findings;
        }

        var names = snapshot.Statuses.Where(s => s.LanguageId == languageId).ToDictionary(s => s.StatusId, s => s.Name);
        foreach (var key in StatusListKeys)
        {
            var ids = settings.GetSection(key).Get<int[]>();
            if (ids is null || ids.Length == 0)
            {
                findings.Add(new Finding(Severity.Info, Section, $"{key} не задано, пропускаємо."));
                continue;
            }
            foreach (var missing in ids.Where(id => !names.ContainsKey(id)))
                findings.Add(new Finding(Severity.Error, Section, $"{key}: статусу {missing} немає в dd_order_status мовою {languageId}."));
            findings.Add(new Finding(Severity.Info, Section, $"{key}: {Named(ids, names)}."));
        }

        // OpenCart вважає оплаченими замовлення в config_complete_status: так його getVoucher вирішує, чи діє куплений сертифікат.
        if (snapshot.Settings.TryGetValue("config_complete_status", out var complete))
        {
            var ids = StatusIds(complete);
            findings.Add(new Finding(Severity.Info, Section, $"config_complete_status (dd_setting): {Named(ids, names)}."));
            var paid = settings.GetSection("Vouchers:PaidStatusIds").Get<int[]>();
            if (paid is { Length: > 0 } && !paid.ToHashSet().SetEquals(ids))
                findings.Add(new Finding(Severity.Warning, Section,
                    $"Vouchers:PaidStatusIds ({List(paid)}) не збігається з config_complete_status ({List(ids)}): сайт і NewErp по-різному вирішать, чи діє куплений сертифікат."));
        }
        if (snapshot.Settings.TryGetValue("config_processing_status", out var processing))
            findings.Add(new Finding(Severity.Info, Section, $"config_processing_status (dd_setting): {Named(StatusIds(processing), names)}."));

        var sources = snapshot.Sources.Where(s => s.LanguageId == languageId).OrderBy(s => s.SourceId).ToList();
        if (sources.Count > 0)
            findings.Add(new Finding(Severity.Info, Section,
                $"Джерела замовлень (dd_order_source): {string.Join(", ", sources.Select(s => $"{s.SourceId} «{s.Name}»"))}."));

        return findings.OrderBy(f => f.Severity).ToList();
    }

    /// <summary>
    /// Id статусів зі значення dd_setting: OpenCart 2+ зберігає список як JSON (["5","3"]), старіші версії — серіалізований
    /// масив PHP, а один статус — просто числом. Беремо всі числа в лапках, а якщо їх немає — усі числа.
    /// </summary>
    public static IReadOnlyList<int> StatusIds(string value)
    {
        var quoted = QuotedNumber().Matches(value).Select(m => m.Groups[1].Value).ToList();
        var numbers = quoted.Count > 0 ? quoted : Number().Matches(value).Select(m => m.Value).ToList();
        return numbers.Select(n => int.Parse(n, CultureInfo.InvariantCulture)).Distinct().ToList();
    }

    private static string Named(IEnumerable<int> ids, IReadOnlyDictionary<int, string> names) =>
        string.Join(", ", ids.Select(id => names.TryGetValue(id, out var name) ? $"{id} «{name}»" : $"{id} (немає)"));

    private static string List(IEnumerable<int> ids) => string.Join(", ", ids);

    [GeneratedRegex("\"(\\d+)\"")]
    private static partial Regex QuotedNumber();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Number();
}
