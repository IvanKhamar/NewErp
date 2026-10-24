using ClosedXML.Excel;

using NewErp.Application.Locations;

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
        return Spreadsheets.Save(book);
    }

    public IReadOnlyList<IReadOnlyList<string>> Read(Stream file) => Spreadsheets.ReadRows(file);
}
