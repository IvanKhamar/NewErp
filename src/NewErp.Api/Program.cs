using NewErp.Api;
using NewErp.Api.Endpoints;
using NewErp.Application;
using NewErp.Application.Staff;
using NewErp.Infrastructure;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandler>();
builder.Services.AddHealthChecks();

builder.Services.Configure<BonusOptions>(builder.Configuration.GetSection(BonusOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // інтерактивна документація: /scalar
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapOrderEndpoints();
app.MapStaffEndpoints();
app.MapStockEndpoints();

app.Run();

// Потрібно, щоб інтеграційні тести могли запустити застосунок через WebApplicationFactory<Program>.
public partial class Program;