namespace NewErp.Application;

/// <summary>
/// Очищує HTML, який вітрина показує як є (описи товарів, таблиці розмірів): лишає дозволені теги й атрибути, тож
/// &lt;script&gt;, onclick= чи javascript:-посилання не потрапляють у базу (stored XSS). Реалізація — в Infrastructure,
/// бо Application завантажується в браузер.
/// </summary>
public interface IHtmlCleaner
{
    string Clean(string? html);
}
