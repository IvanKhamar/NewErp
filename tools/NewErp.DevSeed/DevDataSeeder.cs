using Bogus;

using NewErp.Domain.Stock;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.DevSeed;

/// <summary>Генерує узгоджений набір вигаданих даних: довідники, менеджерів, замовлення, склад.</summary>
public sealed class DevDataSeeder(ErpDbContext db, int seed)
{
    private const int LanguageId = 1;

    // Стандартні id статусів OpenCart 3, назви українською.
    private static readonly (int Id, string Name)[] Statuses =
    [
        (1, "Очікує"), (2, "В обробці"), (3, "Відправлено"), (5, "Виконано"),
        (7, "Скасовано"), (11, "Повернення"), (15, "Оплачено"),
    ];

    private static readonly string[] Sources = ["Сайт", "Instagram", "Телефон", "Партнер"];

    private static readonly string[] Products =
    [
        "Сукня лляна", "Сорочка оверсайз", "Штани палаццо", "Костюм трикотажний",
        "Спідниця міді", "Жакет класичний", "Футболка базова", "Кардиган в'язаний",
    ];

    private readonly Random _random = new(seed);

    public async Task<string> SeedAsync(int orderCount)
    {
        Randomizer.Seed = new Random(seed); // однаковий seed = однакові дані при кожному запуску
        var now = DateTime.Today;

        foreach (var (id, name) in Statuses)
            db.DdOrderStatuses.Add(new DdOrderStatus { OrderStatusId = id, LanguageId = LanguageId, Name = name });
        for (var i = 0; i < Sources.Length; i++)
            db.DdOrderSources.Add(new DdOrderSource { OrderSourceId = i + 1, LanguageId = LanguageId, Name = Sources[i] });

        var userFaker = new Faker<DdUser>("uk")
            .RuleFor(u => u.UserGroupId, 10)
            .RuleFor(u => u.Firstname, f => f.Name.FirstName())
            .RuleFor(u => u.Lastname, f => f.Name.LastName())
            .RuleFor(u => u.Username, f => $"manager{f.IndexFaker + 1}")
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Firstname, u.Lastname, "example.com"))
            .RuleFor(u => u.Password, "")
            .RuleFor(u => u.Salt, "")
            .RuleFor(u => u.Image, "")
            .RuleFor(u => u.Code, "")
            .RuleFor(u => u.Ip, "127.0.0.1")
            .RuleFor(u => u.Status, true)
            .RuleFor(u => u.DateAdded, f => f.Date.Past(2, now));
        var users = userFaker.Generate(5);
        users[^1].Status = false; // один звільнений менеджер, щоб було що перевіряти
        db.DdUsers.AddRange(users);
        await db.SaveChangesAsync();

        var orderFaker = new Faker<DdOrder>("uk")
            .RuleFor(o => o.Firstname, f => f.Name.FirstName())
            .RuleFor(o => o.Lastname, f => f.Name.LastName())
            .RuleFor(o => o.Email, (f, o) => f.Internet.Email(o.Firstname, o.Lastname, "example.com"))
            .RuleFor(o => o.Telephone, f => f.Phone.PhoneNumber("+38067#######"))
            .RuleFor(o => o.ShippingCity, f => f.Address.City())
            .RuleFor(o => o.ShippingAddress1, f => $"Нова Пошта, відділення №{f.Random.Int(1, 250)}")
            .RuleFor(o => o.ShippingMethod, f => f.PickRandom("Нова Пошта: відділення", "Нова Пошта: поштомат", "Укрпошта", "Самовивіз (Київ)"))
            .RuleFor(o => o.PaymentMethod, f => f.PickRandom("Накладений платіж", "LiqPay", "Monobank", "Переказ на рахунок"))
            .RuleFor(o => o.Comment, f => f.Random.Bool(0.2f) ? f.Lorem.Sentence() : "")
            .RuleFor(o => o.OrderStatusId, f => f.PickRandom(Statuses).Id)
            .RuleFor(o => o.OrderSourceId, f => f.Random.Int(1, Sources.Length))
            .RuleFor(o => o.DateAdded, f => f.Date.Between(now.AddDays(-180), now))
            .RuleFor(o => o.DateModified, (f, o) => o.DateAdded)
            .RuleFor(o => o.CurrencyCode, "UAH")
            .RuleFor(o => o.CurrencyId, 1)
            .RuleFor(o => o.CurrencyValue, 1m)
            .RuleFor(o => o.LanguageId, LanguageId)
            .RuleFor(o => o.StoreName, "Demo Store (dev)")
            .RuleFor(o => o.StoreUrl, "http://localhost/")
            .RuleFor(o => o.InvoicePrefix, "DEV-")
            .RuleFor(o => o.Ip, "127.0.0.1");
        var orders = orderFaker.Generate(orderCount);
        foreach (var o in orders)
            FillOpenCartRequiredFields(o);
        db.DdOrders.AddRange(orders);
        await db.SaveChangesAsync(); // після збереження EF заповнює OrderId з бази

        var managers = users.Where(u => u.Status).ToList();
        var lines = 0;
        foreach (var order in orders)
        {
            order.Total = 0;
            var lineCount = _random.Next(1, 4);
            for (var i = 0; i < lineCount; i++)
            {
                var price = _random.Next(8, 45) * 100m;
                var qty = _random.Next(1, 3);
                db.DdOrderProducts.Add(new DdOrderProduct
                {
                    OrderId = order.OrderId,
                    ProductId = _random.Next(50, 400),
                    Name = Products[_random.Next(Products.Length)],
                    Model = $"VD-{_random.Next(1000, 9999)}",
                    Quantity = qty,
                    Price = price,
                    Total = price * qty,
                });
                order.Total += price * qty;
                lines++;
            }

            if (_random.NextDouble() < 0.75)
            {
                var manager = managers[_random.Next(managers.Count)];
                db.ErpOrderManagers.Add(new ErpOrderManager
                {
                    OrderId = order.OrderId,
                    UserId = manager.UserId,
                    AssignedByUserId = manager.UserId,
                    AssignedAt = order.DateAdded.AddMinutes(15),
                });

                if (order.OrderStatusId != 1)
                {
                    db.DdOrderStatusLogs.Add(new DdOrderStatusLog
                    {
                        OrderId = order.OrderId,
                        UserId = manager.UserId,
                        OldStatusId = 1,
                        NewStatusId = order.OrderStatusId,
                        DateAdded = order.DateAdded.AddHours(_random.Next(1, 48)),
                    });
                }
            }
        }
        await db.SaveChangesAsync();

        var stockItems = await SeedStockAsync(users[0].UserId, now);
        return $"{users.Count} користувачів, {orders.Count} замовлень, {lines} рядків товарів, {stockItems} позицій складу.";
    }

    private async Task<int> SeedStockAsync(int userId, DateTime now)
    {
        var fabric = new StockCategory("Тканини");
        var fittings = new StockCategory("Фурнітура");
        var packaging = new StockCategory("Пакування");
        db.StockCategories.AddRange(fabric, fittings, packaging);
        var supplier = new StockSupplier("ТОВ \"Текстиль-Опт\" (вигаданий)", "+380441234567", null);
        var supplier2 = new StockSupplier("ФОП Фурнітура (вигаданий)", null, "Доставка по вівторках");
        db.StockSuppliers.AddRange(supplier, supplier2);
        await db.SaveChangesAsync();

        (StockCategory Cat, StockSupplier? Sup, string Name, string Unit, decimal Min, decimal Start)[] items =
        [
            (fabric, supplier, "Льон бежевий", "м", 30, 85.5m),
            (fabric, supplier, "Трикотаж чорний", "м", 40, 22m),
            (fabric, supplier, "Віскоза принт", "м", 20, 64m),
            (fittings, supplier2, "Ґудзики дерев'яні 15 мм", "шт", 200, 1350),
            (fittings, supplier2, "Блискавка прихована 20 см", "шт", 100, 45),
            (packaging, null, "Пакет кур'єрський 30x40", "шт", 300, 520),
            (packaging, null, "Бірка з логотипом", "шт", 500, 180),
        ];

        foreach (var x in items)
        {
            var item = new StockItem(x.Cat.Id, x.Sup?.Id, x.Name, x.Unit, x.Min);
            db.StockItems.Add(item);
            db.StockMovements.Add(item.Adjust(x.Start, "Початковий залишок", userId, now.AddDays(-30)));
        }
        await db.SaveChangesAsync();
        return items.Length;
    }

    /// <summary>OpenCart вимагає NOT NULL у багатьох колонках, які нам не потрібні. Заповнюємо порожніми значеннями.</summary>
    private static void FillOpenCartRequiredFields(DdOrder o)
    {
        o.Fax = o.CustomField = o.PaymentCustomField = o.ShippingCustomField = "";
        o.PaymentFirstname = o.ShippingFirstname = o.Firstname;
        o.PaymentLastname = o.ShippingLastname = o.Lastname;
        o.PaymentCompany = o.ShippingCompany = "";
        o.PaymentAddress1 = o.ShippingAddress1;
        o.PaymentAddress2 = o.ShippingAddress2 = "";
        o.PaymentCity = o.ShippingCity;
        o.PaymentPostcode = o.ShippingPostcode = "";
        o.PaymentCountry = o.ShippingCountry = "Україна";
        o.PaymentCountryId = o.ShippingCountryId = 220;
        o.PaymentZone = o.ShippingZone = "";
        o.PaymentAddressFormat = o.ShippingAddressFormat = "";
        o.PaymentCode = o.ShippingCode = "";
        o.Tracking = o.ForwardedIp = o.UserAgent = o.AcceptLanguage = "";
    }
}