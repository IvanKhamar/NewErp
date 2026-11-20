using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NewErp.Application.Catalog;

namespace NewErp.Infrastructure.Catalog;

/// <summary>Налаштування Pictures: папка image/ вітрини OpenCart на спільному томі. Порожньо — завантаження вимкнене.</summary>
public sealed class PictureOptions
{
    public const string SectionName = "Pictures";

    public string ImageDir { get; set; } = "";
}

/// <summary>Папка картинок вітрини, коли ERP працює там, де може в неї писати (спільний том).</summary>
public sealed class LocalPictureFolder(IOptions<PictureOptions> options, ILogger<LocalPictureFolder> logger) : IPictureFolder
{
    private readonly string? _root = string.IsNullOrWhiteSpace(options.Value.ImageDir) ? null : Path.GetFullPath(options.Value.ImageDir);

    public bool Enabled => _root is not null && Directory.Exists(_root);

    public async Task SaveAsync(string path, byte[] content, CancellationToken ct)
    {
        var target = Inside(path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        // CreateNew: файл, що вже є, ніколи не перезаписуємо (тоді виняток іде далі, а чужий файл лишається).
        var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            await using (file)
                await file.WriteAsync(content, ct);
        }
        catch
        {
            File.Delete(target); // без недописаної картинки: цей файл створили ми
            throw;
        }
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var target = Inside(path);
        try
        {
            if (File.Exists(target)) // файлу (чи його папки) вже немає — теж гаразд
                File.Delete(target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Picture file {Path} was not deleted: {Error}", path, e.GetType().Name);
        }
        return Task.CompletedTask;
    }

    /// <summary>Шляхи дає сервіс, але це другий замок: шлях не може вийти за папку.</summary>
    private string Inside(string path)
    {
        var root = _root ?? throw new InvalidOperationException("Папку картинок не налаштовано.");
        var target = Path.GetFullPath(Path.Combine(root, path));
        if (!target.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Шлях картинки поза папкою картинок.");
        return target;
    }
}
