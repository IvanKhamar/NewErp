using NewErp.Api.Auth;
using NewErp.Application.Staff;

namespace NewErp.Api.Endpoints;

public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/staff").WithTags("Персонал");

        staff.MapGet("/bonuses", (DateOnly from, DateOnly to, BonusService service, CancellationToken ct) =>
            service.CalculateAsync(from, to, ct))
            .RequireAuthorization(Policies.Bonuses)
            .WithSummary("Бонуси менеджерів за період (дати включно)");
    }
}