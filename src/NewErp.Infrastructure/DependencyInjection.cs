using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Orders;
using NewErp.Application.Production;
using NewErp.Application.Staff;
using NewErp.Application.Stock;
using NewErp.Infrastructure.Auth;
using NewErp.Infrastructure.Orders;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Production;
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
        services.AddScoped<IProductionRepository, ProductionRepository>();
        services.AddScoped<IUserLoginStore, UserLoginStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        return services;
    }
}