using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

using NewErp.Application.Payments;
using NewErp.Domain.Common;

namespace NewErp.Infrastructure.Payments;

/// <summary>Налаштування виписки ПриватБанку (секція PrivatBank). id і token рахунків — лише в dotnet user-secrets.</summary>
public sealed class PrivatBankOptions
{
    public const string SectionName = "PrivatBank";

    public string BaseUrl { get; set; } = "https://acp.privatbank.ua";

    public List<PrivatBankAccountOptions> Accounts { get; set; } = [];

    public bool IsConfigured => Accounts.Any(a => a.Id != "" && a.Token != "");
}

/// <summary>Підключення Автоклієнта в Приват24 для бізнесу: id і token (їх видає банк для кожного клієнта API).</summary>
public sealed class PrivatBankAccountOptions
{
    public string Id { get; set; } = "";

    public string Token { get; set; } = "";
}

/// <summary>
/// Виписка через Автоклієнт ПриватБанку (api/proxy/transactions), як у старого сайту (checkBusinessCards).
/// Формат відповіді відновлено з коду старого сайту; на справжньому ключі ще не перевірено.
/// </summary>
public sealed class PrivatBankClient(HttpClient http, PrivatBankOptions options) : IBankStatementClient
{
    private static readonly string[] DateFormats = ["dd.MM.yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm"];
    private static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss", "H:mm"];

    public bool IsConfigured => options.IsConfigured;

    public async Task<IReadOnlyList<BankTransaction>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = new List<BankTransaction>();
        foreach (var account in options.Accounts.Where(a => a.Id != "" && a.Token != ""))
        {
            var url = $"{options.BaseUrl.TrimEnd('/')}/api/proxy/transactions?startDate={from:dd-MM-yyyy}&endDate={to:dd-MM-yyyy}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("id", account.Id);
            request.Headers.Add("token", account.Token);
            using var response = await http.SendAsync(request, ct);
            var text = Decode(await response.Content.ReadAsByteArrayAsync(ct));
            if (!response.IsSuccessStatusCode)
                throw new DomainException($"ПриватБанк відмовив ({(int)response.StatusCode}): {Trim(text)}");
            result.AddRange(Parse(text));
        }
        return result;
    }

    /// <summary>
    /// {"StatementsResponse": {"statements": [{"рахунок": [{"будь-що": {поля транзакції}}]}]}}.
    /// Поля: ID, TRANTYPE (C — надходження, D — списання), BPL_SUM_E, BPL_DAT_KL, BPL_TIM_P, BPL_OSND (призначення),
    /// BPL_A_NAM, BPL_A_ACC, BPL_A_MFO, BPL_A_CRF (платник), BPL_C_BAL (залишок).
    /// </summary>
    public static IReadOnlyList<BankTransaction> Parse(string json)
    {
        var root = JsonNode.Parse(json);
        if (root?["StatementsResponse"]?["statements"] is not JsonArray statements)
            throw new DomainException($"ПриватБанк повернув невідому відповідь: {Trim(json)}");

        var result = new List<BankTransaction>();
        foreach (var statement in statements.OfType<JsonObject>())
            foreach (var (account, payments) in statement)
                foreach (var wrapper in (payments as JsonArray ?? []).OfType<JsonObject>())
                {
                    // Кожна транзакція загорнута в об'єкт з одним полем (старий сайт брав current()).
                    if (wrapper.FirstOrDefault().Value is not JsonObject p)
                        continue;
                    result.Add(new BankTransaction(
                        Str(p["ID"]),
                        account,
                        ParseDate(Str(p["BPL_DAT_KL"])),
                        ParseTime(Str(p["BPL_TIM_P"])),
                        Money(Str(p["BPL_SUM_E"])) ?? 0,
                        Money(Str(p["BPL_C_BAL"])),
                        Str(p["TRANTYPE"]) != "D",
                        Str(p["BPL_OSND"]),
                        Str(p["BPL_A_NAM"]),
                        Str(p["BPL_A_ACC"]),
                        Str(p["BPL_A_MFO"]),
                        Str(p["BPL_A_CRF"])));
                }
        return result;
    }

    /// <summary>Старий сайт перекодовував відповідь з windows-1251. Якщо це вже коректний UTF-8, лишаємо як є.</summary>
    private static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1251).GetString(bytes);
        }
    }

    private static DateOnly ParseDate(string text) =>
        DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DateOnly.FromDateTime(d)
            : throw new DomainException($"ПриватБанк повернув незрозумілу дату: «{text}».");

    private static TimeOnly ParseTime(string text) =>
        TimeOnly.TryParseExact(text, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : TimeOnly.MinValue;

    private static decimal? Money(string text) =>
        decimal.TryParse(text.Replace(',', '.').Replace(" ", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static string Str(JsonNode? node) => node is JsonValue v ? v.ToString().Trim() : "";

    private static string Trim(string text) => text.Length > 300 ? text[..300] + "…" : text;
}

/// <summary>Без ключів банку: нічого не запитує (IsConfigured = false).</summary>
public sealed class StubBankStatementClient : IBankStatementClient
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<BankTransaction>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BankTransaction>>([]);
}
