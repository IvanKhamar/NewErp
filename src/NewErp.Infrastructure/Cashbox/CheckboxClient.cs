using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using NewErp.Application.Cashbox;
using NewErp.Domain.Cashbox;
using NewErp.Domain.Common;

namespace NewErp.Infrastructure.Cashbox;

/// <summary>Налаштування Checkbox (секція Checkbox). Логіни, паролі й ключі касирів — лише в dotnet user-secrets.</summary>
public sealed class CheckboxOptions
{
    public const string SectionName = "Checkbox";

    public string BaseUrl { get; set; } = "https://api.checkbox.in.ua";

    /// <summary>Заголовок X-Client-Name: Checkbox просить так позначати інтеграцію.</summary>
    public string ClientName { get; set; } = "NewErp";

    public string ClientVersion { get; set; } = "1.0";

    /// <summary>
    /// Лише для локальної розробки: без ключів заглушка «фіскалізує» тестові чеки, щоб пройти весь шлях в інтерфейсі.
    /// Якщо задано ключі хоч одного касира, ігнорується.
    /// </summary>
    public bool FakeReceipts { get; set; }

    public List<CheckboxCashierOptions> Cashiers { get; set; } = [];
}

/// <summary>Ключі касира: CashierId — id з dd_cashbox_user, LicenseKey — ключ ліцензії каси (X-License-Key).</summary>
public sealed class CheckboxCashierOptions
{
    public int CashierId { get; set; }

    public string Login { get; set; } = "";

    public string Password { get; set; } = "";

    public string LicenseKey { get; set; } = "";
}

/// <summary>
/// Клієнт REST API Checkbox (https://api.checkbox.in.ua). Токен касира береться входом (cashier/signin) і тримається
/// в пам'яті; коли він спливає (401), клієнт входить ще раз. Старий сайт натомість зберігав токен у базі.
/// </summary>
public sealed class CheckboxClient(HttpClient http, CheckboxOptions options, TimeSpan? closePollDelay = null) : ICheckboxClient
{
    /// <summary>Скільки разів після закриття зміни питати, чи готовий Z-звіт.</summary>
    public const int ClosePollAttempts = 5;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly ConcurrentDictionary<int, string> _tokens = new();
    private readonly TimeSpan _closePollDelay = closePollDelay ?? TimeSpan.FromSeconds(1);

    public bool IsFake => false;

    public bool CanUse(int cashierId) => Find(cashierId) is not null;

    public async Task<string> OpenShiftAsync(int cashierId, CancellationToken ct)
    {
        // Якщо зміна вже відкрита (відкрили на старому сайті чи в застосунку Checkbox), беремо її.
        var current = await SendAsync(cashierId, HttpMethod.Get, "/api/v1/cashier/shift", null, false, ct);
        if (current is JsonObject shift && Str(shift["id"]) is { Length: > 0 } openId && Str(shift["status"]) is not "CLOSED")
            return openId;

        var created = await SendAsync(cashierId, HttpMethod.Post, "/api/v1/shifts", null, true, ct);
        return Str(created?["id"]) is { Length: > 0 } id ? id : throw new DomainException("Checkbox не повернув id нової зміни.");
    }

    public async Task<CheckboxShiftState> GetShiftAsync(int cashierId, string shiftId, CancellationToken ct)
    {
        var shift = await SendAsync(cashierId, HttpMethod.Get, $"/api/v1/shifts/{Uri.EscapeDataString(shiftId)}", null, false, ct);
        return new CheckboxShiftState(shiftId, Str(shift?["status"]) == "CLOSED", Str(shift?["z_report"]?["id"]));
    }

    public async Task<string?> CloseShiftAsync(int cashierId, CancellationToken ct)
    {
        var shift = await SendAsync(cashierId, HttpMethod.Post, "/api/v1/shifts/close", null, false, ct);
        if (Str(shift?["z_report"]?["id"]) is { Length: > 0 } zReportId)
            return zReportId;

        // Закриття асинхронне: спершу статус CLOSING, Z-звіт з'являється за кілька секунд.
        if (Str(shift?["id"]) is not { Length: > 0 } shiftId)
            return null;
        for (var attempt = 0; attempt < ClosePollAttempts; attempt++)
        {
            await Task.Delay(_closePollDelay, ct);
            var state = await GetShiftAsync(cashierId, shiftId, ct);
            if (state.ZReportId is { Length: > 0 } id)
                return id;
        }
        return null;
    }

    public async Task<string> CreateXReportAsync(int cashierId, CancellationToken ct)
    {
        var report = await SendAsync(cashierId, HttpMethod.Post, "/api/v1/reports", null, false, ct);
        return Str(report?["id"]) is { Length: > 0 } id ? id : throw new DomainException("Checkbox не повернув id X-звіту.");
    }

