using System.Diagnostics;
using Magpie.Desktop.Backend;
using Magpie.Desktop.Pages;
using Magpie.Desktop.Tray;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop;

/// <summary>
/// magpie's Windows app: a tray icon, the quick panel it drops, and the
/// window. It starts magpie as it already ships (`magpie web`, with its own
/// MAGPIE_WEB_KEY and address) and draws the page from it — nothing of
/// magpie's is added or changed for this app. One app runs at a time: a
/// second launch brings this one's window up.
/// </summary>
public partial class App : Application
{
    /// <summary>One app at a time, as the Wails app's SingleInstance was.</summary>
    private static Mutex? _single;

    private TrayIcon? _tray;
    private MainWindow? _main;
    private PanelWindow? _panel;
    private BackendProcess? _backend;
    private MagpieClient? _client;
    private DesktopChannel? _channel;
    /// <summary>The window's thread, which the backend's poll hands work to.</summary>
    private DispatcherQueue _ui = null!;
    /// <summary>Whether the tray has already said the app keeps running with
    /// its window closed: said once, not at every close.</summary>
    private bool _saidStillRunning;

    /// <summary>The page's address and key, once magpie is up.</summary>
    public static App CurrentApp => (App)Current;

    public MagpieClient Client => _client ?? throw new InvalidOperationException("magpie isn't up yet");
    public BackendProcess Backend => _backend ?? throw new InvalidOperationException("magpie isn't up yet");
    public DesktopChannel Channel => _channel ?? throw new InvalidOperationException("magpie isn't up yet");

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ui = DispatcherQueue.GetForCurrentThread();
        // a failure anywhere is said in a box and logged, never a silent exit
        UnhandledException += (_, e) =>
        {
            Debug.WriteLine(e.Exception);
            e.Handled = true;
            Fail(e.Exception);
        };
        // one app per copy: two checkouts' builds are two apps (SingleInstance.Key)
        _single = new Mutex(initiallyOwned: true, SingleInstance.Key, out var first);
        if (!first)
        {
            // the app already running brings its window up instead
            SingleInstance.ShowRunning();
            _single.Dispose();
            _single = null;
            Exit();
            return;
        }
        SingleInstance.Listen(() => _ = _ui.TryEnqueue(() => ShowMain("")));
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            Log("start");
            _backend = await BackendProcess.StartAsync();
            Log("backend " + _backend.Address);
            _client = new MagpieClient();
            await _client.SignInAsync(_backend);
            _backend.Version = await _client.VersionAsync();

            // the words the pages say, in the language Settings picked: the
            // page's own translations (L, from i18n.js), read once here and
            // again when the language changes
            await UseLanguageAsync();

            // what magpie web's host has no window for: the app answers it
            _channel = new DesktopChannel
            {
                ChooseFolderAsync = ChooseFolderAsync,
                FitPanelAsync = () => _panel is null ? Task.CompletedTask : _panel.FitToPageAsync(),
            };

            // Windows' tray takes an .ico, and the app's own icon is the
            // coloured bird it shows there (the Mac's menu bar takes the
            // black template glyph, internal/gui/tray.png)
            var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            _tray = new TrayIcon(icon, $"magpie {_backend.Version}");
            _tray.Activated += () => _ = _ui.TryEnqueue(TogglePanel);
            _tray.MenuRequested += () => _ = _ui.TryEnqueue(ShowTrayMenu);

            // magpie:// links, as the Wails app registered them: this exe,
            // now, so a copy that moved answers from its new place
            Scheme.Register();

            // the window is the first thing a launch shows, as the app's did;
            // the panel comes with the first tray click, or when asked for
            // (`Magpie.Desktop.exe panel`, as `magpie panel` asks)
            var args = Environment.GetCommandLineArgs();
            var link = Scheme.FromArgs(args);
            if (args.Length > 1 && args[1] == "panel") TogglePanel();
            else ShowMain(link.Length > 0 ? LinkView(link) : "");
            if (link.Length > 0) LinkSaid(link);
            Log("window up");
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    /// <summary>The main window, on the view asked for ("" for the one it was
    /// on: the page keeps that itself).</summary>
    public void ShowMain(string view)
    {
        if (_main is null)
        {
            _main = new MainWindow();
            // closing hides the window: the tray stays, and the app says so
            // once, the way Windows' own tray apps do — otherwise a closed
            // window reads as a quit that didn't happen
            _main.Closed += (_, _) =>
            {
                _main = null;
                if (!_saidStillRunning)
                {
                    _saidStillRunning = true;
                    _tray?.Notify("magpie", "magpie is still running: its icon is in the tray, and the gateway keeps serving.");
                }
            };
        }
        _main.Activate();
        if (!string.IsNullOrEmpty(view)) _main.Navigate(view);
    }

    /// <summary>
    /// Draws the window's page again: what the language change needs, since
    /// the words are set when a page is drawn. The window stays where it is
    /// and on the view it was on.
    /// </summary>
    public void ReloadWindow() => _main?.Redraw();

    /// <summary>
    /// Sets the language the pages are drawn in, from Settings' lang: "" or
    /// "system" follows Windows' own UI language, one magpie doesn't speak
    /// falling back to English (as the page does).
    /// </summary>
    public static async Task UseLanguageAsync()
    {
        var lang = "";
        try
        {
            var s = await CurrentApp.Client.GetAsync<Backend.SettingsFullJson>("settings");
            lang = s?.Lang ?? "";
        }
        catch (Exception) { /* English stands */ }
        if (lang.Length == 0 || lang == "system") lang = SystemLanguage();
        L.Use(lang);
        Log("language: " + (lang.Length > 0 ? lang : "en"));
    }

