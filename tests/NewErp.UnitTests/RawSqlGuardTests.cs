using System.Text.RegularExpressions;

namespace NewErp.UnitTests;

/// <summary>
/// Захист від SQL-ін'єкцій: сирий SQL, який склеюють із рядків, є лише у файлах зі списку, і там у нього йдуть тільки числа.
/// Новий такий виклик спершу перевіряють і дописують сюди; краще — LINQ або ExecuteSqlInterpolated (він передає параметри).
/// </summary>
public partial class RawSqlGuardTests
{
    private static readonly string[] Allowed =
    [
        "src/NewErp.Infrastructure/Catalog/PreOrderStore.cs",   // IN (…) зі списку int
        "src/NewErp.Infrastructure/Catalog/ProductSortStore.cs", // UPDATE … CASE з int id і позицій
    ];

    [Fact]
    public void RawSql_OnlyInReviewedFiles()
    {
        var root = RepoRoot();
        var offenders = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => RawSql().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Except(Allowed)
            .ToList();

        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"\b(FromSqlRaw|ExecuteSqlRaw|ExecuteSqlRawAsync|SqlQueryRaw)\s*[<(]")]
    private static partial Regex RawSql();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NewErp.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Не знайдено корінь репозиторію (NewErp.slnx).");
    }
}
