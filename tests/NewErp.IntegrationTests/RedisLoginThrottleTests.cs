using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

using NewErp.Application.Auth;
using NewErp.Infrastructure.Auth;

using StackExchange.Redis;

namespace NewErp.IntegrationTests;

/// <summary>Тест, якому потрібні Redis і MySQL: без NEWERP_TEST_REDIS чи NEWERP_TEST_DB пропускається.</summary>
public sealed class RedisFactAttribute : FactAttribute
{
    public const string EnvVar = "NEWERP_TEST_REDIS";

    public static string? Server => Environment.GetEnvironmentVariable(EnvVar);

    public RedisFactAttribute()
    {
        if (string.IsNullOrEmpty(Server) || string.IsNullOrEmpty(TestDatabase.ServerConnectionString))
            Skip = $"Задайте {EnvVar} і {TestDatabase.EnvVar}, щоб запустити тести Redis.";
    }
}

/// <summary>Лічильники входу в Redis спільні для всіх екземплярів застосунку.</summary>
public class RedisLoginThrottleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Prefix = "newerp:login:";

    private static RedisLoginThrottle Instance() => new(
        RedisLoginThrottle.Connect(RedisFactAttribute.Server!),
        Options.Create(new LoginThrottleOptions { MaxFailuresPerUser = 2, MaxFailuresPerAddress = 3, WindowMinutes = 1 }));

    private static async Task DeleteAsync(params string[] keys)
    {
        using var redis = RedisLoginThrottle.Connect(RedisFactAttribute.Server!);
        foreach (var key in keys)
            await redis.GetDatabase().KeyDeleteAsync(Prefix + key);
    }

    [RedisFact]
    public async Task TwoInstances_ShareTheCounters_AndSuccessResetsTheUser()
    {
        var (user, other, ip) = ($"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}", $"ip-{Guid.NewGuid():N}");
        using var first = Instance();
        using var second = Instance();
        try
        {
            await first.FailedAsync(ip, user);
            Assert.Null(await second.RetryAfterAsync(ip, user));

            await second.FailedAsync(ip, user); // друга невдала спроба, порахована іншим екземпляром
            var wait = await first.RetryAfterAsync(ip, user);
            Assert.InRange(wait!.Value.TotalSeconds, 0.001, 60);

            await first.SucceededAsync(user);
            Assert.Null(await second.RetryAfterAsync(ip, user)); // адреса має 2 із 3

            await first.FailedAsync(ip, other);
            Assert.NotNull(await second.RetryAfterAsync(ip, "хтось")); // а тепер закрита й адреса
        }
        finally
        {
            await DeleteAsync(LoginThrottleKeys.User(user), LoginThrottleKeys.User(other), LoginThrottleKeys.Address(ip));
        }
    }

    [RedisFact]
    public async Task EveryFailure_LeavesTheKeyWithATimeToLive()
    {
        var (user, ip) = ($"user-{Guid.NewGuid():N}", $"ip-{Guid.NewGuid():N}");
        using var throttle = Instance();
        using var redis = RedisLoginThrottle.Connect(RedisFactAttribute.Server!);
        var key = (RedisKey)(Prefix + LoginThrottleKeys.User(user));
        try
        {
            await throttle.FailedAsync(ip, user);
            await throttle.FailedAsync(ip, user);

            Assert.InRange((await redis.GetDatabase().KeyTimeToLiveAsync(key))!.Value.TotalSeconds, 0.001, 60);
        }
        finally
        {
            await DeleteAsync(LoginThrottleKeys.User(user), LoginThrottleKeys.Address(ip));
        }
    }

    [RedisFact]
    public async Task TheApi_UsesRedis_WhenItIsConfigured()
    {
        var user = $"redis-{Guid.NewGuid():N}"[..20];
        HttpClient App() => factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Redis", RedisFactAttribute.Server);
            b.UseSetting("LoginThrottle:MaxFailuresPerUser", "2");
        }).CreateClient();
        try
        {
            var first = App();
            for (var i = 0; i < 2; i++)
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await first.PostAsJsonAsync("/auth/login", new LoginRequest(user, "wrong-password"))).StatusCode);

            var blocked = await App().PostAsJsonAsync("/auth/login", new LoginRequest(user, "wrong-password"));

            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        }
        finally
        {
            await DeleteAsync(LoginThrottleKeys.User(user), LoginThrottleKeys.Address(null));
        }
    }
}