    /// <summary>Windows' own UI language as magpie names it, or "" for
    /// English: the page knows zh, zh-TW, ja and de.</summary>
    private static string SystemLanguage()
    {
        var tag = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault() ?? "";
        return tag.ToLowerInvariant() switch
        {
            var t when t.StartsWith("zh-tw") || t.StartsWith("zh-hk") || t.StartsWith("zh-hant") => "zh-TW",
            var t when t.StartsWith("zh") => "zh",
            var t when t.StartsWith("ja") => "ja",
            var t when t.StartsWith("de") => "de",
            _ => "",
        };
    }

    /// <summary>
    /// The view a <c>magpie://</c> link opens. An <c>import</c> link names a
    /// provider to add, which the Providers page's own import answers; the
    /// page drawn here has no import yet, so the link brings the reader to
    /// Providers and says what it carried (LinkSaid). Every other link is a
    /// view of the page's own (<c>magpie://settings</c>).
    /// </summary>
    private static string LinkView(string link)
    {
        var rest = link["magpie://".Length..];
        var path = rest.Split('?')[0].Trim('/').ToLowerInvariant();
        return path is "import" or "providers" ? "providers" : path.Length > 0 ? path : "agents";
    }

    /// <summary>What a link brought, said once the window is up: an import
    /// link is not applied yet (the Providers page has no import drawn), so
    /// it is said rather than dropped silently.</summary>
    private void LinkSaid(string link)
    {
        var rest = link["magpie://".Length..];
        var path = rest.Split('?')[0].Trim('/').ToLowerInvariant();
        if (path != "import") return;
        var line = "A magpie://import link opened magpie. Importing a provider from a link isn't drawn in this app yet: add it in Providers, or open magpie's page.";
        Log(line + " (" + link + ")");
        _tray?.Notify("magpie", line, warning: true);
    }

    public void HidePanel() => _panel?.HidePanel();

    /// <summary>A tray click: the panel under the icon, or the window if it is
    /// open, as Settings › Click the tray icon says.</summary>
    public void TogglePanel()
    {
        if (_panel is { Shown: true })
        {
            _panel.HidePanel();
            return;
        }
        _panel ??= new PanelWindow();
        _panel.ShowAtCursor();
    }

    private void ShowTrayMenu()
    {
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Open magpie" };
        open.Click += (_, _) => ShowMain("");
        var version = new MenuFlyoutItem { Text = $"Version {_backend?.Version}", IsEnabled = false };
        var quit = new MenuFlyoutItem { Text = "Quit magpie" };
        quit.Click += (_, _) => Quit();
        menu.Items.Add(open);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(version);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(quit);
        // over the cursor, which is on the icon the menu came from
        var (cursor, _) = NativeMethods.CursorAndWorkArea();
        menu.ShowAt(null, new Windows.Foundation.Point(cursor.X, cursor.Y));
    }

    /// <summary>Windows' folder picker, for a page that asks for a folder (a
    /// project, a plugin): the answer goes back to magpie's queue.</summary>
    private async Task<string> ChooseFolderAsync(string title)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = _main is not null ? WinRT.Interop.WindowNative.GetWindowHandle(_main)
            : _panel is not null ? WinRT.Interop.WindowNative.GetWindowHandle(_panel)
            : IntPtr.Zero;
        if (hwnd != IntPtr.Zero) WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.HomeGroup;
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path ?? "";
    }

    /// <summary>Windows' file picker, for a page that needs a file (a backup
    /// to import): its bytes, or null when nothing was picked. The page asks
    /// here rather than opening a picker itself, which needs the window's
    /// handle.</summary>
    public async Task<(string Name, byte[] Data)?> ChooseFileAsync(string title, string extension)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = _main is not null ? WinRT.Interop.WindowNative.GetWindowHandle(_main)
            : _panel is not null ? WinRT.Interop.WindowNative.GetWindowHandle(_panel)
            : IntPtr.Zero;
        if (hwnd != IntPtr.Zero) WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
        picker.FileTypeFilter.Add(extension);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        using var stream = await file.OpenStreamForReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return (file.Name, memory.ToArray());
    }

    /// <summary>Quits magpie and the app: the backend this app started goes
    /// with it.</summary>
    public void Quit()
    {
        _channel?.Dispose();
        _panel?.Close();
        _main?.Close();
        _tray?.Dispose();
        _client?.Dispose();
        _backend?.Dispose();
        Exit();
    }

    /// <summary>A line in the log beside the app: a WinExe has no console to
    /// print to, and what went wrong has to be readable after the fact.</summary>
    public static void Log(string line)
    {
        Debug.WriteLine(line);
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "magpie-desktop.log"),
                $"{DateTime.Now:O} {line}\n");
        }
        catch (IOException) { }
    }

    /// <summary>A failure said where it is seen: a box, and a line in the log
    /// beside the app (a WinExe has no console to print to).</summary>
    private void Fail(Exception e)
    {
        Log("failed: " + e);
        var box = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "magpie couldn't start",
            Content = e.Message,
            CloseButtonText = "Close",
            XamlRoot = (_main?.Content as FrameworkElement)?.XamlRoot,
        };
        _ = box.ShowAsync();
    }
}
