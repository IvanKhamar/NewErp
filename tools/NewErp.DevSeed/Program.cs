// Наповнює локальну базу newerp_dev вигаданими даними для розробки.
// Справжніх даних клієнтів тут немає й не має бути: усі імена, телефони й адреси генерує Bogus.
//
// Запуск: dotnet run --project tools/NewErp.DevSeed
// Рядок підключення береться з user-secrets (ConnectionStrings:ErpDb), як і в NewErp.Api.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using MySql.Data.MySqlClient;

using NewErp.DevSeed;
using NewErp.Infrastructure;
using NewErp.Infrastructure.Persistence;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString(DependencyInjection.ConnectionStringName)
    ?? throw new InvalidOperationException("Немає ConnectionStrings:ErpDb. Див. README, розділ про локальну базу.");

// Запобіжник: сідер пише вигадані замовлення, тож працює лише з локальною базою розробки,
// навіть якщо в user-secrets випадково опиниться підключення до справжньої бази магазину.
const string DevDatabase = "newerp_dev";
var target = new MySqlConnectionStringBuilder(connectionString);
string[] localHosts = ["localhost", "127.0.0.1", "::1"];
if (!localHosts.Contains(target.Server, StringComparer.OrdinalIgnoreCase) || !string.Equals(target.Database, DevDatabase, StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Відмова: сідер працює лише з базою {DevDatabase} на localhost, а підключення вказує на {target.Database} на {target.Server}.");
    return 2;
}

var options = new DbContextOptionsBuilder<ErpDbContext>().UseMySQL(connectionString).Options;
await using var db = new ErpDbContext(options);

if (await db.DdOrders.AnyAsync())
{
    Console.WriteLine("У базі вже є замовлення. Сідер працює лише з порожньою базою, тож нічого не змінено.");
    return 1;
}

var counts = await new DevDataSeeder(db, seed: 2026).SeedAsync(orderCount: 400);
Console.WriteLine($"Готово: {counts}");
Console.WriteLine($"Вхід (пароль {DevDataSeeder.DevPassword}): admin — Admin, sklad — Storekeeper, manager1…manager4 — Manager (manager5 вимкнено).");
return 0;
