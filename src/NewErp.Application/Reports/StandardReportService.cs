using NewErp.Domain.Common;
using NewErp.Domain.Reports;

namespace NewErp.Application.Reports;

/// <summary>Один вигляд для всіх стандартних звітів: заголовок, колонки, рядки. Cut — рядків більше, ніж показано.</summary>
public sealed record StandardReportDto(
    string Kind, string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, DateOnly DateFrom, DateOnly DateTo, bool Cut);

public interface IStandardReportStore
{
    /// <summary>Рядки звіту за дні [from; toExclusive), не більше limit.</summary>
    Task<IReadOnlyList<IReadOnlyList<object?>>> RowsAsync(string kind, DateTime from, DateTime toExclusive, ReportGrouping grouping, int limit, CancellationToken ct);
}

/// <summary>
/// Стандартні звіти OpenCart, яких ще не було. Звіт про маркетинг — це сторінка кодів відстеження, а звіти «на сайті», перегляди,
/// бонуси й податки дає <see cref="StoreReportService"/>. Замовлення враховуються оформлені й не скасовані, як в інших звітах.
/// </summary>
public sealed class StandardReportService(IStandardReportStore store, TimeProvider clock)
{
    public const int MaxDays = 366;
    public const int MaxRows = 500;
    private const int DefaultDays = 29;

    public static readonly IReadOnlyDictionary<string, (string Title, string[] Columns)> Kinds = new Dictionary<string, (string, string[])>
    {
        ["sales-orders"] = ("Продажі за період", ["Період", "Замовлень", "Товарів, шт.", "Сума, грн"]),
        ["sales-returns"] = ("Повернення за період", ["Період", "Повернень"]),
        ["sales-shipping"] = ("Доставка за період", ["Період", "Доставка", "Замовлень", "Сума, грн"]),
        ["sales-coupons"] = ("Купони", ["Купон", "Код", "Замовлень", "Знижка, грн"]),
        ["products-purchased"] = ("Куплені товари", ["Товар", "Модель", "Кількість", "Сума, грн"]),
        ["customer-orders"] = ("Замовлення покупців", ["Покупець", "E-mail", "Замовлень", "Товарів, шт.", "Сума, грн"]),
        ["customer-transactions"] = ("Баланс покупців", ["Покупець", "E-mail", "Сума, грн"]),
        ["customer-activity"] = ("Дії покупців", ["Коли", "Покупець", "Дія", "IP"]),
        ["customer-searches"] = ("Пошук на вітрині", ["Запит", "Пошуків", "Знайдено товарів", "Востаннє"]),
        ["statistics"] = ("Лічильники OpenCart", ["Що", "Значення"]),
    };

    public async Task<StandardReportDto> ReportAsync(string kind, DateOnly? from, DateOnly? to, string? group, CancellationToken ct)
    {
        if (!Kinds.TryGetValue(kind, out var report))
            throw new DomainException($"Звіту «{kind}» немає.");
        var grouping = group switch
        {
            null or "" or "day" => ReportGrouping.Day,
            "week" => ReportGrouping.Week,
            "month" => ReportGrouping.Month,
            _ => throw new DomainException("Групувати можна за day, week або month."),
        };
        var end = to ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var start = from ?? end.AddDays(-DefaultDays);
        if (start > end)
            throw new DomainException("Початок періоду пізніше за кінець.");
        if (end.DayNumber - start.DayNumber >= MaxDays)
            throw new DomainException($"Період — не довше {MaxDays} днів.");

        var rows = await store.RowsAsync(kind, start.ToDateTime(TimeOnly.MinValue), end.AddDays(1).ToDateTime(TimeOnly.MinValue), grouping, MaxRows + 1, ct);
        return new StandardReportDto(
            kind, report.Title, report.Columns,
            rows.Take(MaxRows).Select(row => (IReadOnlyList<object?>)row.Select(Cell).ToList()).ToList(),
            start, end, rows.Count > MaxRows);
    }

    private static object? Cell(object? value) => value switch
    {
        decimal money => (double)money, // 1500.0000 з бази в JSON стає 1500
        null => "",
        _ => value,
    };
}
