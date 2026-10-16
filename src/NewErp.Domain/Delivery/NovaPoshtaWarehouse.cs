using System.Text.RegularExpressions;

namespace NewErp.Domain.Delivery;

/// <summary>
/// Відділення чи поштомат з довідника Нової Пошти (dd_novaposhta_warehouse старого сайту).
/// Ним користується й вітрина старого сайту, тож правила сортування й «працює» — як у старому коді.
/// </summary>
public partial class NovaPoshtaWarehouse
{
    /// <summary>Категорії, які показуємо як робочі: відділення, магазин-партнер, пересувне відділення, поштомат.</summary>
    public static readonly IReadOnlySet<string> ActiveCategories = new HashSet<string> { "Branch", "Store", "MobileBranch", "Postomat" };

    private NovaPoshtaWarehouse() { }

    public NovaPoshtaWarehouse(
        string reference, string description, string category, string cityRef, string cityDescription, int maxWeight, bool working)
    {
        Ref = reference;
        Description = Truncate(description);
        Category = category;
        CityRef = cityRef;
        CityDescription = Truncate(cityDescription.Trim());
        MaxWeight = maxWeight;
        // Як старий сайт: номер відділення з назви («Відділення №12: …» → 12), щоб №2 ішло перед №10.
        SortOrder = BranchNumber().Match(description) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 1;
        Status = working && ActiveCategories.Contains(category);
    }

    public string Ref { get; private set; } = "";

    public string Description { get; private set; } = "";

    /// <summary>CategoryOfWarehouse з API: Branch, Postomat, Store…</summary>
    public string Category { get; private set; } = "";

    public string CityRef { get; private set; } = "";

    public string CityDescription { get; private set; } = "";

    /// <summary>Найбільша вага посилки, кг (TotalMaxWeightAllowed; 0 — без обмеження).</summary>
    public int MaxWeight { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Працює й приймає посилки.</summary>
    public bool Status { get; private set; }

    private static string Truncate(string value) => value.Length > 255 ? value[..255] : value;

    [GeneratedRegex(@"№\s*(\d+)")]
    private static partial Regex BranchNumber();
}

/// <summary>Місце (коробка) у відправленні: вага в кг, розміри в см.</summary>
public sealed record ShipmentSeat(decimal Weight, decimal Width, decimal Length, decimal Height)
{
    /// <summary>Об'єм у м³, як його чекає API Нової Пошти (volumetricVolume).</summary>
    public decimal Volume => Width * Length * Height / 1_000_000m;
}

/// <summary>Готові коробки зі старого сайту (кнопки «маленька / середня / велика»).</summary>
public sealed record SeatPreset(string Name, ShipmentSeat Seat)
{
    public static readonly IReadOnlyList<SeatPreset> All =
    [
        new("Маленька", new ShipmentSeat(1, 31, 36, 6)),
        new("Середня", new ShipmentSeat(1, 41, 48, 8)),
        new("Велика", new ShipmentSeat(1, 49, 61, 14)),
    ];
}
