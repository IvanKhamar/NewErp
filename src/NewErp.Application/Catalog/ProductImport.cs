using System.Globalization;
using System.Text;

using NewErp.Domain.Common;

namespace NewErp.Application.Catalog;

// Імпорт товарів із CSV (Python first, пункт 44) з переглядом перед збереженням. Старий імпортер будував SQL зі значень
// файлу й завантажував картинки з будь-якої адреси; тут — лише оновлення наявних товарів, без залишків і без картинок.

/// <summary>Рядок файлу: що зміниться (колонка → «старе → нове») або чому рядок пропущено.</summary>
public sealed record ImportRowDto(int Line, int? ProductId, string Model, IReadOnlyDictionary<string, string> Changes, string? Error = null);

/// <summary>Rows — лише рядки зі змінами чи помилкою; незмінені тільки лічимо.</summary>
public sealed record ImportPreview(IReadOnlyList<ImportRowDto> Rows, int Unchanged, int Errors);

public sealed record ImportResult(int Updated, int Errors);

/// <summary>Товар, знайдений за product_id чи model, з поточним значенням кожної колонки імпорту, крім ключів.</summary>
public sealed record ImportedProduct(int ProductId, string Model, IReadOnlyDictionary<string, object> Values);

public interface IProductImportStore
{
    Task<IReadOnlyList<ImportedProduct>> FindAsync(IReadOnlyCollection<int> productIds, IReadOnlyCollection<string> models, CancellationToken ct);

    /// <summary>Колонки dd_product і назва; лише передані. date_modified теж змінюється, тож відкрита раніше форма отримає 409.</summary>
    Task UpdateAsync(int productId, IReadOnlyDictionary<string, object> values, CancellationToken ct);
}

public sealed class ProductImportService(IProductImportStore store, IAuditLog audit)
{
    public const int MaxBytes = 1_000_000;
    public const int MaxRows = 5_000;

    private static readonly string[] KeyColumns = ["product_id", "model"];
    private static readonly string[] DecimalColumns = ["price", "weight", "length", "width", "height"];
    private static readonly string[] IntColumns = ["sort_order", "status"];
    private static readonly Dictionary<string, int> TextColumns = new() { ["sku"] = 64, ["name"] = 255 };
    private static readonly string[] ImportColumns = [.. KeyColumns, .. DecimalColumns, .. IntColumns, .. TextColumns.Keys];

    public async Task<ImportPreview> PreviewAsync(byte[] content, CancellationToken ct)
    {
        var rows = await PlanAsync(content, ct);
        var shown = rows.Where(r => r.Row.Changes.Count > 0 || r.Row.Error is not null).Select(r => r.Row).ToList();
        return new ImportPreview(shown, rows.Count - shown.Count, rows.Count(r => r.Row.Error is not null));
    }

    /// <summary>Пише лише рядки без помилки; решту звітує, як у перегляді.</summary>
    public async Task<ImportResult> ApplyAsync(byte[] content, int userId, CancellationToken ct)
    {
        var rows = await PlanAsync(content, ct);
        var updated = 0;
        foreach (var (row, values) in rows)
        {
            if (row.Error is not null || values.Count == 0 || row.ProductId is not { } productId)
                continue;
            await store.UpdateAsync(productId, values, ct);
            var changes = string.Join(", ", row.Changes.Select(c => $"{c.Key}: {c.Value}"));
            audit.Record($"product imported: product {productId}, {changes}, by user {userId}");
            updated++;
        }
        return new ImportResult(updated, rows.Count(r => r.Row.Error is not null));
    }

    private async Task<List<(ImportRowDto Row, Dictionary<string, object> Values)>> PlanAsync(byte[] content, CancellationToken ct)
    {
        var (header, records) = Parse(content);
        var ids = records.Select(r => r.Cells.GetValueOrDefault("product_id", "")).Where(IsDigits).Select(int.Parse).ToList();
        var models = records.Select(r => r.Cells.GetValueOrDefault("model", "").Trim()).Where(m => m.Length > 0).ToList();
        var found = await store.FindAsync(ids, models, ct);
        var byId = found.ToDictionary(p => p.ProductId);
        var byModel = found.ToLookup(p => p.Model);

        var planned = new List<(ImportRowDto, Dictionary<string, object>)>();
        foreach (var (line, record) in records)
        {
            var keyId = record.GetValueOrDefault("product_id", "").Trim();
            var model = record.GetValueOrDefault("model", "").Trim();
            try
            {
                ImportedProduct product;
                if (keyId.Length > 0)
                {
                    if (!IsDigits(keyId) || !byId.TryGetValue(int.Parse(keyId, CultureInfo.InvariantCulture), out product!))
                        throw new DomainException($"товару №{keyId} немає");
                }
                else
                {
                    var matches = byModel[model].ToList();
                    if (matches.Count == 0)
                        throw new DomainException($"товару з моделлю «{model}» немає (імпорт лише оновлює товари)");
                    if (matches.Count > 1)
                        throw new DomainException($"модель «{model}» мають кілька товарів: вкажіть product_id");
                    product = matches[0];
                }
                var values = new Dictionary<string, object>();
                foreach (var column in header.Where(c => !KeyColumns.Contains(c)))
                {
                    if (Value(column, record[column]) is { } value && !Equals(value, product.Values[column]))
                        values[column] = value;
                }
                var changes = values.ToDictionary(v => v.Key, v => $"{Show(product.Values[v.Key])} → {Show(v.Value)}");
                planned.Add((new ImportRowDto(line, product.ProductId, product.Model, changes), values));
            }
            catch (DomainException error)
            {
                planned.Add((new ImportRowDto(line, null, model, new Dictionary<string, string>(), error.Message), []));
            }
        }
        return planned;
    }

