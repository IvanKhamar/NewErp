using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Domain.Cashbox;
using NewErp.Domain.Delivery;
using NewErp.Domain.Payments;
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
    // Id статусів і мови як у справжній базі.
    public const int LanguageId = 3;
    public const int Pending = 24;   // 1) Уточнюємо
    public const int Completed = 22; // 5) Відправлено
    public const int Manufacturing = 25; // 3) Виготовляється
    public const int Accepted = 18;      // 2) Прийнято
    public const int Cancelled = 19;     // Відмінено
    public const int SizeMOptionValueId = 47; // у замовленні OrderId вибрано розмір M
    // Замовлення з товаром, прив'язаним до моделі не напряму, а через категорію й назву (поза періодом інших тестів).
    public const int CategoryOrderId = 4;
    public const int CategoryProductId = 102; // «Сорочка лляна оверсайз», категорія CategoryId, розмір M, 1 шт.
    public const int CategoryId = 5;
    // ТТН замовлення 2: чекає на відділенні, платне зберігання завтра — має бути в нагадуваннях.
    public const string WaitingTtn = "20450000000002";
    // Замовлення без ТТН, з містом і відділенням із тестового довідника й оплатою при отриманні — для створення ТТН.
    public const int ShippingOrderId = 5;
    public const string KyivRef = "city-kyiv";
    public const string Branch1Ref = "wh-kyiv-1";
    public const string Branch1 = "Відділення №1: вул. Хрещатик, 1";
    // Каса: касир за замовчуванням (тестова заглушка Checkbox «фіскалізує» чеки в пам'яті).
    public const int CashierId = 1;

    public const int CancelledOrderId = 6; // скасоване замовлення клієнта RiskyPhone (для перевірки ризику)
    public const string RiskyPhone = "+38 (093) 555-44-33";
    // Надходження на 2000 грн від клієнта замовлення 2 (сума й прізвище збігаються) — має бути першою підказкою.
    public const string BankAppCode = "tx-2000";

    public const string FeedToken = "test-feed-token";

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
        builder.UseSetting("NovaPoshta:FakeDocuments", "true"); // без ключа API: тестові ТТН заглушки
        builder.UseSetting("Checkbox:FakeReceipts", "true"); // без ключів касирів: тестові чеки заглушки
        builder.UseSetting("RevenueFeed:Token", FeedToken);
        builder.UseSetting("Competitors:CheckTime", "-"); // тести не ходять на сайти конкурентів
    }

    private async Task SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>().UseMySQL(_connectionString!).Options;
        await using var db = new ErpDbContext(options);

        db.DdOrderStatuses.AddRange(
            new DdOrderStatus { OrderStatusId = Pending, LanguageId = LanguageId, Name = "1) Уточнюємо" },
            new DdOrderStatus { OrderStatusId = Completed, LanguageId = LanguageId, Name = "5) Відправлено" },
            new DdOrderStatus { OrderStatusId = Manufacturing, LanguageId = LanguageId, Name = "3) Виготовляється" },
            new DdOrderStatus { OrderStatusId = Accepted, LanguageId = LanguageId, Name = "2) Прийнято" },
            new DdOrderStatus { OrderStatusId = Cancelled, LanguageId = LanguageId, Name = "Відмінено" });
        db.DdOrderSources.Add(new DdOrderSource { OrderSourceId = 1, LanguageId = LanguageId, Name = "Instagram" });
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
        db.DdOrderProducts.Add(new DdOrderProduct { OrderProductId = 1, OrderId = OrderId, ProductId = ProductId, Name = "Сукня", Model = "T-1", Quantity = 2, Price = 750m, Total = 1500m });
        // Розмір M у замовленні: опція товару (product_option_value) і вибір покупця (order_option), як їх пише OpenCart.
        db.DdProductOptionValues.Add(new DdProductOptionValue
        {
            ProductOptionValueId = 501, ProductOptionId = 50, ProductId = ProductId, OptionId = 11, OptionValueId = SizeMOptionValueId,
            PricePrefix = "+", PointsPrefix = "+", WeightPrefix = "+",
        });
        db.DdOrderOptions.Add(new DdOrderOption
        {
            OrderId = OrderId, OrderProductId = 1, ProductOptionId = 50, ProductOptionValueId = 501, Name = "Розмір", Value = "M", Type = "radio",
        });

        db.DdOrders.Add(Order(CategoryOrderId, Pending, 900m, new DateTime(2026, 9, 1, 12, 0, 0)));
        db.DdOrderProducts.Add(new DdOrderProduct { OrderProductId = 2, OrderId = CategoryOrderId, ProductId = CategoryProductId, Name = "Сорочка", Model = "T-2", Quantity = 1, Price = 900m, Total = 900m });
        db.DdProductToCategories.Add(new DdProductToCategory { ProductId = CategoryProductId, CategoryId = CategoryId });
        db.DdProductDescriptions.Add(new DdProductDescription
        {
            ProductId = CategoryProductId, LanguageId = LanguageId, Name = "Сорочка лляна оверсайз",
            Description = "", Tag = "", MetaTitle = "", MetaDescription = "", MetaKeyword = "",
        });
        db.DdProductOptionValues.Add(new DdProductOptionValue
        {
            ProductOptionValueId = 502, ProductOptionId = 51, ProductId = CategoryProductId, OptionId = 11, OptionValueId = SizeMOptionValueId,
            PricePrefix = "+", PointsPrefix = "+", WeightPrefix = "+",
        });
        db.DdOrderOptions.Add(new DdOrderOption
        {
            OrderId = CategoryOrderId, OrderProductId = 2, ProductOptionId = 51, ProductOptionValueId = 502, Name = "Розмір", Value = "M", Type = "radio",
        });
        db.ErpOrderManagers.Add(new ErpOrderManager { OrderId = 2, UserId = ActiveUserId, AssignedByUserId = ActiveUserId, AssignedAt = DateTime.Now });

        var shipment = new ShipmentDocument(2, WaitingTtn, 70m);
        var tomorrow = DateOnly.FromDateTime(DateTime.Now).AddDays(1);
        shipment.ApplyTracking(new TrackingInfo(WaitingTtn, "Прибув у відділення", 7, "Київ, відділення 1", false, DateTime.Now, null, tomorrow));
        db.ShipmentDocuments.Add(shipment);

        var shippingOrder = Order(ShippingOrderId, Pending, 1200m, new DateTime(2026, 8, 1, 12, 0, 0));
        (shippingOrder.ShippingCity, shippingOrder.ShippingAddress2, shippingOrder.PaymentCode) = ("Київ", Branch1, "cod");
        db.DdOrders.Add(shippingOrder);
        db.NovaPoshtaWarehouses.AddRange(
            new NovaPoshtaWarehouse(Branch1Ref, Branch1, "Branch", KyivRef, "Київ", 30, working: true),
            new NovaPoshtaWarehouse("wh-kyiv-2", "Поштомат №2", "Postomat", KyivRef, "Київ", 20, working: true),
            new NovaPoshtaWarehouse("wh-kyiv-3", "Відділення №3", "Branch", KyivRef, "Київ", 30, working: false),
            new NovaPoshtaWarehouse("wh-kovel-1", "Відділення №1", "Branch", "city-kovel", "Ковель", 30, working: true));

        db.Cashiers.Add(new Cashier("Олена Каса", "", "", null, 0, 0, isActive: true, DateTime.Now));
        db.BankPayments.Add(new BankPayment(BankAppCode, "UA00", 2000m, 5000m, "Оплата за замовлення 2", "Тарас Клієнт2",
            new DateOnly(2026, 10, 3), new TimeOnly(10, 0)));

        // Товар CategoryProductId у каталозі (ціна й розмір M) — для створення замовлення вручну.
        db.DdProducts.Add(Product(CategoryProductId, "T-2", 900m));
        db.DdOptionDescriptions.Add(new DdOptionDescription { OptionId = 11, LanguageId = LanguageId, Name = "Розмір" });
        db.DdOptionValueDescriptions.Add(new DdOptionValueDescription { OptionValueId = SizeMOptionValueId, LanguageId = LanguageId, OptionId = 11, Name = "M" });
        var cancelled = Order(CancelledOrderId, Cancelled, 700m, new DateTime(2025, 3, 1, 12, 0, 0));
        (cancelled.Telephone, cancelled.ShippingMethod, cancelled.ShippingCode, cancelled.PaymentMethod, cancelled.PaymentCode) =
            (RiskyPhone, "Нова Пошта", "novaposhta_warehouse.novaposhta_warehouse", "Накладений платіж", "cod");
        db.DdOrders.Add(cancelled);

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

    internal static DdProduct Product(int id, string model, decimal price) => new()
    {
        ProductId = id, Model = model, Price = price, Quantity = 5, Status = true, Shipping = true, Subtract = true,
        Sku = "", Upc = "", Ean = "", Jan = "", Isbn = "", Mpn = "", Location = "",
        DateAvailable = new DateTime(2026, 1, 1), DateAdded = new DateTime(2026, 1, 1), DateModified = new DateTime(2026, 1, 1),
    };

    internal static DdOrder Order(int id, int statusId, decimal total, DateTime date) => new()
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