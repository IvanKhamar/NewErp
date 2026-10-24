using NewErp.Domain.Common;
using NewErp.Domain.Stock;

namespace NewErp.Domain.Production;

/// <summary>
/// Модель виробу (dd_stock_production): розміри з залишками крою й пошиву, прив'язка до товарів магазину
/// і специфікація пошиву. Компоненти крою — окремі об'єкти (<see cref="ProductionComponent"/>).
/// </summary>
public class ProductionModel
{
    /// <summary>Власний пошив: крій → пошив.</summary>
    public const string Clothing = "clothing";

    /// <summary>Готовий товар від постачальника: лише залишок «пошито».</summary>
    public const string ReadyMade = "ready_made";

    private readonly List<ProductionSize> _sizes = [];
    private readonly List<ProductionProductLink> _productLinks = [];
    private readonly List<ProductionCategoryLink> _categoryLinks = [];
    private readonly List<SewnBomLine> _sewnBom = [];

    private ProductionModel() { }

    public ProductionModel(string name, string type, int minTotal, int sortOrder) => Update(name, type, minTotal, sortOrder);

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    public string Type { get; private set; } = Clothing;

    /// <summary>Якщо пошитих разом менше, модель потрапляє в список «треба шити».</summary>
    public int MinTotal { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>false — «видалено»: старий модуль ставить status = 0, а не видаляє рядок.</summary>
    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<ProductionSize> Sizes => _sizes;

    public IReadOnlyList<ProductionProductLink> ProductLinks => _productLinks;

    public IReadOnlyList<ProductionCategoryLink> CategoryLinks => _categoryLinks;

    public IReadOnlyList<SewnBomLine> SewnBom => _sewnBom;

    public decimal TotalSewn => _sizes.Sum(s => s.SewnStock);

    public void Update(string name, string type, int minTotal, int sortOrder)
    {
        if (type is not (Clothing or ReadyMade))
            throw new DomainException($"Тип моделі має бути {Clothing} або {ReadyMade}.");
        if (minTotal < 0)
            throw new DomainException("Мінімальний запас не може бути від'ємним.");

        Name = StockItem.RequireText(name, "Назва моделі", 150);
        Type = type;
        MinTotal = minTotal;
        SortOrder = sortOrder;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Задає перелік розмірів. Розміри зіставляються за назвою: наявні зберігають id і залишки,
    /// нові додаються, а прибрати можна лише розмір без залишків і без рядків специфікації пошиву.
    /// Старий модуль натомість перестворював усі розміри, через що змінювались їхні id.
    /// </summary>
    /// <returns>Id прибраних розмірів, щоб сценарій перевірив, що на них не посилаються інші дані.</returns>
    public IReadOnlyList<int> SetSizes(IReadOnlyList<(string Name, decimal Percent)> sizes)
    {
        var names = sizes.Select(s => StockItem.RequireText(s.Name, "Назва розміру", 32)).ToList();
        if (names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new DomainException($"Розмір \"{dup.Key}\" указано двічі.");
        if (sizes.Any(s => s.Percent is < 0 or > 100))
            throw new DomainException("Частка розміру має бути від 0 до 100 %.");

        var removed = _sizes.Where(s => !names.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        foreach (var size in removed)
        {
            if (size.CutStock != 0 || size.SewnStock != 0)
                throw new DomainException($"Розмір \"{size.Name}\" не можна прибрати: на ньому є залишок.");
            if (_sewnBom.Any(l => l.SizeId == size.Id))
                throw new DomainException($"Розмір \"{size.Name}\" використовується в специфікації пошиву.");
            _sizes.Remove(size);
        }

        for (var i = 0; i < names.Count; i++)
        {
            var existing = _sizes.Find(s => string.Equals(s.Name, names[i], StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                _sizes.Add(new ProductionSize(names[i], sizes[i].Percent, i + 1));
            else
            {
                existing.Percent = sizes[i].Percent;
                existing.SortOrder = i + 1;
            }
        }

        return removed.Select(s => s.Id).ToList();
    }

    /// <summary>Замінює прив'язку до товарів OpenCart: напряму і через категорії з фільтром за назвою.</summary>
    public void SetLinks(IReadOnlyCollection<int> productIds, IReadOnlyCollection<(int CategoryId, string? NameFilter, bool MatchExact)> categories)
    {
        if (productIds.Any(id => id <= 0) || categories.Any(c => c.CategoryId <= 0))
            throw new DomainException("Id товарів і категорій мають бути додатними.");
        if (categories.GroupBy(c => c.CategoryId).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new DomainException($"Категорію {dup.Key} указано двічі.");

        var wanted = productIds.ToHashSet();
        _productLinks.RemoveAll(l => !wanted.Contains(l.ProductId));
        foreach (var id in wanted.Where(id => _productLinks.All(l => l.ProductId != id)))
            _productLinks.Add(new ProductionProductLink(id));

        _categoryLinks.RemoveAll(l => categories.All(c => c.CategoryId != l.CategoryId));
        foreach (var (categoryId, filter, exact) in categories)
        {
            var nameFilter = filter?.Trim() ?? "";
            if (_categoryLinks.Find(l => l.CategoryId == categoryId) is { } link)
                (link.NameFilter, link.MatchExact) = (nameFilter, exact);
            else
                _categoryLinks.Add(new ProductionCategoryLink(categoryId, nameFilter, exact));
        }
    }

    /// <summary>Зв'язує розміри моделі зі значеннями опції «Розмір» у товарах OpenCart.</summary>
    public void SetSizeOptions(IReadOnlyCollection<(int SizeId, int? OptionValueId)> mappings)
    {
        foreach (var (sizeId, optionValueId) in mappings)
            RequireSize(sizeId).OptionValueId = optionValueId;
    }

    /// <summary>
    /// Скільки виробів показати на вітрині для кожного зв'язаного значення опції «Розмір»: пошитий залишок розміру,
    /// округлений униз до цілих (кількість у OpenCart ціла). Якщо два розміри зв'язано з тим самим значенням,
    /// береться перший, як у старому модулі.
    /// </summary>
    public IReadOnlyDictionary<int, int> StorefrontQuantities()
    {
        var result = new Dictionary<int, int>();
        foreach (var size in _sizes.Where(s => s.OptionValueId is not null).OrderBy(s => s.SortOrder).ThenBy(s => s.Id))
            result.TryAdd(size.OptionValueId!.Value, (int)Math.Floor(Math.Max(size.SewnStock, 0)));
        return result;
    }

    /// <summary>
    /// Замінює специфікацію пошиву. Кожен рядок — або деталь крою цієї моделі, або розхідник, не обидва.
    /// </summary>
    public void SetSewnBom(
        IReadOnlyCollection<(int? SizeId, int? ComponentId, int? ItemId, decimal QtyPerUnit)> rows,
        IReadOnlyCollection<int> componentIdsOfModel)
    {
        foreach (var row in rows)
        {
            if ((row.ComponentId is null) == (row.ItemId is null))
                throw new DomainException("Рядок специфікації пошиву: вкажіть або деталь крою, або розхідник.");
            if (row.QtyPerUnit <= 0)
                throw new DomainException("Кількість на один виріб має бути більшою за нуль.");
            if (row.SizeId is { } sizeId)
                RequireSize(sizeId);
            if (row.ComponentId is { } componentId && !componentIdsOfModel.Contains(componentId))
                throw new DomainException($"Деталь {componentId} не належить цій моделі.");
        }
        if (rows.GroupBy(r => (r.SizeId, r.ComponentId, r.ItemId)).Any(g => g.Count() > 1))
            throw new DomainException("Специфікація пошиву містить повторювані рядки.");

        _sewnBom.Clear();
        _sewnBom.AddRange(rows.Select(r => new SewnBomLine(r.SizeId, r.ComponentId, r.ItemId, r.QtyPerUnit)));
    }

    /// <summary>Прибирає деталь зі специфікації пошиву, коли її видаляють.</summary>
    public void RemoveComponentFromSewnBom(int componentId) => _sewnBom.RemoveAll(l => l.ComponentId == componentId);

    /// <summary>«+Покроєно»: розкроєно <paramref name="quantity"/> комплектів розміру.</summary>
    public ProductionLogEntry RecordCut(int sizeId, decimal quantity, string? comment, int userId, string username, DateTime now) =>
        Change(RequireSize(sizeId), ProductionLogEntry.Cut, RequirePositive(quantity), comment, userId, username, now);

    /// <summary>«В пошив»: розкроєні комплекти передано швачкам. Більше, ніж розкроєно, передати не можна.</summary>
    public ProductionLogEntry SendToSewing(int sizeId, decimal quantity, string? comment, int userId, string username, DateTime now)
    {
        var size = RequireSize(sizeId);
        RequireAvailable(size, size.CutStock, quantity, "розкроєно");
        return Change(size, ProductionLogEntry.Cut, -quantity, comment, userId, username, now);
    }

    /// <summary>«+Пошито»: готові вироби розміру надійшли на склад (для ready_made — прихід від постачальника).</summary>
    public ProductionLogEntry RecordSewn(int sizeId, decimal quantity, string? comment, int userId, string username, DateTime now) =>
        Change(RequireSize(sizeId), ProductionLogEntry.Sewn, RequirePositive(quantity), comment, userId, username, now);

    /// <summary>«Відвантажено»: пошиті вироби пішли зі складу не через замовлення. Більше, ніж є, відвантажити не можна.</summary>
    public ProductionLogEntry Ship(int sizeId, decimal quantity, string? comment, int userId, string username, DateTime now)
    {
        var size = RequireSize(sizeId);
        RequireAvailable(size, size.SewnStock, quantity, "пошито");
        return Change(size, ProductionLogEntry.Sewn, -quantity, comment, userId, username, now);
    }

    /// <summary>
    /// Інвентаризація: залишок крою (<see cref="ProductionLogEntry.Cut"/>) чи пошиття (<see cref="ProductionLogEntry.Sewn"/>)
    /// розміру стає порахованим, різниця йде в журнал. Повертає null, якщо збіглося.
    /// </summary>
    public ProductionLogEntry? Recount(int sizeId, string stage, decimal actual, string? comment, int userId, string username, DateTime now)
    {
        if (stage != ProductionLogEntry.Cut && stage != ProductionLogEntry.Sewn)
            throw new DomainException($"Етап має бути {ProductionLogEntry.Cut} або {ProductionLogEntry.Sewn}.");
        var size = RequireSize(sizeId);
        RequireCount(size, actual);
        var delta = actual - (stage == ProductionLogEntry.Cut ? size.CutStock : size.SewnStock);
        return delta == 0 ? null : Change(size, stage, delta, comment, userId, username, now);
    }

    internal static void RequireCount(ProductionSize size, decimal actual)
    {
        if (actual < 0 || decimal.Round(actual, 3) != actual)
            throw new DomainException($"Розмір {size.Name}: кількість має бути від 0, не більше 3 знаків після коми.");
    }

    /// <summary>Коментар журналу, яким старий сайт позначає списання пошитих виробів під замовлення.</summary>
    public static string OrderComment(int orderId) => $"Замовлення #{orderId}";

    /// <summary>Коментар журналу для повернення виробів при виході замовлення зі статусів списання.</summary>
    public static string CancelComment(int orderId) => $"Скасування #{orderId}";

    /// <summary>
    /// Замовлення забирає пошиті вироби розміру. Як і списання розхідників, бере те, що є:
    /// замовлення не можна заблокувати через порожній склад. Повертає null, якщо брати нічого.
    /// </summary>
    public ProductionLogEntry? TakeSewnForOrder(int sizeId, decimal wanted, int orderId, int userId, string username, DateTime now)
    {
        var size = RequireSize(sizeId);
        var amount = Math.Min(RequirePositive(wanted), Math.Max(0, size.SewnStock));
        return amount > 0
            ? Change(size, ProductionLogEntry.Sewn, -amount, OrderComment(orderId), userId, username, now)
            : null;
    }

    /// <summary>Замовлення вийшло зі статусів списання: вироби повертаються на склад.</summary>
    public ProductionLogEntry ReturnSewnFromOrder(int sizeId, decimal quantity, int orderId, int userId, string username, DateTime now) =>
        Change(RequireSize(sizeId), ProductionLogEntry.Sewn, RequirePositive(quantity), CancelComment(orderId), userId, username, now);

    private ProductionLogEntry Change(
        ProductionSize size, string stage, decimal delta, string? comment, int userId, string username, DateTime now)
    {
        var before = stage == ProductionLogEntry.Cut ? size.CutStock : size.SewnStock;
        var after = before + delta;
        if (stage == ProductionLogEntry.Cut)
            size.CutStock = after;
        else
            size.SewnStock = after;

        var text = comment?.Trim() ?? "";
        if (text.Length > 255)
            throw new DomainException("Коментар: не довше 255 символів.");
        return new ProductionLogEntry(this, size, stage, delta, before, after, text, userId, username, now);
    }

    private static decimal RequirePositive(decimal quantity) =>
        quantity > 0 ? quantity : throw new DomainException("Кількість має бути більшою за нуль.");

    private static void RequireAvailable(ProductionSize size, decimal available, decimal quantity, string what)
    {
        RequirePositive(quantity);
        if (quantity > available)
            throw new DomainException($"Розмір {size.Name}: {what} лише {available}, а вказано {quantity}.");
    }

    public ProductionSize RequireSize(int sizeId) =>
        _sizes.Find(s => s.Id == sizeId) ?? throw new DomainException($"Розмір {sizeId} не належить моделі \"{Name}\".");
}
