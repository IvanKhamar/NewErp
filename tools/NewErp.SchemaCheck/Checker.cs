using System.Text;

using Microsoft.Extensions.Configuration;

using MySql.Data.MySqlClient;

namespace NewErp.SchemaCheck;

/// <summary>Уся перевірка однієї бази: схема проти моделі NewErp і налаштування проти довідників магазину.</summary>
public static class Checker
{
    public static async Task<IReadOnlyList<Finding>> RunAsync(string connectionString, IConfiguration settings, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var snapshot = await DatabaseSchema.ReadAsync(connection, ct);
        if (snapshot.Columns.Count == 0)
            return [new Finding(Severity.Error, connection.Database, "У базі немає жодної таблиці: її не створено або дамп не завантажено.")];

        return [.. SchemaComparer.Compare(ModelSchema.Read(), snapshot.Columns), .. SettingsCheck.Check(settings, snapshot)];
    }

    /// <summary>Звіт для людини: спершу помилки, далі попередження, потім довідка; згруповано за таблицями.</summary>
    public static string Report(IReadOnlyList<Finding> findings)
    {
        var text = new StringBuilder();
        foreach (var (severity, title) in new[] { (Severity.Error, "Помилки"), (Severity.Warning, "Попередження"), (Severity.Info, "Довідка") })
        {
            var group = findings.Where(f => f.Severity == severity).ToList();
            if (group.Count == 0)
                continue;
            text.AppendLine($"== {title} ({group.Count}) ==");
            foreach (var table in group.GroupBy(f => f.Table))
            {
                text.AppendLine(table.Key);
                foreach (var finding in table)
                    text.AppendLine($"  - {finding.Message}");
            }
            text.AppendLine();
        }
        var errors = findings.Count(f => f.Severity == Severity.Error);
        var warnings = findings.Count(f => f.Severity == Severity.Warning);
        text.AppendLine(errors == 0 && warnings == 0
            ? "Схема збігається з моделлю NewErp, налаштування знайдено в базі."
            : $"Помилок: {errors}, попереджень: {warnings}.");
        return text.ToString();
    }
}
