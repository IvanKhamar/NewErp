using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using NewErp.Application.Delivery;
using NewErp.Domain.Common;
using NewErp.Domain.Delivery;

namespace NewErp.Infrastructure.Delivery;

/// <summary>
/// Налаштування розділу "NovaPostInternational". Ключ API — лише в user-secrets чи змінній середовища. Відправник — компанія
/// магазину, як у налаштуваннях «Міжнародна доставка» старого сайту.
/// </summary>
public sealed class NovaPostInternationalOptions
{
    public const string SectionName = "NovaPostInternational";
    public const string ProductionUrl = "https://api.novapost.com/v.1.0";
    public const string SandboxUrl = "https://api-stage.novapost.com/v.1.0";

    public string ApiKey { get; set; } = "";

    /// <summary>true — api-stage.novapost.com (тестові накладні), false — справжній API.</summary>
    public bool Sandbox { get; set; }

    public string? BaseUrl { get; set; }

    /// <summary>Лише для локальної розробки: без ключа заглушка «створює» тестові накладні (номер на TEST).</summary>
    public bool FakeDocuments { get; set; }

    public SenderOptions Sender { get; set; } = new();

    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey);

    public string Url => string.IsNullOrWhiteSpace(BaseUrl) ? (Sandbox ? SandboxUrl : ProductionUrl) : BaseUrl.Trim().TrimEnd('/');

    /// <summary>Компанія-відправник. З кодом ЄДРПОУ API ще вимагає адресу реєстрації — нею є ця адреса.</summary>
    public sealed class SenderOptions
    {
        public string CompanyTin { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>380XXXXXXXXX</summary>
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string City { get; set; } = "";
        public string Street { get; set; } = "";
        public string Building { get; set; } = "";
        public string Flat { get; set; } = "";
        public string PostCode { get; set; } = "";
    }
}

/// <summary>
/// REST API Nova Post International, як його викликав international_client.php старого сайту. Ключ API міняємо на JWT
/// (GET /clients/authorization), а JWT іде в заголовок Authorization як є, без "Bearer". JWT тримаємо й беремо заново один раз,
/// коли API відповідає 401 (він прострочився). Те саме, що HttpNovaPostInternationalClient у Java-версії.
/// </summary>
public sealed class NovaPostInternationalClient(HttpClient http, NovaPostInternationalOptions options) : INovaPostInternationalClient
{
    private const int HsCodeSuggestions = 10;
    private string? _jwt;

    public bool IsConfigured => true;

    public bool CanCreateDocuments => true;

    public async Task<IReadOnlyList<HsCodeDto>> SearchHsCodesAsync(string keyword, string countryCode, CancellationToken ct)
    {
        var url = $"{options.Url}/dictionary/classifier?country-code={Uri.EscapeDataString(countryCode)}&keyword={Uri.EscapeDataString(keyword)}"
            + $"&fuzzy=true&locale=uk&size={HsCodeSuggestions}";
        var root = await AuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
        var codes = new List<HsCodeDto>();
        foreach (var item in Array(root, "items"))
        {
            var local = Text(item?["product"], "currentLocal");
            var english = Text(item?["product"], "en");
            var subCategory = Text(item?["subCategory"], "currentLocal");
            var product = local == "" ? english : local;
            var label = subCategory == "" ? product : product == "" ? subCategory : $"{product} — {subCategory}";
            codes.Add(new HsCodeDto(Text(item, "hsCode"), label, english));
        }
        return codes;
    }

