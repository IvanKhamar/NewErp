using Bogus;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NewErp.Application.Auth;
using NewErp.Application.Production;
using NewErp.Domain.Cashbox;
using NewErp.Domain.Delivery;
using NewErp.Domain.Partners;
using NewErp.Domain.Payments;
using NewErp.Domain.Production;
using NewErp.Domain.Stock;
using NewErp.Infrastructure;
using NewErp.Infrastructure.Auth;
using NewErp.Infrastructure.Catalog;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;
using NewErp.Infrastructure.Production;

namespace NewErp.DevSeed;

/// <summary>Генерує узгоджений набір вигаданих даних: довідники, менеджерів, замовлення, склад.</summary>
public sealed class DevDataSeeder(ErpDbContext db, int seed)
{
    // Як у справжній базі: усі довідники записані з language_id = 3.
    private const int LanguageId = 3;

    /// <summary>Пароль усіх тестових користувачів.</summary>
    public const string DevPassword = "dev";

    // Статуси справжнього сайту (id, назви й кольори взято з dd_order_status). З "1) Уточнюємо" замовлення починається.
    private const int FirstStatusId = 24;

    private static readonly (int Id, string Name, string Color)[] Statuses =
    [
        (24, "1) Уточнюємо", "#48f9f9"), (18, "2) Прийнято", "#ffff00"), (25, "3) Виготовляється", "#ff9900"),
        (21, "4) Чекає на відправку", "#e8d030"), (22, "5) Відправлено", "#00ff00"), (20, "Виконано (B2B)", "#00ff00"),
        (23, "Терміново", "#ff1a1a"), (1, "Перевірити оплату", "#ff00ff"), (17, "В обробці", "#ffffff"), (19, "Відмінено", "#156d2c"),
    ];

    // Id товару = FirstProductId + позиція в масиві, щоб специфікації (BOM) прив'язувались до тих самих товарів, що й у замовленнях.
    private const int FirstProductId = 101;

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

        foreach (var (id, name, color) in Statuses)
            db.DdOrderStatuses.Add(new DdOrderStatus { OrderStatusId = id, LanguageId = LanguageId, Name = name, Color = color });
        for (var i = 0; i < Sources.Length; i++)
            db.DdOrderSources.Add(new DdOrderSource { OrderSourceId = i + 1, LanguageId = LanguageId, Name = Sources[i] });

        var userFaker = new Faker<DdUser>("uk")
            .RuleFor(u => u.UserGroupId, 10)
            .RuleFor(u => u.Firstname, f => f.Person.FirstName)
            .RuleFor(u => u.Lastname, f => f.Person.LastName)
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
        var admin = userFaker.Generate();
        admin.Username = "admin";
        var storekeeper = userFaker.Generate();
        storekeeper.Username = "sklad";
        db.DdUsers.AddRange([.. users, admin, storekeeper]);
        await db.SaveChangesAsync();

        // Сіль зберігається всередині хешу, тому один хеш підходить усім тестовим користувачам.
        var hash = new Pbkdf2PasswordHasher().Hash(DevPassword);
        db.UserLogins.AddRange(users.Select(u => new ErpUserLogin { UserId = u.UserId, PasswordHash = hash, Role = Roles.Manager }));
        db.UserLogins.Add(new ErpUserLogin { UserId = admin.UserId, PasswordHash = hash, Role = Roles.Admin });
        db.UserLogins.Add(new ErpUserLogin { UserId = storekeeper.UserId, PasswordHash = hash, Role = Roles.Storekeeper });
        await db.SaveChangesAsync();

