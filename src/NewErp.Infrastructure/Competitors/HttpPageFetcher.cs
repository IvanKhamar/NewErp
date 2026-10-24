using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

using NewErp.Application.Competitors;
using NewErp.Domain.Competitors;

namespace NewErp.Infrastructure.Competitors;

/// <summary>
/// Завантажує сторінку конкурента. Переадресації обробляємо самі й лише в межах сайту конкурента, тож посилання не заведе
/// сервер на інший хост. Адреси локальної мережі відхиляються під час самого з'єднання (а не лише при перевірці адреси),
/// тож підмінений DNS не обійде заборону. Тіло обрізається на 2 МБ.
/// </summary>
public sealed partial class HttpPageFetcher : IPageFetcher, IDisposable
{
    private const int MaxRedirects = 3;
    private const int MaxBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    // Українські сайти бувають у windows-1251: без цього провайдера .NET знає лише UTF-8 і кілька інших кодувань.
    static HttpPageFetcher() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly HttpClient _http;

    public HttpPageFetcher() : this(GuardedHandler()) { }

    /// <summary>Для тестів: підставлений обробник без перевірки адрес.</summary>
    public HttpPageFetcher(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = Timeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; NewErp price check)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
    }

    public async Task<string> FetchAsync(Uri page, string siteHost, CancellationToken ct)
    {
        var current = page;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!CompetitorSite.IsOnSite(current, siteHost))
                throw new PageFetchException($"Сторінка веде на інший сайт: {current.Host}");

            using var response = await SendAsync(current, ct);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                var location = response.Headers.Location
                    ?? throw new PageFetchException($"Сайт переадресував без адреси (HTTP {status}).");
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }
            if (status != 200)
                throw new PageFetchException($"Сайт відповів HTTP {status}.");
            return await ReadBodyAsync(response, ct);
        }
        throw new PageFetchException("Забагато переадресацій.");
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Чи адреса з інтернету, а не з локальної чи службової мережі.</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast);

        var b = address.GetAddressBytes();
        return !(b[0] == 0 || b[0] == 10 || b[0] >= 224        // 0.0.0.0/8, 10/8, мультикаст і зарезервовані
            || b[0] == 100 && b[1] >= 64 && b[1] < 128           // 100.64/10 (спільна адресація провайдерів)
            || b[0] == 169 && b[1] == 254                        // link-local
            || b[0] == 172 && b[1] >= 16 && b[1] < 32            // 172.16/12
            || b[0] == 192 && b[1] == 168);                      // 192.168/16
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            return await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException e) when (e.InnerException is PageFetchException refused)
        {
            throw refused;
        }
        catch (HttpRequestException e)
        {
            throw new PageFetchException($"Сайт недоступний: {e.HttpRequestError}.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PageFetchException("Сайт не відповів за 20 секунд.");
        }
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxBytes];
        var length = 0;
        int read;
        while (length < MaxBytes && (read = await body.ReadAsync(buffer.AsMemory(length, MaxBytes - length), ct)) > 0)
            length += read;
        return Charset(response.Content.Headers.ContentType?.ToString()).GetString(buffer, 0, length);
    }

    private static Encoding Charset(string? contentType)
    {
        var m = CharsetPattern().Match(contentType ?? "");
        if (!m.Success)
            return Encoding.UTF8;
        try
        {
            return Encoding.GetEncoding(m.Groups[1].Value);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    // З'єднуємося лише з публічними адресами: перевірка тут, у момент з'єднання, тож DNS не підмінить адресу між перевіркою й запитом.
    private static SocketsHttpHandler GuardedHandler() => new()
    {
        AllowAutoRedirect = false,
        ConnectTimeout = Timeout,
        ConnectCallback = async (context, ct) =>
        {
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            }
            catch (SocketException)
            {
                throw new PageFetchException($"Сайт {context.DnsEndPoint.Host} не знайдено.");
            }
            if (addresses.Length == 0 || !addresses.All(IsPublic))
                throw new PageFetchException("Адреса сайту веде в локальну мережу.");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    [GeneratedRegex("charset=\"?([\\w-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CharsetPattern();
}
