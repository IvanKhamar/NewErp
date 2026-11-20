using System.Text.RegularExpressions;

using NewErp.Application.Common;
using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

using static NewErp.Application.Catalog.RequestLimits;

namespace NewErp.Application.Catalog;

// Довідники каталогу старої адмінки (Python first, пункт 49). Фільтри, атрибути й опції в OpenCart дворівневі: група
// («Колір», «Склад», «Розмір») зі значеннями («Червоний», «Льон 100%», «M»). Один сервіс обслуговує всі три, а виробники й
// завантаження — пласкі списки. Значення чи групу, які ще є в товарах, видалити не можна: вітрина показала б зламані
// фільтри й розміри.

/// <summary>Значення групи. Image — лише в значень опцій; Products — у скількох товарах воно є (лише для читання).</summary>
public sealed record ReferenceItemDto(int Id = 0, string Name = "", int SortOrder = 0, string Image = "", int Products = 0);

/// <summary>Група зі значеннями. Type — лише в опцій: select, radio, checkbox, text, textarea, date.</summary>
public sealed record ReferenceGroupDto(int Id = 0, string Name = "", int SortOrder = 0, string Type = "select", IReadOnlyList<ReferenceItemDto>? Items = null)
{
    public IReadOnlyList<ReferenceItemDto> Items { get; init; } = Items ?? [];
}

public sealed record ManufacturerDto(int Id = 0, string Name = "", string Image = "", int SortOrder = 0, int Products = 0);

/// <summary>Файл, який покупець отримує після покупки; сам файл лежить у storage/download вітрини. Mask — ім'я, яке бачить покупець.</summary>
public sealed record DownloadDto(int Id = 0, string Name = "", string Filename = "", string Mask = "", int Products = 0);

/// <summary>Який дворівневий довідник: filters, attributes чи options.</summary>
public enum ReferenceKind
{
    Filters,
    Attributes,
    Options,
}

public interface ICatalogReferenceStore
{
    Task<IReadOnlyList<ReferenceGroupDto>> GroupsAsync(ReferenceKind kind, CancellationToken ct);

    Task<int> SaveGroupAsync(ReferenceKind kind, ReferenceGroupDto group, CancellationToken ct);

    Task<int> SaveItemAsync(ReferenceKind kind, int groupId, ReferenceItemDto item, CancellationToken ct);

    Task<int?> GroupOfAsync(ReferenceKind kind, int itemId, CancellationToken ct);

    /// <summary>Група разом зі значеннями.</summary>
    Task DeleteGroupAsync(ReferenceKind kind, int groupId, CancellationToken ct);

    Task DeleteItemAsync(ReferenceKind kind, int itemId, CancellationToken ct);

    Task<IReadOnlyList<ManufacturerDto>> ManufacturersAsync(CancellationToken ct);

    Task<int> SaveManufacturerAsync(ManufacturerDto manufacturer, CancellationToken ct);

    Task DeleteManufacturerAsync(int manufacturerId, CancellationToken ct);

    Task<IReadOnlyList<DownloadDto>> DownloadsAsync(CancellationToken ct);

    Task<int> SaveDownloadAsync(DownloadDto download, CancellationToken ct);

    Task DeleteDownloadAsync(int downloadId, CancellationToken ct);
}

public sealed partial class CatalogReferenceService(ICatalogReferenceStore store, IAuditLog audit)
{
    public static readonly IReadOnlyList<string> OptionTypes = ["select", "radio", "checkbox", "text", "textarea", "date"];

    /// <summary>Довідник з адреси: filters, attributes чи options; інше — 400.</summary>
    public static ReferenceKind Kind(string kind) => kind switch
    {
        "filters" => ReferenceKind.Filters,
        "attributes" => ReferenceKind.Attributes,
        "options" => ReferenceKind.Options,
        _ => throw new DomainException("Довідник: filters, attributes або options."),
    };

    // ═══ Дворівневі довідники ═══

    public Task<IReadOnlyList<ReferenceGroupDto>> GroupsAsync(ReferenceKind kind, CancellationToken ct) => store.GroupsAsync(kind, ct);

    public async Task<ReferenceGroupDto> SaveGroupAsync(ReferenceKind kind, int? groupId, ReferenceGroupDto group, CancellationToken ct)
    {
        var name = Name(kind, group.Name);
        if (kind == ReferenceKind.Options && !OptionTypes.Contains(group.Type))
            throw new DomainException($"Тип опції: {string.Join(", ", OptionTypes)}.");
        if (groupId is { } id)
            await GroupAsync(kind, id, ct);
        var saved = await store.SaveGroupAsync(kind, group with { Id = groupId ?? 0, Name = name }, ct);
        return await GroupAsync(kind, saved, ct);
    }

    public async Task<ReferenceGroupDto> SaveItemAsync(ReferenceKind kind, int groupId, int? itemId, ReferenceItemDto item, CancellationToken ct)
    {
        await GroupAsync(kind, groupId, ct);
        if (itemId is { } id && await store.GroupOfAsync(kind, id, ct) != groupId)
            throw new NotFoundException($"Значення {id} у групі {groupId} немає.");
        Length("Картинка", item.Image, 255);
        var image = kind == ReferenceKind.Options ? CatalogText.ImagePath(item.Image) : "";
        await store.SaveItemAsync(kind, groupId, item with { Id = itemId ?? 0, Name = Name(kind, item.Name), Image = image }, ct);
        return await GroupAsync(kind, groupId, ct);
    }

