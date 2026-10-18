using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using MudBlazor.Services;

using NewErp.Web;
using NewErp.Web.Api;
using NewErp.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API на тій самій адресі, що й інтерфейс, тож браузер сам надсилає cookie входу.
// Адреси API починаються з "/", бо інтерфейс живе під /app/.
builder.Services.AddScoped(sp => new HttpClient(new UnauthorizedHandler(sp) { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddMudServices();
builder.Services.AddScoped<StockApi>();
builder.Services.AddScoped<ProductionApi>();
builder.Services.AddScoped<OrdersApi>();
builder.Services.AddScoped<AdminApi>();
builder.Services.AddScoped<FinanceApi>();
builder.Services.AddScoped<PartnersApi>();
builder.Services.AddScoped<ProductSortApi>();
builder.Services.AddScoped<PreOrdersApi>();
builder.Services.AddScoped<MarketingApi>();
builder.Services.AddScoped<Clipboard>();

await builder.Build().RunAsync();
