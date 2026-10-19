using Magpie.Desktop.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Magpie.Desktop;

/// <summary>
/// The window, as a Windows app is laid out: a NavigationView whose pane is
/// on the left. The pane is expanded on a wide window and shows icons only on
/// a narrow one (PaneDisplayMode Auto), which is what Windows 11's own apps
/// do, and Settings is the pane's last item, where Windows puts it. The
/// window extends its content into the title bar, so the NavigationView's own
/// top strip — the pane toggle and the app's name — is the caption row.
///
/// The pages drawn here are Pages/*; a page not drawn yet is the page magpie
/// serves, in a WebView2 (Pages/WebHostPage).
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // the window keeps its own title bar: dragging it must not take the
        // NavigationView's clicks with it (a caption row over the whole view
        // swallows the page's)
        Title = "magpie";
        AppWindow.Title = "magpie";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 800));

        // a view named on the command line (`Magpie.Desktop.exe settings`),
        // else the page the reader left
        var args = Environment.GetCommandLineArgs();
        var asked = args.Length > 1 ? args[1] : "";
        var last = string.IsNullOrEmpty(asked) ? ViewMemory.Last : asked;
        Navigate(string.IsNullOrEmpty(last) ? "agents" : last);
    }

    /// <summary>Opens a page by the view name the page's own links use
    /// ("usage&amp;tab=requests"): the page's name before the &amp;.</summary>
    public void Navigate(string view)
    {
        var key = view.Split('&')[0];
        if (key.Length == 0) key = "agents";
        if (key == "settings")
        {
            Nav.SelectedItem = Nav.SettingsItem;
            return;
        }
        foreach (var item in Nav.MenuItems)
        {
            if (item is NavigationViewItem { Tag: string tag } && tag == key)
            {
                Nav.SelectedItem = item;
                return;
            }
        }
        // a page this window has no tab for (a community plugin's): Plugins
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string?)i.Tag == "plugins");
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.SelectedItem switch
        {
            NavigationViewItem { Tag: string t } => t,
            _ when ReferenceEquals(args.SelectedItem, sender.SettingsItem) => "settings",
            _ => "",
        };
        if (tag.Length == 0) return;
        ViewMemory.Last = tag;
        var page = PagesOf.Tag(tag);
        if (Pages.Content?.GetType() == page)
        {
            if (Pages.Content is WebHostPage web) web.Open(tag);
            else if (Pages.Content is IRefreshable drawn) _ = drawn.Kit.Refresh();
            return;
        }
        Pages.Navigate(page, tag, new SuppressNavigationTransitionInfo());
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        switch (Pages.Content)
        {
            case WebHostPage web: web.Open(web.View); break;
            case IRefreshable drawn: _ = drawn.Kit.Refresh(); break;
        }
    }
}

/// <summary>Which page a view name opens, drawn here or not.</summary>
internal static class PagesOf
{
    public static Type Tag(string view) => view switch
    {
        "agents" => typeof(AgentsPage),
        "sessions" => typeof(SessionsPage),
        "providers" => typeof(ProvidersPage),
        "usage" => typeof(UsagePage),
        // the pages not drawn here yet are the page magpie serves, in a
        // WebView2 (Pages/WebHostPage): each takes one of these away as it
        // is drawn (desktop/README.md)
        _ => typeof(WebHostPage),
    };
}

/// <summary>
/// The page the window last had open, kept across runs as the Wails app kept
/// it in the webview's localStorage (magpie.lastView).
/// </summary>
internal static class ViewMemory
{
    private static string _last = Read();

    public static string Last
    {
        get => _last;
        set
        {
            _last = value;
            try { File.WriteAllText(FilePath(), value); } catch (IOException) { }
        }
    }

    private static string FilePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "magpie");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "desktop-last-view");
    }

    private static string Read()
    {
        try { return File.Exists(FilePath()) ? File.ReadAllText(FilePath()).Trim() : ""; }
        catch (IOException) { return ""; }
    }
}
