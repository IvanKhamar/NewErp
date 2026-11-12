using System.Security.Cryptography;

using NewErp.Domain.Catalog;
using NewErp.Domain.Common;

namespace NewErp.Application.Catalog;

public sealed record PictureDto(string Path);

/// <summary>Папка картинок вітрини, коли ERP може в неї писати (спільний том).</summary>
public interface IPictureFolder
{
    bool Enabled { get; }

    /// <summary>Записує новий файл; наявний ніколи не перезаписує.</summary>
    Task SaveAsync(string path, byte[] content, CancellationToken ct);
}

/// <summary>
/// Завантаження картинок у папку вітрини (Python first, пункт 75); поля форм зберігають шлях, як у старій адмінці.
/// Назву й тип файлу від браузера не беремо зовсім: тип — за першими байтами, назву робить сервер. Тож завантажений
/// скрипт не ляже під назвою, яку вебсервер запустить.
/// </summary>
public sealed class PictureService(IPictureFolder folder, IAuditLog audit)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>Для чого картинка й її папка під catalog/. З запиту в шлях потрапляє лише цей вибір.</summary>
    private static readonly Dictionary<string, string> Folders = new()
    {
        ["products"] = "products", ["categories"] = "categories", ["banners"] = "banners", ["vouchers"] = "vouchers",
    };

    public async Task<PictureDto> UploadAsync(string kind, byte[] content, int userId, CancellationToken ct)
    {
        if (!Folders.TryGetValue(kind, out var name))
            throw new DomainException("Невідомий вид картинки.");
        if (!folder.Enabled)
            throw new DomainException("Папку картинок вітрини не налаштовано: Pictures:ImageDir.");
        if (content.Length > MaxBytes)
            throw new DomainException($"Картинка завелика: не більше {MaxBytes / (1024 * 1024)} МБ.");
        var extension = CatalogText.PictureExtension(content)
            ?? throw new DomainException("Це не картинка: приймаються лише JPEG, PNG, WebP і GIF.");
        var path = CatalogText.ImagePath($"catalog/{name}/{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}.{extension}");
        await folder.SaveAsync(path, content, ct);
        audit.Record($"picture uploaded: {path}, {content.Length} bytes, by user {userId}");
        return new PictureDto(path);
    }
}
