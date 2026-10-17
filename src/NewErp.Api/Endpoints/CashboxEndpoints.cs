using System.Security.Claims;

using NewErp.Api.Auth;
using NewErp.Application.Cashbox;

namespace NewErp.Api.Endpoints;

/// <summary>Каса Checkbox: касири, зміни, чеки до замовлень, X- і Z-звіти.</summary>
public static class CashboxEndpoints
{
    public static void MapCashboxEndpoints(this IEndpointRouteBuilder app)
    {
        var cashbox = app.MapGroup("/cashbox").WithTags("Каса").RequireAuthorization(Policies.Finance);

        cashbox.MapGet("/setup", (CashboxService service, CancellationToken ct) => service.SetupAsync(ct))
            .WithSummary("Касири (з відкритою зміною, якщо є), склади й реквізити; fakeMode — працює тестова заглушка Checkbox");

        var setup = cashbox.MapGroup("/").RequireAuthorization(Policies.FinanceSetup);

        setup.MapPost("/cashiers", async (SaveCashierRequest request, CashboxService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateCashierAsync(request, ct) }))
            .WithSummary("Новий касир (ключі Checkbox задаються в user-secrets, Checkbox:Cashiers)");
        setup.MapPut("/cashiers/{id:int}", async (int id, SaveCashierRequest request, CashboxService service, CancellationToken ct) =>
        {
            await service.UpdateCashierAsync(id, request, ct);
            return Results.NoContent();
        }).WithSummary("Змінити касира");

        setup.MapPost("/warehouses", async (SaveDirectoryEntryRequest request, CashboxService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateWarehouseAsync(request, ct) })).WithSummary("Новий склад (точка продажу)");
        setup.MapPut("/warehouses/{id:int}", async (int id, SaveDirectoryEntryRequest request, CashboxService service, CancellationToken ct) =>
        {
            await service.UpdateWarehouseAsync(id, request, ct);
            return Results.NoContent();
        }).WithSummary("Змінити склад");
        setup.MapDelete("/warehouses/{id:int}", async (int id, CashboxService service, CancellationToken ct) =>
        {
            await service.DeleteWarehouseAsync(id, ct);
            return Results.NoContent();
        }).WithSummary("Видалити склад, якщо на нього ніхто не посилається");

        setup.MapPost("/requisites", async (SaveDirectoryEntryRequest request, CashboxService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateRequisitesAsync(request, ct) })).WithSummary("Нові реквізити (ФОП)");
        setup.MapPut("/requisites/{id:int}", async (int id, SaveDirectoryEntryRequest request, CashboxService service, CancellationToken ct) =>
        {
            await service.UpdateRequisitesAsync(id, request, ct);
            return Results.NoContent();
        }).WithSummary("Змінити реквізити");
        setup.MapDelete("/requisites/{id:int}", async (int id, CashboxService service, CancellationToken ct) =>
        {
            await service.DeleteRequisitesAsync(id, ct);
            return Results.NoContent();
        }).WithSummary("Видалити реквізити, якщо на них ніхто не посилається");

        cashbox.MapGet("/shifts", (int? cashierId, DateOnly? from, DateOnly? to, CashboxService service, CancellationToken ct) =>
                service.ShiftsAsync(new WorkShiftFilter(cashierId, from, to), ct))
            .WithSummary("Зміни (останні 500) з кількістю й сумою фіскалізованих чеків");

        cashbox.MapPost("/cashiers/{id:int}/shift/open", (int id, CashboxService service, ClaimsPrincipal user, CancellationToken ct) =>
                service.OpenShiftAsync(id, user.GetUserId(), ct))
            .WithSummary("Відкрити зміну касира (якщо в Checkbox вона вже відкрита — взяти її)");

        cashbox.MapPost("/cashiers/{id:int}/shift/close", async (int id, CashboxService service, ClaimsPrincipal user, CancellationToken ct) =>
                await service.CloseShiftAsync(id, user.GetUserId(), ct) is { } report ? Results.Ok(report) : Results.Accepted())
            .WithSummary("Закрити зміну (Z-звіт). 202 — Checkbox ще закриває зміну, звіт запише звірка");

        cashbox.MapPost("/shifts/{id:int}/sync", async (int id, CashboxService service, ClaimsPrincipal user, CancellationToken ct) =>
                await service.SyncShiftAsync(id, user.GetUserId(), ct) is { } report ? Results.Ok(report) : Results.NoContent())
            .WithSummary("Звірити зміну з Checkbox: якщо каса закрила її сама, записати Z-звіт (204 — зміна ще відкрита)");

        cashbox.MapPost("/shifts/auto-close", (CashboxService service, CancellationToken ct) => service.AutoCloseAsync(ct))
            .WithSummary("Те саме, що робить фонова задача: звірити відкриті зміни й закрити ті, у яких настав час автозакриття");

        cashbox.MapPost("/cashiers/{id:int}/x-report", (int id, CashboxService service, CancellationToken ct) => service.XReportAsync(id, ct))
            .WithSummary("X-звіт (підсумок зміни без закриття) текстом");

        cashbox.MapGet("/reports", (int? cashierId, DateOnly? from, DateOnly? to, CashboxService service, CancellationToken ct) =>
                service.ReportsAsync(cashierId, from, to, ct))
            .WithSummary("Z-звіти (останні 500)");

        cashbox.MapGet("/reports/{id:int}/text", (int id, CashboxService service, CancellationToken ct) => service.ZReportTextAsync(id, ct))
            .WithSummary("Текст Z-звіту з Checkbox");

        cashbox.MapGet("/cheques", ([AsParameters] ChequeFilter filter, CashboxService service, CancellationToken ct) =>
                service.ChequesAsync(filter, ct))
            .WithSummary("Чеки з фільтрами (касир, замовлення, період, fiscalized), посторінково");

        cashbox.MapPost("/cheques/{id:int}/fiscalize", (int id, CashboxService service, CancellationToken ct) => service.FiscalizeAsync(id, ct))
            .WithSummary("Повторити фіскалізацію чека, якого Checkbox не прийняв");

        cashbox.MapDelete("/cheques/{id:int}", async (int id, CashboxService service, CancellationToken ct) =>
        {
            await service.DeleteChequeAsync(id, ct);
            return Results.NoContent();
        }).WithSummary("Видалити нефіскалізований чек");

        var order = app.MapGroup("/orders/{id:int}/cheque").WithTags("Каса").RequireAuthorization(Policies.Finance);

        order.MapGet("/", async (int id, CashboxService service, CancellationToken ct) =>
                await service.ChequeForOrderAsync(id, ct) is { } cheque ? Results.Ok(cheque) : Results.NoContent())
            .WithSummary("Чек замовлення (204 — чека ще немає)");

        order.MapPost("/", (int id, IssueChequeRequest request, CashboxService service, CancellationToken ct) =>
                service.IssueForOrderAsync(id, request, ct))
            .WithSummary("Видати фіскальний чек (касир — вказаний або за кодом оплати; сума — мінус завдаток)");
    }
}
