using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Auth;
using NewErp.Application.Cashbox;
using NewErp.Application.Catalog;
using NewErp.Application.Customers;
using NewErp.Application.Delivery;
using NewErp.Application.Locations;
using NewErp.Application.Marketing;
using NewErp.Application.Orders;
using NewErp.Application.Partners;
using NewErp.Application.Payments;
using NewErp.Application.Production;
using NewErp.Application.Reports;
using NewErp.Application.Returns;
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
        services.AddScoped<NewOrderService>();
        services.AddScoped<ReturnService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<ReportService>();
        services.AddScoped<LocationService>();
        services.AddScoped<BonusService>();
        services.AddScoped<StockService>();
        services.AddScoped<ReorderService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<ProductionService>();
        services.AddScoped<SewingPlanService>();
        services.AddScoped<SmsService>();
        services.AddScoped<DeliveryService>();
        services.AddScoped<ShipmentService>();
        services.AddScoped<CashboxService>();
        services.AddScoped<BankPaymentService>();
        services.AddScoped<LiqPayService>();
        services.AddScoped<PartnerService>();
        services.AddScoped<ProductSortService>();
        services.AddScoped<PreOrderService>();
        services.AddScoped<MarketingService>();
        // OrderService викликає всіх IOrderStatusHandler при зміні статусу замовлення.
        services.AddScoped<IOrderStatusHandler, OrderSewnStockHandler>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserAdminService>();
        return services;
    }
}