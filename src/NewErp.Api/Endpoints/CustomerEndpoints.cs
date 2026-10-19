using NewErp.Api.Auth;
using NewErp.Application.Customers;

namespace NewErp.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/customers").WithTags("Покупці").RequireAuthorization(Policies.Orders);

        customers.MapGet("/", ([AsParameters] CustomerFilter filter, CustomerService service, CancellationToken ct) => service.ListAsync(filter, ct))
            .WithSummary("Зареєстровані покупці посторінково, нові зверху; фільтри: search (ім'я, e-mail, телефон, id), groupId, status");

        customers.MapGet("/groups", (CustomerService service, CancellationToken ct) => service.GroupsAsync(ct))
            .WithSummary("Групи покупців");

        customers.MapGet("/{id:int}", (int id, CustomerService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithSummary("Картка покупця: адреси, замовлення (за покупцем і тим самим телефоном), підсумки, ризик і нотатки");

        customers.MapPost("/{id:int}/notes", async (int id, AddCustomerNoteRequest request, CustomerService service, CancellationToken ct) =>
        {
            await service.AddNoteAsync(id, request, ct);
            return Results.NoContent();
        })
        .WithSummary("Нотатка менеджера про покупця");
    }
}