        var orderFaker = new Faker<DdOrder>("uk")
            .RuleFor(o => o.Firstname, f => f.Person.FirstName)
            .RuleFor(o => o.Lastname, f => f.Person.LastName)
            .RuleFor(o => o.Email, (f, o) => f.Internet.Email(o.Firstname, o.Lastname, "example.com"))
            .RuleFor(o => o.Telephone, f => f.Phone.PhoneNumber("+38067#######"))
            .RuleFor(o => o.ShippingCity, f => f.PickRandom(DevCities))
            .RuleFor(o => o.ShippingAddress1, "")
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
        var warehouses = DevWarehouses();
        db.NovaPoshtaWarehouses.AddRange(warehouses);
        foreach (var o in orders)
        {
            FillOpenCartRequiredFields(o);
            // Як на старому сайті: відділення Нової Пошти — в address_2, оплата при отриманні — payment_code "cod".
            var branches = warehouses.Where(w => w.CityDescription == o.ShippingCity).ToList();
            o.ShippingAddress2 = o.ShippingMethod.StartsWith("Нова Пошта", StringComparison.Ordinal)
                ? branches[_random.Next(branches.Count)].Description
                : "";
            o.PaymentCode = o.PaymentMethod == "Накладений платіж" ? "cod" : "";
        }
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
                var product = _random.Next(Products.Length);
                db.DdOrderProducts.Add(new DdOrderProduct
                {
                    OrderId = order.OrderId,
                    ProductId = FirstProductId + product,
                    Name = Products[product],
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

                if (order.OrderStatusId != FirstStatusId)
                {
                    db.DdOrderStatusLogs.Add(new DdOrderStatusLog
                    {
                        OrderId = order.OrderId,
                        UserId = manager.UserId,
                        OldStatusId = FirstStatusId,
                        NewStatusId = order.OrderStatusId,
                        DateAdded = order.DateAdded.AddHours(_random.Next(1, 48)),
                    });
                }
            }
        }
        await db.SaveChangesAsync();

        var shipments = SeedShipments(orders, now);
        await db.SaveChangesAsync();

        var payments = await SeedFinanceAsync(orders, now);

