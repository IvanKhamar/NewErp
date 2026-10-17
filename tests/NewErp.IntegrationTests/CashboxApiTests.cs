using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Cashbox;
using NewErp.Application.Common;

namespace NewErp.IntegrationTests;

public class CashboxApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task ShiftChequeAndZReport_FullDay()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var setup = await client.GetFromJsonAsync<CashboxSetupDto>("/cashbox/setup");
        Assert.True(setup!.FakeMode);
        Assert.True(setup.Cashiers.Single(c => c.Id == ApiFactory.CashierId).CanFiscalize);

        // Без відкритої зміни чек не видається, і запису не лишається.
        var refused = await client.PostAsJsonAsync($"/orders/{ApiFactory.OrderId}/cheque/", new IssueChequeRequest(null));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/orders/{ApiFactory.OrderId}/cheque/")).StatusCode);

        var shift = await (await client.PostAsync($"/cashbox/cashiers/{ApiFactory.CashierId}/shift/open", null))
            .Content.ReadFromJsonAsync<WorkShiftDto>();
        Assert.Null(shift!.DateEnd);

        var issued = await client.PostAsJsonAsync($"/orders/{ApiFactory.OrderId}/cheque/", new IssueChequeRequest(null));
        issued.EnsureSuccessStatusCode();
        var cheque = await issued.Content.ReadFromJsonAsync<ChequeDto>();
        Assert.Equal((1500m, true, shift.Id), (cheque!.Sum, cheque.IsFiscalized, cheque.WorkShiftId));
        Assert.Equal("Тарас Клієнт1", cheque.Customer);

        var x = await (await client.PostAsync($"/cashbox/cashiers/{ApiFactory.CashierId}/x-report", null)).Content.ReadFromJsonAsync<ReportTextDto>();
        Assert.Contains("ТЕСТОВИЙ", x!.Text);

        var shifts = await client.GetFromJsonAsync<List<WorkShiftDto>>("/cashbox/shifts");
        var only = Assert.Single(shifts!);
        Assert.Equal((1, 1500m), (only.Cheques, only.ChequesSum));

        var closed = await client.PostAsync($"/cashbox/cashiers/{ApiFactory.CashierId}/shift/close", null);
        var report = await closed.Content.ReadFromJsonAsync<ZReportDto>();
        Assert.Equal("Марта Тест", report!.ClosedBy);

        var cheques = await client.GetFromJsonAsync<PagedResult<ChequeDto>>("/cashbox/cheques?fiscalized=true");
        Assert.Equal(report.Id, Assert.Single(cheques!.Items).ReportId);
        Assert.NotNull((await client.GetFromJsonAsync<List<WorkShiftDto>>("/cashbox/shifts"))!.Single().DateEnd);

        // Фіскалізований чек не видаляється.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/cashbox/cheques/{cheque.Id}")).StatusCode);
    }

    [MySqlFact]
    public async Task Setup_EditsOnlyForAdmin_AndWarehouseInUseIsKept()
    {
        var manager = await factory.LoginAsync(ApiFactory.Manager);
        var admin = await factory.LoginAsync(ApiFactory.Admin);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await manager.PostAsJsonAsync("/cashbox/warehouses", new SaveDirectoryEntryRequest("Склад", null))).StatusCode);

        var created = await admin.PostAsJsonAsync("/cashbox/warehouses", new SaveDirectoryEntryRequest("Магазин на Подолі", "вул. Сагайдачного"));
        var warehouseId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;
        var cashier = await admin.PostAsJsonAsync("/cashbox/cashiers",
            new SaveCashierRequest("Ігор", null, "cod", new TimeOnly(21, 30), warehouseId, 0, true));
        cashier.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/cashbox/warehouses/{warehouseId}")).StatusCode);
        var setup = await admin.GetFromJsonAsync<CashboxSetupDto>("/cashbox/setup");
        var igor = setup!.Cashiers.Single(c => c.Name == "Ігор");
        Assert.Equal(("Магазин на Подолі", new TimeOnly(21, 30)), (igor.Warehouse, igor.AutoCloseTime));

        var storekeeper = await factory.LoginAsync(ApiFactory.Storekeeper);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/cashbox/setup")).StatusCode);
    }

    private sealed record IdDto(int Id);
}
