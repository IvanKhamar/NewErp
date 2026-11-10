using NewErp.Domain.Orders;

namespace NewErp.UnitTests.Domain;

/// <summary>Перевірка відправки й посилка для тарифу Нової Пошти (пункти 36, 41; правила Python-версії).</summary>
public class ShippingChecksTests
{
    private const string NovaPoshta = "novaposhta_warehouse.novaposhta_warehouse";

    [Fact]
    public void Shipped_ByNovaPoshta_NeedsAWaybill_ThenItsSms_ToTheOrderPhone()
    {
        Assert.Equal(IssueLevel.Danger, Assert.Single(ShippingChecks.ShippedIssues(NovaPoshta, "+380671112233", "", [])).Level);
        Assert.Equal(IssueLevel.Warning, Assert.Single(ShippingChecks.ShippedIssues(NovaPoshta, "+380671112233", "20450000001111", [])).Level);
        Assert.Empty(ShippingChecks.ShippedIssues(NovaPoshta, "+380671112233", "20450000001111", ["0671112233"]));

        var wrong = Assert.Single(ShippingChecks.ShippedIssues(NovaPoshta, "+380671112233", "20450000001111", ["380509999999", "380509999999"]));
        Assert.Equal("SMS з цією ТТН пішла на номер 380509999999, а в замовленні +380671112233", wrong.Text);
    }

    [Fact]
    public void Shipped_WithoutNovaPoshta_NeedsNothing() =>
        Assert.Empty(ShippingChecks.ShippedIssues("pickup.pickup", "+380671112233", "", []));

    [Fact]
    public void AwaitingShipment_IsForPickupsWithoutAWaybill()
    {
        Assert.Empty(ShippingChecks.PickupIssues("pickup.pickup", "Самовивіз", ""));
        Assert.Equal(IssueLevel.Warning, Assert.Single(ShippingChecks.PickupIssues(NovaPoshta, "Нова Пошта", "")).Level);
        Assert.Equal(IssueLevel.Danger, Assert.Single(ShippingChecks.PickupIssues("pickup.pickup", "Самовивіз", "20450000001111")).Level);
    }

    [Fact]
    public void WaybillDigits_DropsSpacesAndLetters() =>
        Assert.Equal("20450000001111", ShippingChecks.WaybillDigits("2045 0000 0011 11 "));

    [Fact]
    public void Parcel_CountsQuantity_RoundsSizesUp_AndWeighsAtLeastATenthOfAKilo()
    {
        ParcelItem[] items = [new(2, 0.7m, 29.2m, 20, 5), new(1, 0, 10, 10, 10)];

        Assert.Equal(1.4m, Parcel.Weight(items));
        Assert.Equal((2 * 30 * 20 * 5 + 10 * 10 * 10) / 1_000_000m, Parcel.Volume(items));
        Assert.Equal(0.1m, Parcel.Weight([new(1, 0.02m, 1, 1, 1)]));
    }
}
