using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Cashbox;
using NewErp.Application.Catalog;
using NewErp.Application.Delivery;
using NewErp.Application.Marketing;
using NewErp.Application.Orders;
using NewErp.Application.Partners;
using NewErp.Application.Payments;
using NewErp.Application.Production;
using NewErp.Application.Returns;
using NewErp.Application.Sms;
using NewErp.Application.Staff;
using NewErp.Application.Stock;
using NewErp.Infrastructure.Auth;
using NewErp.Infrastructure.Cashbox;
using NewErp.Infrastructure.Catalog;
using NewErp.Infrastructure.Delivery;
using NewErp.Infrastructure.Marketing;
using NewErp.Infrastructure.Orders;
using NewErp.Infrastructure.Partners;
using NewErp.Infrastructure.Payments;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Production;
using NewErp.Infrastructure.Returns;
using NewErp.Infrastructure.Sms;
using NewErp.Infrastructure.Staff;
using NewErp.Infrastructure.Stock;

namespace NewErp.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ErpDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Немає рядка підключення ConnectionStrings:{ConnectionStringName}. " +
                "Задайте його командою dotnet user-secrets (див. README).");

        services.AddDbContext<ErpDbContext>(options => options.UseMySQL(connectionString));
        services.Configure<ErpOptions>(configuration.GetSection(ErpOptions.SectionName));

        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<IOrderStore, OrderStore>();
        services.AddScoped<IReturnStore, ReturnStore>();
        services.AddScoped<INewOrderStore, NewOrderStore>();
        services.AddScoped<IBonusDataSource, BonusDataSource>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<ProductionRepository>();
        services.AddScoped<IProductionRepository>(sp => sp.GetRequiredService<ProductionRepository>());
        services.AddScoped<ISewingPlanSource>(sp => sp.GetRequiredService<ProductionRepository>());
        services.AddScoped<IStorefrontStock, StorefrontStock>();
        services.AddScoped<ISmsLog, SmsLog>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IWarehouseDirectory, WarehouseDirectory>();
        services.AddScoped<ICashboxRepository, CashboxRepository>();
        services.AddScoped<IBankPaymentRepository, BankPaymentRepository>();
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IProductSortStore, ProductSortStore>();
        services.AddScoped<IPreOrderStore, PreOrderStore>();
        services.AddScoped<IMarketingStore, MarketingStore>();

        // Справжній клієнт Нової Пошти — лише якщо ключ задано (dotnet user-secrets: NovaPoshta:ApiKey чи ключі відправників).
        // Без ключа заглушка: локальна розробка ніколи не звертається до справжнього сервісу.
        var novaPoshta = configuration.GetSection(NovaPoshtaOptions.SectionName).Get<NovaPoshtaOptions>() ?? new NovaPoshtaOptions();
        if (string.IsNullOrWhiteSpace(novaPoshta.TrackingKey))
            services.AddSingleton<INovaPoshtaClient>(new StubNovaPoshtaClient(novaPoshta.FakeDocuments));
        else
            services.AddSingleton<INovaPoshtaClient>(_ => new NovaPoshtaClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, novaPoshta));
        // Checkbox і ПриватБанк — так само: справжній клієнт лише з ключами в user-secrets, інакше заглушка.
        var checkbox = configuration.GetSection(CheckboxOptions.SectionName).Get<CheckboxOptions>() ?? new CheckboxOptions();
        if (checkbox.Cashiers.Any(c => c.Login != "" && c.Password != ""))
            services.AddSingleton<ICheckboxClient>(_ => new CheckboxClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, checkbox));
        else
            services.AddSingleton<ICheckboxClient>(new StubCheckboxClient(checkbox.FakeReceipts));
        var privatBank = configuration.GetSection(PrivatBankOptions.SectionName).Get<PrivatBankOptions>() ?? new PrivatBankOptions();
        if (privatBank.IsConfigured)
            services.AddSingleton<IBankStatementClient>(_ => new PrivatBankClient(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, privatBank));
        else
            services.AddSingleton<IBankStatementClient, StubBankStatementClient>();
        // Поки лише заглушка: справжній SMS-сервіс підключимо разом з новим токеном SMSClub (у user-secrets).
        services.AddSingleton<ISmsSender, StubSmsSender>();
        services.AddScoped<IUserLoginStore, UserLoginStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        return services;
    }
}