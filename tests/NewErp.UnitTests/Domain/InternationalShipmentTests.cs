using NewErp.Domain.Common;
using NewErp.Domain.Delivery;

namespace NewErp.UnitTests.Domain;

/// <summary>Перенесено з Java InternationalShipmentTest; вигадана отримувачка живе в Польщі.</summary>
public class InternationalShipmentTests
{
    private static readonly InternationalShipment.RecipientInfo Anna =
        new(" Anna ", "Kowalska", "+48 600-100-200", null, "pl", null, "Kraków", "Floriańska", "5", null, "31-019");
    private static readonly InternationalShipment.Item Dress = new("Сукня лляна", "Linen dress", "6204.49.10", 450, 2m, 3000m);
    private static readonly InternationalShipment.Item Belt = new("Пояс", "Leather belt", "4203300000", 150, null, 500m);
    private static readonly ShipmentSeat Medium = SeatPreset.All[1].Seat;

    [Fact]
    public void CleansTheInput_AndFillsTheDefaults()
    {
        var shipment = InternationalShipment.Of(1001, Anna, null, [Dress], [Medium], 3000m, null);

        Assert.Equal(("PL", "Anna", "48600100200"), (shipment.Recipient.CountryCode, shipment.Recipient.FirstName, shipment.Recipient.Phone));
        Assert.Equal("62044910", shipment.Items[0].HsCode);
        Assert.Equal(("DAP", "Selling", "Recipient", "parcel", null, "Printed apparel / merchandise"),
            (shipment.Terms.Incoterm, shipment.Terms.ExportReason, shipment.Terms.PayerFeesCustoms, shipment.Terms.CargoCategory,
                shipment.Terms.DeliveryType, shipment.Terms.ParcelDescription));
        Assert.Equal("UAH", shipment.Currency);
    }

    [Fact]
    public void OneParcel_WeighsItsItems_AndInsuresTheWholeCost()
    {
        var shipment = InternationalShipment.Of(1001, Anna, null, [Dress, Belt], [Medium], 3500m, "UAH");

        // Середня коробка 41 × 48 × 8 см.
        Assert.Equal([new InternationalShipment.Parcel(1, 410, 480, 80, 600, 3500m)], shipment.Parcels());
        Assert.Equal(1m, shipment.Items[1].Amount);
    }

    [Fact]
    public void SeveralParcels_WeighThemselves_AndShareTheInsurance()
    {
        var light = new ShipmentSeat(0.35m, 31, 36, 6.5m);
        var heavy = new ShipmentSeat(1.2m, 49, 61, 14);

        var parcels = InternationalShipment.Of(1001, Anna, null, [Dress], [light, heavy, heavy], 1000m, "UAH").Parcels();

        Assert.Equal([350, 1200, 1200], parcels.Select(p => p.WeightGrams));
        Assert.Equal(65, parcels[0].HeightMm);
        Assert.All(parcels, p => Assert.Equal(333.33m, p.InsuranceCost));
        Assert.Equal([1, 2, 3], parcels.Select(p => p.RowNumber));
    }

    [Fact]
    public void ListsEverythingMissing_InOneMessage()
    {
        var nobody = new InternationalShipment.RecipientInfo("", null, null, null, "UA", null, null, null, null, null, null);
        var bare = new InternationalShipment.Item("Сукня", "", "abc", 0, null, null);

        Assert.Equal("Не заповнені або некоректні поля: Прізвище, Ім'я, Телефон, Населений пункт, Вулиця, Будинок, Індекс, "
            + "Країна отримувача (не Україна), код УКТЗЕД (Сукня), англійська назва товару (Сукня), "
            + "вага товару в грамах (Сукня), декларована вартість, параметри місця відправлення (розміри/вага).",
            Assert.Throws<DomainException>(() => InternationalShipment.Of(1001, nobody, null, [bare], [], 0m, null)).Message);
    }

    [Fact]
    public void TheUsIrelandAndCanada_NeedARegion()
    {
        var john = new InternationalShipment.RecipientInfo("John", "Smith", "12025550100", "", "US", " ", "Austin", "Congress Ave", "100", "", "78701");
        Assert.Contains("Штат/область", Assert.Throws<DomainException>(() => InternationalShipment.Of(1001, john, null, [Dress], [Medium], 10m, null)).Message);

        var texas = john with { Region = "TX" };
        Assert.Equal("TX", InternationalShipment.Of(1001, texas, null, [Dress], [Medium], 10m, null).Recipient.Region);
    }
}
