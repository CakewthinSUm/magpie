using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// A page the app doesn't draw yet: magpie's own page, in a WebView2, on the
/// view asked for. It is the page a browser gets from `magpie web`, so every
/// rule it keeps (no native select, a click never moves it, the strings in
/// five languages) holds here too, unchanged — and magpie serves it without
/// knowing whether a browser or this app is on the other end.
/// </summary>
public sealed partial class WebHostPage : Page
{
    /// <summary>The view the page is on ("usage&amp;tab=requests").</summary>
    public string View { get; private set; } = "agents";

    public WebHostPage()
    {
        InitializeComponent();
        Web.NavigationCompleted += (_, _) => Busy.IsActive = false;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Open(e.Parameter as string is { Length: > 0 } view ? view : View);
    }

    /// <summary>Opens the page on a view: the page's own ?view=, which it
    /// reads at start as a reload does. The link carries the run's key, so
    /// the webview's own cookie jar signs it in as a browser's does.</summary>
    public void Open(string view)
    {
        View = view;
        var app = App.CurrentApp;
        if (app.Client.BaseUrl.Length == 0) return;
        Busy.IsActive = true;
        Web.Source = new Uri(app.Backend.Link + "&view=" + Uri.EscapeDataString(view));
    }

    /// <summary>
    /// Answers what magpie web's host can't, without magpie knowing: a folder
    /// picker the page asks for is Windows' own here (magpie web answers "in
    /// the browser magpie can't show a folder picker"), and the panel's
    /// height is read from the page rather than asked of magpie.
    /// </summary>
    public void Hook(DesktopChannel channel)
    {
        Web.CoreWebView2Initialized += (_, _) =>
        {
            var core = Web.CoreWebView2;
            if (core is null) return;

            core.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += async (_, e) =>
            {
                if (e.Request.Method != "POST" || !DesktopChannel.IsChoosePath(e.Request.Uri)) return;
                var deferral = e.GetDeferral();
                try
                {
                    var dir = await channel.ChooseFolderAsync!(DesktopChannel.ChooseTitle(e.Request.Uri));
                    e.Response = Answer(core, DesktopChannel.ChooseAnswer(dir), 200, "OK", "application/json");
                }
                catch (Exception ex)
                {
                    e.Response = Answer(core, ex.Message, 500, "Internal Server Error", "text/plain");
                }
                finally
                {
                    deferral.Complete();
                }
            };
        };
    }

    /// <summary>A response the webview takes: its bytes as the stream WebView2
    /// reads (Windows.Storage.Streams, not .NET's MemoryStream).</summary>
    private static Microsoft.Web.WebView2.Core.CoreWebView2WebResourceResponse Answer(
        Microsoft.Web.WebView2.Core.CoreWebView2 core, string body, int status, string reason, string mime)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        using (var w = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0)))
        {
            w.WriteBytes(bytes);
            w.StoreAsync().AsTask().GetAwaiter().GetResult();
        }
        stream.Seek(0);
        return core.Environment.CreateWebResourceResponse(stream, status, reason, "Content-Type: " + mime);
    }

    /// <summary>The page's own height: what it asks magpie's window for
    /// (/api/window/fit), which a browser's magpie web has no window to give.</summary>
    public async Task<int> ScrollHeightAsync()
    {
        if (Web.CoreWebView2 is not { } core) return 0;
        try
        {
            var json = await core.ExecuteScriptAsync(
                "Math.max(document.documentElement.scrollHeight, document.body ? document.body.scrollHeight : 0)");
            return int.TryParse(json, out var h) ? h : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
