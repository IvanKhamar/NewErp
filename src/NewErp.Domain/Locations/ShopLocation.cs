using NewErp.Domain.Common;

namespace NewErp.Domain.Locations;

/// <summary>Поля складу чи пункту видачі (dd_warehouse), уже обрізані від пробілів.</summary>
public sealed record ShopLocationData(string Name, string Description, string Contact, string Telephone, string City, string Address, string Type);

/// <summary>Рядок файлу імпорту: Id 0 — новий запис, інакше оновлення наявного.</summary>
public sealed record LocationImportRow(int RowNumber, int Id, ShopLocationData Data);

/// <summary>
/// Склад або пункт видачі магазину, як «Каталог → Склади» старої адмінки: назва, контакт, телефон, місто, адреса й тип.
/// </summary>
public static class ShopLocation
{
    /// <summary>Перевіряє поля. Назва обов'язкова, до 64 символів, як у формі старого сайту; решта — у межах колонок.</summary>
    public static ShopLocationData Validate(
        string? name, string? description, string? contact, string? telephone, string? city, string? address, string? type)
    {
        var data = new ShopLocationData(T(name), T(description), T(contact), T(telephone), T(city), T(address), T(type));
        if (data.Name.Length is < 1 or > 64)
            throw new DomainException("Назва має містити від 1 до 64 символів.");
        Max(data.Contact, 128, "Контакт");
        Max(data.Telephone, 255, "Телефон");
        Max(data.City, 128, "Місто");
        Max(data.Address, 255, "Адреса");
        Max(data.Type, 64, "Тип");
        Max(data.Description, 5000, "Опис");
        return data;
    }

    /// <summary>Колонки файлу Excel у тому ж порядку, що й у вивантаженні старого сайту.</summary>
    public static readonly IReadOnlyList<string> Columns = ["ID", "Назва", "Опис", "Контакт", "Телефон", "Місто", "Адреса", "Тип"];

    /// <summary>
    /// Розбирає рядки файлу (перший — заголовок, порожні пропускаються). Старий імпорт записував кожен рядок одразу
    /// і без перевірок, тож помилка посередині файлу лишала половину змін. Тут помилки збираються для всього файлу,
    /// і якщо є хоч одна, нічого не записується.
    /// </summary>
    public static (IReadOnlyList<LocationImportRow> Rows, IReadOnlyList<string> Errors) ParseImport(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var result = new List<LocationImportRow>();
        var errors = new List<string>();
        for (var i = 1; i < rows.Count; i++)
        {
            var cells = rows[i];
            string Cell(int index) => index < cells.Count ? cells[index] : "";
            if (Enumerable.Range(0, Columns.Count).All(c => string.IsNullOrWhiteSpace(Cell(c))))
                continue;

            var rowNumber = i + 1;
            var idText = Cell(0).Trim();
            if (idText.Length > 0 && (!int.TryParse(idText, out _) || int.Parse(idText) < 0))
            {
                errors.Add($"Рядок {rowNumber}: ID «{idText}» має бути числом або порожнім.");
                continue;
            }
            try
            {
                var data = Validate(Cell(1), Cell(2), Cell(3), Cell(4), Cell(5), Cell(6), Cell(7));
                result.Add(new LocationImportRow(rowNumber, idText.Length == 0 ? 0 : int.Parse(idText), data));
            }
            catch (DomainException e)
            {
                errors.Add($"Рядок {rowNumber}: {e.Message}");
            }
        }

        foreach (var duplicate in result.Where(r => r.Id > 0).GroupBy(r => r.Id).Where(g => g.Count() > 1))
            errors.Add($"ID {duplicate.Key} повторюється в рядках {string.Join(", ", duplicate.Select(r => r.RowNumber))}.");
        if (result.Count == 0 && errors.Count == 0)
            errors.Add("У файлі немає рядків для імпорту.");
        return (result, errors);
    }

    private static string T(string? value) => (value ?? "").Trim();

    private static void Max(string value, int max, string field)
    {
        if (value.Length > max)
            throw new DomainException($"{field} довший за {max} символів.");
    }
}
