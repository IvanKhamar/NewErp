using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

using NewErp.Application.Delivery;
using NewErp.Domain.Common;
using NewErp.Domain.Delivery;

namespace NewErp.Infrastructure.Delivery;

/// <summary>
/// Налаштування з розділу "NovaPoshta". Ключі API — лише в dotnet user-secrets, ніколи в appsettings.json.
/// Відправників кілька (наприклад, різні ФОП), у кожного свій ключ: user-secrets "NovaPoshta:Senders:0:ApiKey" тощо.
/// </summary>
public sealed class NovaPoshtaOptions
{
    public const string SectionName = "NovaPoshta";

    /// <summary>Ключ для трекінгу й довідника. Якщо не задано — береться ключ типового відправника.</summary>
    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.novaposhta.ua/v2.0/json/";

    /// <summary>Звідки завантажувати PDF накладних і маркувань (як на старому сайті).</summary>
    public string PrintBaseUrl { get; set; } = "https://my.novaposhta.ua/";

    /// <summary>Опис вантажу в ТТН. Старий сайт пише «Vidro».</summary>
    public string CargoDescription { get; set; } = "Vidro";

    /// <summary>
    /// Лише для локальної розробки (appsettings.Development.json): без ключа заглушка «створює» тестові ТТН з номером
    /// на 00, щоб можна було пройти весь шлях в інтерфейсі. Із заданим ключем ігнорується.
    /// </summary>
    public bool FakeDocuments { get; set; }

    public List<NovaPoshtaSenderOptions> Senders { get; set; } = [];

    public List<NovaPoshtaDropoffPointOptions> DropoffPoints { get; set; } = [];

    /// <summary>Ключ для трекінгу й довідника: окремий або типового відправника.</summary>
    public string? TrackingKey =>
        !string.IsNullOrWhiteSpace(ApiKey) ? ApiKey : (Senders.FirstOrDefault(s => s.IsDefault) ?? Senders.FirstOrDefault())?.ApiKey;
}

/// <summary>Відправник: контрагент і контактна особа в кабінеті Нової Пошти (їхні Ref беруться з API чи кабінету).</summary>
public sealed class NovaPoshtaSenderOptions
{
    /// <summary>Короткий id для бази (erp_shipment_sender.sender_id), наприклад "fop-ivanenko". Не змінювати після першої ТТН.</summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string CounterpartyRef { get; set; } = "";

    public string ContactRef { get; set; } = "";

    /// <summary>Телефон відправника, 380XXXXXXXXX.</summary>
    public string Phone { get; set; } = "";

    public bool IsDefault { get; set; }
}

/// <summary>Місце відправки: відділення, куди відвозять посилки (Ref міста й відділення).</summary>
public sealed class NovaPoshtaDropoffPointOptions
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string CityRef { get; set; } = "";

    public string AddressRef { get; set; } = "";

    public bool IsDefault { get; set; }
}

/// <summary>
/// Справжній клієнт API Нової Пошти (JSON API 2.0). Перевірено на тестових відповідях у форматі документації;
/// на справжньому ключі ще не запускався. HttpClient створюється без журналювання запитів,
/// тож ключі (вони є і в тілі запитів, і в адресі друку) у журнал не потрапляють.
/// </summary>
public sealed class NovaPoshtaClient : INovaPoshtaClient
{
    private const int WarehousesPageSize = 500;
    private const int MaxWarehousePages = 1000; // запобіжник від нескінченного циклу

    private static readonly string[] DateFormats = ["dd-MM-yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy"];

    private readonly HttpClient _http;
    private readonly NovaPoshtaOptions _options;