        var stockItems = await SeedStockAsync(storekeeper.UserId, storekeeper.Username, now);
        await SeedSizeOptionsAsync();
        await SeedPartnerAsync(now);
        await SeedProductSortAsync();
        var birthdays = await SeedMarketingAsync();
        var customers = await SeedCustomersAsync();
        var returns = await SeedReturnsAsync();
        var locations = await SeedLocationsAsync();
        var models = await SeedProductionAsync();
        var warnings = await SeedProductionOperationsAsync(storekeeper.UserId, storekeeper.Username);
        return $"{users.Count + 2} користувачів, {orders.Count} замовлень, {lines} рядків товарів, {shipments} ТТН, {payments} надходжень на рахунок, {warehouses.Count} відділень у довіднику, {stockItems} позицій складу, 1 партнер з фідом, 2 мітки товарів, 2 розміри на передзамовленні, {birthdays} клієнтів з днем народження (кілька — сьогодні й цього тижня), {customers} зареєстрованих покупців, {returns} повернень, {locations} склади й пункти видачі, " +
               $"{models} моделей виробництва (крій і пошив: {warnings} попереджень про нестачу).";
    }

    /// <summary>
    /// Каса Checkbox (склад, реквізити, два касири) і вигадана виписка банку: частина платежів уже прив'язана до замовлень,
    /// частина чекає на зіставлення. Кілька замовлень з накладеним платежем отримують передоплату.
    /// </summary>
    private async Task<int> SeedFinanceAsync(List<DdOrder> orders, DateTime now)
    {
        var warehouse = new CashboxWarehouse("Склад Київ", "Вигаданий склад для розробки", now);
        var requisites = new CashboxRequisites("ФОП Тестовий", "Вигадані реквізити для розробки", now);
        db.CashboxWarehouses.Add(warehouse);
        db.CashboxRequisites.Add(requisites);
        await db.SaveChangesAsync();

        db.Cashiers.Add(new Cashier("Каса сайту", "Типовий касир", "", null, warehouse.Id, requisites.Id, true, now));
        db.Cashiers.Add(new Cashier("Каса Nova Pay", "Накладений платіж", "cod", new TimeOnly(23, 0), warehouse.Id, requisites.Id, true, now));

        foreach (var order in orders.Where(o => o.PaymentCode == "cod" && o.Total > 0).Take(5))
            order.Prepayment = 200;

        var count = 0;
        foreach (var order in orders.Where(o => o.Total > 0).Take(8))
        {
            var payment = new BankPayment(
                $"dev-{order.OrderId}", "26000000000000", order.Total, null,
                $"Оплата за замовлення {order.OrderId}", $"{order.Firstname} {order.Lastname}",
                DateOnly.FromDateTime(order.DateAdded), new TimeOnly(10 + count, 15));
            if (count % 2 == 0)
                payment.LinkTo(order.OrderId); // половина вже зіставлена, решта чекає на менеджера
            db.BankPayments.Add(payment);
            count++;
        }
        await db.SaveChangesAsync();
        return count;
    }

    private static readonly string[] DevCities = ["Київ", "Львів", "Харків", "Одеса", "Дніпро", "Конотоп", "Ковель", "Житомир"];

    /// <summary>
    /// Вигаданий довідник Нової Пошти: у кожному місті шість відділень, два поштомати й одне закрите відділення
    /// (його в підказках не видно). Ref вигадані, справжньому API вони нічого не скажуть.
    /// </summary>
    private static List<NovaPoshtaWarehouse> DevWarehouses() =>
    [
        .. DevCities.SelectMany((city, i) => Enumerable.Range(1, 6)
            .Select(n => new NovaPoshtaWarehouse($"dev-wh-{i}-{n}", $"Відділення №{n}: вул. Тестова, {n}", "Branch", $"dev-city-{i}", city, 30, working: true))
            .Append(new NovaPoshtaWarehouse($"dev-wh-{i}-p1", $"Поштомат \"Нова Пошта\" №{100 + i}", "Postomat", $"dev-city-{i}", city, 20, working: true))
            .Append(new NovaPoshtaWarehouse($"dev-wh-{i}-p2", $"Поштомат \"Нова Пошта\" №{200 + i}", "Postomat", $"dev-city-{i}", city, 20, working: true))
            .Append(new NovaPoshtaWarehouse($"dev-wh-{i}-x", "Відділення №99: зачинено", "Branch", $"dev-city-{i}", city, 30, working: false))),
    ];

    // Коди й тексти трекінгу Нової Пошти, які найчастіше трапляються.
    private static readonly (int Code, string Status)[] TrackingStatuses =
    [
        (1, "Відправник самостійно створив цю накладну, але ще не надав до відправки"),
        (41, "Відправлення у дорозі"),
        (7, "Прибув на відділення"),
        (8, "Прибув у поштомат"),
        (9, "Відправлення отримано"),
        (11, "Відправлення отримано. Грошовий переказ видано одержувачу."),
        (102, "Відмова від отримання"),
    ];

    /// <summary>
    /// Вигадані ТТН для відправлених замовлень: номер 2045 + id замовлення, різні статуси трекінгу.
    /// Посилки на відділенні отримують платне зберігання за 0–4 дні від сьогодні, щоб список нагадувань не був порожнім.
    /// </summary>
    private int SeedShipments(List<DdOrder> orders, DateTime now)
    {
        var shipped = orders.Where(o => o.OrderStatusId is 20 or 21 or 22).ToList();
        var waiting = 0;
        foreach (var order in shipped)
        {
            var document = new ShipmentDocument(order.OrderId, $"2045{order.OrderId:D10}", _random.Next(60, 151));
            var (code, status) = TrackingStatuses[_random.Next(TrackingStatuses.Length)];
            DateOnly? keeping = ShipmentDocument.WaitingAtBranchCodes.Contains(code)
                ? DateOnly.FromDateTime(now).AddDays(waiting++ % 5)
                : null;
            document.ApplyTracking(new TrackingInfo(
                document.Number, status, code, $"{order.ShippingCity}, {order.ShippingAddress2}",
                order.PaymentMethod == "Накладений платіж", order.DateAdded.AddDays(1),
                DateOnly.FromDateTime(order.DateAdded).AddDays(3), keeping));
            db.ShipmentDocuments.Add(document);
        }

        return shipped.Count;
    }

    /// <summary>
    /// Крій і пошив через справжній ProductionService, щоб залишки, журнали й списання були такими, як у роботі.
    /// Трикотажу навмисно замало, тож для костюма буде нестача.
    /// </summary>
    private async Task<int> SeedProductionOperationsAsync(int userId, string username)
    {
        var erpOptions = Options.Create(new ErpOptions { LanguageId = LanguageId });
        var service = new ProductionService(new ProductionRepository(db, erpOptions), new StorefrontStock(db, erpOptions, new PreOrderStore(db, erpOptions)), TimeProvider.System);
        var warnings = 0;
        foreach (var model in db.ProductionModels.Include(m => m.Sizes).ToList())
        {
            if (model.Type == ProductionModel.Clothing)
            {
                warnings += (await Run(ProductionOperation.Cut, 4)).Warnings.Count;
                await Run(ProductionOperation.SendToSewing, 3);
                warnings += (await Run(ProductionOperation.Sewn, 3)).Warnings.Count;
            }
            else
                await Run(ProductionOperation.Sewn, 15); // готовий товар: прихід від постачальника

            Task<ProductionOperationResult> Run(ProductionOperation operation, decimal perSize) =>
                service.RecordAsync(model.Id, operation,
                    new ProductionOperationRequest(model.Sizes.Select(s => new SizeQuantity(s.Id, perSize)).ToList(), "Початкові дані"),
                    userId, username, CancellationToken.None);
        }
        return warnings;
    }

    /// <summary>
    /// Моделі виробництва для перших товарів зі списку Products: розміри, деталі крою, специфікації крою й пошиву.
    /// Залишки крою й пошиву нульові, тож усі моделі одразу видно в списку «треба шити».
    /// </summary>
    // Вигадані id значень опції «Розмір» (dd_option_value), спільні для всіх товарів.
    private const int SizeOptionId = 11;
    private static readonly Dictionary<string, int> SizeOptionValues = new() { ["S"] = 46, ["M"] = 47, ["L"] = 48, ["XL"] = 49 };

    /// <summary>
    /// Кожен товар отримує опцію «Розмір» (dd_product_option_value), а кожен рядок замовлення — вибраний розмір
    /// (dd_order_option), як це робить OpenCart при оформленні замовлення.
    /// </summary>
    private async Task SeedSizeOptionsAsync()
    {
        // Каталог магазину: товар (ціна, наявність), його назва й назви опції «Розмір» та її значень.
        for (var i = 0; i < Products.Length; i++)
        {
            db.DdProducts.Add(new DdProduct
            {
                ProductId = FirstProductId + i, Model = $"VD-{FirstProductId + i}", Price = 900 + i * 150, Quantity = 10, Status = true,
                Shipping = true, Subtract = true, Sku = "", Upc = "", Ean = "", Jan = "", Isbn = "", Mpn = "", Location = "",
                DateAvailable = DateTime.Today, DateAdded = DateTime.Today, DateModified = DateTime.Today,
            });
            db.DdProductDescriptions.Add(new DdProductDescription
            {
                ProductId = FirstProductId + i, LanguageId = LanguageId, Name = Products[i],
                Description = "", Tag = "", MetaTitle = Products[i], MetaDescription = "", MetaKeyword = "",
            });
        }
        db.DdOptionDescriptions.Add(new DdOptionDescription { OptionId = SizeOptionId, LanguageId = LanguageId, Name = "Розмір" });
        foreach (var (size, valueId) in SizeOptionValues)
            db.DdOptionValueDescriptions.Add(new DdOptionValueDescription { OptionValueId = valueId, LanguageId = LanguageId, OptionId = SizeOptionId, Name = size });

        var optionValues = new Dictionary<(int ProductId, string Size), DdProductOptionValue>();
        for (var i = 0; i < Products.Length; i++)
        {
            foreach (var (size, valueId) in SizeOptionValues)
            {
                var value = new DdProductOptionValue
                {
                    ProductOptionId = 1000 + i, ProductId = FirstProductId + i, OptionId = SizeOptionId, OptionValueId = valueId,
                    Quantity = 3, Subtract = true, PricePrefix = "+", PointsPrefix = "+", WeightPrefix = "+",
                    Price = 900 + i * 150, // як на сайті: ціна розміру — повна ціна товару
                };
                db.DdProductOptionValues.Add(value);
                optionValues[(FirstProductId + i, size)] = value;
            }
        }
        // Передзамовлення: найбільший розмір двох останніх товарів закінчився, але його продають наперед до дати.
        var largest = SizeOptionValues.Keys.Last();
        for (var i = Products.Length - 2; i < Products.Length; i++)
        {
            var value = optionValues[(FirstProductId + i, largest)];
            (value.Quantity, value.PreOrderQuantity, value.DateAvailable) = (i == Products.Length - 1 ? -2 : 0, 5, DateTime.Today.AddDays(14));
        }
        await db.SaveChangesAsync();

        var sizes = SizeOptionValues.Keys.ToArray();
        foreach (var line in db.DdOrderProducts.ToList())
        {
            var size = sizes[_random.Next(sizes.Length)];
            var value = optionValues[(line.ProductId, size)];
            db.DdOrderOptions.Add(new DdOrderOption
            {
                OrderId = line.OrderId, OrderProductId = line.OrderProductId,
                ProductOptionId = value.ProductOptionId, ProductOptionValueId = value.ProductOptionValueId,
                Name = "Розмір", Value = size, Type = "radio",
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Дві категорії каталогу (з терміном виготовлення) з фото товарів і партнер «eva» з увімкненим фідом: сукні й спідниці з націнкою,
    /// окремо — сорочка з власним артикулом і ціною. Фід: /feeds/eva.xml.
    /// </summary>
    private async Task SeedPartnerAsync(DateTime now)
    {
        const int dresses = 20, tops = 21;
        // Термін виготовлення категорії (днів від дати замовлення) — для дати готовності в плані пошиву.
        foreach (var (id, name, days) in new[] { (dresses, "Сукні та спідниці", 7), (tops, "Сорочки й жакети", 10) })
        {
            db.DdCategories.Add(new DdCategory { CategoryId = id, Status = true, ProductionPeriod = days, DateAdded = now, DateModified = now });
            db.DdCategoryDescriptions.Add(new DdCategoryDescription
            {
                CategoryId = id, LanguageId = LanguageId, Name = name, Description = "", MetaTitle = name, MetaDescription = "", MetaKeyword = "",
            });
        }
        for (var i = 0; i < Products.Length; i++)
        {
            var category = Products[i].StartsWith("Сукня") || Products[i].StartsWith("Спідниця") ? dresses : tops;
            db.DdProductToCategories.Add(new DdProductToCategory { ProductId = FirstProductId + i, CategoryId = category });
            await db.DdProducts.Where(p => p.ProductId == FirstProductId + i)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Image, $"catalog/products/vd-{FirstProductId + i}.jpg"));
        }

        var partner = new Partner(new PartnerSettings("eva", "EVA (вигаданий партнер)", true, "", true, "Vidro", "", 5), now);
        partner.SetCategoryMap([
            new CategoryMapping(dresses, null, "1001", "Жіночий одяг / Сукні", 15, 30),
            new CategoryMapping(dresses, "XL", "1002", "Жіночий одяг / Великі розміри", 20, null),
        ]);
        partner.SetProductMap([new ProductMapping(FirstProductId + 1, null, "EVA-SHIRT-01", "", 1200, 1500)]);
        db.Partners.Add(partner);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Для сторінки «Порядок товарів»: мітки «Хіт» і «Новинка», усі товари в магазині 0, категорія сорочок — у панелі допродажу.
    /// </summary>
    private async Task SeedProductSortAsync()
    {
        db.DdProductLabels.AddRange(
            new DdProductLabel { LabelId = 1, Name = "Хіт", BackgroundColor = "#d32f2f", TextColor = "#ffffff", SortOrder = 1, Status = true },
            new DdProductLabel { LabelId = 2, Name = "Новинка", BackgroundColor = "#388e3c", TextColor = "#ffffff", SortOrder = 2, Status = true });
        for (var i = 0; i < Products.Length; i++)
        {
            db.DdProductToStores.Add(new DdProductToStore { ProductId = FirstProductId + i, StoreId = 0 });
            if (i < 3)
                db.DdProductToLabels.Add(new DdProductToLabel { ProductId = FirstProductId + i, LabelId = 1 });
            if (i >= Products.Length - 2)
                db.DdProductToLabels.Add(new DdProductToLabel { ProductId = FirstProductId + i, LabelId = 2 });
        }
        db.DdCrosssellItems.Add(new DdCrosssellItem { Type = "category", TargetId = 21 });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Для маркетингу: дні народження в частині замовлень (кілька — сьогодні й цього тижня, щоб сторінка не була порожньою),
    /// запитання про товар і підписки на розсилку для вивантаження контактів.
    /// </summary>
    private async Task<int> SeedMarketingAsync()
    {
        var today = DateTime.Today;
        var orders = await db.DdOrders.Where(o => o.OrderStatusId > 0).OrderBy(o => o.OrderId).ToListAsync();
        var birthdays = 0;
        for (var i = 0; i < orders.Count; i += 7)
        {
            var date = i < 7 * 5 ? today.AddDays(i / 7) : today.AddDays(_random.Next(365));
            (orders[i].BirthdayDay, orders[i].BirthdayMonth) = (date.Day, date.Month);
            birthdays++;
        }
        foreach (var order in orders.Where((_, i) => i % 11 == 0))
            order.Instagram = "@" + order.Firstname.ToLowerInvariant() + order.OrderId;

        for (var i = 0; i < 20; i++)
        {
            db.DdQuestions.Add(new DdQuestion
            {
                ProductId = FirstProductId + i % Products.Length, Name = "Гість " + (i + 1), Telephone = $"+38 (093) {i:000}-00-{i % 100:00}",
                Question = "Чи буде цей розмір знову?", DateAdded = today.AddDays(-i * 3),
            });
            db.DdSubscribes.Add(new DdSubscribe { Email = $"subscriber{i + 1}@example.com", DateAdded = today.AddDays(-i * 5) });
        }
        await db.SaveChangesAsync();
        return birthdays;
    }

    /// <summary>
    /// Групи й зареєстровані покупці: кожен п'ятий клієнт замовлень «зареєструвався», його замовлення отримує customer_id.
    /// Пароля немає (порожній), тож увійти на сайт ними не можна.
    /// </summary>
    private async Task<int> SeedCustomersAsync()
    {
        db.DdCustomerGroupDescriptions.AddRange(
            new DdCustomerGroupDescription { CustomerGroupId = 1, LanguageId = LanguageId, Name = "Роздріб", Description = "" },
            new DdCustomerGroupDescription { CustomerGroupId = 2, LanguageId = LanguageId, Name = "Опт", Description = "" });

        var orders = await db.DdOrders.Where(o => o.OrderStatusId > 0).OrderBy(o => o.OrderId).ToListAsync();
        var count = 0;
        foreach (var (order, i) in orders.Where((_, i) => i % 5 == 0).Select((o, i) => (o, i)))
        {
            var customer = new DdCustomer
            {
                CustomerGroupId = i % 8 == 0 ? 2 : 1, LanguageId = LanguageId, Firstname = order.Firstname, Lastname = order.Lastname,
                Email = order.Email, Telephone = order.Telephone, Fax = "", Password = "", Salt = "", CustomField = "", Ip = "",
                Newsletter = i % 3 == 0, Status = i % 10 != 9, Token = "", Code = "", DateAdded = order.DateAdded.AddMinutes(-5),
            };
            db.DdCustomers.Add(customer);
            await db.SaveChangesAsync();
            var address = new DdAddress
            {
                CustomerId = customer.CustomerId, Firstname = order.Firstname, Lastname = order.Lastname, Company = "",
                Address1 = order.ShippingAddress2, Address2 = "", City = order.ShippingCity, Postcode = "", CustomField = "",
            };
            db.DdAddresses.Add(address);
            await db.SaveChangesAsync();
            customer.AddressId = address.AddressId;
            order.CustomerId = customer.CustomerId;
            if (i % 6 == 0 && orders.IndexOf(order) + 1 < orders.Count)
            {
                // Наступне замовлення ця ж людина оформила як гість, записавши телефон інакше: картка знайде його за телефоном.
                var guest = orders[orders.IndexOf(order) + 1];
                (guest.Firstname, guest.Lastname, guest.Telephone) = (order.Firstname, order.Lastname, new string(order.Telephone.Where(char.IsAsciiDigit).ToArray()));
            }
            if (i % 4 == 0)
                db.DdCustomerHistories.Add(new DdCustomerHistory
                {
                    CustomerId = customer.CustomerId, Comment = "Просила дзвонити після 18:00.", DateAdded = order.DateAdded.AddDays(1),
                });
            count++;
        }
        await db.SaveChangesAsync();
        return count;
    }

    /// <summary>Довідник «Склади й пункти видачі»: склад і два пункти видачі з вигаданими адресами й телефонами.</summary>
    private async Task<int> SeedLocationsAsync()
    {
        (string Name, string Type, string City, string Address, string Contact, string Phone)[] items =
        [
            ("Склад і цех", "склад", "Київ", "вул. Вигадана, 10", "Комірник", "+38 (067) 000-00-01"),
            ("Пункт видачі на Подолі", "пункт видачі", "Київ", "вул. Тестова, 5", "Адміністратор", "+38 (067) 000-00-02"),
            ("Пункт видачі у Львові", "пункт видачі", "Львів", "пл. Прикладна, 3", "Адміністратор", "+38 (067) 000-00-03"),
        ];
        foreach (var x in items)
            db.DdWarehouses.Add(new DdWarehouse
            {
                Name = x.Name, Type = x.Type, City = x.City, Address = x.Address, Contact = x.Contact, Telephone = x.Phone,
                Description = "", Image = "",
            });
        await db.SaveChangesAsync();
        return items.Length;
    }

    /// <summary>Довідники повернень (статуси, причини, дії) і кілька повернень з виконаних замовлень, як оформив би менеджер.</summary>
    private async Task<int> SeedReturnsAsync()
    {
        string[] statuses = ["Очікує", "Отримано", "Завершено"];
        string[] reasons = ["Не підійшов розмір", "Брак", "Не той товар", "Інше"];
        string[] actions = ["Повернено кошти", "Обмін", "Відмовлено"];
        for (var i = 0; i < statuses.Length; i++)
            db.DdReturnStatuses.Add(new DdReturnStatus { ReturnStatusId = i + 1, LanguageId = LanguageId, Name = statuses[i] });
        for (var i = 0; i < reasons.Length; i++)
            db.DdReturnReasons.Add(new DdReturnReason { ReturnReasonId = i + 1, LanguageId = LanguageId, Name = reasons[i] });
        for (var i = 0; i < actions.Length; i++)
            db.DdReturnActions.Add(new DdReturnAction { ReturnActionId = i + 1, LanguageId = LanguageId, Name = actions[i] });

        var lines = await db.DdOrders.Where(o => o.OrderStatusId == 22)
            .Join(db.DdOrderProducts, o => o.OrderId, p => p.OrderId, (o, p) => new { Order = o, Line = p })
            .OrderBy(x => x.Order.OrderId).Take(6).ToListAsync();
        for (var i = 0; i < lines.Count; i++)
        {
            var (order, line) = (lines[i].Order, lines[i].Line);
            var added = order.DateAdded.AddDays(10 + i);
            var statusId = i % statuses.Length + 1;
            var item = new DdReturn
            {
                OrderId = order.OrderId, ProductId = line.ProductId, CustomerId = order.CustomerId,
                Firstname = order.Firstname, Lastname = order.Lastname, Email = order.Email, Telephone = order.Telephone,
                Product = line.Name, Model = line.Model, Quantity = 1, Opened = i % 2 == 0,
                ReturnReasonId = i % reasons.Length + 1, ReturnActionId = statusId == 3 ? 1 : 0, ReturnStatusId = statusId,
                Comment = "Клієнт написав у Viber.", DateOrdered = order.DateAdded, DateAdded = added, DateModified = added.AddDays(statusId - 1),
            };
            db.DdReturns.Add(item);
            await db.SaveChangesAsync();
            for (var s = 1; s <= statusId; s++)
                db.DdReturnHistories.Add(new DdReturnHistory { ReturnId = item.ReturnId, ReturnStatusId = s, Comment = "", DateAdded = added.AddDays(s - 1) });
        }
        await db.SaveChangesAsync();
        return lines.Count;
    }

    private async Task<int> SeedProductionAsync()
    {
        var item = db.StockItems.ToDictionary(i => i.Name, i => i.Id);
        int linen = item["Льон бежевий"], viscose = item["Віскоза принт"], knit = item["Трикотаж чорний"],
            buttons = item["Ґудзики дерев'яні 15 мм"], zipper = item["Блискавка прихована 20 см"],
            bag = item["Пакет кур'єрський 30x40"], tag = item["Бірка з логотипом"];

        // (товар, тканина, метрів на перед, метрів на спинку, фурнітура на виріб)
        (int Product, int Fabric, decimal Front, decimal Back, (int Item, decimal Qty) Fitting)[] specs =
        [
            (0, linen, 1.3m, 1.5m, (zipper, 1)),   // Сукня лляна
            (1, viscose, 1.0m, 1.2m, (buttons, 8)), // Сорочка оверсайз
            (3, knit, 1.4m, 1.7m, (zipper, 1)),    // Костюм трикотажний
        ];

        foreach (var spec in specs)
        {
            var model = new ProductionModel(Products[spec.Product], ProductionModel.Clothing, minTotal: 30, sortOrder: spec.Product);
            model.SetSizes([("S", 25m), ("M", 40m), ("L", 25m), ("XL", 10m)]);
            model.SetLinks([FirstProductId + spec.Product], []);
            db.ProductionModels.Add(model);
            await db.SaveChangesAsync();
            // Розміри моделі зв'язуються з тими самими значеннями опції «Розмір», що й у замовленнях.
            model.SetSizeOptions(model.Sizes.Select(s => (s.Id, (int?)SizeOptionValues[s.Name])).ToList());

            var front = new ProductionComponent(model.Id, "Перед", 1);
            var back = new ProductionComponent(model.Id, "Спинка", 2);
            var sizeIds = model.Sizes.Select(s => s.Id).ToList();
            var xl = model.Sizes.Single(s => s.Name == "XL").Id;
            front.SetBom([(null, spec.Fabric, spec.Front), (xl, spec.Fabric, spec.Front + 0.2m)], sizeIds); // на XL тканини більше
            back.SetBom([(null, spec.Fabric, spec.Back)], sizeIds);
            db.ProductionComponents.AddRange(front, back);
            await db.SaveChangesAsync();

            // На один пошитий виріб: по одній деталі крою, фурнітура, бірка й пакет.
            model.SetSewnBom(
                [
                    (null, front.Id, null, 1), (null, back.Id, null, 1),
                    (null, null, spec.Fitting.Item, spec.Fitting.Qty), (null, null, tag, 1), (null, null, bag, 1),
                ],
                [front.Id, back.Id]);
            await db.SaveChangesAsync();
        }

        var bagModel = new ProductionModel("Шопер (готовий, від постачальника)", ProductionModel.ReadyMade, minTotal: 10, sortOrder: 99);
        bagModel.SetSizes([("Єдиний", 100m)]);
        db.ProductionModels.Add(bagModel);
        await db.SaveChangesAsync();

        return specs.Length + 1;
    }

    private async Task<int> SeedStockAsync(int userId, string username, DateTime now)
    {
        var fabric = new StockCategory("Тканини");
        var fittings = new StockCategory("Фурнітура");
        var packaging = new StockCategory("Пакування");
        db.StockCategories.AddRange(fabric, fittings, packaging);
        var supplier = new StockSupplier("ТОВ \"Текстиль-Опт\" (вигаданий)", "+380441234567", null, "Тканини", null);
        var supplier2 = new StockSupplier("ФОП Фурнітура (вигаданий)", null, null, "Фурнітура", "Доставка по вівторках");
        db.StockSuppliers.AddRange(supplier, supplier2);
        await db.SaveChangesAsync();

        // Ціни вигадані, лише щоб у звіті «що дозамовити» були суми.
        (StockCategory Cat, StockSupplier? Sup, string Name, string Unit, decimal Min, decimal Start, decimal Price)[] items =
        [
            (fabric, supplier, "Льон бежевий", "м", 30, 85.5m, 240),
            (fabric, supplier, "Трикотаж чорний", "м", 40, 22m, 180),
            (fabric, supplier, "Віскоза принт", "м", 20, 64m, 210),
            (fittings, supplier2, "Ґудзики дерев'яні 15 мм", "шт", 200, 1350, 3.5m),
            (fittings, supplier2, "Блискавка прихована 20 см", "шт", 100, 45, 12),
            (packaging, null, "Пакет кур'єрський 30x40", "шт", 300, 520, 2.2m),
            (packaging, null, "Бірка з логотипом", "шт", 500, 180, 1.8m),
        ];

        foreach (var x in items)
        {
            var item = new StockItem(x.Cat.Id, x.Sup?.Id, x.Name, x.Unit, x.Min, price: x.Price);
            db.StockItems.Add(item);
            db.StockMovements.Add(item.Adjust(x.Start, "Початковий залишок", userId, username, now.AddDays(-30)));
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