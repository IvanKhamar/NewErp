using System.Net;
using System.Net.Http.Json;

using NewErp.Application.Sms;
using NewErp.Infrastructure.Sms;

namespace NewErp.IntegrationTests;

public class SmsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task Send_StubDoesNotSendButLogsToOrderHistory()
    {
        var client = await factory.LoginAsync(ApiFactory.Manager);

        var response = await client.PostAsJsonAsync($"/orders/{ApiFactory.OrderId}/sms", new SendSmsRequest("Ваше замовлення прийнято"));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SendSmsResultDto>();

        Assert.False(result!.Sent);
        Assert.Equal("380670000001", result.Message!.Phone); // +380670000001 з тестового замовлення
        var history = await client.GetFromJsonAsync<List<SmsDto>>($"/orders/{ApiFactory.OrderId}/sms");
        Assert.Equal(StubSmsSender.Response, Assert.Single(history!).Response);
    }

    [MySqlFact]
    public async Task Send_EmptyText_BadRequest()
    {
        var client = await factory.LoginAsync(ApiFactory.Admin);

        var response = await client.PostAsJsonAsync($"/orders/{ApiFactory.OrderId}/sms", new SendSmsRequest("  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [MySqlFact]
    public async Task Send_StorekeeperHasNoAccess()
    {
        var client = await factory.LoginAsync(ApiFactory.Storekeeper);

        var response = await client.PostAsJsonAsync($"/orders/{ApiFactory.OrderId}/sms", new SendSmsRequest("Текст"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
