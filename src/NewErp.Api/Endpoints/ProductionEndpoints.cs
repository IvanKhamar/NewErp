using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Production;

namespace NewErp.Api.Endpoints;

public static class ProductionEndpoints
{
    public static void MapProductionEndpoints(this IEndpointRouteBuilder app)
    {
        var production = app.MapGroup("/production").WithTags("Виробництво: специфікації").RequireAuthorization(Policies.Stock);

        production.MapGet("/boms", (IProductionRepository repo, CancellationToken ct) => repo.ListAsync(ct))
            .WithSummary("Товари, для яких задано специфікацію");

        production.MapGet("/boms/{productId:int}", async (int productId, IProductionRepository repo, CancellationToken ct) =>
            await repo.GetDtoAsync(productId, ct) is { } bom ? Results.Ok(bom) : Results.NotFound());

        production.MapPut("/boms/{productId:int}", async (int productId, SetBomRequest request, ClaimsPrincipal user, ProductionService service, CancellationToken ct) =>
        {
            await service.SetBomAsync(productId, request, user.GetUserId(), ct);
            return Results.NoContent();
        })
            .WithSummary("Створює або повністю замінює специфікацію: розхідники й кількість на одну одиницю товару");

        production.MapDelete("/boms/{productId:int}", async (int productId, ProductionService service, CancellationToken ct) =>
        {
            await service.DeleteBomAsync(productId, ct);
            return Results.NoContent();
        });

        production.MapGet("/orders/{orderId:int}/requirements", (int orderId, ProductionService service, CancellationToken ct) =>
            service.GetOrderRequirementsAsync(orderId, ct))
            .WithSummary("Скільки розхідників потрібно на замовлення і чого бракує на складі");

        production.MapGet("/orders/{orderId:int}/writeoff", async (int orderId, IProductionRepository repo, CancellationToken ct) =>
            await repo.GetWriteOffDtoAsync(orderId, ct) is { } writeOff ? Results.Ok(writeOff) : Results.NotFound())
            .WithSummary("Що списано за замовленням і скільки не вистачило");
    }
}
