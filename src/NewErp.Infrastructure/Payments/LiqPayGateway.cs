using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using NewErp.Application.Payments;
using NewErp.Domain.Common;

namespace NewErp.Infrastructure.Payments;

/// <summary>Налаштування LiqPay (секція LiqPay). PublicKey і PrivateKey — лише в dotnet user-secrets.</summary>
public sealed class LiqPayOptions
{
    public const string SectionName = "LiqPay";

    public string BaseUrl { get; set; } = "https://www.liqpay.ua";

    public string PublicKey { get; set; } = "";

    public string PrivateKey { get; set; } = "";

    public bool IsConfigured => PublicKey != "" && PrivateKey != "";
}

/// <summary>
/// Справжній LiqPay. Кожен запит — це JSON у base64 (data) і підпис base64(sha1(private + data + private)),
/// як у PHP SDK, яким користувався старий сайт. Посилання на оплату будуємо локально, без запиту до LiqPay.
/// </summary>
public sealed class LiqPayGateway(HttpClient http, LiqPayOptions options) : ILiqPayGateway
{
    public bool IsTest => false;

    public string CheckoutUrl(LiqPayCheckout checkout) => BuildCheckoutUrl(options, checkout);

    public async Task<LiqPayStatusReply> StatusAsync(string orderId, CancellationToken ct)
    {
        var (data, signature) = Sign(options, new JsonObject
        {
            ["action"] = "status",
            ["version"] = "3",
            ["public_key"] = options.PublicKey,
            ["order_id"] = orderId,
        });
        using var response = await http.PostAsync($"{options.BaseUrl.TrimEnd('/')}/api/request",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = data, ["signature"] = signature }), ct);
        if (!response.IsSuccessStatusCode)
            throw new DomainException($"LiqPay недоступний: {(int)response.StatusCode}.");
        return ParseStatus(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Посилання на сторінку оплати LiqPay (checkout) з параметрами, як у формі payment.php старого сайту.</summary>
    public static string BuildCheckoutUrl(LiqPayOptions options, LiqPayCheckout checkout)
    {
        var (data, signature) = Sign(options, new JsonObject
        {
            ["public_key"] = options.PublicKey,
            ["version"] = "3",
            ["action"] = "pay",
            ["amount"] = checkout.Amount,
            ["currency"] = checkout.Currency,
            ["description"] = checkout.Description,
            ["order_id"] = checkout.OrderId,
            ["language"] = "uk",
        });
        return $"{options.BaseUrl.TrimEnd('/')}/api/3/checkout?data={Uri.EscapeDataString(data)}&signature={Uri.EscapeDataString(signature)}";
    }

    public static (string Data, string Signature) Sign(LiqPayOptions options, JsonObject parameters)
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(parameters.ToJsonString()));
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(options.PrivateKey + data + options.PrivateKey));
        return (data, Convert.ToBase64String(hash));
    }

    public static LiqPayStatusReply ParseStatus(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            throw new DomainException("LiqPay повернув незрозумілу відповідь.");
        }
        // end_date — час завершення платежу в мілісекундах Unix.
        var endDate = Number(root?["end_date"]) is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime : (DateTime?)null;
        return new LiqPayStatusReply(Text(root?["status"]), Text(root?["err_code"]), Number(root?["amount"]), endDate);
    }

    private static string? Text(JsonNode? node) => node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;

    private static decimal? Number(JsonNode? node) => node?.GetValueKind() switch
    {
        JsonValueKind.Number => node.GetValue<decimal>(),
        JsonValueKind.String when decimal.TryParse(node.GetValue<string>(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    };
}

/// <summary>
/// Заглушка без ключів: будує посилання того ж формату з тестовим ключем (LiqPay його не прийме)
/// і завжди відповідає, що оплати ще немає. До LiqPay не звертається.
/// </summary>
public sealed class StubLiqPayGateway : ILiqPayGateway
{
    private static readonly LiqPayOptions TestOptions = new() { PublicKey = "sandbox_newerp_test", PrivateKey = "sandbox_newerp_test" };

    public bool IsTest => true;

    public string CheckoutUrl(LiqPayCheckout checkout) => LiqPayGateway.BuildCheckoutUrl(TestOptions, checkout);

    public Task<LiqPayStatusReply> StatusAsync(string orderId, CancellationToken ct) =>
        Task.FromResult(new LiqPayStatusReply(null, "payment_not_found", null, null));
}
