using NewErp.Domain.Common;

namespace NewErp.Domain.Catalog;

/// <summary>Вид групи товарів, у межах якої рахується порядок. Назви — як group_type у таблиці пінів старого сайту.</summary>
public enum SortGroupType
{
    /// <summary>Тема (фільтр з групи «теми»), group_key — назва фільтра.</summary>
    Tag,

    /// <summary>Категорія без кольорів, group_key — id категорії.</summary>
    Category,

    /// <summary>Категорія в одному кольорі, group_key — «idкатегорії_idфільтра».</summary>
    CategoryColor,

    /// <summary>Мітка («Новинка», «Хіт»), group_key — назва мітки.</summary>
    Label,

    /// <summary>Панель допродажу при оформленні, group_key — «panel». Порядок пишеться окремо від sort_order товару.</summary>
    CrossSell,
}

/// <summary>Група товарів для впорядкування.</summary>
/// <param name="Name">Назва для людини: «Сукні — Чорний», «Мітка: Хіт».</param>
public sealed record SortGroup(SortGroupType Type, string Key, string Name, int? CategoryId = null, int? FilterId = null, int? LabelId = null)
{
    public const string CrossSellKey = "panel";

    /// <summary>Значення group_type у таблиці пінів старого сайту.</summary>
    public string TypeCode => TypeToCode(Type);

    public static string TypeToCode(SortGroupType type) => type switch
    {
        SortGroupType.Tag => "tag",
        SortGroupType.Category => "category",
        SortGroupType.CategoryColor => "category_color",
        SortGroupType.Label => "label",
        SortGroupType.CrossSell => "crosssell",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static SortGroupType CodeToType(string code) => code switch
    {
        "tag" => SortGroupType.Tag,
        "category" => SortGroupType.Category,
        "category_color" => SortGroupType.CategoryColor,
        "label" => SortGroupType.Label,
        "crosssell" => SortGroupType.CrossSell,
        _ => throw new DomainException($"Невідомий вид групи «{code}»."),
    };
}

/// <summary>Товар, закріплений вручну на позиції в групі (позиції рахуються з 1).</summary>
public sealed record SortPin(int Position, int ProductId);

/// <summary>Розрахунок порядку товарів, як у модулі «Порядок товарів» старого сайту.</summary>
public static class ProductSorter
{
    /// <summary>
    /// Автоматична черга: товари за спаданням продажів, непродані — у кінці.
    /// За однакових продажів зберігається вихідний порядок (як стабільне сортування в PHP 8).
    /// </summary>
    public static IReadOnlyList<int> BySales(IEnumerable<int> productIds, IReadOnlyDictionary<int, int> sales) =>
        productIds.Distinct().OrderByDescending(id => sales.GetValueOrDefault(id)).ToList();

    /// <summary>
    /// Зливає автоматичну чергу з пінами: закріплений товар стає на свою позицію, решта заповнює вільні місця по черзі.
    /// Пін, позиція якого більша за кількість товарів, опиняється одразу після них, а не губиться.
    /// </summary>
    public static IReadOnlyList<int> Merge(IReadOnlyList<int> autoQueue, IReadOnlyCollection<SortPin> pins)
    {
        // Як у старому коді: на позиції — останній пін (таблиця не дає двох), товар з піном у черзі не повторюється.
        var byPosition = new SortedDictionary<int, int>();
        foreach (var pin in pins.Where(p => p.Position >= 1))
            byPosition[pin.Position] = pin.ProductId;
        var pinned = byPosition.Values.ToHashSet();
        var queue = new Queue<int>(autoQueue.Where(id => !pinned.Contains(id)));

        var result = new List<int>();
        var total = queue.Count + byPosition.Count;
        for (var position = 1; position <= total; position++)
        {
            if (byPosition.TryGetValue(position, out var productId))
                result.Add(productId);
            else if (queue.Count > 0)
                result.Add(queue.Dequeue());
        }
        result.AddRange(queue);

        // Піни за межами списку старий код губив (цикл закінчувався раніше). Ставимо їх у кінець за порядком позицій.
        result.AddRange(byPosition.Where(p => p.Key > total).Select(p => p.Value));
        return result.Distinct().ToList();
    }
}
