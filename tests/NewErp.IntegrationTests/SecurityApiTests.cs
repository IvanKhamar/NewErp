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
    public async Task Login_TakesOnlyJson_SoAFormOnAnotherSiteCannotLogIn()
    {
        // Чужа сторінка може надіслати без CORS лише форму або text/plain. Вхід на такий запит не видає cookie.
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = ApiFactory.Admin, ["password"] = ApiFactory.Password });
        var text = new StringContent($$"""{"username":"{{ApiFactory.Admin}}","password":"{{ApiFactory.Password}}"}""", System.Text.Encoding.UTF8, "text/plain");

        foreach (HttpContent content in new HttpContent[] { form, text })
        {
            var response = await factory.CreateClient().PostAsync("/auth/login", content);

            Assert.False(response.IsSuccessStatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    [MySqlFact]
    public async Task Login_IgnoresXForwardedFor_WithoutTrustedProxies()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("LoginThrottle:MaxFailuresPerAddress", "3")).CreateClient();

        async Task<HttpStatusCode> Attempt(int i)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new LoginRequest($"guess{i}", "wrong-password")) };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i}");
            return (await client.SendAsync(request)).StatusCode;
        }

        for (var i = 1; i <= 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await Attempt(i));

        Assert.Equal(HttpStatusCode.TooManyRequests, await Attempt(4)); // нова «адреса» в заголовку не обходить ліміт
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
