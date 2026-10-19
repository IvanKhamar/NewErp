using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Production;

namespace NewErp.Api.Endpoints;

public static class ProductionEndpoints
{
    public static void MapProductionEndpoints(this IEndpointRouteBuilder app)
    {
        var production = app.MapGroup("/production").WithTags("Виробництво").RequireAuthorization(Policies.Stock);

        production.MapGet("/models", (IProductionRepository repo, [AsParameters] ProductionModelFilter filter, CancellationToken ct) =>
            repo.ListAsync(filter, ct))
            .WithSummary("Моделі з підсумками крою й пошиву; belowMinimumOnly=true — ті, яких пошито менше за minTotal");

        production.MapGet("/models/{id:int}", async (int id, IProductionRepository repo, CancellationToken ct) =>
            await repo.GetDtoAsync(id, ct) is { } model ? Results.Ok(model) : Results.NotFound())
            .WithSummary("Модель: розміри із залишками, прив'язки до товарів, деталі крою, специфікації");

        production.MapPost("/models", async (SaveProductionModelRequest request, ProductionService service, CancellationToken ct) =>
        {
            var id = await service.CreateModelAsync(request, ct);
            return Results.Created($"/production/models/{id}", new { id });
        });

        production.MapPut("/models/{id:int}", async (int id, SaveProductionModelRequest request, ProductionService service, CancellationToken ct) =>
        {
            await service.UpdateModelAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Змінити модель і перелік розмірів (розміри зіставляються за назвою, залишки зберігаються)");

        production.MapDelete("/models/{id:int}", async (int id, ProductionService service, CancellationToken ct) =>
        {
            await service.DeactivateModelAsync(id, ct);
            return Results.NoContent();
        })
            .WithSummary("Приховати модель (status = 0, як у старому модулі)");

        production.MapPut("/models/{id:int}/links", async (int id, SetProductionLinksRequest request, ProductionService service, CancellationToken ct) =>
        {
            await service.SetLinksAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Прив'язати модель до товарів OpenCart (напряму або через категорію з фільтром) і розміри до опцій");

        production.MapPut("/models/{id:int}/sewn-bom", async (int id, SetSewnBomRequest request, ProductionService service, CancellationToken ct) =>
        {
            await service.SetSewnBomAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Специфікація пошиву: деталі крою й розхідники на один виріб");

        production.MapPost("/storefront/sync", (ProductionService service, CancellationToken ct) => service.PushStorefrontAsync(ct))
            .WithSummary("Передати пошиті залишки всіх активних моделей на вітрину магазину (кількість розмірів і товарів у OpenCart)");

        // Чотири операції з залишками. Кожна приймає кількості для кількох розмірів одразу.
        MapOperation(production, "cut", ProductionOperation.Cut,
            "+Покроєно: крій зростає, розхідники списуються за специфікацією крою (бракує — списується те, що є, у warnings)");
        MapOperation(production, "to-sewing", ProductionOperation.SendToSewing,
            "В пошив: розкроєні комплекти передано швачкам (більше, ніж розкроєно, не можна)");
        MapOperation(production, "sewn", ProductionOperation.Sewn,
            "+Пошито: пошиті вироби зростають, деталі й розхідники списуються за специфікацією пошиву");
        MapOperation(production, "ship", ProductionOperation.Ship,
            "Відвантажено не через замовлення (більше, ніж пошито, не можна)");

        production.MapGet("/plan", (bool? onlyToSew, SewingPlanService service, CancellationToken ct) =>
            service.GetAsync(onlyToSew ?? true, ct))
            .WithSummary("План пошиву: скільки кожного розміру пошити й розкроїти для активних замовлень (Production:PlanStatusIds); "
                + "onlyToSew=false — також розміри, яких вистачає");

        production.MapGet("/log", (IProductionRepository repo, [AsParameters] ProductionLogFilter filter, CancellationToken ct) =>
            repo.ListLogAsync(filter, ct))
            .WithSummary("Журнал крою й пошиву (stage = cut або sewn), нові записи першими");

        production.MapPost("/models/{id:int}/components", async (int id, SaveComponentRequest request, ProductionService service, CancellationToken ct) =>
        {
            var componentId = await service.AddComponentAsync(id, request, ct);
            return Results.Created($"/production/components/{componentId}", new { id = componentId });
        })
            .WithSummary("Додати деталь крою (перед, спинка, рукав...)");

        production.MapPut("/components/{id:int}", async (int id, SaveComponentRequest request, ProductionService service, CancellationToken ct) =>
        {
            await service.UpdateComponentAsync(id, request, ct);
            return Results.NoContent();
        });

        production.MapDelete("/components/{id:int}", async (int id, ProductionService service, CancellationToken ct) =>
        {
            await service.DeleteComponentAsync(id, ct);
            return Results.NoContent();
        });

        production.MapPut("/components/{id:int}/bom", async (int id, SetComponentBomRequest request, ProductionService service, CancellationToken ct) =>
        {
            await service.SetComponentBomAsync(id, request, ct);
            return Results.NoContent();
        })
            .WithSummary("Специфікація крою: розхідники на одну деталь");
    }

    private static void MapOperation(RouteGroupBuilder group, string route, ProductionOperation operation, string summary) =>
        group.MapPost($"/models/{{id:int}}/{route}",
            (int id, ProductionOperationRequest request, ClaimsPrincipal user, ProductionService service, CancellationToken ct) =>
                service.RecordAsync(id, operation, request, user.GetUserId(), user.GetUsername(), ct))
            .WithSummary(summary);
}
