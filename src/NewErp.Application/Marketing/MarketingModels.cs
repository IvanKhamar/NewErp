using NewErp.Domain.Marketing;

namespace NewErp.Application.Marketing;

/// <summary>Розділ "Marketing" в appsettings.json. Значення за замовчуванням — як у коді старого сайту.</summary>
public sealed class MarketingOptions
{
    public const string SectionName = "Marketing";

    public decimal BirthdayDiscountPercent { get; set; } = 20;

    /// <summary>Скільки днів діє купон від дня створення.</summary>
    public int BirthdayCouponDays { get; set; } = 30;

    /// <summary>Місто клієнта, який забирав замовлення сам (у замовленні міста немає, а спосіб доставки містить маркер).</summary>
    public string PickupCity { get; set; } = "Львів";

    public string PickupMethodMarker { get; set; } = "амовив";

    /// <summary>Пауза між SMS масової розсилки, щоб не впертися в обмеження SMS-сервісу.</summary>
    public int BulkSmsDelayMilliseconds { get; set; } = 300;
}

/// <summary>Іменинник: дані з його найновішого замовлення, де вказано день народження.</summary>
/// <param name="SentThisYear">Скільки привітань цьому номеру вже надіслано цього року.</param>
public sealed record BirthdayContactDto(
    string Phone, string FirstName, string LastName, string Email, string City, string Instagram,
    int Day, int Month, DateOnly NextBirthday, int SentThisYear);

/// <param name="CouponCode">Якщо задано — спершу створюється купон з цим кодом (його вставляють у текст).</param>
public sealed record BirthdaySmsRequest(string Phone, string Text, string? CouponCode = null);

public sealed record BirthdaySmsResult(bool Sent, int? CouponId, string Response);

public sealed record BulkSmsRequest(IReadOnlyList<string> Phones, string Text);

/// <param name="Skipped">Номери, які не схожі на український мобільний.</param>
public sealed record BulkSmsResult(int Sent, int NotSent, IReadOnlyList<string> Skipped);

public sealed record CouponCodeDto(string Code);

public sealed record CustomerOrderDto(int OrderId, DateTime DateAdded, decimal Total, string PaymentMethod, string Status, string Products);

/// <summary>Замовлення з днем народження (сирі дані для списку іменинників).</summary>
public sealed record BirthdayOrder(
    string Phone, string FirstName, string LastName, string Email, string ShippingCity, string ShippingMethod, string Instagram,
    int Day, int Month, DateTime DateAdded);

public sealed record CouponDraft(string Code, string Name, decimal DiscountPercent, DateOnly Start, DateOnly End, DateTime CreatedAt);

/// <summary>Що вивантажувати: поле, джерела й період (за датою додавання, включно).</summary>
public sealed record ContactExportRequest(ContactField Field, ContactSource Sources, DateOnly? From = null, DateOnly? To = null);
