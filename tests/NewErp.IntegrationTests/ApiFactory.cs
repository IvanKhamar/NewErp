using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Domain.Stock;
using NewErp.Infrastructure.Auth;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>
/// Запускає справжній NewErp.Api у пам'яті тестового процесу (без мережі) поверх тимчасової бази.
/// Один екземпляр на клас тестів завдяки IClassFixture.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? _connectionString;

    public const int ActiveUserId = 1;
    public const int InactiveUserId = 2;
    public const int ManagerUserId = 3;
    public const int StorekeeperUserId = 4;
    public const string Admin = "олена";
    public const string Inactive = "ігор";
    public const string Manager = "марта";
    public const string Storekeeper = "петро";
    public const string Password = "test-password";
    public const int OrderId = 1;
    public const int ProductId = 101; // товар у замовленні OrderId, 2 шт.
    public const int Pending = 1;
    public const int Completed = 5;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(TestDatabase.ServerConnectionString))
            return; // тести будуть пропущені, базу не створюємо

        _connectionString = await TestDatabase.CreateAsync();
        await SeedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_connectionString is not null)
            await TestDatabase.DropAsync(_connectionString);
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ErpDb", _connectionString ?? "server=unused");
        builder.UseSetting("Bonus:Percent", "10");
        builder.UseSetting("Bonus:CountedStatusIds:0", Completed.ToString());
    }

    private async Task SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>().UseMySQL(_connectionString!).Options;
        await using var db = new ErpDbContext(options);

        db.DdOrderStatuses.AddRange(
            new DdOrderStatus { OrderStatusId = Pending, LanguageId = 1, Name = "Очікує" },
            new DdOrderStatus { OrderStatusId = Completed, LanguageId = 1, Name = "Виконано" });
        db.DdOrderSources.Add(new DdOrderSource { OrderSourceId = 1, LanguageId = 1, Name = "Instagram" });
        db.DdUsers.AddRange(
            User(ActiveUserId, "Олена", active: true),
            User(InactiveUserId, "Ігор", active: false),
            User(ManagerUserId, "Марта", active: true),
            User(StorekeeperUserId, "Петро", active: true));
        var hash = new Pbkdf2PasswordHasher().Hash(Password);
        db.UserLogins.AddRange(
            new ErpUserLogin { UserId = ActiveUserId, PasswordHash = hash, Role = Roles.Admin },
            new ErpUserLogin { UserId = InactiveUserId, PasswordHash = hash, Role = Roles.Manager },
            new ErpUserLogin { UserId = ManagerUserId, PasswordHash = hash, Role = Roles.Manager },
            new ErpUserLogin { UserId = StorekeeperUserId, PasswordHash = hash, Role = Roles.Storekeeper });
        db.DdOrders.Add(Order(OrderId, Pending, 1500m, new DateTime(2026, 10, 1, 12, 0, 0)));
        db.DdOrders.Add(Order(2, Completed, 2000m, new DateTime(2026, 10, 2, 12, 0, 0)));
        db.DdOrders.Add(Order(3, 0, 999m, new DateTime(2026, 10, 3, 12, 0, 0))); // незавершене, не має показуватися
        db.DdOrderProducts.Add(new DdOrderProduct { OrderId = OrderId, ProductId = ProductId, Name = "Сукня", Model = "T-1", Quantity = 2, Price = 750m, Total = 1500m });
        db.ErpOrderManagers.Add(new ErpOrderManager { OrderId = 2, UserId = ActiveUserId, AssignedByUserId = ActiveUserId, AssignedAt = DateTime.Now });

        var category = new StockCategory("Тканини");
        db.StockCategories.Add(category);
        await db.SaveChangesAsync();
    }

    /// <summary>Клієнт, який увійшов як вказаний користувач. Cookie сесії клієнт зберігає сам.</summary>
    public async Task<HttpClient> LoginAsync(string username)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(username, Password));
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static DdUser User(int id, string name, bool active) => new()
    {
        UserId = id,
        UserGroupId = 1,
        Username = name.ToLowerInvariant(),
        Password = "",
        Salt = "",
        Firstname = name,
        Lastname = "Тест",
        Email = "",
        Image = "",
        Code = "",
        Ip = "",
        Status = active,
        DateAdded = DateTime.Now,
    };

    private static DdOrder Order(int id, int statusId, decimal total, DateTime date) => new()
    {
        OrderId = id,
        OrderStatusId = statusId,
        OrderSourceId = 1,
        Total = total,
        DateAdded = date,
        DateModified = date,
        Firstname = "Тарас",
        Lastname = $"Клієнт{id}",
        Telephone = $"+38067000000{id}",
        Email = "",
        CurrencyCode = "UAH",
        InvoicePrefix = "",
        StoreName = "",
        StoreUrl = "",
        Fax = "",
        CustomField = "",
        Comment = "",
        PaymentFirstname = "",
        PaymentLastname = "",
        PaymentCompany = "",
        PaymentAddress1 = "",
        PaymentAddress2 = "",
        PaymentCity = "",
        PaymentPostcode = "",
        PaymentCountry = "",
        PaymentZone = "",
        PaymentAddressFormat = "",
        PaymentCustomField = "",
        PaymentMethod = "",
        PaymentCode = "",
        ShippingFirstname = "",
        ShippingLastname = "",
        ShippingCompany = "",
        ShippingAddress1 = "",
        ShippingAddress2 = "",
        ShippingCity = "",
        ShippingPostcode = "",
        ShippingCountry = "",
        ShippingZone = "",
        ShippingAddressFormat = "",
        ShippingCustomField = "",
        ShippingMethod = "",
        ShippingCode = "",
        Tracking = "",
        Ip = "",
        ForwardedIp = "",
        UserAgent = "",
        AcceptLanguage = "",
    };
}