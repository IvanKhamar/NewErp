using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Delivery;
using NewErp.Application.Orders;
using NewErp.Application.Production;
using NewErp.Application.Sms;
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
        services.AddScoped<ReorderService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<ProductionService>();
        services.AddScoped<SmsService>();
        services.AddScoped<DeliveryService>();
        // OrderService викликає всіх IOrderStatusHandler при зміні статусу замовлення.
        services.AddScoped<IOrderStatusHandler, OrderSewnStockHandler>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserAdminService>();
        return services;
    }
}