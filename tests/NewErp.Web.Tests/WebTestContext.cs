using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor.Services;

using NewErp.Web.Api;
using NewErp.Web.Auth;

namespace NewErp.Web.Tests;

/// <summary>
/// Спільне налаштування для тестів компонентів: MudBlazor, «API» з наперед заданими відповідями і стан входу.
/// </summary>
public abstract class WebTestContext : BunitContext, IAsyncLifetime
{
    // Сервіси MudBlazor (KeyInterceptorService тощо) звільняються лише асинхронно, тож xUnit має викликати DisposeAsync.
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected WebTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; // MudBlazor викликає JavaScript; у тестах ці виклики просто ігноруються
        Services.AddMudServices();
        Services.AddSingleton(new HttpClient(Api) { BaseAddress = new Uri("http://localhost/app/") });
        Services.AddScoped<ApiAuthenticationStateProvider>();
        Services.AddScoped<StockApi>();
        Services.AddScoped<FinanceApi>();
        Services.AddScoped<MarketingApi>(); // картка замовлення показує сертифікат
        Services.AddScoped<Clipboard>();
    }

    protected FakeApi Api { get; } = new();

    /// <summary>Підроблений API: відповідь за шляхом запиту, решта — 404.</summary>
    protected sealed class FakeApi : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];

        public List<string> Requests { get; } = [];

        /// <summary>Тіла запитів, що змінюють дані (POST/PUT), за шляхом.</summary>
        public Dictionary<string, string> Bodies { get; } = [];

        /// <summary>Відповідь за шляхом; з method — лише для цього методу (коли GET і POST мають одну адресу).</summary>
        public void Respond(string path, HttpStatusCode status, object? body = null, HttpMethod? method = null) =>
            _responses[method is null ? path : $"{method} {path}"] = () => new HttpResponseMessage(status) { Content = body is null ? null : JsonContent.Create(body) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            if (request.Content is not null)
                Bodies[path] = await request.Content.ReadAsStringAsync(cancellationToken);
            return _responses.TryGetValue($"{request.Method} {path}", out var respond) || _responses.TryGetValue(path, out respond) ? respond() : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
