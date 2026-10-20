using NewErp.Api.Auth;
using NewErp.Application.Locations;
using NewErp.Domain.Common;

namespace NewErp.Api.Endpoints;

public static class LocationEndpoints
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var locations = app.MapGroup("/locations").WithTags("Склади й пункти видачі").RequireAuthorization(Policies.Orders);

        locations.MapGet("/", (string? search, LocationService service, CancellationToken ct) => service.ListAsync(search, ct))
            .WithSummary("Довідник складів і пунктів видачі; search — назва, місто, адреса, тип");

        locations.MapGet("/{id:int}", (int id, LocationService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithSummary("Склад чи пункт видачі");

        locations.MapPost("/", async (SaveLocationRequest request, LocationService service, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(request, ct);
            return Results.Created($"/locations/{id}", new { id });
        })
        .WithSummary("Новий склад чи пункт видачі");

        locations.MapPut("/{id:int}", async (int id, SaveLocationRequest request, LocationService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, ct);
            return Results.NoContent();
        })
        .WithSummary("Змінити склад чи пункт видачі");

        locations.MapDelete("/{id:int}", async (int id, LocationService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        })
        .WithSummary("Видалити склад чи пункт видачі");

        locations.MapGet("/export.xlsx", async (LocationService service, CancellationToken ct) =>
                Results.File(await service.ExportAsync(ct), Xlsx, $"sklady-{DateTime.Now:yyyy-MM-dd}.xlsx"))
            .WithSummary("Вивантажити довідник у Excel (колонки як у старого сайту: ID, Назва, Опис, Контакт, Телефон, Місто, Адреса, Тип)");

        // Файл приходить сирим тілом запиту (application/octet-stream), а не формою: так не потрібна антифорджері-форма,
        // а браузер з іншого сайту не надішле такий запит без дозволу CORS.
        locations.MapPost("/import", async (HttpRequest http, LocationService service, CancellationToken ct) =>
        {
            if (http.ContentLength is > LocationService.MaxImportBytes)
                throw new DomainException("Файл більший за 5 МБ.");
            using var buffer = new MemoryStream();
            await http.Body.CopyToAsync(buffer, ct);
            if (buffer.Length > LocationService.MaxImportBytes)
                throw new DomainException("Файл більший за 5 МБ.");
            buffer.Position = 0;
            return await service.ImportAsync(buffer, ct);
        })
        .Accepts<byte[]>("application/octet-stream")
        .WithSummary("Імпорт з Excel: рядок з ID наявного запису оновлює його, решта додаються; з помилками нічого не записується");
    }
}
