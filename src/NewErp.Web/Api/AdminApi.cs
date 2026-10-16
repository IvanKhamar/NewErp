using System.Globalization;
using System.Net.Http.Json;

using NewErp.Application.Auth;
using NewErp.Application.Staff;

namespace NewErp.Web.Api;

/// <summary>Виклики адмінських розділів: користувачі й бонуси менеджерів (див. <see cref="StockApi"/> про те, навіщо окремий клас).</summary>
public sealed class AdminApi(HttpClient http)
{
    public Task<List<UserDto>> UsersAsync() => GetAsync<List<UserDto>>("/users");

    /// <returns>Id нового користувача й тимчасовий пароль (показується один раз).</returns>
    public async Task<UserAccessResult> CreateUserAsync(CreateUserRequest request) =>
        await ReadAsync<UserAccessResult>(await http.PostAsJsonAsync("/users", request));

    /// <returns>TemporaryPassword не null, якщо доступ видано вперше або пароль задано.</returns>
    public async Task<UserAccessResult> SetAccessAsync(int userId, SetAccessRequest request) =>
        await ReadAsync<UserAccessResult>(await http.PutAsJsonAsync($"/users/{userId}/access", request));

    public async Task RevokeAsync(int userId) => await EnsureAsync(await http.DeleteAsync($"/users/{userId}/access"));

    public async Task<UserAccessResult> ResetPasswordAsync(int userId) =>
        await ReadAsync<UserAccessResult>(await http.PostAsJsonAsync($"/users/{userId}/reset-password", new ResetPasswordRequest()));

    public Task<BonusReport> BonusesAsync(DateOnly from, DateOnly to) => GetAsync<BonusReport>(
        $"/staff/bonuses?from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

    private async Task<T> GetAsync<T>(string url) => await ReadAsync<T>(await http.GetAsync(url));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ApiErrors.ReadAsync(response));
    }
}
