// Порівнює базу з тим, що чекає NewErp (фаза 14: копія справжньої бази магазину перед запуском).
// Читає лише information_schema і довідники налаштувань (статуси, мова, джерела); замовлень і клієнтів не читає,
// тож звіт можна показувати. Базу не змінює: сесія лише для читання.
//
// Запуск: dotnet run --project tools/NewErp.SchemaCheck -- --database newerp_shop_copy
// Рядок підключення — ConnectionStrings:ErpDb з user-secrets (як у NewErp.Api); --database замінює лише назву бази.
// Код виходу: 0 — помилок немає, 1 — є помилки, 2 — не вдалося підключитися або неправильні аргументи.

using System.Text;

using Microsoft.Extensions.Configuration;

using MySql.Data.MySqlClient;

using NewErp.Infrastructure;
using NewErp.SchemaCheck;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length is not (0 or 2) || args.Length == 2 && args[0] != "--database")
{
    Console.WriteLine("Використання: dotnet run --project tools/NewErp.SchemaCheck -- [--database <назва бази>]");
    return 2;
}

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "api-appsettings.json"), optional: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

if (configuration.GetConnectionString(DependencyInjection.ConnectionStringName) is not { } connectionString)
{
    Console.WriteLine("Немає ConnectionStrings:ErpDb. Див. README, розділ про локальну базу.");
    return 2;
}

var target = new MySqlConnectionStringBuilder(connectionString);
if (args.Length == 2)
    target.Database = args[1];
// Сам рядок підключення не друкуємо: у ньому пароль.
Console.WriteLine($"База {target.Database} на {target.Server}");
Console.WriteLine();

IReadOnlyList<Finding> findings;
try
{
    findings = await Checker.RunAsync(target.ConnectionString, configuration);
}
catch (MySqlException e)
{
    Console.WriteLine($"Не вдалося прочитати базу: {e.Message}");
    return 2;
}

Console.Write(Checker.Report(findings));
return findings.Any(f => f.Severity == Severity.Error) ? 1 : 0;
