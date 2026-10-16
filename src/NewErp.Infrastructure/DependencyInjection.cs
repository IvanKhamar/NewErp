using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Delivery;
using NewErp.Application.Orders;
using NewErp.Application.Production;
using NewErp.Application.Sms;
using NewErp.Application.Staff;
using NewErp.Application.Stock;
using NewErp.Infrastructure.Auth;
using NewErp.Infrastructure.Delivery;
using NewErp.Infrastructure.Orders;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Production;
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
        services.AddScoped<IBonusDataSource, BonusDataSource>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IProductionRepository, ProductionRepository>();
        services.AddScoped<ISmsLog, SmsLog>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();

        // Справжній клієнт Нової Пошти — лише якщо ключ задано (dotnet user-secrets, NovaPoshta:ApiKey).
        // Без ключа заглушка: локальна розробка ніколи не звертається до справжнього сервісу.
        var novaPoshta = configuration.GetSection(NovaPoshtaOptions.SectionName).Get<NovaPoshtaOptions>() ?? new NovaPoshtaOptions();
        if (string.IsNullOrWhiteSpace(novaPoshta.ApiKey))
            services.AddSingleton<INovaPoshtaClient, StubNovaPoshtaClient>();
        else
            services.AddSingleton<INovaPoshtaClient>(_ => new NovaPoshtaClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, novaPoshta));
        // Поки лише заглушка: справжній SMS-сервіс підключимо разом з новим токеном SMSClub (у user-secrets).
        services.AddSingleton<ISmsSender, StubSmsSender>();
        services.AddScoped<IUserLoginStore, UserLoginStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        return services;
    }
}