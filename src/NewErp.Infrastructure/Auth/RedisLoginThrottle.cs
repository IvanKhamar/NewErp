using Microsoft.Extensions.Options;

using NewErp.Application.Auth;

using StackExchange.Redis;

namespace NewErp.Infrastructure.Auth;

/// <summary>
/// Обмеження входу з лічильниками в Redis: усі екземпляри застосунку бачать ті самі невдалі спроби, і перезапуск їх не скидає.
/// Правила ті самі, що в <see cref="LoginThrottle"/>. Вікно — це час життя ключа: він починається з першої невдалої спроби
/// і закінчується сам. Якщо Redis недоступний, вхід відповідає 500: без лічильників підбір пароля нічим не обмежений.
/// </summary>
public sealed class RedisLoginThrottle(IConnectionMultiplexer redis, IOptions<LoginThrottleOptions> options) : ILoginThrottle, IDisposable
{
    private const string Prefix = "newerp:login:";

    private readonly LoginThrottleOptions _options = options.Value;

    /// <summary>abortConnect=false: застосунок стартує, навіть якщо Redis ще піднімається.</summary>
    public static IConnectionMultiplexer Connect(string connectionString)
    {
        var configuration = ConfigurationOptions.Parse(connectionString);
        configuration.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(configuration);
    }

    public async Task<TimeSpan?> RetryAfterAsync(string? address, string? username)
    {
        var db = redis.GetDatabase();
        TimeSpan? wait = null;
        foreach (var (key, max) in Keys(address, username))
        {
            var count = await db.StringGetAsync(key);
            if (count.HasValue && (long)count >= max && await db.KeyTimeToLiveAsync(key) is { } left && (wait is null || left > wait))
                wait = left;
        }
        return wait;
    }

    public async Task FailedAsync(string? address, string? username)
    {
        var db = redis.GetDatabase();
        var window = TimeSpan.FromMinutes(_options.WindowMinutes);
        foreach (var (key, _) in Keys(address, username))
        {
            await db.StringIncrementAsync(key);
            // HasNoExpiry: вікно не подовжується, а ключ без часу життя не лишиться навіть після збою між двома командами.
            await db.KeyExpireAsync(key, window, ExpireWhen.HasNoExpiry);
        }
    }

    public Task SucceededAsync(string? username) =>
        redis.GetDatabase().KeyDeleteAsync(Prefix + LoginThrottleKeys.User(username));

    public void Dispose() => redis.Dispose();

    private (string Key, int Max)[] Keys(string? address, string? username) =>
    [
        (Prefix + LoginThrottleKeys.User(username), _options.MaxFailuresPerUser),
        (Prefix + LoginThrottleKeys.Address(address), _options.MaxFailuresPerAddress),
    ];
}
