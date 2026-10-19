using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Magpie.Desktop.Backend;

/// <summary>
/// The magpie this app draws, started as it already ships: `magpie web`, the
/// command that serves the window's page to a browser (internal/cli/web_cli.go).
/// Nothing of magpie's is added for this app — the address, the key
/// (MAGPIE_WEB_KEY) and the printed link are the ones magpie web has always
/// had, so the app is one more client of the same page and API, beside a
/// browser tab, `magpie tui` and the Wails app.
/// </summary>
public sealed partial class BackendProcess : IDisposable
{
    /// <summary>Where magpie's link was printed; the app reads it from here
    /// rather than from the pipe, which magpie writes and never reads.</summary>
    private readonly List<string> _lines = [];

    public string Address { get; private set; } = "";
    public string Key { get; private set; } = "";
    public string Link { get; private set; } = "";
    /// <summary>The version serving the page, as /api/settings says it.</summary>
    public string Version { get; set; } = "";

    private Process? _process;
    private readonly bool _started;

    private BackendProcess(bool started) => _started = started;

    /// <summary>
    /// Starts magpie web on a free loopback port with a key of the app's own,
    /// or uses the one already serving that address (a second launch), and
    /// reads the link it prints.
    /// </summary>
    public static async Task<BackendProcess> StartAsync(CancellationToken cancel = default)
    {
        var exe = FindMagpie();

        // the key stays the app's across runs, so a page kept in the webview's
        // profile stays signed in as magpie web's own cookie does
        var key = AppKey();
        var address = "127.0.0.1:" + FreePort();

        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory,
            },
            EnableRaisingEvents = true,
        };
        // magpie web's own flags, no more: the page on this port, the key
        // this app keeps, and no browser opened for it
        proc.StartInfo.ArgumentList.Add("web");
        proc.StartInfo.ArgumentList.Add("--addr");
        proc.StartInfo.ArgumentList.Add(address);
        proc.StartInfo.ArgumentList.Add("--no-open");
        proc.StartInfo.Environment["MAGPIE_WEB_KEY"] = key;

        var backend = new BackendProcess(started: true) { _process = proc };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (backend._lines) backend._lines.Add(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (backend._lines) backend._lines.Add(e.Data);
            App.Log("magpie: " + e.Data);
        };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            cancel.ThrowIfCancellationRequested();
            if (proc.HasExited)
            {
                throw new InvalidOperationException(
                    $"magpie exited while starting (code {proc.ExitCode}): {exe} web --addr {address}");
            }
            if (backend.TakePrintedLink()) return backend;
            await Task.Delay(120, cancel).ConfigureAwait(false);
        }
        backend.Dispose();
        throw new TimeoutException($"magpie web printed no link in 30s: {exe} web --addr {address}");
    }

    /// <summary>
    /// The link magpie web printed: "magpie web on http://127.0.0.1:PORT/?k=…"
    /// (printWeb). Its address and key are the page's.
    /// </summary>
    private bool TakePrintedLink()
    {
        string[] lines;
        lock (_lines) lines = [.. _lines];
        foreach (var line in lines)
        {
            var m = LinkPattern().Match(line);
            if (!m.Success) continue;
            Link = m.Groups["url"].Value;
            Address = m.Groups["addr"].Value;
            Key = Uri.UnescapeDataString(m.Groups["key"].Value);
            App.Log($"magpie web at {Address}");
            return true;
        }
        return false;
    }

    [GeneratedRegex(@"magpie web on\s+(?<url>https?://(?<addr>[^/\s]+)/\?k=(?<key>[^&\s]+))")]
    private static partial Regex LinkPattern();

    /// <summary>The page's address without the key: the key became a cookie
    /// when the page was opened.</summary>
    public string BaseUrl => $"http://{Address}/";

    /// <summary>
    /// Where magpie is: beside this app (a release ships both), or on PATH
    /// (a development build), or MAGPIE_EXE's.
    /// </summary>
    public static string FindMagpie()
    {
        if (Environment.GetEnvironmentVariable("MAGPIE_EXE") is { Length: > 0 } named && File.Exists(named))
        {
            return Path.GetFullPath(named);
        }
        var here = AppContext.BaseDirectory;
        foreach (var name in new[] { "magpie.exe", "magpie-cli-windows-amd64.exe", "magpie-cli-windows-arm64.exe" })
        {
            var beside = Path.Combine(here, name);
            if (File.Exists(beside)) return beside;
        }
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (dir.Length == 0) continue;
            var p = Path.Combine(dir, "magpie.exe");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException(
            "magpie.exe is neither beside this app nor on PATH: set MAGPIE_EXE to it");
    }

    /// <summary>The key this app's page has, kept between runs (as magpie web
    /// keeps MAGPIE_WEB_KEY's): 32 hex characters, the run-key shape
    /// (web.go's NewWebKey).</summary>
    private static string AppKey()
    {
        var file = Path.Combine(AppDataDir(), "web-key");
        try
        {
            if (File.Exists(file))
            {
                var kept = File.ReadAllText(file).Trim();
                if (kept.Length == 32 && kept.All(Uri.IsHexDigit)) return kept;
            }
        }
        catch (IOException) { }
        var key = Convert.ToHexString(Guid.NewGuid().ToByteArray())
            .Concat(Convert.ToHexString(Guid.NewGuid().ToByteArray()))
            .Take(32).Aggregate("", (a, c) => a + c).ToLowerInvariant();
        try
        {
            Directory.CreateDirectory(AppDataDir());
            File.WriteAllText(file, key);
        }
        catch (IOException) { }
        return key;
    }

    /// <summary>The app's own folder: its page key and last view. magpie's
    /// files stay where magpie keeps them (~/.config/magpie), read and
    /// written by the magpie web this app starts, as they are for a browser
    /// tab.</summary>
    public static string AppDataDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "magpie-desktop");

    /// <summary>A free loopback port, so two of these never meet, nor meet a
    /// browser's magpie web on 3430.</summary>
    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public void Dispose()
    {
        if (!_started || _process is null) return;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch (Exception)
        {
            // it was already gone
        }
        _process.Dispose();
        _process = null;
    }
}
