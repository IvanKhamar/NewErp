using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Common;
using NewErp.Application.Returns;
using NewErp.Infrastructure.Persistence;
using NewErp.Infrastructure.Persistence.Legacy;

namespace NewErp.IntegrationTests;

/// <summary>Повернення товарів. Окрема тестова база, бо тест додає довідники й повернення.</summary>
public class ReturnApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int Waiting = 1, Received = 2, WrongSize = 1, Refunded = 1;

    private sealed record Created(int Id);

    [MySqlFact]
    public async Task Return_FromOrderLine_WithHistory_AndQuantityLimit()
    {
        await SeedLookupsAsync();
        var manager = await factory.LoginAsync(ApiFactory.Manager);

        var lookups = await manager.GetFromJsonAsync<ReturnLookupsDto>("/returns/lookups");
        Assert.Equal(["Очікує", "Отримано"], lookups!.Statuses.Select(s => s.Name));

        // У замовленні OrderId 2 сукні: повертаємо одну.
        var response = await manager.PostAsJsonAsync("/returns",
            new CreateReturnRequest(ApiFactory.OrderId, ApiFactory.ProductId, 1, WrongSize, Waiting, Comment: "Мала"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<Created>())!.Id;

        (await manager.PostAsJsonAsync($"/returns/{id}/history", new AddReturnHistoryRequest(Received, "Посилку отримали", Refunded))).EnsureSuccessStatusCode();
        var details = await manager.GetFromJsonAsync<ReturnDetails>($"/returns/{id}");
        Assert.Equal((ApiFactory.OrderId, "Сукня", "T-1", 1, "Отримано", "Повернено кошти", "Не підійшов розмір"),
            (details!.OrderId, details.Product, details.Model, details.Quantity, details.StatusName, details.ActionName, details.ReasonName));
        Assert.Equal(["Мала", "Посилку отримали"], details.History.Select(h => h.Comment));

        var list = await manager.GetFromJsonAsync<PagedResult<ReturnListItem>>($"/returns?orderId={ApiFactory.OrderId}");
        Assert.Equal(id, Assert.Single(list!.Items).Id);

        // Друга сукня ще можна повернути, третьої немає.
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/returns",
            new CreateReturnRequest(ApiFactory.OrderId, ApiFactory.ProductId, 2, WrongSize, Waiting))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync("/returns/9999")).StatusCode);
        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/returns")).StatusCode);
    }

    private async Task SeedLookupsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.DdReturnStatuses.AddRange(
            new DdReturnStatus { ReturnStatusId = Waiting, LanguageId = ApiFactory.LanguageId, Name = "Очікує" },
            new DdReturnStatus { ReturnStatusId = Received, LanguageId = ApiFactory.LanguageId, Name = "Отримано" });
        db.DdReturnReasons.Add(new DdReturnReason { ReturnReasonId = WrongSize, LanguageId = ApiFactory.LanguageId, Name = "Не підійшов розмір" });
        db.DdReturnActions.Add(new DdReturnAction { ReturnActionId = Refunded, LanguageId = ApiFactory.LanguageId, Name = "Повернено кошти" });
        await db.SaveChangesAsync();
    }
}
