namespace NewErp.Infrastructure;

/// <summary>Загальні налаштування з розділу "Erp" в appsettings.json.</summary>
public sealed class ErpOptions
{
    public const string SectionName = "Erp";

    /// <summary>
    /// Мова довідників OpenCart (dd_order_status, dd_order_source зберігають назви для кожної мови).
    /// Значення треба звірити з таблицею dd_language старого сайту.
    /// </summary>
    public int LanguageId { get; set; } = 1;
}