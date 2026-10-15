using NewErp.Api;
using NewErp.Api.Auth;
using NewErp.Api.Endpoints;
using NewErp.Application;
using NewErp.Application.Production;
using NewErp.Application.Staff;
using NewErp.Infrastructure;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddErpAuth();

builder.Services.Configure<BonusOptions>(builder.Configuration.GetSection(BonusOptions.SectionName));
builder.Services.Configure<ProductionOptions>(builder.Configuration.GetSection(ProductionOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
app.MapOrderEndpoints();
app.MapStaffEndpoints();
app.MapStockEndpoints();
app.MapProductionEndpoints();

app.Run();

// Потрібно, щоб інтеграційні тести могли запустити застосунок через WebApplicationFactory<Program>.
public partial class Program;