using System.Net.Http.Json;

namespace NewErp.Web;

/// <summary>Текст помилки з відповіді API. API повертає ProblemDetails, де в detail — пояснення українською.</summary>
public static class ApiErrors
{
    public static async Task<string> ReadAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Problem>();
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return problem.Title;
        }
        catch (System.Text.Json.JsonException)
        {
            // Відповідь не JSON — нижче загальне повідомлення.
        }
        return $"Помилка сервера ({(int)response.StatusCode}).";
    }

    // Клас ProblemDetails є лише в серверних пакетах ASP.NET, тож читаємо потрібні поля самі.
    private sealed record Problem(string? Title, string? Detail);
}
