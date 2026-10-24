using System.Globalization;
using System.Text.RegularExpressions;

using NewErp.Domain.Common;

namespace NewErp.Domain.Competitors;

/// <summary>
/// Знаходить ціну товару на сторінці магазину. Спершу власний вираз конкурента (якщо задано), далі звична розмітка:
/// мета-тег product:price:amount, елемент itemprop="price", "price" у JSON-LD schema.org.
/// </summary>
public static class PriceExtractor
{
    // Вираз конкурента пишуть люди, а сторінку — чужий сайт: обмеження часу не дасть невдалому виразу «зависнути».
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static readonly Regex[] Standard =
    [
        new("""property=["']product:price:amount["'][^>]*content=["']([^"']+)["']""", RegexOptions.IgnoreCase, Timeout),
        new("""content=["']([^"']+)["'][^>]*property=["']product:price:amount["']""", RegexOptions.IgnoreCase, Timeout),
        new("""itemprop=["']price["'][^>]*content=["']([^"']+)["']""", RegexOptions.IgnoreCase, Timeout),
        new("""content=["']([^"']+)["'][^>]*itemprop=["']price["']""", RegexOptions.IgnoreCase, Timeout),
        new(@"""price""\s*:\s*""?([0-9][0-9\s.,]*)", RegexOptions.None, Timeout),
    ];

    private static readonly Regex Number = new(@"[0-9][0-9\s  .,]*", RegexOptions.None, Timeout);

    /// <summary>Перевіряє вираз конкурента: він має компілюватися й мати групу для ціни. Порожній — без власного виразу.</summary>
    public static string RequireRegex(string? regex)
    {
        var trimmed = regex?.Trim() ?? "";
        if (trimmed.Length == 0)
            return "";
        if (trimmed.Length > 255)
            throw new DomainException("Вираз для ціни — не довше 255 символів.");
        Regex compiled;
        try
        {
            compiled = new Regex(trimmed, RegexOptions.None, Timeout);
        }
        catch (ArgumentException e)
        {
            throw new DomainException($"Вираз для ціни з помилкою: {e.Message}");
        }
        if (compiled.GetGroupNumbers().Length < 2)
            throw new DomainException("Вираз для ціни має містити групу в дужках, наприклад class=\"price\">([0-9 ]+).");
        return trimmed;
    }

    public static decimal? Extract(string? html, string? customRegex)
    {
        if (string.IsNullOrEmpty(html))
            return null;
        if (!string.IsNullOrWhiteSpace(customRegex)
            && First(new Regex(customRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline, Timeout), html) is { } custom)
            return custom;
        foreach (var pattern in Standard)
            if (First(pattern, html) is { } found)
                return found;
        return null;
    }

    private static decimal? First(Regex pattern, string html)
    {
        try
        {
            for (var m = pattern.Match(html); m.Success; m = m.NextMatch())
                if (Parse(m.Groups[1].Value) is { } price)
                    return price;
        }
        catch (RegexMatchTimeoutException)
        {
            // Вираз не встиг: вважаємо, що ціни ним не знайдено, і пробуємо наступний.
        }
        return null;
    }

    /// <summary>
    /// «1 299,00», «1,299.00», «1299.5» чи «1 299 грн» як число: пробіли розділяють тисячі; з коми й крапки десятковий
    /// знак — останній; одна кома чи крапка з трьома цифрами після неї розділяє тисячі.
    /// </summary>
    public static decimal? Parse(string? text)
    {
        if (text is null)
            return null;
        var m = Number.Match(text);
        if (!m.Success)
            return null;
        var s = Regex.Replace(m.Value, @"[\s  ]", "").TrimEnd('.', ',');
        var comma = s.LastIndexOf(',');
        var dot = s.LastIndexOf('.');
        var point = Math.Max(comma, dot);
        if (point >= 0)
        {
            var whole = s[..point].Replace(",", "").Replace(".", "");
            var fraction = s[(point + 1)..];
            var onlySeparator = (comma < 0 || dot < 0) && fraction.Length == 3 && s.IndexOf(s[point]) == point && whole.Length > 0;
            s = onlySeparator ? whole + fraction : whole + "." + fraction;
        }
        return decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) && price > 0 ? price : null;
    }
}
