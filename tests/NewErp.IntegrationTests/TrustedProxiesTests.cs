using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NewErp.Api.Auth;

namespace NewErp.IntegrationTests;

/// <summary>Адреса клієнта для обмеження входу: X-Forwarded-For діє лише від проксі зі списку.</summary>
public class TrustedProxiesTests
{
    private const string Client = "203.0.113.5";

    [Theory]
    [InlineData("10.0.0.1", Client)] // наш проксі
    [InlineData("10.0.0.2", "10.0.0.2")] // хтось інший підмінює заголовок
    [InlineData("127.0.0.1", "127.0.0.1")] // localhost довірений типово, але не в цьому списку
    public async Task XForwardedFor_CountsOnlyFromAListedProxy(string connection, string seen)
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Apply(options, ["10.0.0.1"]);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(connection);
        http.Request.Headers["X-Forwarded-For"] = Client;

        await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options)).Invoke(http);

        Assert.Equal(seen, http.Connection.RemoteIpAddress!.ToString());
    }

    [Fact]
    public void ABadAddress_StopsTheStart() =>
        Assert.Throws<FormatException>(() => TrustedProxies.Apply(new ForwardedHeadersOptions(), ["nginx"]));
}
