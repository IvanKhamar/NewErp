using ClosedXML.Excel;

using NewErp.Application.Locations;
using NewErp.Domain.Common;

namespace NewErp.Infrastructure.Locations;

/// <summary>Файл .xlsx через бібліотеку ClosedXML (старий сайт писав XLSXWriter, а читав SimpleXLSX).</summary>
public sealed class LocationSpreadsheet : ILocationSpreadsheet
{
    public byte[] Write(IReadOnlyList<string> header, IReadOnlyList<LocationDto> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Склади");
        for (var c = 0; c < header.Count; c++)
            sheet.Cell(1, c + 1).Value = header[c];
        sheet.Row(1).Style.Font.Bold = true;
        for (var r = 0; r < rows.Count; r++)
        {
            var x = rows[r];
            object[] values = [x.Id, x.Name, x.Description, x.Contact, x.Telephone, x.City, x.Address, x.Type];
            for (var c = 0; c < values.Length; c++)
                // Текст записуємо як текст: телефон «0671234567» не має стати числом без нуля.
                sheet.Cell(r + 2, c + 1).Value = values[c] is int id ? id : (string)values[c];
        }
        sheet.Columns().AdjustToContents(1, 100);

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    public IReadOnlyList<IReadOnlyList<string>> Read(Stream file)
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
}