    /// <summary>UTF-8 (з BOM чи без), «;» або «,» між колонками, перший рядок — назви колонок.</summary>
    public static (IReadOnlyList<string> Header, IReadOnlyList<(int Line, Dictionary<string, string> Cells)> Records) Parse(byte[] content)
    {
        if (content.Length > MaxBytes)
            throw new DomainException($"Файл більший за {MaxBytes / 1_000_000} МБ.");
        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            throw new DomainException("Файл має бути в кодуванні UTF-8.");
        }
        text = text.TrimStart('﻿');
        var first = text.Split('\n', 2)[0];
        var delimiter = first.Count(c => c == ';') >= first.Count(c => c == ',') ? ';' : ',';
        var lines = CsvLines(text, delimiter).ToList();
        if (lines.Count == 0)
            throw new DomainException("Файл порожній.");
        var header = lines[0].Cells.Select(c => c.Trim().ToLowerInvariant()).ToList();
        var unknown = header.Where(h => !ImportColumns.Contains(h)).ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Невідомі колонки: {string.Join(", ", unknown)}. Можна: {string.Join(", ", ImportColumns)}.");
        if (!KeyColumns.Any(header.Contains) || header.Distinct().Count() != header.Count)
            throw new DomainException("Потрібна колонка model або product_id, і кожна колонка лише раз.");
        var records = new List<(int, Dictionary<string, string>)>();
        foreach (var (line, cells) in lines.Skip(1))
        {
            if (cells.All(c => c.Trim().Length == 0))
                continue;
            if (records.Count >= MaxRows)
                throw new DomainException($"Більше за {MaxRows} рядків: розбийте файл.");
            records.Add((line, header.Select((h, i) => (h, i < cells.Count ? cells[i] : "")).ToDictionary(x => x.h, x => x.Item2)));
        }
        return (header, records);
    }

    /// <summary>Рядки CSV за RFC 4180: лапки навколо комірки, "" усередині — лапка, перенос рядка в лапках — частина комірки.</summary>
    private static IEnumerable<(int Line, List<string> Cells)> CsvLines(string text, char delimiter)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var line = 1;
        var start = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else { if (c == '\n') line++; cell.Append(c); }
                continue;
            }
            if (c == '"') quoted = true;
            else if (c == delimiter) { cells.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n')
            {
                cells.Add(cell.ToString());
                yield return (start, cells);
                cells = [];
                cell.Clear();
                start = ++line;
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            yield return (start, cells);
        }
    }

    /// <summary>Комірка в типі колонки; порожня — значення лишається (null).</summary>
    private static object? Value(string column, string cell)
    {
        var text = cell.Trim();
        if (text.Length == 0)
            return null;
        if (DecimalColumns.Contains(column))
        {
            if (!decimal.TryParse(text.Replace(',', '.').Replace(" ", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                throw new DomainException($"{column}: «{text}» не число");
            if (number < 0 || number > 10_000_000)
                throw new DomainException($"{column}: «{text}» поза межами");
            return number;
        }
        if (IntColumns.Contains(column))
        {
            if (!IsDigits(text.TrimStart('-')) || !int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                throw new DomainException($"{column}: «{text}» не ціле число");
            if (column == "status" && number is not (0 or 1))
                throw new DomainException("status: 1 — увімкнений, 0 — вимкнений");
            return number;
        }
        if (text.Length > TextColumns[column])
            throw new DomainException($"{column}: довше за {TextColumns[column]} символів");
        return text;
    }

    private static bool IsDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private static string Show(object value) => value switch
    {
        decimal d => d.ToString(CultureInfo.InvariantCulture), // як у Python: «1100.0000 → 1250.50»
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}
