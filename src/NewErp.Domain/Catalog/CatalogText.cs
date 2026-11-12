using System.Text.RegularExpressions;

using NewErp.Domain.Common;

namespace NewErp.Domain.Catalog;

/// <summary>
/// Перевірки тексту з форм каталогу (Python first, пункти 43–45 і 75): шлях до картинки, SEO-адреса, тег «тема:NEW»
/// і вид файлу картинки за першими байтами. Ті самі правила й тексти, що в Python-версії.
/// </summary>
public static partial class CatalogText
{
    /// <summary>Мітка, значення фільтра «За темою» й тег нового принта.</summary>
    public const string NewLabel = "NEW";

    public const string NewTag = "тема:NEW";

    /// <summary>
    /// "" — без картинки; інакше перевірений шлях під catalog/ сайту OpenCart. Лише літери, цифри, - _ . / і пробіл,
    /// тож шлях не виходить за цю папку й не несе розмітки.
    /// </summary>
    public static string ImagePath(string? value)
    {
        var path = (value ?? "").Trim();
        if (path.Length > 0 && (!ImagePathPattern().IsMatch(path) || path.Contains("..") || path.Contains("//")))
            throw new DomainException($"Шлях до картинки «{path}» має бути на кшталт catalog/folder/photo.jpg.");
        return path;
    }

    /// <summary>Адреса товару на вітрині: латинські літери, цифри, - і _ ("" — без адреси).</summary>
    public static string SeoKeyword(string? value)
    {
        var keyword = (value ?? "").Trim().ToLowerInvariant();
        if (keyword.Length > 0 && (keyword.Length > 255 || !SeoKeywordPattern().IsMatch(keyword)))
            throw new DomainException("SEO-адреса: лише латинські літери, цифри, - та _, наприклад sukniia-lion-chorna.");
        return keyword;
    }

    /// <summary>Теги товару без «тема:NEW» (у будь-якому регістрі), решта — у своєму порядку.</summary>
    public static string WithoutNewTag(string? tags) => string.Join(",",
        (tags ?? "").Split(',').Select(t => t.Trim()).Where(t => t.Length > 0 && !t.Equals(NewTag, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Що це за файл за першими байтами («магічне число»), або null. SVG не приймаємо: він може нести скрипти.</summary>
    public static string? PictureExtension(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return "jpg";
        if (content.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return "png";
        if (content.StartsWith("GIF87a"u8) || content.StartsWith("GIF89a"u8))
            return "gif";
        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
            return "webp";
        return null;
    }

    // \z, а не $: $ пропускає \n у кінці.
    [GeneratedRegex(@"^catalog/[\w\-./ ]+\.(?:jpe?g|png|webp|gif)\z", RegexOptions.IgnoreCase)]
    private static partial Regex ImagePathPattern();

    [GeneratedRegex(@"^[a-z0-9]+(?:[-_][a-z0-9]+)*\z")]
    private static partial Regex SeoKeywordPattern();
}
