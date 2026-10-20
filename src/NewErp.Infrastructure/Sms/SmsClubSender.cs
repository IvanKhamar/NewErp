using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using NewErp.Application.Sms;

namespace NewErp.Infrastructure.Sms;

/// <summary>Налаштування SMSClub (секція SmsClub). Token — лише в dotnet user-secrets.</summary>
public sealed class SmsClubOptions
{
    public const string SectionName = "SmsClub";

    public string BaseUrl { get; set; } = "https://im.smsclub.mobi";

    public string Token { get; set; } = "";

    /// <summary>Ім'я відправника (альфа-ім'я), як на старому сайті.</summary>
    public string Sender { get; set; } = "Vidro.ua";

    /// <summary>
    /// Якщо список не порожній, SMS ідуть лише на ці номери (380XXXXXXXXX). Для перевірки на локальній базі,
    /// де телефони вигадані й можуть належати реальним людям.
    /// </summary>
    public List<string> OnlyPhones { get; set; } = [];

    public bool IsConfigured => Token != "";
}

/// <summary>
/// Справжній SMSClub (API v2, як у admin/model/tool/sms.php старого сайту): POST /sms/send з токеном Bearer.
/// Сиру відповідь сервісу повертаємо як є, вона потрапляє в журнал dd_sms_log.
/// </summary>
public sealed class SmsClubSender(HttpClient http, SmsClubOptions options) : ISmsSender
{
    public const string NotAllowedResponse = "НЕ НАДІСЛАНО: номера немає в SmsClub:OnlyPhones";

    public async Task<SmsSendResult> SendAsync(string phone, string text, CancellationToken ct)
    {
        if (options.OnlyPhones.Count > 0 && !options.OnlyPhones.Contains(phone))
            return new SmsSendResult(false, NotAllowedResponse);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl.TrimEnd('/')}/sms/send")
        {
            Content = JsonContent.Create(new { phone = new[] { phone }, message = text, src_addr = options.Sender }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return new SmsSendResult(response.IsSuccessStatusCode && IsAccepted(body, phone),
                body.Length > 0 ? body : $"HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException e)
        {
            // Журнал однаково запишемо: так видно, що спроба була, а SMS не пішло.
            return new SmsSendResult(false, $"НЕ НАДІСЛАНО: SMSClub недоступний ({e.Message})");
        }
    }

    /// <summary>
    /// Успішна відповідь: {"success_request": {"info": {"id повідомлення": "номер"}}}.
    /// Номери, яким SMS не пішло, SMSClub перелічує в add_info, тож рахуємо надісланим лише номер з info.
    /// </summary>
    public static bool IsAccepted(string body, string phone)
    {
        try
        {
            return JsonNode.Parse(body)?["success_request"]?["info"] is JsonObject info
                && info.Any(p => p.Value?.GetValueKind() == JsonValueKind.String && p.Value.GetValue<string>() == phone);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
