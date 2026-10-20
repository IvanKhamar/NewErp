using NewErp.Api;
using NewErp.Api.Auth;
using NewErp.Api.Endpoints;
using NewErp.Application;
using NewErp.Application.Catalog;
using NewErp.Application.Marketing;
using NewErp.Application.Partners;
using NewErp.Application.Production;
using NewErp.Application.Staff;
using NewErp.Application.Stock;
using NewErp.Infrastructure;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandler>();
// Зламаний JSON у тілі запиту завжди кидає виняток, а ErrorHandler відповідає 400 з поясненням.
// Інакше поза Development була б порожня 400, а в Development — 500.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddHealthChecks();
builder.Services.AddErpAuth();

builder.Services.Configure<BonusOptions>(builder.Configuration.GetSection(BonusOptions.SectionName));
builder.Services.Configure<StockOptions>(builder.Configuration.GetSection(StockOptions.SectionName));
builder.Services.Configure<ProductionOptions>(builder.Configuration.GetSection(ProductionOptions.SectionName));
builder.Services.Configure<PartnerFeedOptions>(builder.Configuration.GetSection(PartnerFeedOptions.SectionName));
builder.Services.Configure<ProductSortOptions>(builder.Configuration.GetSection(ProductSortOptions.SectionName));
builder.Services.Configure<PreOrderOptions>(builder.Configuration.GetSection(PreOrderOptions.SectionName));
builder.Services.AddHostedService<PreOrderSettleWorker>();
builder.Services.Configure<MarketingOptions>(builder.Configuration.GetSection(MarketingOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<CashboxScheduleOptions>(builder.Configuration.GetSection(CashboxScheduleOptions.SectionName));
builder.Services.AddHostedService<CashboxAutoCloseWorker>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous(); // інтерактивна документація: /scalar
}

app.UseHttpsRedirection();
app.UseAuthentication(); // читає cookie й визначає, хто робить запит
app.UseAuthorization();  // перевіряє, чи має він доступ до ендпоінта

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapOrderEndpoints();
app.MapStaffEndpoints();
app.MapStockEndpoints();
app.MapProductionEndpoints();
app.MapDeliveryEndpoints();
app.MapCashboxEndpoints();
app.MapPaymentEndpoints();
app.MapPartnerEndpoints();
app.MapProductSortEndpoints();
app.MapPreOrderEndpoints();
app.MapMarketingEndpoints();
app.MapReturnEndpoints();
app.MapCustomerEndpoints();
app.MapReportEndpoints();
app.MapLocationEndpoints();

// Веб-інтерфейс (NewErp.Web, Blazor WebAssembly) під /app/. Його файли й index.html доступні без входу,
// інакше не відкрилася б сторінка входу; дані однаково захищає API.
app.MapStaticAssets().AllowAnonymous();
app.MapFallbackToFile("app/{*path:nonfile}", "app/index.html").AllowAnonymous();

app.Run();

// Потрібно, щоб інтеграційні тести могли запустити застосунок через WebApplicationFactory<Program>.
public partial class Program;