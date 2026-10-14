using MySql.Data.MySqlClient;

namespace NewErp.IntegrationTests;

/// <summary>Створює окрему тимчасову базу з db/*.sql для кожного набору тестів і видаляє її після.</summary>
public static class TestDatabase
{
    public const string EnvVar = "NEWERP_TEST_DB";

    /// <summary>Підключення до сервера без назви бази, напр. "server=localhost;user=newerp;password=...".</summary>
    public static string? ServerConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    private static readonly string[] SchemaFiles = ["schema.sql", "reconstructed.sql", "erp.sql"];

    public static async Task<string> CreateAsync()
    {
        var name = $"newerp_test_{Guid.NewGuid():N}"[..30];
        await ExecuteAsync(ServerConnectionString!, $"CREATE DATABASE `{name}` CHARACTER SET utf8mb4");

        var connectionString = $"{ServerConnectionString};database={name}";
        var dbDir = Path.Combine(FindRepoRoot(), "db");
        foreach (var file in SchemaFiles)
            await ExecuteAsync(connectionString, await File.ReadAllTextAsync(Path.Combine(dbDir, file)));

        return connectionString;
    }

    public static Task DropAsync(string connectionString)
    {
        var name = new MySqlConnectionStringBuilder(connectionString).Database;
        return ExecuteAsync(ServerConnectionString!, $"DROP DATABASE IF EXISTS `{name}`");
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NewErp.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Не знайдено корінь репозиторію (NewErp.slnx).");
    }
}