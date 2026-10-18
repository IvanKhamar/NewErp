using NewErp.Domain.Common;

namespace NewErp.Domain.Marketing;

/// <summary>За який період показати іменинників (як фільтри сторінки «Дні народження» старого сайту).</summary>
public enum BirthdayPeriod
{
    Today,
    Tomorrow,

    /// <summary>Від сьогодні до сьомого дня включно.</summary>
    Week,

    /// <summary>Поточний календарний місяць.</summary>
    Month,

    All,
}

public static class Birthdays
{
    /// <summary>
    /// Найближча (сьогодні або пізніше) дата дня народження. 29 лютого в невисокосний рік — 28 лютого.
    /// Повертає null для неможливої дати (31 квітня тощо).
    /// </summary>
    public static DateOnly? Next(int day, int month, DateOnly today)
    {
        if (month is < 1 or > 12 || day < 1 || day > 31)
            return null;
        if (day > DateTime.DaysInMonth(2024, month)) // 2024 високосний: так 29.02 вважається можливою датою
            return null;

        var date = OnYear(day, month, today.Year);
        return date >= today ? date : OnYear(day, month, today.Year + 1);
    }

    /// <summary>
    /// Чи потрапляє день народження в період. На відміну від старого коду, «тиждень» містить і сьогоднішніх іменинників
    /// і переходить через Новий рік.
    /// </summary>
    public static bool InPeriod(int day, int month, BirthdayPeriod period, DateOnly today)
    {
        if (Next(day, month, today) is not { } next)
            return false;
        return period switch
        {
            BirthdayPeriod.Today => next == today,
            BirthdayPeriod.Tomorrow => next == today.AddDays(1),
            BirthdayPeriod.Week => next <= today.AddDays(7),
            BirthdayPeriod.Month => month == today.Month,
            _ => true,
        };
    }

    private static DateOnly OnYear(int day, int month, int year) => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}

/// <summary>Персональний купон на знижку до дня народження (рядок dd_coupon, як createCoupon старого сайту).</summary>
public static class BirthdayCoupon
{
    public const string Prefix = "DN";

    /// <summary>Код DN + 8 шістнадцяткових символів, як у старому generateCouponCode.</summary>
    public static string NewCode(Random random)
    {
        var bytes = new byte[4];
        random.NextBytes(bytes);
        return Prefix + Convert.ToHexString(bytes);
    }

    /// <summary>Код купона: латиниця й цифри, до 20 символів (довжина колонки dd_coupon.code).</summary>
    public static string RequireCode(string? code)
    {
        var trimmed = code?.Trim().ToUpperInvariant() ?? "";
        if (trimmed.Length is < 4 or > 20 || !trimmed.All(char.IsAsciiLetterOrDigit))
            throw new DomainException("Код купона — від 4 до 20 латинських літер і цифр.");
        return trimmed;
    }
}
