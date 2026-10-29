using Microsoft.Extensions.Configuration;

using MySql.Data.MySqlClient;

using NewErp.SchemaCheck;

namespace NewErp.IntegrationTests;

/// <summary>
/// Перевірка схеми (фаза 14). Перші тести — без бази; MySqlFact будують тимчасову базу з db/*.sql. Її повна відповідність моделі
/// заодно стежить, щоб модель EF Core і SQL-файли не розійшлися.
/// </summary>
public class SchemaCheckTests
{
    private static readonly IConfiguration NoSettings = new ConfigurationBuilder().Build();

    private static ExpectedColumn Want(string column, string type, bool nullable = false, bool explicitType = true) =>
        new("dd_order", column, type, nullable, explicitType);

    private static ActualColumn Have(string column, string type, bool nullable = false, bool hasDefault = false, bool generated = false) =>
        new("dd_order", column, type, nullable, hasDefault, generated);

    [Fact]
    public void Compare_FindsMissingTablesAndColumns_NullabilityBothWays_AndExtraRequiredColumns()
    {
        var findings = SchemaComparer.Compare(
            [
                Want("order_id", "int"), Want("telephone", "varchar(32)"), Want("firstname", "varchar(32)"), Want("comment", "text", nullable: true),
                new ExpectedColumn("erp_missing", "id", "int", false, true),
            ],
            [
                Have("order_id", "int(11)", generated: true), Have("firstname", "varchar(32)", nullable: true), Have("comment", "text"),
                Have("store_flag", "int", hasDefault: true), Have("extra_required", "int"), Have("note", "varchar(9)", nullable: true),
            ]);

        Assert.Equal(
            [
                (Severity.Error, "dd_order", "Немає стовпця telephone (varchar(32))."),
                (Severity.Error, "dd_order", "Стовпець firstname у базі може бути NULL, а NewErp чекає значення завжди: читання рядка з NULL упаде."),
                (Severity.Error, "erp_missing", "Таблиці немає."),
                (Severity.Warning, "dd_order", "Стовпець comment у базі NOT NULL, а NewErp допускає порожнє значення: запис NULL не пройде."),
                (Severity.Warning, "dd_order",
                    "Зайвий стовпець extra_required (int) NOT NULL без значення за замовчуванням: новий рядок від NewErp сюди не запишеться."),
            ],
            findings.Select(f => (f.Severity, f.Table, f.Message)));
    }

    [Theory]
    [InlineData("int", "int(11)", true, true)]              // MySQL 5.7 пише ширину показу, 8.0 — ні
    [InlineData("int unsigned", "INT(10) UNSIGNED", true, true)]
    [InlineData("tinyint(1)", "tinyint(1)", true, true)]    // bool
    [InlineData("tinyint(1)", "tinyint(4)", true, false)]
    [InlineData("varchar(96)", "varchar(64)", true, false)] // коротший стовпець обріже дані
    [InlineData("decimal(15,4)", "decimal(15,2)", true, false)]
    [InlineData("longtext", "varchar(255)", false, true)]   // тип вибрав провайдер: порівнюємо лише вид
    [InlineData("decimal(18,2)", "decimal(15,4)", false, true)]
    [InlineData("datetime(6)", "datetime", false, true)]
    [InlineData("int", "varchar(10)", false, false)]
    public void SameType_ComparesExactTypesExactly_AndProviderTypesByKind(string want, string have, bool explicitType, bool same) =>
        Assert.Equal(same, SchemaComparer.SameType(Want("c", want, explicitType: explicitType), have));