    public async Task<CreatedShipment> CreateAsync(InternationalShipment shipment, CancellationToken ct)
    {
        var payload = Payload(shipment);
        var created = await AuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Post, $"{options.Url}/shipments")
        {
            Content = JsonContent.Create(payload),
        }, ct);
        var number = Text(created, "number");
        if (number == "")
            throw new DomainException("Nova Post International не повернула номер накладної.");
        var id = Text(created, "id");
        var cost = created?["cost"] is JsonValue c && c.TryGetValue(out decimal value) ? value : shipment.DeclaredCost;
        return new CreatedShipment(id == "" ? number : id, number, cost);
    }

    public async Task DeleteAsync(string shipmentId, CancellationToken ct) =>
        await AuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Delete, $"{options.Url}/shipments/{Uri.EscapeDataString(shipmentId)}"), ct);

    /// <summary>Тіло запиту старого сайту, поле в поле.</summary>
    internal JsonObject Payload(InternationalShipment s)
    {
        var from = options.Sender;
        JsonObject SenderAddress() => new()
        {
            ["city"] = from.City, ["street"] = from.Street, ["postCode"] = from.PostCode, ["building"] = from.Building, ["flat"] = from.Flat,
        };
        var sender = new JsonObject
        {
            ["companyTin"] = from.CompanyTin, ["companyName"] = from.CompanyName, ["phone"] = new string(from.Phone.Where(char.IsDigit).ToArray()),
            ["email"] = from.Email, ["name"] = from.Name, ["countryCode"] = "UA", ["addressParts"] = SenderAddress(),
        };
        // Підтримка Nova Post підтвердила старому сайту: відправнику-юрособі потрібен ще registrationAddressParts.
        if (from.CompanyTin != "")
        {
            var registration = SenderAddress();
            registration["region"] = "";
            registration["note"] = "";
            sender["registrationAddressParts"] = registration;
        }

        var to = s.Recipient;
        var recipient = new JsonObject
        {
            ["phone"] = to.Phone, ["email"] = to.Email, ["name"] = $"{to.FirstName} {to.LastName}".Trim(), ["countryCode"] = to.CountryCode,
            ["addressParts"] = new JsonObject
            {
                ["city"] = to.City, ["region"] = to.Region, ["street"] = to.Street, ["postCode"] = to.PostCode, ["building"] = to.Building,
                ["flat"] = to.Flat,
            },
        };
        var items = new JsonArray(s.Items.Select(i => (JsonNode)new JsonObject
        {
            ["hsCode"] = i.HsCode, ["name"] = i.Name, ["nameEng"] = i.NameEng, ["actualWeight"] = i.WeightGrams, ["measurementCode"] = "pieces",
            ["amount"] = i.Amount, ["cost"] = i.Cost,
        }).ToArray());
        var parcels = new JsonArray(s.Parcels().Select(p => (JsonNode)new JsonObject
        {
            ["cargoCategory"] = s.Terms.CargoCategory, ["parcelDescription"] = s.Terms.ParcelDescription, ["insuranceCost"] = p.InsuranceCost,
            ["rowNumber"] = p.RowNumber, ["width"] = p.WidthMm, ["length"] = p.LengthMm, ["height"] = p.HeightMm, ["actualWeight"] = p.WeightGrams,
        }).ToArray());

        var payload = new JsonObject
        {
            ["status"] = "ReadyToShip",
            ["clientOrder"] = s.OrderId.ToString(),
            ["note"] = $"Vidro order #{s.OrderId}",
            ["payerType"] = "Sender",
            ["invoice"] = new JsonObject
            {
                ["incoterm"] = s.Terms.Incoterm, ["exportReason"] = s.Terms.ExportReason, ["cost"] = s.DeclaredCost, ["currency"] = s.Currency,
                ["payerFeesCustoms"] = s.Terms.PayerFeesCustoms, ["items"] = items,
            },
            ["parcels"] = parcels,
            ["sender"] = sender,
            ["recipient"] = recipient,
        };
        if (s.Terms.DeliveryType is { } deliveryType)
            payload["deliveryType"] = deliveryType;
        return payload;
    }

    /// <summary>Виклик із JWT; на 401 бере новий і пробує ще раз. Інші помилки API — зрозумілий DomainException.</summary>
    private async Task<JsonNode?> AuthorizedAsync(Func<HttpRequestMessage> request, CancellationToken ct)
    {
        var response = await SendAsync(request(), await TokenAsync(renew: false, ct), ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            response = await SendAsync(request(), await TokenAsync(renew: true, ct), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new DomainException($"Nova Post International відмовила (HTTP {(int)response.StatusCode}): {Errors(body)}");
        return Parse(body);
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string jwt, CancellationToken ct)
    {
        request.Headers.TryAddWithoutValidation("Authorization", jwt);
        return http.SendAsync(request, ct);
    }

    private async Task<string> TokenAsync(bool renew, CancellationToken ct)
    {
        if (_jwt is { } current && !renew)
            return current;
        var response = await http.GetAsync($"{options.Url}/clients/authorization?apiKey={Uri.EscapeDataString(options.ApiKey)}", ct);
        if (!response.IsSuccessStatusCode)
            throw new DomainException($"Nova Post International не прийняла ключ API (HTTP {(int)response.StatusCode}).");
        var jwt = Text(Parse(await response.Content.ReadAsStringAsync(ct)), "jwt");
        if (jwt == "")
            throw new DomainException("Nova Post International не видала токен: перевірте ключ API.");
        return _jwt = jwt;
    }

    /// <summary>Помилки API: {"errors": {"поле": "текст"}} або {"error"/"message": "..."}.</summary>
    internal static string Errors(string body)
    {
        JsonNode? root;
        try
        {
            root = Parse(body);
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(body) ? "без пояснення" : body[..Math.Min(body.Length, 300)];
        }
        switch (root?["errors"])
        {
            case JsonObject errors when errors.Count > 0:
                return string.Join("; ", errors.Select(e => (e.Key == "" ? "" : e.Key + ": ") + Plain(e.Value)));
            case JsonArray errors when errors.Count > 0:
                return string.Join("; ", errors.Select(Plain));
        }
        var message = Text(root, "error");
        if (message == "")
            message = Text(root, "message");
        return message == "" ? "без пояснення" : message;
    }

    private static JsonNode? Parse(string body) => string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body);

    private static IEnumerable<JsonNode?> Array(JsonNode? node, string name) => node?[name] as JsonArray ?? [];

    private static string Plain(JsonNode? node) => node is JsonValue v && v.TryGetValue(out string? s) ? s : node?.ToJsonString() ?? "";

    private static string Text(JsonNode? node, string name) => node?[name] switch
    {
        JsonValue v when v.TryGetValue(out string? s) => s,
        JsonValue v => v.ToJsonString(),
        _ => "",
    };
}

