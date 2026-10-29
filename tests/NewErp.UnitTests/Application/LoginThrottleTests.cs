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
    public void User_IsBlockedAfterTheLimit_UntilTheWindowEnds()
    {
        for (var i = 0; i < 2; i++)
            _throttle.Failed("10.0.0.1", "olena");
        Assert.Null(_throttle.RetryAfter("10.0.0.1", "olena"));

        _throttle.Failed("10.0.0.2", " Olena "); // інша адреса й запис логіна — той самий обліковий запис
        Assert.Equal(TimeSpan.FromMinutes(15), _throttle.RetryAfter("10.0.0.9", "OLENA"));
        Assert.Null(_throttle.RetryAfter("10.0.0.9", "marta"));

        _clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Null(_throttle.RetryAfter("10.0.0.9", "olena"));
    }

    [Fact]
    public void Address_IsBlockedAfterGuessingManyUsers_AndSuccessDoesNotResetIt()
    {
        for (var i = 0; i < 5; i++)
            _throttle.Failed("10.0.0.1", $"user{i}");
        _throttle.Succeeded("user0");

        Assert.NotNull(_throttle.RetryAfter("10.0.0.1", "someone"));
        Assert.Null(_throttle.RetryAfter("10.0.0.2", "someone"));
    }

    [Fact]
    public void Success_ResetsTheUserCounter()
    {
        _throttle.Failed("10.0.0.1", "olena");
        _throttle.Failed("10.0.0.1", "olena");
        _throttle.Succeeded("olena");
        _throttle.Failed("10.0.0.1", "olena");

        Assert.Null(_throttle.RetryAfter("10.0.0.1", "olena"));
    }
}
