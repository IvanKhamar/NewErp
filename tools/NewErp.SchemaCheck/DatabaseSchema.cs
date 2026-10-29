using System.Data.Common;

using MySql.Data.MySqlClient;

namespace NewErp.SchemaCheck;

/// <summary>Стовпець, який є в базі. HasDefault і Generated: значення з'явиться й без нас (типове, автоінкремент, обчислюване).</summary>
public sealed record ActualColumn(string Table, string Column, string ColumnType, bool Nullable, bool HasDefault, bool Generated);

public sealed record StatusRow(int StatusId, int LanguageId, string Name);

public sealed record SourceRow(int SourceId, int LanguageId, string Name);

/// <summary>Що є в базі: стовпці й кілька довідників налаштувань. Персональних даних (замовлень, клієнтів) тут немає.</summary>
public sealed record DatabaseSnapshot(
    IReadOnlyList<ActualColumn> Columns,
    IReadOnlyList<StatusRow> Statuses,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<SourceRow> Sources);

public static class DatabaseSchema
{
    /// <summary>Ключі dd_setting, які порівнюємо з налаштуваннями NewErp.</summary>
    public static readonly string[] SettingKeys = ["config_complete_status", "config_processing_status"];

    public static async Task<DatabaseSnapshot> ReadAsync(MySqlConnection connection, CancellationToken ct)
    {
        // Лише читання: навіть помилка в цьому коді не змінить базу.
        await ExecuteAsync(connection, "SET SESSION TRANSACTION READ ONLY", ct);

        var columns = await QueryAsync(connection,
            "SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, EXTRA FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()",
            r => new ActualColumn(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3) == "YES", !r.IsDBNull(4),
                Generated(r.IsDBNull(5) ? "" : r.GetString(5))),
            ct);

        // Довідники читаємо, лише якщо в них є потрібні стовпці: інакше це вже помилка схеми, і запит упав би.
        var statuses = Has(columns, "dd_order_status", "order_status_id", "language_id", "name")
            ? await QueryAsync(connection, "SELECT order_status_id, language_id, name FROM dd_order_status",
                r => new StatusRow(r.GetInt32(0), r.GetInt32(1), r.GetString(2)), ct)
            : [];
        var sources = Has(columns, "dd_order_source", "order_source_id", "language_id", "name")
            ? await QueryAsync(connection, "SELECT order_source_id, language_id, name FROM dd_order_source",
                r => new SourceRow(r.GetInt32(0), r.GetInt32(1), r.GetString(2)), ct)
            : [];
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Has(columns, "dd_setting", "store_id", "key", "value"))
        {
            await using var command = new MySqlCommand("SELECT `key`, value FROM dd_setting WHERE store_id = 0 AND `key` IN (@a, @b)", connection);
            command.Parameters.AddWithValue("@a", SettingKeys[0]);
            command.Parameters.AddWithValue("@b", SettingKeys[1]);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                settings[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
        }

        return new DatabaseSnapshot(columns, statuses, settings, sources);
    }

    // auto_increment і обчислювані стовпці (VIRTUAL/STORED GENERATED) база заповнює сама; DEFAULT_GENERATED — це типове значення-вираз.
    private static bool Generated(string extra) =>
        extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase)
        || extra.Contains("VIRTUAL GENERATED", StringComparison.OrdinalIgnoreCase)
        || extra.Contains("STORED GENERATED", StringComparison.OrdinalIgnoreCase)
        || extra.Contains("DEFAULT_GENERATED", StringComparison.OrdinalIgnoreCase);

    private static bool Has(IReadOnlyList<ActualColumn> columns, string table, params string[] names) =>
        names.All(n => columns.Any(c => c.Table.Equals(table, StringComparison.OrdinalIgnoreCase) && c.Column.Equals(n, StringComparison.OrdinalIgnoreCase)));

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<T>> QueryAsync<T>(MySqlConnection connection, string sql, Func<DbDataReader, T> map, CancellationToken ct)
    {
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct))
            rows.Add(map(reader));
        return rows;
    }
}