/// <summary>
/// Заглушка без ключа Nova Post International: нікуди не звертається. З FakeDocuments (лише розробка) «створює» тестові
/// накладні TEST… і пропонує один тестовий код, щоб увесь шлях можна було пройти в інтерфейсі.
/// </summary>
public sealed class StubNovaPostInternationalClient(bool fakeDocuments = false) : INovaPostInternationalClient
{
    public bool IsConfigured => false;

    public bool CanCreateDocuments => fakeDocuments;

    public Task<IReadOnlyList<HsCodeDto>> SearchHsCodesAsync(string keyword, string countryCode, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HsCodeDto>>(fakeDocuments ? [new("6204491000", "Сукні з інших текстильних матеріалів (заглушка)", "Dresses")] : []);

    public Task<CreatedShipment> CreateAsync(InternationalShipment shipment, CancellationToken ct)
    {
        if (!fakeDocuments)
            throw new DomainException("Ключ API Nova Post International не задано, тож міжнародну накладну створити не можна.");
        var number = "TEST" + Random.Shared.NextInt64(10_000_000_000L).ToString("D10");
        return Task.FromResult(new CreatedShipment(Guid.NewGuid().ToString(), number, shipment.DeclaredCost));
    }

    // Видаляти ніде нічого: тестові накладні є лише в нас.
    public Task DeleteAsync(string shipmentId, CancellationToken ct) => Task.CompletedTask;
}
