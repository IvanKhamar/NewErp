using System.Globalization;

using NewErp.Domain.Common;

namespace NewErp.Domain.Stock;

/// <summary>
/// Інвентаризація розхідників: комірник вивантажує список, записує пораховане в останній стовпчик і завантажує назад.
/// Стара CRM робила це через імпорт і експорт Excel.
/// </summary>
public static class Stocktaking
{
    /// <summary>Коментар журналу кожної різниці; за ним звіти відрізняють виправлення від справжньої витрати.</summary>
    public const string CommentText = "Інвентаризація";

    /// <summary>Кількості зберігаються з 3 знаками після коми (decimal(12,3)).</summary>
    public const int Scale = 3;

    public static readonly IReadOnlyList<string> Columns = ["ID", "Назва", "Категорія", "Од.", "За обліком", "Фактично"];

    public sealed record Count(int ItemId, decimal Actual);

    public sealed record Parsed(IReadOnlyList<Count> Counts, IReadOnlyList<string> Errors);

    /// <summary>
    /// Читає аркуш: стовпчик ID і останній («Фактично»). Рядок з порожньою кількістю не рахували — пропускаємо;
    /// рядок заголовка пропускаємо, якщо перша клітинка «ID». Помилки називають номер рядка, як його показує Excel.
    /// </summary>
    public static Parsed Parse(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var counts = new List<Count>();
        var errors = new List<string>();
        var seen = new HashSet<int>();
        var actualColumn = Columns.Count - 1;
        for (var r = 0; r < rows.Count; r++)
        {
            var id = Cell(rows[r], 0);
            var actual = Cell(rows[r], actualColumn);
            if (r == 0 && id.Equals("ID", StringComparison.OrdinalIgnoreCase) || actual.Length == 0)
                continue;

            var line = r + 1;
            if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) || itemId <= 0)
                errors.Add($"Рядок {line}: немає ID позиції або він не число.");
            else if (ParseQuantity(actual) is not { } quantity)
                errors.Add($"Рядок {line}: «{actual}» — не кількість.");
            else if (!IsValid(quantity))
                errors.Add($"Рядок {line}: кількість має бути від 0, не більше {Scale} знаків після коми.");
            else if (!seen.Add(itemId))
                errors.Add($"Рядок {line}: позиція {itemId} указана двічі.");
            else
                counts.Add(new Count(itemId, quantity));
        }
        return new Parsed(counts, errors);
    }

    /// <summary>«Інвентаризація» або «Інвентаризація: примітка», обрізане до 255 символів колонки журналу.</summary>
    public static string Comment(string? note)
    {
        var text = string.IsNullOrWhiteSpace(note) ? CommentText : $"{CommentText}: {note.Trim()}";
        return text.Length > 255 ? text[..255] : text;
    }

    public static void RequireValid(decimal actual)
    {
        if (!IsValid(actual))
            throw new DomainException($"Фактична кількість має бути від 0, не більше {Scale} знаків після коми.");
    }

    internal static bool IsValid(decimal quantity) => quantity >= 0 && decimal.Round(quantity, Scale) == quantity;

    private static string Cell(IReadOnlyList<string> row, int index) => index < row.Count ? row[index]?.Trim() ?? "" : "";

    /// <summary>Excel українською показує «12,5», пробіли розділяють тисячі («1 250»).</summary>
    private static decimal? ParseQuantity(string text) =>
        decimal.TryParse(text.Replace(',', '.').Replace(" ", "").Replace(" ", ""), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value) ? value : null;
}
