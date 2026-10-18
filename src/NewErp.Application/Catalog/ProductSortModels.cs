using NewErp.Domain.Catalog;

namespace NewErp.Application.Catalog;

/// <summary>Розділ "ProductSort" в appsettings.json. Значення — як у коді старого сайту.</summary>
public sealed class ProductSortOptions
{
    public const string SectionName = "ProductSort";

    /// <summary>Статуси замовлень, продажі яких рахуються.</summary>
    public int[] CountedStatusIds { get; set; } = [];

    /// <summary>Замовлення, створені цими користувачами адмінки (dd_order.user_id), не рахуються.</summary>
    public int[] ExcludedUserIds { get; set; } = [];

    /// <summary>Не рахуються замовлення, у імені, прізвищі, компанії чи коментарі яких є один з цих рядків (оптові, B2B).</summary>
    public string[] ExcludedOrderMarkers { get; set; } = [];

    /// <summary>Не рахуються замовлення, у коментарі яких є один з цих рядків.</summary>
    public string[] ExcludedCommentMarkers { get; set; } = [];

    /// <summary>Фільтри-кольори: категорія з товарами в цих кольорах впорядковується окремо для кожного кольору.</summary>
    public int[] ColorFilterIds { get; set; } = [];

    /// <summary>Група фільтрів «теми» (кожен фільтр групи — окрема група товарів).</summary>
    public int TagFilterGroupId { get; set; }

    /// <summary>Мітка, товари якої йдуть від найновішого, а не за продажами.</summary>
    public string NewLabelName { get; set; } = "Новинка";

    public int DefaultDays { get; set; } = 90;
}

/// <summary>Період продажів: останні Days днів або з From по To включно.</summary>
public sealed record SalesPeriod(int? Days = null, DateOnly? From = null, DateOnly? To = null);

public sealed record SortGroupDto(SortGroupType Type, string Key, string Name);

/// <summary>Рядок прев'ю: де товар зараз (серед товарів групи) і де стане.</summary>
public sealed record SortPreviewRow(int ProductId, string Name, string Image, int Sales, bool Pinned, int CurrentPosition, int NewPosition);

public sealed record ApplySortRequest(SortGroupType Type, string Key, IReadOnlyList<int> ProductIds);

public sealed record RecalculateGroupRequest(SortGroupType Type, string Key, SalesPeriod? Period = null);

/// <param name="SnapshotId">Знімок порядку перед зміною (для відкату); null для панелі допродажу, яка має власний порядок.</param>
public sealed record SortResult(int? SnapshotId, int Updated);

public sealed record SortPinDto(int Id, SortGroupType Type, string Key, int Position, int ProductId, string ProductName);

public sealed record SetPinRequest(SortGroupType Type, string Key, int Position, int ProductId);

public sealed record SortSnapshotDto(int Id, string Note, DateTime CreatedAt);

public sealed record SortProductDto(int ProductId, string Name, string Image, int SortOrder);

/// <summary>Чим міряти популярність у панелі допродажу: усіма продажами чи лише тими, що прийшли через панель.</summary>
public sealed record CrossSellMetricDto(bool PanelOnly);