    public NovaPoshtaClient(HttpClient http, NovaPoshtaOptions options)
    {
        _http = http;
        _options = options;
        foreach (var sender in options.Senders)
        {
            if (new[] { sender.Id, sender.ApiKey, sender.CounterpartyRef, sender.ContactRef, sender.Phone }.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException(
                    $"Відправник Нової Пошти \"{sender.Id}\": потрібні Id, ApiKey, CounterpartyRef, ContactRef і Phone.");
        }
        if (options.DropoffPoints.Any(p => new[] { p.Id, p.CityRef, p.AddressRef }.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidOperationException("Місце відправки Нової Пошти: потрібні Id, CityRef і AddressRef.");

        Senders = options.Senders.Select(s => new SenderOption(s.Id, s.Name, s.IsDefault)).ToList();
        DropoffPoints = options.DropoffPoints.Select(p => new DropoffPointOption(p.Id, p.Name, p.IsDefault)).ToList();
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.TrackingKey);

    public bool CanCreateDocuments => Senders.Count > 0 && DropoffPoints.Count > 0;

    public IReadOnlyList<SenderOption> Senders { get; }

    public IReadOnlyList<DropoffPointOption> DropoffPoints { get; }

    public async Task<IReadOnlyList<TrackingInfo>> TrackAsync(IReadOnlyList<TrackingRequest> documents, CancellationToken ct)
    {
        if (documents.Count == 0)
            return [];

        var data = await CallAsync(_options.TrackingKey!, "TrackingDocument", "getStatusDocuments",
            new { Documents = documents.Select(d => new { DocumentNumber = d.Number, d.Phone }).ToList() }, ct);
        return data.EnumerateArray().Select(Parse).ToList();
    }

    public async Task<CreatedShipment> CreateAsync(NewShipment shipment, CancellationToken ct)
    {
        var sender = SenderOptions(shipment.SenderId);
        var point = _options.DropoffPoints.FirstOrDefault(p => p.Id == shipment.DropoffPointId)
            ?? throw new DomainException($"Невідоме місце відправки: {shipment.DropoffPointId}.");

        // 1. Отримувач-фізособа (якщо такий уже є, Нова Пошта поверне наявного) і його контактна особа.
        var recipient = (await CallAsync(sender.ApiKey, "Counterparty", "save", new
        {
            shipment.FirstName,
            shipment.MiddleName,
            shipment.LastName,
            shipment.Phone,
            Email = "",
            CounterpartyType = "PrivatePerson",
            CounterpartyProperty = "Recipient",
            shipment.CityRef,
        }, ct))[0];
        var recipientRef = Text(recipient, "Ref");
        var contactRef = recipient.TryGetProperty("ContactPerson", out var contact)
            && contact.TryGetProperty("data", out var contacts) && contacts.GetArrayLength() > 0
                ? Text(contacts[0], "Ref")
                : "";
        if (recipientRef == "" || contactRef == "")
            throw new DomainException("Нова Пошта не повернула отримувача чи його контактну особу.");

        // 2. Сама накладна: відділення → відділення, оплата готівкою, як на старому сайті.
        var properties = new Dictionary<string, object>
        {
            ["PayerType"] = shipment.SenderPays ? "Sender" : "Recipient",
            ["PaymentMethod"] = "Cash",
            ["DateTime"] = shipment.ShipDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["CargoType"] = "Cargo",
            ["ServiceType"] = "WarehouseWarehouse",
            ["Description"] = _options.CargoDescription,
            ["Cost"] = Number(shipment.DeclaredCost),
            ["SeatsAmount"] = shipment.Seats.Count.ToString(CultureInfo.InvariantCulture),
            ["Weight"] = Number(shipment.Seats.Sum(s => s.Weight)),
            ["OptionsSeat"] = shipment.Seats.Select(s => new
            {
                volumetricVolume = Number(s.Volume),
                volumetricWidth = Number(s.Width),
                volumetricLength = Number(s.Length),
                volumetricHeight = Number(s.Height),
                weight = Number(s.Weight),
            }).ToList(),
            ["InfoRegClientBarcodes"] = shipment.OrderId.ToString(CultureInfo.InvariantCulture),
            ["Sender"] = sender.CounterpartyRef,
            ["ContactSender"] = sender.ContactRef,
            ["SendersPhone"] = sender.Phone,
            ["CitySender"] = point.CityRef,
            ["SenderAddress"] = point.AddressRef,
            ["Recipient"] = recipientRef,
            ["ContactRecipient"] = contactRef,
            ["RecipientsPhone"] = shipment.Phone,
            ["CityRecipient"] = shipment.CityRef,
            ["RecipientAddress"] = shipment.WarehouseRef,
        };
        // Накладений платіж через «контроль оплати», як на старому сайті.
        if (shipment.CashOnDelivery > 0)
            properties["AfterpaymentOnGoodsCost"] = Number(shipment.CashOnDelivery);

        var document = (await CallAsync(sender.ApiKey, "InternetDocument", "save", properties, ct))[0];
        var number = Text(document, "IntDocNumber");
        if (number == "")
            throw new DomainException("Нова Пошта не повернула номер ТТН.");
        return new CreatedShipment(
            Text(document, "Ref"), number,
            decimal.TryParse(Text(document, "CostOnSite"), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) ? cost : 0);
    }

    public async Task DeleteAsync(string senderId, string documentRef, CancellationToken ct) =>
        await CallAsync(SenderOptions(senderId).ApiKey, "InternetDocument", "delete", new { DocumentRefs = new[] { documentRef } }, ct);

    public async Task<byte[]> PrintAsync(string senderId, string documentRef, ShipmentPrintKind kind, CancellationToken ct)
    {
        var method = kind == ShipmentPrintKind.Document ? "printDocument" : "printMarkings";
        var url = $"{_options.PrintBaseUrl}orders/{method}/orders[]/{Uri.EscapeDataString(documentRef)}/type/pdf/zebra/zebra/apiKey/{SenderOptions(senderId).ApiKey}";
        using var response = await _http.GetAsync(url, ct);
        var pdf = await response.Content.ReadAsByteArrayAsync(ct);
        // Замість PDF Нова Пошта при помилці віддає сторінку чи JSON; справжній PDF починається з «%PDF».
        if (!response.IsSuccessStatusCode || pdf.Length < 4 || pdf[0] != '%' || pdf[1] != 'P' || pdf[2] != 'D' || pdf[3] != 'F')
            throw new DomainException("Нова Пошта не віддала PDF для друку. Перевірте, чи ТТН не видалена.");
        return pdf;
    }

    public async Task<IReadOnlyList<NovaPoshtaWarehouse>> FetchWarehousesAsync(CancellationToken ct)
    {
        var result = new List<NovaPoshtaWarehouse>();
        for (var page = 1; page <= MaxWarehousePages; page++)
        {
            var data = await CallAsync(_options.TrackingKey!, "Address", "getWarehouses", new { Page = page, Limit = WarehousesPageSize }, ct);
            if (data.GetArrayLength() == 0)
                break;
            result.AddRange(data.EnumerateArray().Select(ParseWarehouse));
        }
        return result;
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

    internal static NovaPoshtaWarehouse ParseWarehouse(JsonElement item) => new(
        Text(item, "Ref"),
        Text(item, "Description"),
        Text(item, "CategoryOfWarehouse"),
        Text(item, "CityRef"),
        Text(item, "CityDescription"),
        decimal.TryParse(Text(item, "TotalMaxWeightAllowed"), NumberStyles.Number, CultureInfo.InvariantCulture, out var weight) ? (int)weight : 0,
        Text(item, "WarehouseStatus") == "Working");

    private NovaPoshtaSenderOptions SenderOptions(string senderId) =>
        _options.Senders.FirstOrDefault(s => s.Id == senderId)
            ?? throw new DomainException($"Відправника \"{senderId}\" немає в налаштуваннях Нової Пошти.");

    /// <summary>Один виклик API. Помилки Нової Пошти стають DomainException, тож людина побачить їх текст (400).</summary>
    private async Task<JsonElement> CallAsync(string apiKey, string model, string method, object properties, CancellationToken ct)
    {
        var request = new { apiKey, modelName = model, calledMethod = method, methodProperties = properties };
        // Без camelCase за замовчуванням: API Нової Пошти чекає імена як є (Documents, DocumentNumber, Phone).
        using var response = await _http.PostAsJsonAsync(_options.BaseUrl, request, JsonSerializerOptions.Default, ct);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!json.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
            throw new DomainException("Нова Пошта відмовила: " + Errors(json.RootElement));
        // Clone — щоб дані пережили звільнення JsonDocument.
        return json.RootElement.GetProperty("data").Clone();
    }

    // Шість знаків — щоб не загубити об'єм у м³ (31×36×6 см = 0.006696).
    private static string Number(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

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
        root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
            ? string.Join("; ", errors.EnumerateArray().Select(e => e.GetString()))
            : "без пояснення";
}

/// <summary>
/// Заглушка, коли ключа немає: ні до кого не звертається. Якщо увімкнено FakeDocuments (лише для розробки),
/// «створює» тестові ТТН з номером на 00 від тестового відправника; друкувати їх нічого.
/// </summary>
public sealed class StubNovaPoshtaClient(bool fakeDocuments = false) : INovaPoshtaClient
{
    public const string TestSenderId = "test";
    public const string TestDropoffPointId = "test";

    public bool IsConfigured => false;

    public bool CanCreateDocuments => fakeDocuments;

    public IReadOnlyList<SenderOption> Senders { get; } =
        fakeDocuments ? [new SenderOption(TestSenderId, "Тестовий відправник (заглушка)", true)] : [];

    public IReadOnlyList<DropoffPointOption> DropoffPoints { get; } =
        fakeDocuments ? [new DropoffPointOption(TestDropoffPointId, "Тестове відділення (заглушка)", true)] : [];

    public Task<IReadOnlyList<TrackingInfo>> TrackAsync(IReadOnlyList<TrackingRequest> documents, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TrackingInfo>>([]);

    public Task<CreatedShipment> CreateAsync(NewShipment shipment, CancellationToken ct)
    {
        if (!fakeDocuments)
            throw new DomainException("Ключ API Нової Пошти не задано, тож ТТН створити не можна.");
        var number = "00" + Random.Shared.NextInt64(0, 1_000_000_000_000).ToString("D12", CultureInfo.InvariantCulture);
        return Task.FromResult(new CreatedShipment(Guid.NewGuid().ToString(), number, 0));
    }

    public Task DeleteAsync(string senderId, string documentRef, CancellationToken ct) => Task.CompletedTask;

    public Task<byte[]> PrintAsync(string senderId, string documentRef, ShipmentPrintKind kind, CancellationToken ct) =>
        throw new DomainException("Друк недоступний: ключ API Нової Пошти не задано.");

    public Task<IReadOnlyList<NovaPoshtaWarehouse>> FetchWarehousesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NovaPoshtaWarehouse>>([]);
}
