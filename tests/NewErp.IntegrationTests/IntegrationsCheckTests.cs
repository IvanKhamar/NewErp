using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NewErp.Application.Cashbox;
using NewErp.Application.Delivery;
using NewErp.Application.Payments;
using NewErp.Application.Sms;
using NewErp.Domain.Common;
using NewErp.Infrastructure;
using NewErp.Infrastructure.Cashbox;
using NewErp.Infrastructure.Delivery;
using NewErp.Infrastructure.Payments;
using NewErp.Infrastructure.Sms;
using NewErp.SchemaCheck;

namespace NewErp.IntegrationTests;

/// <summary>Режим --integrations: без ключів нічого не запитує, а помилка не друкує ключ.</summary>
public class IntegrationsCheckTests
{
    private const string FakeKey = "fake-key-in-a-message";

    [Fact]
    public async Task WithoutKeys_NothingIsAsked_AndNothingIsWrong()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<INovaPoshtaClient>(new StubNovaPoshtaClient())
            .AddSingleton<IBankStatementClient, StubBankStatementClient>()
            .AddSingleton<ILiqPayGateway, StubLiqPayGateway>()
            .AddSingleton<ICheckboxClient>(new StubCheckboxClient(false))
            .AddSingleton<ISmsSender, StubSmsSender>()
            .BuildServiceProvider();
        var output = new StringWriter();

        var problems = await IntegrationsCheck.RunAsync(services, output, TimeProvider.System);

        Assert.Empty(problems);
        Assert.Contains("Нова Пошта (трекінг вигаданої ТТН): ключ не задано, пропущено", output.ToString());
        Assert.Contains("LiqPay (статус неіснуючого замовлення): ключ не задано, пропущено", output.ToString());
        Assert.Contains("SMSClub: ключ не задано", output.ToString());
    }

    [Fact]
    public async Task TheToolsOwnWiring_ResolvesEveryClient_AndAsksNothingWithoutKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ErpDb"] = "server=127.0.0.1;database=not_used" })
            .Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System).AddInfrastructure(configuration).BuildServiceProvider();

        Assert.Empty(await IntegrationsCheck.RunAsync(services, new StringWriter(), TimeProvider.System));
    }

    [Fact]
    public async Task AFailure_NamesTheService_ButNeverTheKey()
    {
        var problems = new List<string>();
        var output = new StringWriter();

        await IntegrationsCheck.ProbeAsync("Сервіс", true, () => throw new HttpRequestException($"bad url with {FakeKey}"), output, problems);

        Assert.Equal("Сервіс: HttpRequestException", Assert.Single(problems));
        Assert.DoesNotContain(FakeKey, output.ToString());
    }

    [Fact]
    public async Task ARefusalInWords_IsShownInWords()
    {
        var problems = new List<string>();

        await IntegrationsCheck.ProbeAsync("Сервіс", true, () => throw new DomainException("Ключ прострочено."), new StringWriter(), problems);

        Assert.Equal("Сервіс: Ключ прострочено.", Assert.Single(problems));
    }

    [Fact]
    public async Task AnAnswer_IsSaidToWork()
    {
        var problems = new List<string>();
        var output = new StringWriter();

        await IntegrationsCheck.ProbeAsync("Сервіс", true, () => Task.CompletedTask, output, problems);

        Assert.Empty(problems);
        Assert.Contains("Сервіс: відповідає", output.ToString());
    }
}
