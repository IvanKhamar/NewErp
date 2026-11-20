using NewErp.Domain.Common;

namespace NewErp.Application.Catalog;

/// <summary>Межі, які Python перевіряє моделлю запиту (довжини колонок, діапазони чисел, розміри списків); тут — 400.</summary>
internal static class RequestLimits
{
    public static void Length(string what, string? value, int max)
    {
        if (value is not null && value.Length > max)
            throw new DomainException($"{what}: не довше за {max} символів.");
    }

    public static void Range(string what, decimal value, decimal min, decimal max)
    {
        if (value < min || value > max)
            throw new DomainException($"{what}: від {min} до {max}.");
    }

    public static void Count<T>(string what, IReadOnlyCollection<T>? items, int max)
    {
        if (items is not null && items.Count > max)
            throw new DomainException($"Не більше {max} {what}.");
    }
}
