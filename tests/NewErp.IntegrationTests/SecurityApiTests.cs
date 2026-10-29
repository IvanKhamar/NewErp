using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

using NewErp.Application.Auth;

namespace NewErp.IntegrationTests;

/// <summary>Заголовки безпеки й обмеження невдалих спроб входу.</summary>
public class SecurityApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [MySqlFact]
    public async Task EveryResponse_HasSecurityHeaders()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [MySqlFact]
    public async Task Login_AfterTooManyFailures_Is429EvenWithTheRightPassword_OtherUsersStillLogIn()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("LoginThrottle:MaxFailuresPerUser", "3")).CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Manager, "wrong-password"))).StatusCode);

        var blocked = await client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Manager, ApiFactory.Password));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal("Забагато невдалих спроб входу", (await blocked.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Admin, ApiFactory.Password))).StatusCode);
    }
}
