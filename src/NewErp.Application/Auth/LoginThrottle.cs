using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

namespace NewErp.Application.Auth;

/// <summary>Обмеження невдалих спроб входу (секція LoginThrottle у appsettings).</summary>
public sealed class LoginThrottleOptions
{
    public const string SectionName = "LoginThrottle";

    /// <summary>Скільки невдалих спроб за вікно можна для одного логіна (захищає обліковий запис від підбору з багатьох адрес).</summary>
    public int MaxFailuresPerUser { get; set; } = 10;

    /// <summary>Скільки невдалих спроб за вікно можна з однієї IP-адреси (захищає від перебору багатьох логінів).</summary>
    public int MaxFailuresPerAddress { get; set; } = 30;

    /// <summary>Вікно від першої невдалої спроби; після нього лічильник починається заново.</summary>
    public int WindowMinutes { get; set; } = 15;
}

/// <summary>
/// Захист входу від підбору пароля: рахує лише невдалі спроби, тож людина з правильним паролем не впирається в обмеження.
/// Коли спроб забагато, вхід для цього логіна чи адреси закритий до кінця вікна (навіть з правильним паролем, інакше
/// підбір тривав би).
/// </summary>
public interface ILoginThrottle
{
    /// <summary>Скільки ще чекати, якщо вхід зараз закритий; null — можна пробувати.</summary>
    Task<TimeSpan?> RetryAfterAsync(string? address, string? username);

    Task FailedAsync(string? address, string? username);

    /// <summary>Правильний вхід скидає лічильник логіна; лічильник адреси лишається, щоб перебір інших логінів не обнулявся.</summary>
    Task SucceededAsync(string? username);
}

/// <summary>Ключі лічильників: логін без різниці в регістрі й пробілах, адреса як є.</summary>
public static class LoginThrottleKeys
{
    public static string User(string? username) => "user:" + (username ?? "").Trim().ToLowerInvariant();

    public static string Address(string? address) => "ip:" + (string.IsNullOrEmpty(address) ? "unknown" : address);
}

/// <summary>
/// Лічильники в пам'яті одного процесу: після перезапуску вони скидаються. Цього досить для одного екземпляра застосунку;
/// для кількох є RedisLoginThrottle (рядок підключення ConnectionStrings:Redis).
/// </summary>
public sealed class LoginThrottle(IOptions<LoginThrottleOptions> options, TimeProvider clock) : ILoginThrottle
{
    // Межа пам'яті на випадок перебору тисяч вигаданих логінів: тоді спершу прибираємо прострочені лічильники.
    private const int MaxEntries = 10_000;

    private readonly LoginThrottleOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, Window> _failures = new(StringComparer.Ordinal);

    private sealed record Window(int Count, DateTimeOffset Until);

    public Task<TimeSpan?> RetryAfterAsync(string? address, string? username)
    {
        var now = clock.GetUtcNow();
        TimeSpan? wait = null;
        foreach (var (key, max) in Keys(address, username))
            if (_failures.TryGetValue(key, out var window) && window.Until > now && window.Count >= max)
                wait = Max(wait, window.Until - now);
        return Task.FromResult(wait);
    }

    public Task FailedAsync(string? address, string? username)
    {
        var now = clock.GetUtcNow();
        if (_failures.Count >= MaxEntries)
            Prune(now);
        foreach (var (key, _) in Keys(address, username))
            _failures.AddOrUpdate(key, _ => New(now),
                (_, window) => window.Until <= now ? New(now) : window with { Count = window.Count + 1 });
        return Task.CompletedTask;
    }

    public Task SucceededAsync(string? username)
    {
        _failures.TryRemove(LoginThrottleKeys.User(username), out _);
        return Task.CompletedTask;
    }

    private Window New(DateTimeOffset now) => new(1, now.AddMinutes(_options.WindowMinutes));

    private IEnumerable<(string Key, int Max)> Keys(string? address, string? username)
    {
        yield return (LoginThrottleKeys.User(username), _options.MaxFailuresPerUser);
        yield return (LoginThrottleKeys.Address(address), _options.MaxFailuresPerAddress);
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, window) in _failures)
            if (window.Until <= now)
                _failures.TryRemove(key, out _);
        // Якщо й живих лічильників забагато, це масова атака з різних адрес: краще почати лік заново, ніж з'їсти пам'ять.
        if (_failures.Count >= MaxEntries)
            _failures.Clear();
    }

    private static TimeSpan Max(TimeSpan? a, TimeSpan b) => a is { } x && x > b ? x : b;
}