    public async Task<string> GetReportTextAsync(int cashierId, string reportId, CancellationToken ct)
    {
        using var response = await SendRawAsync(cashierId, HttpMethod.Get, $"/api/v1/reports/{Uri.EscapeDataString(reportId)}/text", null, false, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> SellAsync(int cashierId, FiscalReceipt receipt, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["cashier_name"] = receipt.CashierName,
            ["goods"] = new JsonArray(receipt.Goods.Select(g => (JsonNode)new JsonObject
            {
                ["good"] = new JsonObject { ["code"] = g.Code, ["name"] = g.Name, ["price"] = g.PriceKopecks },
                ["quantity"] = g.QuantityThousandths,
            }).ToArray()),
            ["payments"] = new JsonArray(new JsonObject
            {
                ["type"] = receipt.Payment.Type == ReceiptPaymentType.Cash ? "CASH" : "CASHLESS",
                ["value"] = receipt.Payment.ValueKopecks,
                ["label"] = receipt.Payment.Label,
            }),
            ["footer"] = receipt.Footer,
        };
        if (receipt.DiscountKopecks > 0)
            body["discounts"] = new JsonArray(new JsonObject { ["type"] = "DISCOUNT", ["mode"] = "VALUE", ["value"] = receipt.DiscountKopecks });
        if (receipt.Email is not null)
            body["delivery"] = new JsonObject { ["email"] = receipt.Email };

        var created = await SendAsync(cashierId, HttpMethod.Post, "/api/v1/receipts/sell", body, false, ct);
        return Str(created?["id"]) is { Length: > 0 } id ? id : throw new DomainException("Checkbox не повернув id чека.");
    }

    public string? ReceiptUrl(string receiptId) => $"{options.BaseUrl.TrimEnd('/')}/api/v1/receipts/{Uri.EscapeDataString(receiptId)}/html";

    private CheckboxCashierOptions? Find(int cashierId) =>
        options.Cashiers.FirstOrDefault(c => c.CashierId == cashierId && c.Login != "" && c.Password != "");

    private async Task<JsonNode?> SendAsync(int cashierId, HttpMethod method, string path, JsonNode? body, bool withLicense, CancellationToken ct)
    {
        using var response = await SendRawAsync(cashierId, method, path, body, withLicense, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }

    /// <summary>Запит від імені касира. На 401 входить ще раз (токен міг спливти) і повторює запит один раз.</summary>
    private async Task<HttpResponseMessage> SendRawAsync(int cashierId, HttpMethod method, string path, JsonNode? body, bool withLicense, CancellationToken ct)
    {
        var cashier = Find(cashierId) ?? throw new DomainException($"Для касира {cashierId} не задано ключі Checkbox.");
        for (var attempt = 0; ; attempt++)
        {
            var token = _tokens.TryGetValue(cashierId, out var cached) ? cached : await SignInAsync(cashier, ct);
            var request = Request(method, path, body);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (withLicense)
                request.Headers.Add("X-License-Key", cashier.LicenseKey);

            var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                response.Dispose();
                _tokens.TryRemove(cashierId, out _);
                continue;
            }
            await EnsureSuccessAsync(response, ct);
            return response;
        }
    }

    private async Task<string> SignInAsync(CheckboxCashierOptions cashier, CancellationToken ct)
    {
        using var response = await http.SendAsync(
            Request(HttpMethod.Post, "/api/v1/cashier/signin", new JsonObject { ["login"] = cashier.Login, ["password"] = cashier.Password }), ct);
        await EnsureSuccessAsync(response, ct);
        var token = Str(JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))?["access_token"])
            ?? throw new DomainException("Checkbox не повернув токен касира.");
        _tokens[cashier.CashierId] = token;
        return token;
    }

    private HttpRequestMessage Request(HttpMethod method, string path, JsonNode? body)
    {
        var request = new HttpRequestMessage(method, options.BaseUrl.TrimEnd('/') + path);
        request.Headers.Add("X-Client-Name", options.ClientName);
        request.Headers.Add("X-Client-Version", options.ClientVersion);
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(Json), Encoding.UTF8, "application/json");
        return request;
    }

    /// <summary>Відмову Checkbox ({"message": "..."}) показуємо як є: там зрозуміло, що не так (зміну не відкрито, сума не сходиться).</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var text = await response.Content.ReadAsStringAsync(ct);
        string? message = null;
        try
        {
            message = Str(JsonNode.Parse(text)?["message"]);
        }
        catch (JsonException)
        {
        }
        response.Dispose();
        throw new DomainException($"Checkbox відмовив ({(int)response.StatusCode}): {message ?? text}");
    }

    private static string? Str(JsonNode? node) =>
        node is JsonValue value ? (value.TryGetValue<string>(out var s) ? s : value.ToString()) : null;
}

/// <summary>
/// Заглушка без ключів. З Checkbox:FakeReceipts «відкриває» зміни й «фіскалізує» чеки в пам'яті, щоб пройти весь шлях
/// локально; без нього нічого не вміє (CanUse = false), і NewErp пояснює, що ключів не задано.
/// </summary>
public sealed class StubCheckboxClient(bool fakeReceipts = false) : ICheckboxClient
{
    private readonly ConcurrentDictionary<int, string> _openShifts = new();

    public bool IsFake => true;

    public bool CanUse(int cashierId) => fakeReceipts;

    public Task<string> OpenShiftAsync(int cashierId, CancellationToken ct) =>
        Task.FromResult(_openShifts.GetOrAdd(cashierId, _ => $"test-shift-{Guid.NewGuid():N}"));

    public Task<CheckboxShiftState> GetShiftAsync(int cashierId, string shiftId, CancellationToken ct) =>
        Task.FromResult(new CheckboxShiftState(shiftId, false, null));

    public Task<string?> CloseShiftAsync(int cashierId, CancellationToken ct)
    {
        _openShifts.TryRemove(cashierId, out _);
        return Task.FromResult<string?>($"test-z-{Guid.NewGuid():N}");
    }

    public Task<string> CreateXReportAsync(int cashierId, CancellationToken ct) => Task.FromResult($"test-x-{Guid.NewGuid():N}");

    public Task<string> GetReportTextAsync(int cashierId, string reportId, CancellationToken ct) =>
        Task.FromResult($"ТЕСТОВИЙ ЗВІТ (заглушка Checkbox, не фіскальний)\nЗвіт: {reportId}");

    public Task<string> SellAsync(int cashierId, FiscalReceipt receipt, CancellationToken ct) =>
        Task.FromResult($"test-{Guid.NewGuid():N}");

    public string? ReceiptUrl(string receiptId) => null;
}
