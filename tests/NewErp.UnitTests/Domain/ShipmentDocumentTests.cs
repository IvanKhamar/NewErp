using NewErp.Domain.Delivery;

namespace NewErp.UnitTests.Domain;

public class ShipmentDocumentTests
{
    private static TrackingInfo Info(int code, bool redelivery = false, DateTime? created = null) =>
        new("20450000000001", $"Статус {code}", code, "Київ, відділення 1", redelivery, created, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25));

    private static ShipmentDocument Tracked(int code, bool redelivery = false)
    {
        var document = new ShipmentDocument(1, "20450000000001", 70m);
        document.ApplyTracking(Info(code, redelivery));
        return document;
    }

    [Theory]
    [InlineData(9, false, ShipmentTone.Success)]
    [InlineData(9, true, ShipmentTone.Info)]     // отримано, але гроші ще не повернулися
    [InlineData(11, true, ShipmentTone.Success)]
    [InlineData(11, false, ShipmentTone.Info)]
    [InlineData(106, false, ShipmentTone.Success)]
    [InlineData(3, false, ShipmentTone.Error)]
    [InlineData(107, false, ShipmentTone.Error)]
    [InlineData(102, false, ShipmentTone.Warning)]
    [InlineData(108, false, ShipmentTone.Warning)]
    [InlineData(7, false, ShipmentTone.Info)]
    public void Tone_FollowsOldSiteColors(int code, bool redelivery, ShipmentTone expected) =>
        Assert.Equal(expected, Tracked(code, redelivery).Tone);

    [Theory]
    [InlineData(9, true)]
    [InlineData(103, true)]
    [InlineData(7, false)]
    [InlineData(1, false)]
    public void IsFinal_OnlyForClosedStatuses(int code, bool expected) =>
        Assert.Equal(expected, Tracked(code).IsFinal);

    [Fact]
    public void ApplyTracking_SameCode_ChangesNothing()
    {
        var document = Tracked(7);

        var changed = document.ApplyTracking(Info(7) with { Status = "Інший текст" });

        Assert.False(changed);
        Assert.Equal("Статус 7", document.Status);
    }

    [Fact]
    public void ApplyTracking_NewCode_UpdatesAndKeepsKnownCreatedDate()
    {
        var created = new DateTime(2026, 10, 15, 10, 0, 0);
        var document = new ShipmentDocument(1, "20450000000001", 70m);
        document.ApplyTracking(Info(1, created: created));

        var changed = document.ApplyTracking(Info(7) with { Status = new string('x', 300) });

        Assert.True(changed);
        Assert.Equal(7, document.StatusCode);
        Assert.Equal(255, document.Status.Length);
        Assert.Equal(created, document.DateCreated);
        Assert.Equal(new DateOnly(2026, 10, 25), document.DatePayedKeeping);
    }
}

public class NovaPoshtaWarehouseTests
{
    [Theory]
    [InlineData("Відділення №12 (до 30 кг): вул. Шевченка, 1", "Branch", true, 12, true)]
    [InlineData("Поштомат \"Нова Пошта\" №5107", "Postomat", true, 5107, true)]
    [InlineData("Пункт приймання-видачі", "Branch", true, 1, true)]      // без номера — 1, як на старому сайті
    [InlineData("Відділення №3", "Branch", false, 3, false)]               // не працює
    [InlineData("Вантажне відділення №1", "CargoBranch", true, 1, false)]  // не для посилок клієнтам
    public void SortOrderAndStatus_AsOldSite(string description, string category, bool working, int sortOrder, bool status)
    {
        var warehouse = new NovaPoshtaWarehouse("ref", description, category, "city", " Київ ", 30, working);

        Assert.Equal((sortOrder, status, "Київ"), (warehouse.SortOrder, warehouse.Status, warehouse.CityDescription));
    }

    [Fact]
    public void SeatVolume_InCubicMeters() =>
        Assert.Equal(0.006696m, new ShipmentSeat(1, 31, 36, 6).Volume);
}
