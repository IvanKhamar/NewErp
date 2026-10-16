using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Delivery;
using NewErp.Application.Orders;
using NewErp.Application.Sms;
using NewErp.Domain.Delivery;

namespace NewErp.IntegrationTests;

public class DeliveryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task Shipment_ShownForOrderWithTtn_NoContentOtherwise()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var shipment = await client.GetFromJsonAsync<ShipmentDto>("/orders/2/shipment");
        var none = await client.GetAsync($"/orders/{ApiFactory.OrderId}/shipment");

        Assert.Equal(ApiFactory.WaitingTtn, shipment!.Number);
        Assert.Equal((7, ShipmentTone.Info, false), (shipment.StatusCode, shipment.Tone, shipment.IsFinal));
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
    }

    [MySqlFact]
    public async Task Reminders_ListWaitingParcel_UntilMarked()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var reminders = await client.GetFromJsonAsync<List<ReminderDto>>("/delivery/reminders");
        var reminder = Assert.Single(reminders!);
        Assert.Equal((2, ApiFactory.WaitingTtn, "Олена Тест"), (reminder.OrderId, reminder.Number, reminder.ManagerName));

        var done = await client.PostAsync("/delivery/reminders/2/done", null);
        Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ReminderDto>>("/delivery/reminders"))!);

        var missing = await client.PostAsync("/delivery/reminders/999/done", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [MySqlFact]
    public async Task Refresh_WithoutKey_ReportsNotConfigured()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var response = await client.PostAsync("/delivery/tracking/refresh", null);
        var result = await response.Content.ReadFromJsonAsync<TrackingRefreshResult>();

        Assert.Equal(new TrackingRefreshResult(false, 0, 0), result);
    }

    [MySqlFact]
    public async Task OrderSearch_FindsByTtn()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var page = await client.GetFromJsonAsync<PagedResult<OrderListItem>>($"/orders?search={ApiFactory.WaitingTtn}");

        Assert.Equal([2], page!.Items.Select(o => o.Id));
    }

    [MySqlFact]
    public async Task Sms_WithTtnOfAnotherOrder_AsksForConfirmation()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);
        var text = $"Ваша ТТН {ApiFactory.WaitingTtn}";

        var blocked = await client.PostAsJsonAsync($"/orders/{ApiFactory.OrderId}/sms", new SendSmsRequest(text));
        var result = await blocked.Content.ReadFromJsonAsync<SendSmsResultDto>();
        Assert.Equal(new TtnMismatchDto(ApiFactory.WaitingTtn, 2), result!.TtnMismatch);
        Assert.Empty((await client.GetFromJsonAsync<List<SmsDto>>($"/orders/{ApiFactory.OrderId}/sms"))!);

        var forced = await client.PostAsJsonAsync($"/orders/{ApiFactory.OrderId}/sms", new SendSmsRequest(text, Force: true));
        Assert.NotNull((await forced.Content.ReadFromJsonAsync<SendSmsResultDto>())!.Message);
    }

    [MySqlFact]
    public async Task Delivery_StorekeeperHasNoAccess()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);

        var response = await client.GetAsync("/delivery/reminders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
