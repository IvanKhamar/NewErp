using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Orders;

namespace NewErp.IntegrationTests;

public class OrdersApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task List_HidesUnfinishedOrders_AndJoinsLookups()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var page = await client.GetFromJsonAsync<PagedResult<OrderListItem>>("/orders?from=2026-10-01&to=2026-10-31");

        Assert.NotNull(page);
        Assert.DoesNotContain(page.Items, o => o.Id == 3);
        var completed = Assert.Single(page.Items, o => o.Id == 2);
        Assert.Equal("5) Відправлено", completed.StatusName);
        Assert.Equal("Instagram", completed.SourceName);
        Assert.Equal("Олена Тест", completed.ManagerName);
    }

    [MySqlFact]
    public async Task List_FiltersByManager()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var page = await client.GetFromJsonAsync<PagedResult<OrderListItem>>($"/orders?managerId={ApiFactory.ActiveUserId}");

        Assert.Equal([2], page!.Items.Select(o => o.Id));
    }

    [MySqlFact]
    public async Task ChangeStatus_UpdatesOrderAndWritesHistory()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var response = await client.PutAsJsonAsync($"/orders/{ApiFactory.OrderId}/status",
            new ChangeOrderStatusRequest(ApiFactory.Completed));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<ChangeOrderStatusResult>())!.Warnings);

        var order = await client.GetFromJsonAsync<OrderDetails>($"/orders/{ApiFactory.OrderId}");
        Assert.Equal(ApiFactory.Completed, order!.StatusId);
        var entry = Assert.Single(order.StatusHistory);
        Assert.Equal((ApiFactory.Pending, ApiFactory.Completed), (entry.OldStatusId, entry.NewStatusId));
        Assert.Equal(ApiFactory.ActiveUserId, entry.UserId); // хто змінив, сервер узяв із сесії
    }

    [MySqlFact]
    public async Task Get_UnknownOrder_Returns404()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var response = await client.GetAsync("/orders/12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [MySqlFact]
    public async Task Managers_ListsOnlyActiveUsers_AndIsOpenToManagers()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var managers = await client.GetFromJsonAsync<List<LookupItem>>("/orders/managers");

        Assert.Contains(managers!, m => m.Id == ApiFactory.ManagerUserId && m.Name == "Марта Тест");
        Assert.DoesNotContain(managers!, m => m.Id == ApiFactory.InactiveUserId);
    }
}
