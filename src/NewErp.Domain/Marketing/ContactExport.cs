using System.Net.Mail;
using System.Text;

using NewErp.Domain.Sms;

namespace NewErp.Domain.Marketing;

/// <summary>Яке поле вивантажувати: телефони чи e-mail (друге поле береться з останнього замовлення контакту).</summary>
public enum ContactField
{
    Phone,
    Email,
}

/// <summary>Звідки брати контакти, як таблиці вивантаження старого сайту.</summary>
[Flags]
public enum ContactSource
{
    None = 0,
    Orders = 1,

    /// <summary>Запитання про товар (лише телефони).</summary>
    Questions = 2,

    /// <summary>Підписка на розсилку (лише e-mail).</summary>
    Subscribers = 4,
}

/// <summary>Дані контакту з одного замовлення.</summary>
public sealed record OrderContact(string Phone, string Email, string FirstName, string LastName, string City, DateTime DateAdded);

/// <summary>Рядок вивантаження: ім'я, прізвище й місто — з останнього замовлення цього контакту.</summary>
public sealed record ContactRow(string FirstName, string LastName, string City, string Phone, string Email);

/// <summary>Вивантаження контактів клієнтів для розсилок, як marketing/export старого сайту.</summary>
public static class ContactExport
{
    /// <summary>Номер у вигляді 380XXXXXXXXX або null, якщо це не український мобільний.</summary>
    public static string? CleanPhone(string? phone)
    {
        try
        {
            return string.IsNullOrWhiteSpace(phone) ? null : SmsMessage.NormalizePhone(phone);
        }
        catch (Common.DomainException)
        {
            return null;
        }
    }

    public static string? CleanEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed || !trimmed.Contains('.', StringComparison.Ordinal))
            return null;
        return trimmed;
    }

    /// <summary>
    /// Унікальні контакти за вибраним полем, відсортовані; ім'я, прізвище, місто й друге поле — з найновішого замовлення
    /// з цим контактом (якщо такого немає, лишаються порожніми).
    /// </summary>
    public static IReadOnlyList<ContactRow> Build(ContactField field, IEnumerable<string> values, IEnumerable<OrderContact> orders)
    {
        Func<string?, string?> clean = field == ContactField.Phone ? CleanPhone : CleanEmail;
        var latest = new Dictionary<string, OrderContact>();
        foreach (var order in orders)
            if (clean(field == ContactField.Phone ? order.Phone : order.Email) is { } key
                && (!latest.TryGetValue(key, out var known) || order.DateAdded > known.DateAdded))
                latest[key] = order;

        return values.Select(clean).OfType<string>().Distinct().Order(StringComparer.Ordinal)
            .Select(value =>
            {
                var order = latest.GetValueOrDefault(value);
                var (first, last, city) = order is null ? ("", "", "") : (order.FirstName.Trim(), order.LastName.Trim(), order.City.Trim());
                return field == ContactField.Phone
                    ? new ContactRow(first, last, city, value, CleanEmail(order?.Email) ?? "")
                    : new ContactRow(first, last, city, CleanPhone(order?.Phone) ?? "", value);
            })
            .ToList();
    }

    /// <summary>CSV для Excel: UTF-8 з BOM, крапка з комою, стовпці як у старому файлі (головне поле — після міста).</summary>
    public static string ToCsv(ContactField field, IEnumerable<ContactRow> rows)
    {
        var csv = new StringBuilder();
        var (main, second) = field == ContactField.Phone ? ("Телефон", "E-mail") : ("E-mail", "Телефон");
        csv.Append("Ім'я;Прізвище;Місто;").Append(main).Append(';').Append(second).Append("\r\n");
        foreach (var row in rows)
        {
            var (mainValue, secondValue) = field == ContactField.Phone ? (row.Phone, row.Email) : (row.Email, row.Phone);
            csv.AppendJoin(';', new[] { row.FirstName, row.LastName, row.City, mainValue, secondValue }.Select(Escape)).Append("\r\n");
        }
        return csv.ToString();
    }

    // Лапки для полів з ; " або переносом, і апостроф перед =+-@, щоб Excel не виконав значення як формулу.
    private static string Escape(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0], StringComparison.Ordinal))
            value = "'" + value;
        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }
}
