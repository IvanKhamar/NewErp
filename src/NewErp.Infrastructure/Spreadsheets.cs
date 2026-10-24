using ClosedXML.Excel;

using NewErp.Domain.Common;

namespace NewErp.Infrastructure;

/// <summary>Спільне читання файлів .xlsx через ClosedXML.</summary>
internal static class Spreadsheets
{
    /// <summary>Рядки першого аркуша як текст; DomainException, якщо це не .xlsx.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ReadRows(Stream file)
    {
        XLWorkbook book;
        try
        {
            book = new XLWorkbook(file);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new DomainException("Не вдалося прочитати файл. Потрібен файл Excel (.xlsx).");
        }

        using (book)
        {
            var sheet = book.Worksheets.First();
            var used = sheet.RangeUsed();
            if (used is null)
                return [];
            var lastColumn = used.LastColumn().ColumnNumber();
            return sheet.Rows(1, used.LastRow().RowNumber())
                .Select(row => (IReadOnlyList<string>)Enumerable.Range(1, lastColumn).Select(c => row.Cell(c).GetFormattedString()).ToList())
                .ToList();
        }
    }

    public static byte[] Save(XLWorkbook book)
    {
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }
}