    [Fact]
    public void Settings_UnknownStatusIsAnError_AndCompleteStatusesAreComparedWithPaidVouchers()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Erp:LanguageId"] = "3",
            ["Bonus:CountedStatusIds:0"] = "22",
            ["Bonus:CountedStatusIds:1"] = "99",
            ["Vouchers:PaidStatusIds:0"] = "22",
            ["Vouchers:PaidStatusIds:1"] = "20",
        }).Build();
        var snapshot = new DatabaseSnapshot([], [new(22, 3, "5) Відправлено"), new(20, 3, "Виконано (B2B)"), new(22, 1, "Shipped")],
            new Dictionary<string, string> { ["config_complete_status"] = "[\"22\"]" }, [new(1, 3, "Сайт")]);

        var findings = SettingsCheck.Check(settings, snapshot);

        Assert.Equal("Bonus:CountedStatusIds: статусу 99 немає в dd_order_status мовою 3.", Assert.Single(findings, f => f.Severity == Severity.Error).Message);
        Assert.StartsWith("Vouchers:PaidStatusIds (22, 20) не збігається з config_complete_status (22)",
            Assert.Single(findings, f => f.Severity == Severity.Warning).Message);
        Assert.Contains(findings, f => f.Message == "Bonus:CountedStatusIds: 22 «5) Відправлено», 99 (немає).");
        Assert.Contains(findings, f => f.Message == "Джерела замовлень (dd_order_source): 1 «Сайт».");
    }

    [Fact]
    public void Settings_LanguageWithoutStatuses_IsAnError()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Erp:LanguageId"] = "3" }).Build();
        var snapshot = new DatabaseSnapshot([], [new(1, 1, "Pending"), new(1, 2, "Очікує")], new Dictionary<string, string>(), []);

        var error = Assert.Single(SettingsCheck.Check(settings, snapshot));

        Assert.Equal((Severity.Error, "Erp:LanguageId = 3, а статусів цією мовою в dd_order_status немає (є мови: 1, 2)."), (error.Severity, error.Message));
    }

    [Theory]
    [InlineData("[\"5\",\"22\"]", new[] { 5, 22 })]                       // OpenCart 2+: JSON
    [InlineData("a:2:{i:0;s:1:\"5\";i:1;s:2:\"22\";}", new[] { 5, 22 })] // старіший серіалізований масив PHP
    [InlineData("5", new[] { 5 })]
    public void StatusIds_ReadsEveryFormatOpenCartUses(string value, int[] expected) =>
        Assert.Equal(expected, SettingsCheck.StatusIds(value));

    [MySqlFact]
    public async Task DatabaseFromSqlFiles_MatchesTheModelCompletely()
    {
        var connectionString = await TestDatabase.CreateAsync();
        try
        {
            var findings = await Checker.RunAsync(connectionString, NoSettings);

            Assert.DoesNotContain(findings, f => f.Severity != Severity.Info);
        }
        finally
        {
            await TestDatabase.DropAsync(connectionString);
        }
    }

    [MySqlFact]
    public async Task ChangedColumns_AreReported_AndNothingElse()
    {
        var connectionString = await TestDatabase.CreateAsync();
        try
        {
            await ExecuteAsync(connectionString,
                "ALTER TABLE dd_order DROP COLUMN telephone, MODIFY firstname varchar(32) NULL, MODIFY email varchar(50) NOT NULL, ADD COLUMN extra_flag int NOT NULL");

            var findings = await Checker.RunAsync(connectionString, NoSettings);

            // Порядок усередині таблиці — як у моделі, і він не важливий, тож порівнюємо без порядку.
            Assert.Equivalent(
                new[]
                {
                    (Severity.Error, "Немає стовпця telephone (varchar(32))."),
                    (Severity.Error, "Стовпець firstname у базі може бути NULL, а NewErp чекає значення завжди: читання рядка з NULL упаде."),
                    (Severity.Warning, "Стовпець email: NewErp чекає varchar(96), у базі varchar(50)."),
                    (Severity.Warning, "Зайвий стовпець extra_flag (int) NOT NULL без значення за замовчуванням: новий рядок від NewErp сюди не запишеться."),
                },
                findings.Where(f => f.Severity != Severity.Info).Select(f => (f.Severity, f.Message)).ToArray(), strict: true);
            Assert.All(findings.Where(f => f.Severity != Severity.Info), f => Assert.Equal("dd_order", f.Table));
        }
        finally
        {
            await TestDatabase.DropAsync(connectionString);
        }
    }

    [MySqlFact]
    public async Task EmptyDatabase_IsOneClearError()
    {
        var connectionString = await TestDatabase.CreateAsync();
        var empty = new MySqlConnectionStringBuilder(connectionString) { Database = new MySqlConnectionStringBuilder(connectionString).Database + "_e" };
        await ExecuteAsync(connectionString, $"CREATE DATABASE `{empty.Database}`");
        try
        {
            var error = Assert.Single(await Checker.RunAsync(empty.ConnectionString, NoSettings));

            Assert.Equal(Severity.Error, error.Severity);
            Assert.Contains("немає жодної таблиці", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            await ExecuteAsync(connectionString, $"DROP DATABASE IF EXISTS `{empty.Database}`");
            await TestDatabase.DropAsync(connectionString);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
