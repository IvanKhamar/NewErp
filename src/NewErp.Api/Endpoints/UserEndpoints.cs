using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Auth;

namespace NewErp.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Користувачі").RequireAuthorization(Policies.Users);

        users.MapGet("/", (UserAdminService service, CancellationToken ct) => service.ListAsync(ct))
            .WithSummary("Усі користувачі старого сайту (dd_user): чи є доступ до NewErp, роль, останній вхід");

        users.MapPost("/", async (CreateUserRequest request, UserAdminService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return Results.Created($"/users/{result.UserId}", result);
        })
            .WithSummary("Новий працівник: рядок у dd_user (без входу в стару адмінку) і доступ до NewErp з тимчасовим паролем");

        users.MapPut("/{id:int}/access", (int id, SetAccessRequest request, ClaimsPrincipal user, UserAdminService service, CancellationToken ct) =>
            service.SetAccessAsync(user.GetUserId(), id, request, ct))
            .WithSummary("Видати доступ наявному користувачу або змінити роль (Admin, Manager, Storekeeper)");

        users.MapDelete("/{id:int}/access", async (int id, ClaimsPrincipal user, UserAdminService service, CancellationToken ct) =>
        {
            await service.RevokeAsync(user.GetUserId(), id, ct);
            return Results.NoContent();
        })
            .WithSummary("Забрати доступ до NewErp (на старому сайті користувач лишається)");

        users.MapPost("/{id:int}/reset-password", (int id, ResetPasswordRequest request, UserAdminService service, CancellationToken ct) =>
            service.ResetPasswordAsync(id, request, ct))
            .WithSummary("Скинути пароль на тимчасовий (повертається один раз)");
    }
}
