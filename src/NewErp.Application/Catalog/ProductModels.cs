using NewErp.Application.Common;

namespace NewErp.Application.Catalog;

/// <summary>Фільтри списку товарів магазину (?categoryId=30&amp;enabled=true&amp;page=2). Search — частина назви чи моделі або номер товару.</summary>
public sealed record ProductFilter(string? Search = null, int? CategoryId = null, int? LabelId = null, bool? Enabled = null, int Page = 1, int PageSize = 20);

/// <summary>Товар у списку. Quantity — залишок на вітрині, його заповнює виробництво з пошитого складу.</summary>
public sealed record ProductListItem(int Id, string Name, string Model, decimal Price, bool Enabled, int Quantity, string? Image);

/// <summary>Розмір товару із залишком (лише читання: залишки веде виробництво).</summary>
public sealed record ProductSize(int OptionValueId, string Name, int Quantity);

/// <summary>
/// Картка товару. DateModified — dd_product.date_modified, його ставить і OpenCart при кожному збереженні;
/// за ним редагування перевіряє, що товар не змінили тим часом.
/// </summary>
public sealed record ProductCardDto(
    int Id, string Name, string Model, decimal Price, bool Enabled, int Quantity, string? Image, DateTime DateModified,
    IReadOnlyList<ProductSize> Sizes, IReadOnlyList<LookupItem> Labels, IReadOnlyList<LookupItem> Categories);

/// <summary>
/// PATCH товару: null — без змін. DateModified — той, що був у картці, коли її відкрили; якщо товар відтоді змінили
/// (в OpenCart чи в іншому вікні), зміну відхилено з 409.
/// </summary>
public sealed record UpdateProductRequest(
    decimal? Price = null, bool? Enabled = null, IReadOnlyList<int>? LabelIds = null, IReadOnlyList<int>? CategoryIds = null, DateTime? DateModified = null);
