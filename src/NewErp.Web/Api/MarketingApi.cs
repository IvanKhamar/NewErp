using System.Globalization;
using System.Net.Http.Json;

using NewErp.Application.Common;
using NewErp.Application.Marketing;
using NewErp.Application.Sms;
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

    public Task<List<VoucherCardDto>> VouchersAsync(string search) =>
        GetAsync<List<VoucherCardDto>>($"/marketing/vouchers?search={Uri.EscapeDataString(search)}");

    public Task<VoucherCardDto> VoucherAsync(int id) => GetAsync<VoucherCardDto>($"/marketing/vouchers/{id}");

    public Task<List<LookupItem>> VoucherThemesAsync() => GetAsync<List<LookupItem>>("/marketing/vouchers/themes");

    public async Task<VoucherCardDto> CreateVoucherAsync(SaveVoucherRequest request) =>
        await ReadAsync<VoucherCardDto>(await http.PostAsJsonAsync("/marketing/vouchers", request));

    public async Task<VoucherCardDto> UpdateVoucherAsync(int id, SaveVoucherRequest request) =>
        await ReadAsync<VoucherCardDto>(await http.PutAsJsonAsync($"/marketing/vouchers/{id}", request));

    public async Task DeleteVoucherAsync(int id)
    {
        var response = await http.DeleteAsync($"/marketing/vouchers/{id}");
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
    }

    public async Task<SmsSendResult> SendVoucherSmsAsync(int id, string phone) =>
        await ReadAsync<SmsSendResult>(await http.PostAsJsonAsync($"/marketing/vouchers/{id}/sms", new SendVoucherSmsRequest(phone)));

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
