using NewErp.Api.Auth;
using NewErp.Application.Returns;

namespace NewErp.Api.Endpoints;

public static class ReturnEndpoints
{
    public static void MapReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var returns = app.MapGroup("/returns").WithTags("Повернення").RequireAuthorization(Policies.Orders);

        returns.MapGet("/", ([AsParameters] ReturnFilter filter, ReturnService service, CancellationToken ct) => service.ListAsync(filter, ct))
            .WithSummary("Повернення посторінково, нові зверху; фільтри: returnId, orderId, statusId, search (клієнт, телефон, товар, модель)");

        returns.MapGet("/lookups", (ReturnService service, CancellationToken ct) => service.LookupsAsync(ct))
            .WithSummary("Статуси, причини й дії повернень");

        returns.MapGet("/{id:int}", (int id, ReturnService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithSummary("Повернення з журналом статусів");

        returns.MapPost("/", async (CreateReturnRequest request, ReturnService service, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(request, ct);
            return Results.Created($"/returns/{id}", new { id });
        })
        .WithSummary("Оформити повернення товару із замовлення: клієнт і товар беруться із замовлення, кількість — не більше, ніж ще можна повернути");

        returns.MapPost("/{id:int}/history", async (int id, AddReturnHistoryRequest request, ReturnService service, CancellationToken ct) =>
        {
            await service.AddHistoryAsync(id, request, ct);
            return Results.NoContent();
        })
        .WithSummary("Новий статус повернення з коментарем у журнал; actionId — змінити дію (повернено кошти, обмін…)");
    }
}
