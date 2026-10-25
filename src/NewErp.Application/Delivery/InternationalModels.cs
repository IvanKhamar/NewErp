using NewErp.Domain.Delivery;

namespace NewErp.Application.Delivery;

/// <summary>Nova Post International (api.novapost.com): накладні за кордон. Без ключа — заглушка (див. DependencyInjection).</summary>
public interface INovaPostInternationalClient
{
    /// <summary>Ключ задано справжній.</summary>
    bool IsConfigured { get; }

    /// <summary>Накладні можна створювати: з ключем або з FakeDocuments у розробці.</summary>
    bool CanCreateDocuments { get; }

    /// <summary>Митні коди (УКТЗЕД) за словом для країни отримувача.</summary>
    Task<IReadOnlyList<HsCodeDto>> SearchHsCodesAsync(string keyword, string countryCode, CancellationToken ct);

    /// <returns>id відправлення (зберігаємо як Ref документа), номер і вартість</returns>
    Task<CreatedShipment> CreateAsync(InternationalShipment shipment, CancellationToken ct);

    Task DeleteAsync(string shipmentId, CancellationToken ct);
}

/// <summary>Підказка митного коду: Label — товар і підкатегорія українською, NameEng — англійська назва товару.</summary>
public sealed record HsCodeDto(string HsCode, string Label, string NameEng);

/// <summary>Країна для вибору: ISO-код і назва українською.</summary>
public sealed record CountryDto(string Code, string Name);

/// <summary>Форма міжнародної накладної. Частини — прості записи домену; InternationalShipment.Of перевіряє все разом.</summary>
/// <param name="Parcels">Розміри в см, вага в кг (вага важить лише тоді, коли місць кілька).</param>
public sealed record CreateInternationalShipmentRequest(
    InternationalShipment.RecipientInfo? Recipient, InternationalShipment.TermsInfo? Terms, IReadOnlyList<InternationalShipment.Item>? Items,
    IReadOnlyList<ShipmentSeat>? Parcels, decimal? DeclaredCost, string? Currency);

/// <summary>
/// З чим відкривається форма: отримувач і рядки із замовлення (коди УКТЗЕД, англійські назви й вагу вводять вручну), сума
/// замовлення як оголошена вартість, готові коробки і чи можна створити накладну. Test — створиться лише тестова.
/// </summary>
public sealed record InternationalFormDto(
    InternationalShipment.RecipientInfo Recipient, InternationalShipment.TermsInfo Terms, IReadOnlyList<InternationalShipment.Item> Items,
    decimal DeclaredCost, string Currency, IReadOnlyList<SeatPreset> SeatPresets, bool CanCreate, bool Test);

/// <summary>Дані замовлення для міжнародної форми. Country — текст із замовлення, як його записав магазин.</summary>
public sealed record InternationalOrderInfo(
    string FirstName, string LastName, string Telephone, string Email, string Country, string Region, string City, string PostCode,
    string Address, decimal Total, string CurrencyCode, IReadOnlyList<InternationalOrderLine> Lines);

public sealed record InternationalOrderLine(string Name, int Quantity, decimal Total);
