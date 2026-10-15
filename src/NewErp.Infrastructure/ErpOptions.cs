namespace NewErp.Infrastructure;

/// <summary>Загальні налаштування з розділу "Erp" в appsettings.json.</summary>
public sealed class ErpOptions
{
    public const string SectionName = "Erp";

    /// <summary>
    /// Мова довідників OpenCart (dd_order_status, dd_order_source зберігають назви для кожної мови).
    /// У справжній базі всі назви записані з language_id = 3; інші мови там так і не запрацювали.
    /// Тому id мови задається лише тут, а не в кожному запиті.
    /// </summary>
    public int LanguageId { get; set; } = 3;
}