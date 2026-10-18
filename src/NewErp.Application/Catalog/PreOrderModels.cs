using NewErp.Domain.Catalog;

namespace NewErp.Application.Catalog;

/// <summary>Розмір товару з залишком на вітрині, лімітом передзамовлення й датою, коли його чекають.</summary>
public sealed record PreOrderRow(
    int ProductOptionValueId, int ProductId, string ProductName, string Model, string Size,
    int Quantity, int Quota, DateOnly? DateAvailable, SizeAvailability Availability);

/// <param name="Search">Частина назви чи артикула товару.</param>
/// <param name="OnlyWithQuota">Лише розміри, де ліміт передзамовлення більший за 0.</param>
public sealed record PreOrderFilter(string? Search = null, bool OnlyWithQuota = false);

public sealed record UpdatePreOrderRequest(int Quota, DateOnly? DateAvailable);

/// <summary>Sizes — у скількох розмірах продане наперед списано з ліміту.</summary>
public sealed record PreOrderSettleResult(int Sizes);

/// <summary>Розділ "PreOrders" в appsettings.json.</summary>
public sealed class PreOrderOptions
{
    public const string SectionName = "PreOrders";

    /// <summary>Як часто списувати продане наперед з лімітів (хвилини). 0 — лише кнопкою чи POST /preorders/settle.</summary>
    public int SettleIntervalMinutes { get; set; } = 60;
}
