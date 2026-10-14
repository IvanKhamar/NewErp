using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Orders;
using NewErp.Application.Staff;
using NewErp.Application.Stock;

namespace NewErp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<OrderService>();
        services.AddScoped<BonusService>();
        services.AddScoped<StockService>();
        return services;
    }
}