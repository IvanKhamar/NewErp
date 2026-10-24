namespace NewErp.Application.Stock;

/// <summary>Рядок аркуша інвентаризації: позиція й скільки її за обліком у вибраному місці.</summary>
public sealed record StocktakingSheetRow(int ItemId, string Name, string Category, string Unit, decimal Booked);

/// <summary>Booked — за обліком зараз; Difference = Actual − Booked (мінус — нестача).</summary>
public sealed record StocktakingLineDto(int ItemId, string Name, string Unit, decimal Booked, decimal Actual, decimal Difference);

/// <summary>Що змінить завантажений аркуш. Errors — рядки, які не вдалося прочитати (нічого не записано).</summary>
public sealed record StocktakingPreviewDto(IReadOnlyList<StocktakingLineDto> Lines, IReadOnlyList<string> Errors);

public sealed record StocktakingCountRequest(int ItemId, decimal Actual);

/// <summary>Пораховані кількості (не різниці). WarehouseId — склад, який рахували; null — основний.</summary>
public sealed record ApplyStocktakingRequest(IReadOnlyList<StocktakingCountRequest> Counts, string? Comment = null, int? WarehouseId = null);

/// <summary>Changed — скільки позицій змінилося; Value — сума різниць × ціна (мінус — нестача).</summary>
public sealed record StocktakingResult(int Counted, int Changed, decimal Value);

/// <summary>Файл Excel (.xlsx) інвентаризації.</summary>
public interface IStocktakingSheet
{
    byte[] Write(IReadOnlyList<string> header, IReadOnlyList<StocktakingSheetRow> rows);

    /// <summary>Рядки першого аркуша як текст; DomainException, якщо це не .xlsx.</summary>
    IReadOnlyList<IReadOnlyList<string>> Read(Stream file);
}
