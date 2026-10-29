using System.Text.RegularExpressions;

namespace NewErp.SchemaCheck;

public enum Severity
{
    Error,
    Warning,
    Info,
}

/// <summary>Одна знахідка звіту. Table — таблиця, а для налаштувань — розділ «Налаштування».</summary>
public sealed record Finding(Severity Severity, string Table, string Message);

/// <summary>
/// Порівнює те, що NewErp чекає (модель EF Core), з тим, що є в базі. Зайві таблиці й зайві стовпці, які можна не заповнювати,
/// не цікаві: у справжньому магазині багато таблиць OpenCart, якими NewErp не користується.
/// </summary>
public static partial class SchemaComparer
{
    public static IReadOnlyList<Finding> Compare(IEnumerable<ExpectedColumn> expected, IEnumerable<ActualColumn> actual)
    {
        var tables = actual
            .GroupBy(c => c.Table, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToDictionary(c => c.Column, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var findings = new List<Finding>();

        foreach (var table in expected.GroupBy(c => c.Table, StringComparer.OrdinalIgnoreCase))
        {
            if (!tables.TryGetValue(table.Key, out var columns))
            {
                findings.Add(new Finding(Severity.Error, table.Key, "Таблиці немає."));
                continue;
            }

            foreach (var want in table)
            {
                if (!columns.TryGetValue(want.Column, out var have))
                {
                    findings.Add(new Finding(Severity.Error, table.Key, $"Немає стовпця {want.Column} ({want.StoreType})."));
                    continue;
                }
                if (!SameType(want, have.ColumnType))
                    findings.Add(new Finding(Severity.Warning, table.Key, $"Стовпець {want.Column}: NewErp чекає {want.StoreType}, у базі {have.ColumnType}."));
                if (!want.Nullable && have.Nullable)
                    findings.Add(new Finding(Severity.Error, table.Key,
                        $"Стовпець {want.Column} у базі може бути NULL, а NewErp чекає значення завжди: читання рядка з NULL упаде."));
                if (want.Nullable && !have.Nullable)
                    findings.Add(new Finding(Severity.Warning, table.Key,
                        $"Стовпець {want.Column} у базі NOT NULL, а NewErp допускає порожнє значення: запис NULL не пройде."));
            }

            var known = table.Select(c => c.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var extra in columns.Values.Where(c => !known.Contains(c.Column) && !c.Nullable && !c.HasDefault && !c.Generated))
                findings.Add(new Finding(Severity.Warning, table.Key,
                    $"Зайвий стовпець {extra.Column} ({extra.ColumnType}) NOT NULL без значення за замовчуванням: новий рядок від NewErp сюди не запишеться."));
        }

        return findings.OrderBy(f => f.Severity).ThenBy(f => f.Table, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Тип, заданий у моделі, має збігатися точно (після нормалізації); тип, вибраний провайдером за типом C#, — лише видом.
    /// </summary>
    public static bool SameType(ExpectedColumn want, string actualType) =>
        want.ExplicitType
            ? NormalizeType(want.StoreType) == NormalizeType(actualType)
            : Kind(want.StoreType) == Kind(actualType);

    /// <summary>
    /// Нижній регістр, одинарні пробіли, без ширини показу цілих чисел (MySQL 5.7 пише int(11), 8.0 — int). tinyint(1) лишається:
    /// так зберігається bool.
    /// </summary>
    public static string NormalizeType(string type)
    {
        var t = Spaces().Replace(type.Trim().ToLowerInvariant(), " ");
        var match = IntegerWithWidth().Match(t);
        if (!match.Success)
            return t == "integer" ? "int" : t;
        var name = match.Groups["name"].Value == "integer" ? "int" : match.Groups["name"].Value;
        var width = name == "tinyint" && match.Groups["width"].Value == "1" ? "(1)" : "";
        return name + width + match.Groups["rest"].Value;
    }

    /// <summary>Вид типу для порівняння «на око»: рядок, ціле, дробове, дата й час, bool, двійкові дані.</summary>
    public static string Kind(string type)
    {
        var t = NormalizeType(type);
        if (t.StartsWith("tinyint(1)", StringComparison.Ordinal) || t is "bit" or "bit(1)" or "bool" or "boolean")
            return "bool";
        var name = t.Split('(', ' ')[0];
        return name switch
        {
            "char" or "varchar" or "tinytext" or "text" or "mediumtext" or "longtext" or "enum" or "set" or "json" => "text",
            "tinyint" or "smallint" or "mediumint" or "int" or "bigint" or "year" => "integer",
            "decimal" or "numeric" or "float" or "double" or "real" => "number",
            "date" => "date",
            "datetime" or "timestamp" => "datetime",
            "time" => "time",
            "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob" => "binary",
            _ => name,
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(?<name>tinyint|smallint|mediumint|int|integer|bigint)\((?<width>\d+)\)(?<rest>.*)$")]
    private static partial Regex IntegerWithWidth();
}
