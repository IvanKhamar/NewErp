using System.Text.RegularExpressions;

using NewErp.Domain.Common;

namespace NewErp.Domain.Delivery;

/// <summary>
/// Накладна Nova Post International, як її збирала вкладка «Міжнародна» старого сайту: отримувач за кордоном, митний
/// інвойс (код УКТЗЕД, англійська назва й вага для кожного рядка) і місця. Вага товарів у грамах, розміри місць у см і кг
/// (як <see cref="ShipmentSeat"/>); <see cref="Parcels"/> перераховує їх у мм і грами для API. Те саме, що в Java-версії.
/// </summary>
public sealed partial record InternationalShipment(
    int OrderId, InternationalShipment.RecipientInfo Recipient, InternationalShipment.TermsInfo Terms, IReadOnlyList<InternationalShipment.Item> Items,
    IReadOnlyList<ShipmentSeat> Seats, decimal DeclaredCost, string Currency)
{
    /// <summary>Для цих країн API вимагає штат чи область.</summary>
    public static readonly IReadOnlySet<string> RegionRequired = new HashSet<string> { "US", "IE", "CA" };

    public sealed record RecipientInfo(
        string? FirstName, string? LastName, string? Phone, string? Email, string? CountryCode, string? Region,
        string? City, string? Street, string? Building, string? Flat, string? PostCode);

    /// <summary>Митні умови й умови доставки; порожні беруть типові значення старого сайту. DeliveryType — лише якщо обрано.</summary>
    public sealed record TermsInfo(
        string? Incoterm = null, string? ExportReason = null, string? PayerFeesCustoms = null, string? CargoCategory = null,
        string? DeliveryType = null, string? ParcelDescription = null)
    {
        public string Incoterm { get; } = OrDefault(Incoterm, "DAP");
        public string ExportReason { get; } = OrDefault(ExportReason, "Selling");
        public string PayerFeesCustoms { get; } = OrDefault(PayerFeesCustoms, "Recipient");
        public string CargoCategory { get; } = OrDefault(CargoCategory, "parcel");
        public string? DeliveryType { get; } = string.IsNullOrWhiteSpace(DeliveryType) ? null : DeliveryType.Trim();
        public string ParcelDescription { get; } = OrDefault(ParcelDescription, "Printed apparel / merchandise");
    }

    /// <summary>Рядок митного інвойсу. WeightGrams — вага всього рядка, як питала форма старого сайту.</summary>
    public sealed record Item(string? Name, string? NameEng, string? HsCode, int WeightGrams, decimal? Amount, decimal? Cost);

    /// <summary>Місце, як його хоче API: розміри в мм, вага в грамах, його частка оголошеної вартості як страхування.</summary>
    public sealed record Parcel(int RowNumber, int WidthMm, int LengthMm, int HeightMm, int WeightGrams, decimal InsuranceCost);

    /// <summary>Перевіряє все, що перевіряла форма старого сайту, і перелічує все пропущене одним повідомленням.</summary>
    public static InternationalShipment Of(
        int orderId, RecipientInfo? recipient, TermsInfo? terms, IReadOnlyList<Item>? items, IReadOnlyList<ShipmentSeat>? seats,
        decimal? declaredCost, string? currency)
    {
        var missing = new List<string>();
        var r = recipient ?? new RecipientInfo(null, null, null, null, null, null, null, null, null, null, null);
        Require(missing, r.LastName, "Прізвище");
        Require(missing, r.FirstName, "Ім'я");
        Require(missing, r.Phone, "Телефон");
        Require(missing, r.City, "Населений пункт");
        Require(missing, r.Street, "Вулиця");
        Require(missing, r.Building, "Будинок");
        Require(missing, r.PostCode, "Індекс");
        var country = string.IsNullOrWhiteSpace(r.CountryCode) ? "" : r.CountryCode.Trim().ToUpperInvariant();
        if (!CountryCode().IsMatch(country) || country == "UA")
        {
            missing.Add("Країна отримувача (не Україна)");
            country = "";
        }
        if (RegionRequired.Contains(country) && string.IsNullOrWhiteSpace(r.Region))
            missing.Add("Штат/область (обов'язково для США, Ірландії, Канади)");

        var lines = items ?? [];
        if (lines.Count == 0)
            missing.Add("товари в замовленні");
        var cleanLines = new List<Item>();
        foreach (var item in lines)
        {
            var label = string.IsNullOrWhiteSpace(item.Name) ? "товар" : item.Name;
            var hsCode = NonDigits().Replace(item.HsCode ?? "", "");
            if (hsCode.Length == 0)
                missing.Add($"код УКТЗЕД ({label})");
            if (string.IsNullOrWhiteSpace(item.NameEng))
                missing.Add($"англійська назва товару ({label})");
            if (item.WeightGrams <= 0)
                missing.Add($"вага товару в грамах ({label})");
            cleanLines.Add(new Item(item.Name?.Trim() ?? "", item.NameEng?.Trim() ?? "", hsCode, item.WeightGrams,
                item.Amount is > 0 ? item.Amount : 1m, item.Cost ?? 0m));
        }
        if (declaredCost is not > 0)
            missing.Add("декларована вартість");
        if (seats is null || seats.Count == 0)
            missing.Add("параметри місця відправлення (розміри/вага)");
        if (missing.Count > 0)
            throw new DomainException($"Не заповнені або некоректні поля: {string.Join(", ", missing)}.");

        var clean = new RecipientInfo(r.FirstName!.Trim(), r.LastName!.Trim(), NonDigits().Replace(r.Phone!, ""), r.Email?.Trim() ?? "", country,
            r.Region?.Trim() ?? "", r.City!.Trim(), r.Street!.Trim(), r.Building!.Trim(), r.Flat?.Trim() ?? "", r.PostCode!.Trim());
        return new InternationalShipment(orderId, clean, terms ?? new TermsInfo(), cleanLines, seats!.ToList(), declaredCost!.Value,
            string.IsNullOrWhiteSpace(currency) ? "UAH" : currency.Trim().ToUpperInvariant());
    }

    public int ItemsWeightGrams => Items.Sum(i => i.WeightGrams);

    /// <summary>
    /// Місця для API, як рахував старий сайт: см × 10 = мм, оголошена вартість порівну на місця як страхування, а одне
    /// місце важить стільки, скільки товари в ньому (кілька місць — кожне свою вагу в кг × 1000).
    /// </summary>
    public IReadOnlyList<Parcel> Parcels()
    {
        var insurance = Math.Round(DeclaredCost / Seats.Count, 2, MidpointRounding.AwayFromZero);
        return Seats.Select((seat, i) => new Parcel(i + 1, Mm(seat.Width), Mm(seat.Length), Mm(seat.Height),
                Seats.Count == 1 ? ItemsWeightGrams : (int)Math.Round(seat.Weight * 1000, MidpointRounding.AwayFromZero), insurance))
            .ToList();
    }

    private static int Mm(decimal cm) => (int)Math.Round((cm > 0 ? cm : 1) * 10, MidpointRounding.AwayFromZero);

    private static void Require(List<string> missing, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            missing.Add(label);
    }

    private static string OrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCode();

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}
