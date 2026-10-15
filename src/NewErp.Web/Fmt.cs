using System.Globalization;

namespace NewErp.Web;

/// <summary>Однакове відображення кількостей, грошей і дат на всіх сторінках.</summary>
public static class Fmt
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    /// <summary>Кількість без зайвих нулів: 12.5, 100, 0.25.</summary>
    public static string Qty(decimal value) => value.ToString("0.###", Uk);

    public static string Money(decimal value) => value.ToString("#,0.00", Uk) + " ₴";

    public static string Date(DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Uk);

    public static string Date(DateTime? value) => value is { } v ? Date(v) : "—";
}
