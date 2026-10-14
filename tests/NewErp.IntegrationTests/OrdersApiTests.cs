using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Orders;

namespace NewErp.IntegrationTests;

public class OrdersApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [MySqlFact]
    public async Task List_HidesUnfinishedOrders_AndJoinsLookups()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<OrderListItem>>("/orders?from=2026-10-01&to=2026-10-31");

        Assert.NotNull(page);
        Assert.DoesNotContain(page.Items, o => o.Id == 3);
        var completed = Assert.Single(page.Items, o => o.Id == 2);
        Assert.Equal("Виконано", completed.StatusName);
        Assert.Equal("Instagram", completed.SourceName);
        Assert.Equal("Олена Тест", completed.ManagerName);
    }

    [MySqlFact]
    public async Task List_FiltersByManager()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<OrderListItem>>($"/orders?managerId={ApiFactory.ActiveUserId}");

        Assert.Equal([2], page!.Items.Select(o => o.Id));
    }

    [MySqlFact]
    public async Task ChangeStatus_UpdatesOrderAndWritesHistory()
    {
        var response = await _client.PutAsJsonAsync($"/orders/{ApiFactory.OrderId}/status",
            new ChangeOrderStatusRequest(ApiFactory.Completed, ApiFactory.ActiveUserId));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var order = await _client.GetFromJsonAsync<OrderDetails>($"/orders/{ApiFactory.OrderId}");
        Assert.Equal(ApiFactory.Completed, order!.StatusId);
        var entry = Assert.Single(order.StatusHistory);
        Assert.Equal((ApiFactory.Pending, ApiFactory.Completed), (entry.OldStatusId, entry.NewStatusId));
    }

    [MySqlFact]
    public async Task ChangeStatus_InactiveUser_Returns400WithMessage()
    {
        var response = await _client.PutAsJsonAsync("/orders/2/status",
            new ChangeOrderStatusRequest(ApiFactory.Pending, ApiFactory.InactiveUserId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("вимкнено", await response.Content.ReadAsStringAsync());
    }

    [MySqlFact]
    public async Task Get_UnknownOrder_Returns404()
    {
        var response = await _client.GetAsync("/orders/12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}