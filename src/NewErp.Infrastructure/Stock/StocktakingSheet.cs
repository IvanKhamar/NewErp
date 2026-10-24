using ClosedXML.Excel;

using NewErp.Application.Stock;

namespace NewErp.Infrastructure.Stock;

/// <summary>Аркуш інвентаризації .xlsx: стовпчик «Фактично» лишається порожнім, щоб комірник уписав пораховане.</summary>
public sealed class StocktakingSheet : IStocktakingSheet
{
    public byte[] Write(IReadOnlyList<string> header, IReadOnlyList<StocktakingSheetRow> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Інвентаризація");
        for (var c = 0; c < header.Count; c++)
            sheet.Cell(1, c + 1).Value = header[c];
        sheet.Row(1).Style.Font.Bold = true;
        for (var r = 0; r < rows.Count; r++)
        {
            var x = rows[r];
            sheet.Cell(r + 2, 1).Value = x.ItemId;
            sheet.Cell(r + 2, 2).Value = x.Name;
            sheet.Cell(r + 2, 3).Value = x.Category;
            sheet.Cell(r + 2, 4).Value = x.Unit;
            sheet.Cell(r + 2, 5).Value = x.Booked;
        }
        sheet.Columns().AdjustToContents(1, 100);
        sheet.SheetView.FreezeRows(1);
        return Spreadsheets.Save(book);
    }

    public IReadOnlyList<IReadOnlyList<string>> Read(Stream file) => Spreadsheets.ReadRows(file);
}
