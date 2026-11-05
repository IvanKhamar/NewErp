using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Auth;

namespace NewErp.UnitTests.Application;

public class LoginThrottleTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 10, 0, 0, TimeSpan.Zero));
    private readonly LoginThrottle _throttle;

    public LoginThrottleTests() =>
        _throttle = new LoginThrottle(Options.Create(new LoginThrottleOptions { MaxFailuresPerUser = 3, MaxFailuresPerAddress = 5, WindowMinutes = 15 }),
            _clock);

    [Fact]
    public async Task User_IsBlockedAfterTheLimit_UntilTheWindowEnds()
    {
        for (var i = 0; i < 2; i++)
            await _throttle.FailedAsync("10.0.0.1", "olena");
        Assert.Null(await _throttle.RetryAfterAsync("10.0.0.1", "olena"));

        await _throttle.FailedAsync("10.0.0.2", " Olena "); // інша адреса й запис логіна — той самий обліковий запис
        Assert.Equal(TimeSpan.FromMinutes(15), await _throttle.RetryAfterAsync("10.0.0.9", "OLENA"));
        Assert.Null(await _throttle.RetryAfterAsync("10.0.0.9", "marta"));

        _clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Null(await _throttle.RetryAfterAsync("10.0.0.9", "olena"));
    }

    [Fact]
    public async Task Address_IsBlockedAfterGuessingManyUsers_AndSuccessDoesNotResetIt()
    {
        for (var i = 0; i < 5; i++)
            await _throttle.FailedAsync("10.0.0.1", $"user{i}");
        await _throttle.SucceededAsync("user0");

        Assert.NotNull(await _throttle.RetryAfterAsync("10.0.0.1", "someone"));
        Assert.Null(await _throttle.RetryAfterAsync("10.0.0.2", "someone"));
    }

    [Fact]
    public async Task Success_ResetsTheUserCounter()
    {
        await _throttle.FailedAsync("10.0.0.1", "olena");
        await _throttle.FailedAsync("10.0.0.1", "olena");
        await _throttle.SucceededAsync("olena");
        await _throttle.FailedAsync("10.0.0.1", "olena");

        Assert.Null(await _throttle.RetryAfterAsync("10.0.0.1", "olena"));
    }
}
