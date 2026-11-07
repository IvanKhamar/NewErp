namespace NewErp.Application;

/// <summary>
/// Годинник застосунку: «сьогодні» і місцевий час завжди київські, хоч би в якому поясі працював сервер.
/// Магазин працює за київським часом, а на сервері в UTC дата інша від 22:00 до 24:00 UTC (улітку від 21:00).
/// Те саме правило в Java (<c>BusinessTime</c>) і Python (<c>SystemClock</c>).
/// </summary>
public class BusinessClock : TimeProvider
{
    /// <summary>Часовий пояс бізнес-дати.</summary>
    public static TimeZoneInfo Zone { get; } = FindZone();

    public override TimeZoneInfo LocalTimeZone => Zone;

    // Windows може не знати нової назви Europe/Kyiv (2022 рік), а стару Europe/Kiev знають усюди.
    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
        }
    }
}
