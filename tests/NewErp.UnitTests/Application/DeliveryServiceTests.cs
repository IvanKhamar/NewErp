using Microsoft.Extensions.Time.Testing;

using NewErp.Application.Common;
using NewErp.Application.Delivery;
using NewErp.Domain.Delivery;

namespace NewErp.UnitTests.Application;

public class DeliveryServiceTests
{
    private readonly FakeShipmentRepository _repository = new();
    private readonly FakeNovaPoshtaClient _client = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
    private readonly DeliveryService _service;

    public DeliveryServiceTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new DeliveryService(_repository, _client, _clock);
    }

    private static TrackingInfo Info(string number, int code) =>
        new(number, $"Статус {code}", code, "", false, null, null, null);

    [Fact]
    public async Task Refresh_WithoutKey_DoesNotCallApi()
    {
        _client.IsConfigured = false;
        _repository.Open.Add(new OpenShipment(new ShipmentDocument(1, "20450000000001", 0), "380670000001"));

        var result = await _service.RefreshTrackingAsync(CancellationToken.None);

        Assert.Equal(new TrackingRefreshResult(false, 0, 0), result);
        Assert.Empty(_client.Batches);
        Assert.False(_repository.Saved);
    }

    [Fact]
    public async Task Refresh_SendsBatchesOf100_AndCountsOnlyChangedStatuses()
    {
        for (var i = 0; i < 150; i++)
            _repository.Open.Add(new OpenShipment(new ShipmentDocument(i, $"2045{i:D10}", 0), "380670000001"));
        _client.Answer = number => number == "20450000000003" ? Info(number, 7) : Info(number, 0); // 0 = як було

        var result = await _service.RefreshTrackingAsync(CancellationToken.None);

        Assert.Equal([100, 50], _client.Batches.Select(b => b.Count));
        Assert.Equal(new TrackingRefreshResult(true, 150, 1), result);
        Assert.Equal(7, _repository.Open[3].Document.StatusCode);
        Assert.True(_repository.Saved);
    }

    [Fact]
    public async Task Reminders_FromTodayToThreeDaysAhead()
    {
        await _service.RemindersAsync(CancellationToken.None);

        Assert.Equal((new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 19)), _repository.ReminderRange);
    }

    [Fact]
    public async Task MarkReminded_UnknownOrder_NotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkRemindedAsync(42, CancellationToken.None));
}
