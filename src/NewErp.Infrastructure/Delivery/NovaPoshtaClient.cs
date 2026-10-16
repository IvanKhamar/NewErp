using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

using NewErp.Application.Delivery;
using NewErp.Domain.Delivery;

namespace NewErp.Infrastructure.Delivery;

/// <summary>Налаштування з розділу "NovaPoshta". Ключ — лише в dotnet user-secrets, ніколи в appsettings.json.</summary>
public sealed class NovaPoshtaOptions
{
    public const string SectionName = "NovaPoshta";

    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.novaposhta.ua/v2.0/json/";
}

/// <summary>
/// Справжній клієнт API Нової Пошти (JSON API 2.0). Зараз використовує лише трекінг — він тільки читає.
/// Перевірено на тестових відповідях у форматі документації; на справжньому ключі ще не запускався.
/// </summary>
public sealed class NovaPoshtaClient(HttpClient http, NovaPoshtaOptions options) : INovaPoshtaClient
{
    private static readonly string[] DateFormats = ["dd-MM-yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy"];

    public bool IsConfigured => true;

    public async Task<IReadOnlyList<TrackingInfo>> TrackAsync(IReadOnlyList<TrackingRequest> documents, CancellationToken ct)
    {
        if (documents.Count == 0)
            return [];

        var request = new
        {
            apiKey = options.ApiKey,
            modelName = "TrackingDocument",
            calledMethod = "getStatusDocuments",
            methodProperties = new { Documents = documents.Select(d => new { DocumentNumber = d.Number, d.Phone }).ToList() },
        };
        // Без camelCase за замовчуванням: API Нової Пошти чекає імена як є (Documents, DocumentNumber, Phone).
        using var response = await http.PostAsJsonAsync(options.BaseUrl, request, JsonSerializerOptions.Default, ct);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!json.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
            throw new InvalidOperationException("Нова Пошта відмовила: " + Errors(json.RootElement));

        return json.RootElement.GetProperty("data").EnumerateArray().Select(Parse).ToList();
    }

    /// <summary>Розбирає один запис трекінгу. Поля приходять то рядками, то числами, а дати — у різних форматах.</summary>
    internal static TrackingInfo Parse(JsonElement item) => new(
        Text(item, "Number"),
        Text(item, "Status"),
        int.TryParse(Text(item, "StatusCode"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : 0,
        Text(item, "RecipientAddress"),
        Text(item, "Redelivery") is "1" or "true",
        Date(item, "DateCreated"),
        Date(item, "ScheduledDeliveryDate") is { } scheduled ? DateOnly.FromDateTime(scheduled) : null,
        Date(item, "DatePayedKeeping") is { } keeping ? DateOnly.FromDateTime(keeping) : null);

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "",
        } : "";

    private static DateTime? Date(JsonElement item, string name) =>
        DateTime.TryParseExact(Text(item, name), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static string Errors(JsonElement root) =>
        root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array
            ? string.Join("; ", errors.EnumerateArray().Select(e => e.GetString()))
            : "без пояснення";
}

/// <summary>Заглушка, коли ключа немає: нічого не запитує й нічого не повертає.</summary>
public sealed class StubNovaPoshtaClient : INovaPoshtaClient
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<TrackingInfo>> TrackAsync(IReadOnlyList<TrackingRequest> documents, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TrackingInfo>>([]);
}
