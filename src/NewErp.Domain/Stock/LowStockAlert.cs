using System.Globalization;

namespace NewErp.Domain.Stock;

public sealed record LowStockLine(string Name, decimal Quantity, decimal Minimum, string Unit);

public static class LowStockAlert
{
    private const string Head = "Мало на складі: ";

    /// <summary>Одна SMS: «Мало на складі: Льон 12 м (мін. 30); …». Те, що не вміщається, рахуємо наприкінці.</summary>
    public static string Text(IReadOnlyList<LowStockLine> lines, int maxLength)
    {
        var text = Head;
        for (var shown = 0; shown < lines.Count; shown++)
        {
            var line = lines[shown];
            var part = $"{line.Name} {Plain(line.Quantity)} {line.Unit} (мін. {Plain(line.Minimum)})";
            var rest = lines.Count - shown - 1;
            var tail = rest > 0 ? $"; ще {rest} поз." : "";
            var candidate = text + (shown > 0 ? "; " : "") + part;
            if ((candidate + tail).Length > maxLength)
                return $"{text}; ще {lines.Count - shown} поз.";
            text = candidate;
        }
        return text;
    }

    private static string Plain(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
