using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magpie.Desktop.Backend;

/// <summary>
/// The page's API, as the app's own windows call it: the same JSON routes
/// under /api (internal/gui/api.go), on the address the backend gave. The key
/// became a cookie when the page was first opened, and every window here
/// shares that cookie, as the page in a browser would.
/// </summary>
public sealed class MagpieClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly CookieContainer _cookies = new();
    private string _base = "";

    public MagpieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <summary>Points the client at a backend, and takes the key into the
    /// cookie jar as the page's first load does: webGuard trades ?k= for a
    /// cookie and sends the page on its way.</summary>
    public async Task SignInAsync(BackendProcess backend, CancellationToken cancel = default)
    {
        _base = backend.BaseUrl;
        using var resp = await _http.GetAsync(backend.Link, HttpCompletionOption.ResponseHeadersRead, cancel)
            .ConfigureAwait(false);
        // 303 to the clean address is the sign-in; the page itself is 200
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("magpie turned this app away: its key is not the one it printed");
        }
    }

    public string BaseUrl => _base;

    /// <summary>The version serving the page (settingsJSON's version), for
    /// the tray's menu, as the Wails app's menu says it.</summary>
    public async Task<string> VersionAsync(CancellationToken cancel = default)
    {
        try
        {
            var s = await GetAsync<System.Text.Json.JsonElement>("settings", cancel).ConfigureAwait(false);
            return s.ValueKind == System.Text.Json.JsonValueKind.Object &&
                   s.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancel = default)
    {
        using var resp = await _http.GetAsync(_base + "api/" + path, cancel).ConfigureAwait(false);
        await EnsureOkAsync(resp, path).ConfigureAwait(false);
        return await resp.Content.ReadFromJsonAsync<T>(Json.Options, cancel).ConfigureAwait(false);
    }

    /// <summary>A file the page serves beside its API (an icon): its bytes,
    /// read with this client's cookie. An image control loading the address
    /// itself has no cookie, and magpie's guard turns it away.</summary>
    public async Task<byte[]?> GetBytesAsync(string path, CancellationToken cancel = default)
    {
        using var resp = await _http.GetAsync(_base + path, cancel).ConfigureAwait(false);
        await EnsureOkAsync(resp, path).ConfigureAwait(false);
        return await resp.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false);
    }

    public async Task<T?> PostAsync<T>(string path, object? body = null, CancellationToken cancel = default)
    {
        using var resp = await _http.PostAsJsonAsync(_base + "api/" + path, body ?? new { }, Json.Options, cancel)
            .ConfigureAwait(false);
        await EnsureOkAsync(resp, path).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NoContent) return default;
        return await resp.Content.ReadFromJsonAsync<T>(Json.Options, cancel).ConfigureAwait(false);
    }

    /// <summary>A write whose answer the caller doesn't read: a click that
    /// changes something and redraws from the next state read.</summary>
    public async Task PostAsync(string path, object? body = null, CancellationToken cancel = default)
    {
        using var resp = await _http.PostAsJsonAsync(_base + "api/" + path, body ?? new { }, Json.Options, cancel)
            .ConfigureAwait(false);
        await EnsureOkAsync(resp, path).ConfigureAwait(false);
    }

    /// <summary>The page's own failure text, as the page shows it: magpie
    /// answers a refused change with the reason in the body, as
    /// <c>{"error":"…"}</c> (gui.fail), so the reason alone is shown, not
    /// the JSON around it.</summary>
    private static async Task EnsureOkAsync(HttpResponseMessage resp, string path)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = "";
        try { body = (await resp.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim(); }
        catch (Exception) { /* the reason is the status, then */ }
        var reason = Reason(body);
        throw new MagpieException(path, (int)resp.StatusCode,
            reason.Length > 0 ? reason : $"{(int)resp.StatusCode} {resp.ReasonPhrase}");
    }

    /// <summary>The reason in an answer's body: the <c>error</c> of
    /// magpie's <c>{"error":"…"}</c>, or the body as it is when it is no
    /// such JSON (a proxy's HTML page, say).</summary>
    private static string Reason(string body)
    {
        if (body.Length == 0 || body[0] != '{') return body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var err) &&
                err.ValueKind == JsonValueKind.String)
            {
                return err.GetString() ?? "";
            }
        }
        catch (JsonException) { /* not the page's JSON: the body stands */ }
        return body;
    }

    public void Dispose() => _http.Dispose();
}

public sealed class MagpieException(string path, int status, string message)
    : Exception(message)
{
    public string Path { get; } = path;
    public int Status { get; } = status;
}

/// <summary>The JSON the page reads, with the names it reads it by.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

// The API's shapes are in Api.cs, page by page.
