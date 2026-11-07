using Microsoft.Extensions.DependencyInjection;

using NewErp.Application;

namespace NewErp.UnitTests.Application;

public class BusinessClockTests
{
    private sealed class FixedBusinessClock(DateTimeOffset utc) : BusinessClock
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }

    [Theory]
    [InlineData("2026-11-06T21:59:00Z", "2026-11-06", 2)] // зима: UTC+2, дата ще та сама
    [InlineData("2026-11-06T22:00:00Z", "2026-11-07", 2)] // у Києві вже наступний день, в UTC ще ні
    [InlineData("2026-11-06T23:30:00Z", "2026-11-07", 2)]
    [InlineData("2026-11-07T00:00:00Z", "2026-11-07", 2)] // далі дати знову однакові
    [InlineData("2026-07-01T20:59:00Z", "2026-07-01", 3)] // літо: UTC+3
    [InlineData("2026-07-01T21:00:00Z", "2026-07-02", 3)]
    public void LocalDate_IsTheKyivDate_WhateverTheMachineZone(string utc, string kyivDate, int offsetHours)
    {
        var now = new FixedBusinessClock(DateTimeOffset.Parse(utc)).GetLocalNow();

        Assert.Equal(DateOnly.Parse(kyivDate), DateOnly.FromDateTime(now.DateTime));
        Assert.Equal(TimeSpan.FromHours(offsetHours), now.Offset);
    }

    [Fact]
    public void Application_UsesTheBusinessClock()
    {
        var clock = new ServiceCollection().AddApplication().Single(d => d.ServiceType == typeof(TimeProvider)).ImplementationInstance;

        Assert.IsType<BusinessClock>(clock);
    }
}
