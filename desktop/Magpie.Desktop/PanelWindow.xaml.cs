using Magpie.Desktop.Backend;
using Magpie.Desktop.Tray;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Magpie.Desktop;

/// <summary>
/// The quick panel: frameless, always on top, opened under the tray icon and
/// closed when it loses the focus, as the Wails app's panel was
/// (internal/gui/app.go's panelOptions). Its height is the page's, which asks
/// for it with /api/window/fit.
/// </summary>
public sealed partial class PanelWindow : Window
{
    private const int PanelWidth = 440;
    private int _height = 520;
    private double _scale = 1;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>Whether the panel is on the screen (not merely the window's
    /// object still existing).</summary>
    public bool Shown { get; private set; }

    public PanelWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(Drag);
        AppWindow.Title = "magpie";
        // acrylic, as Windows 11 gives a flyout: the desktop blurred behind
        // the panel, not a solid card over it
        try { SystemBackdrop = new DesktopAcrylicBackdrop(); }
        catch (Exception e) { App.Log("no acrylic backdrop: " + e.Message); }
        // the page sizes the panel by CSS pixels (/api/window/fit); the
        // window is in the monitor's, so its scale is kept here
        _scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        if (_scale <= 0) _scale = 1;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(PanelWidth * _scale), (int)(_height * _scale)));

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        // a popup: no task bar entry, no Alt-Tab, and rounded like one
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE) | NativeMethods.WS_EX_TOOLWINDOW);
        var round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated) HidePanel();
        };
        Closed += (_, _) => Shown = false;

        var app = App.CurrentApp;
        // the page says how tall it is, and the panel follows (FitToPageAsync)
        View.CoreWebView2Initialized += (_, _) =>
        {
            if (View.CoreWebView2 is { } core)
            {
                core.WebMessageReceived += (_, e) =>
                {
                    var text = e.TryGetWebMessageAsString();
                    if (text is not null && text.StartsWith("fit:", StringComparison.Ordinal) &&
                        int.TryParse(text[4..], out var h))
                    {
                        Fit(h);
                    }
                };
            }
        };
        View.Source = new Uri(app.Backend.Link + "&mode=panel");
        _ = DispatcherQueue.TryEnqueue(async () => await FitToPageAsync());
    }

    /// <summary>Opens under the cursor, which is over the tray icon when the
    /// click comes, inside the monitor's work area.</summary>
    public void ShowAtCursor()
    {
        var (cursor, work) = NativeMethods.CursorAndWorkArea();
        var width = (int)(PanelWidth * _scale);
        var height = (int)(_height * _scale);
        var x = Math.Clamp(cursor.X - width + 24, work.Left + 8, work.Right - width - 8);
        var y = Math.Clamp(cursor.Y + 6, work.Top + 8, work.Bottom - height - 8);
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        Activate();
        Shown = true;
    }

    public void HidePanel()
    {
        AppWindow.Hide();
        Shown = false;
    }

    /// <summary>
    /// Fits the panel to the page's own height. The page asks magpie's window
    /// for it (/api/window/fit) when the app is Wails'; here magpie is
    /// magpie web, whose host has no window to size (webHost.FitPanel is a
    /// no-op), so the app reads the height from the page itself and follows
    /// it — on every resize of its content, not once.
    /// </summary>
    public async Task FitToPageAsync()
    {
        if (View.CoreWebView2 is not { } core) return;
        // the page reports its height whenever it changes: a tab picked, a
        // list unrolled, a card refreshed
        await core.ExecuteScriptAsync("""
            (() => {
              if (window.__magpieDesktopFit) return;
              window.__magpieDesktopFit = true;
              const post = () => window.chrome.webview.postMessage(
                'fit:' + Math.ceil(document.documentElement.scrollHeight));
              new ResizeObserver(post).observe(document.documentElement);
              if (document.body) new ResizeObserver(post).observe(document.body);
              window.addEventListener('load', post);
              post();
            })();
            """);
    }

    /// <summary>The page said how tall it is (its postMessage above).</summary>
    public void Fit(int height)
    {
        height = Math.Clamp(height, 200, 1400);
        if (height == _height) return;
        _height = height;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(PanelWidth * _scale), (int)(height * _scale)));
    }

    /// <summary>The page's text size: the webview zooms, as a browser's does,
    /// on the page's own percentage (100, 110, 125, 150). WebView2 has no
    /// zoom of its own on this control, so the page's CSS zoom is set in it,
    /// which is what the page reads for its own layout.</summary>
    public void SetTextSize(int percent)
    {
        if (View.CoreWebView2 is not { } core) return;
        core.Settings.IsZoomControlEnabled = true;
        var z = Math.Clamp(percent / 100.0, 0.5, 2.0);
        _ = core.ExecuteScriptAsync(
            $"document.documentElement.style.zoom = {z.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }
}
