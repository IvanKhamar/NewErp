using AngleSharp.Dom;

using Ganss.Xss;

using NewErp.Application;

namespace NewErp.Infrastructure.Content;

/// <summary>
/// HtmlSanitizer лишає лише перелічені теги й атрибути, як nh3 у Python-версії: &lt;script&gt;, onclick=, javascript:-посилання
/// чи стиль, що ховає вміст, не доходять до бази, тож обліковий запис редактора не можна використати проти покупців (stored XSS).
/// </summary>
public sealed class HtmlCleaner : IHtmlCleaner
{
    private static readonly HtmlSanitizer Sanitizer = Create();

    public string Clean(string? html) => string.IsNullOrWhiteSpace(html) ? "" : Sanitizer.Sanitize(html);

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "b", "blockquote", "br", "caption", "div", "em", "h2", "h3", "h4", "h5", "hr", "i", "img", "li", "ol", "p",
                "s", "small", "span", "strong", "sub", "sup", "table", "tbody", "td", "tfoot", "th", "thead", "tr", "u", "ul",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "title", "target", "src", "alt", "width", "height", "colspan", "rowspan", "class",
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto", "tel" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
            AllowedCssProperties = new HashSet<string>(),
        });
        // Посилання не дає новій вкладці доступу до вкладки магазину (rel, як у Python-версії).
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { LocalName: "a" } link)
                link.SetAttribute("rel", "noopener noreferrer");
        };
        return sanitizer;
    }
}