    public async Task DeleteGroupAsync(ReferenceKind kind, int groupId, int userId, CancellationToken ct)
    {
        var group = await GroupAsync(kind, groupId, ct);
        var used = group.Items.Where(i => i.Products > 0).Select(i => i.Name).ToList();
        if (used.Count > 0)
            throw new DomainException($"Значення групи «{group.Name}» є в товарах: {string.Join(", ", used)}. Спершу приберіть їх.");
        await store.DeleteGroupAsync(kind, groupId, ct);
        audit.Record($"{Path(kind)} group deleted: {groupId}, by user {userId}");
    }

    public async Task DeleteItemAsync(ReferenceKind kind, int itemId, int userId, CancellationToken ct)
    {
        var groupId = await store.GroupOfAsync(kind, itemId, ct) ?? throw new NotFoundException($"Значення {itemId} немає.");
        var item = (await GroupAsync(kind, groupId, ct)).Items.First(i => i.Id == itemId);
        if (item.Products > 0)
            throw new DomainException($"«{item.Name}» є в {item.Products} товарах: спершу приберіть його з них.");
        await store.DeleteItemAsync(kind, itemId, ct);
        audit.Record($"{Path(kind)} value deleted: {itemId}, by user {userId}");
    }

    private async Task<ReferenceGroupDto> GroupAsync(ReferenceKind kind, int groupId, CancellationToken ct) =>
        (await store.GroupsAsync(kind, ct)).FirstOrDefault(g => g.Id == groupId) ?? throw new NotFoundException($"Групи {groupId} немає.");

    /// <summary>Назви фільтрів і атрибутів — varchar(64), опцій — varchar(128).</summary>
    private static string Name(ReferenceKind kind, string? value)
    {
        Length("Назва", value, kind == ReferenceKind.Options ? 128 : 64);
        return Required(value);
    }

    private static string Path(ReferenceKind kind) => kind.ToString().ToLowerInvariant();

    // ═══ Виробники ═══

    public Task<IReadOnlyList<ManufacturerDto>> ManufacturersAsync(CancellationToken ct) => store.ManufacturersAsync(ct);

    public async Task<ManufacturerDto> SaveManufacturerAsync(int? manufacturerId, ManufacturerDto manufacturer, CancellationToken ct)
    {
        Length("Назва", manufacturer.Name, 64);
        Length("Картинка", manufacturer.Image, 255);
        if (manufacturerId is { } id && (await store.ManufacturersAsync(ct)).All(m => m.Id != id))
            throw new NotFoundException($"Виробника {id} немає.");
        var clean = manufacturer with { Id = manufacturerId ?? 0, Name = Required(manufacturer.Name), Image = CatalogText.ImagePath(manufacturer.Image) };
        var saved = await store.SaveManufacturerAsync(clean, ct);
        return (await store.ManufacturersAsync(ct)).First(m => m.Id == saved);
    }

    public async Task DeleteManufacturerAsync(int manufacturerId, int userId, CancellationToken ct)
    {
        var manufacturer = (await store.ManufacturersAsync(ct)).FirstOrDefault(m => m.Id == manufacturerId)
            ?? throw new NotFoundException($"Виробника {manufacturerId} немає.");
        if (manufacturer.Products > 0)
            throw new DomainException($"Виробник «{manufacturer.Name}» є в {manufacturer.Products} товарах.");
        await store.DeleteManufacturerAsync(manufacturerId, ct);
        audit.Record($"manufacturer deleted: {manufacturerId}, by user {userId}");
    }

    // ═══ Завантаження ═══

    public Task<IReadOnlyList<DownloadDto>> DownloadsAsync(CancellationToken ct) => store.DownloadsAsync(ct);

    public async Task<DownloadDto> SaveDownloadAsync(int? downloadId, DownloadDto download, CancellationToken ct)
    {
        Length("Назва", download.Name, 64);
        Length("Ім'я для покупця", download.Mask, 128);
        if (downloadId is { } id && (await store.DownloadsAsync(ct)).All(d => d.Id != id))
            throw new NotFoundException($"Завантаження {id} немає.");
        var filename = download.Filename.Trim();
        if (!DownloadFile().IsMatch(filename) || filename.Contains("..", StringComparison.Ordinal))
            throw new DomainException("Ім'я файлу: лише літери, цифри, - _ . і пробіл, без папок.");
        var mask = download.Mask.Trim();
        var clean = download with { Id = downloadId ?? 0, Name = Required(download.Name), Filename = filename, Mask = mask.Length > 0 ? mask : filename };
        var saved = await store.SaveDownloadAsync(clean, ct);
        return (await store.DownloadsAsync(ct)).First(d => d.Id == saved);
    }

    public async Task DeleteDownloadAsync(int downloadId, int userId, CancellationToken ct)
    {
        var download = (await store.DownloadsAsync(ct)).FirstOrDefault(d => d.Id == downloadId)
            ?? throw new NotFoundException($"Завантаження {downloadId} немає.");
        if (download.Products > 0)
            throw new DomainException($"Завантаження «{download.Name}» прив'язане до {download.Products} товарів.");
        await store.DeleteDownloadAsync(downloadId, ct);
        audit.Record($"download deleted: {downloadId}, by user {userId}");
    }

    private static string Required(string? value)
    {
        var name = (value ?? "").Trim();
        return name.Length > 0 ? name : throw new DomainException("Вкажіть назву.");
    }

    /// <summary>Ім'я файлу в storage/download OpenCart, без папок.</summary>
    [GeneratedRegex(@"^[\w\-. ]{1,160}$")]
    private static partial Regex DownloadFile();
}
