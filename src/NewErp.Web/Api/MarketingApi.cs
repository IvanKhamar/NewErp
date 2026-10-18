using System.Globalization;
using System.Net.Http.Json;

using NewErp.Application.Marketing;
using NewErp.Domain.Marketing;

namespace NewErp.Web.Api;

/// <summary>Виклики API маркетингу (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class MarketingApi(HttpClient http)
{
    public Task<List<BirthdayContactDto>> BirthdaysAsync(BirthdayPeriod period) => GetAsync<List<BirthdayContactDto>>($"/marketing/birthdays?period={period}");

    public async Task<string> NewCouponCodeAsync() =>
        (await ReadAsync<CouponCodeDto>(await http.PostAsync("/marketing/birthdays/coupon-code", null))).Code;

    public async Task<BirthdaySmsResult> SendAsync(BirthdaySmsRequest request) =>
        await ReadAsync<BirthdaySmsResult>(await http.PostAsJsonAsync("/marketing/birthdays/sms", request));

    public async Task<BulkSmsResult> SendBulkAsync(BulkSmsRequest request) =>
        await ReadAsync<BulkSmsResult>(await http.PostAsJsonAsync("/marketing/birthdays/sms-bulk", request));

    public Task<List<CustomerOrderDto>> CustomerOrdersAsync(string phone) =>
        GetAsync<List<CustomerOrderDto>>($"/marketing/customers/orders?phone={Uri.EscapeDataString(phone)}");

    /// <summary>Адреса CSV від кореня сервера: файл віддає API, браузер завантажує його за звичайним посиланням з тією ж cookie.</summary>
    public static string ExportUrl(ContactField field, IEnumerable<ContactSource> sources, DateOnly? from, DateOnly? to)
    {
        var query = $"field={field}" + string.Concat(sources.Select(s => $"&sources={s}"));
        if (from is { } f)
            query += "&from=" + f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (to is { } t)
            query += "&to=" + t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return "/marketing/contacts.csv?" + query;
    }

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
