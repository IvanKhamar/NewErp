using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NewErp.Application.Auth;
using NewErp.Infrastructure;

namespace NewErp.IntegrationTests;

/// <summary>
/// Рядки журналу справді доходять до логів зі звичайними налаштуваннями застосунку. Рівень тут навмисне не задаємо:
/// у Python-версії такі рядки зникали через типовий рівень WARNING, і помітити це може лише такий тест.
/// </summary>
public class AuditLogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed class Capture : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<(string Category, string Text)> Lines { get; } = new();

        private string _category = "";

        public ILogger CreateLogger(string categoryName) => new Capture { _category = categoryName, Sink = Lines };

        private ConcurrentQueue<(string Category, string Text)> Sink { get; init; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Sink.Enqueue((_category, formatter(state, exception)));

        public void Dispose()
        {
        }
    }

    [MySqlFact]
    public async Task AChangeOfAccess_IsWrittenToTheAuditLog_WithoutThePassword()
    {
        const string password = "test-password-1";
        var capture = new Capture();
        var client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ILoggerProvider>(capture))).CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.Admin, ApiFactory.Password));
        login.EnsureSuccessStatusCode();

        var reset = await client.PostAsJsonAsync($"/users/{ApiFactory.StorekeeperUserId}/reset-password", new ResetPasswordRequest(password));

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var line = Assert.Single(capture.Lines, l => l.Category == AuditLog.Category);
        Assert.Equal($"user password reset: {ApiFactory.StorekeeperUserId}, by user {ApiFactory.ActiveUserId}", line.Text);
        Assert.DoesNotContain(capture.Lines, l => l.Text.Contains(password, StringComparison.Ordinal));
    }
}
