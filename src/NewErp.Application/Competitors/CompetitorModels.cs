namespace NewErp.Application.Competitors;

/// <summary>Розклад перевірки цін (розділ Competitors).</summary>
public sealed class CompetitorOptions
{
    public const string SectionName = "Competitors";

    /// <summary>О котрій щодня читати ціни, «HH:mm» за часом сервера; «-» — не перевіряти самому.</summary>
    public string CheckTime { get; set; } = "07:00";
}

/// <summary>Конкурент і скільки сторінок наших товарів у нього стежимо.</summary>
public sealed record CompetitorDto(int Id, string Name, string SiteUrl, string PriceRegex, bool IsActive, int Links);

/// <summary>PriceRegex — необов'язковий, перша група — ціна; пробується перед звичною розміткою.</summary>
public sealed record SaveCompetitorRequest(string? Name, string? SiteUrl, string? PriceRegex = null, bool IsActive = true);

/// <summary>Сторінка нашого товару в конкурента з останньою ціною; LastError порожній, якщо остання перевірка вдалася.</summary>
public sealed record CompetitorLinkDto(
    int Id, int CompetitorId, string CompetitorName, int ProductId, string ProductName, string Url,
    decimal? LastPrice, DateTime? LastCheckedAt, string LastError);

public sealed record AddCompetitorLinkRequest(int CompetitorId, int ProductId, string? Url);

/// <summary>Ціна, вписана вручну, для сайту, який не дає її прочитати.</summary>
public sealed record CompetitorPriceRequest(decimal Price);

/// <summary>Source — auto (зі сторінки) чи manual (вписано).</summary>
public sealed record CompetitorPriceDto(decimal Price, string Source, DateTime CheckedAt);

/// <summary>Перевірено сторінок, у скількох ціна змінилася, скільки не вдалося (чому — в LastError).</summary>
public sealed record CompetitorCheckResult(int Checked, int Changed, int Failed);

public sealed record CompetitorColumn(int Id, string Name);

/// <summary>
/// Рядок порівняння: Prices — за стовпчиками Competitors (null — немає сторінки чи ціни). DifferencePercent — наскільки
/// наша ціна вища (+) чи нижча (−) за найнижчу ціну конкурентів.
/// </summary>
public sealed record CompetitorReportRow(
    int ProductId, string ProductName, decimal OurPrice, IReadOnlyList<decimal?> Prices, decimal? Lowest, decimal? DifferencePercent);

/// <summary>Наша ціна проти останніх цін активних конкурентів, рядок на кожен товар зі сторінками.</summary>
public sealed record CompetitorReportDto(IReadOnlyList<CompetitorColumn> Competitors, IReadOnlyList<CompetitorReportRow> Rows);

/// <summary>Сторінку не вдалося завантажити; Message — пояснення для людини, воно йде в LastError.</summary>
public sealed class PageFetchException(string message) : Exception(message);

/// <summary>Завантажує сторінку конкурента. Переадресації — лише в межах сайту siteHost.</summary>
public interface IPageFetcher
{
    /// <exception cref="PageFetchException">Сайт недоступний, відповів помилкою чи веде деінде.</exception>
    Task<string> FetchAsync(Uri page, string siteHost, CancellationToken ct);
}
