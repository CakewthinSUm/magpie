using Magpie.Desktop.Pages;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Color = Windows.UI.Color;

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
        // the page runs up into the title bar, as Windows' own apps do: the
        // NavigationView's top strip is the caption, and only its empty part
        // drags (the pane toggle and the name are still clicks)
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CaptionDragArea);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        // the caption follows the app's light or dark, and the system's accent
        // for its buttons, rather than a strip of its own above the page
        ApplyBackdrop();
        TitleBarTheme();
        // the pane's own words, in the language Settings picked (L): the
        // XAML has the English, which is the page's key for it
        TranslatePane();

        // a view named on the command line (`Magpie.Desktop.exe settings`),
        // else the page the reader left
        var args = Environment.GetCommandLineArgs();
        var asked = args.Length > 1 ? args[1] : "";
        var last = string.IsNullOrEmpty(asked) ? ViewMemory.Last : asked;
        Navigate(string.IsNullOrEmpty(last) ? "agents" : last);
    }

    /// <summary>
    /// The caption's own colors: the system's buttons (close, minimize,
    /// maximize) drawn in the theme's ink over the page's material, hovered
    /// and pressed in a wash of that ink — as Windows' own apps do, so no
    /// strip of its own sits above the page.
    /// </summary>
    private void TitleBarTheme()
    {
        var bar = AppWindow.TitleBar;
        var dark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
        var ink = dark ? Colors.White : Colors.Black;
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonForegroundColor = ink;
        bar.ButtonHoverForegroundColor = ink;
        bar.ButtonPressedForegroundColor = ink;
        bar.ButtonInactiveForegroundColor = dark ? Color.FromArgb(255, 160, 160, 160) : Color.FromArgb(255, 100, 100, 100);
        // the wash is the ink's own, at an alpha, so it reads on either material
        var r = ink.R; var g = ink.G; var b = ink.B;
        bar.ButtonHoverBackgroundColor = Color.FromArgb(dark ? (byte)24 : (byte)16, r, g, b);
        bar.ButtonPressedBackgroundColor = Color.FromArgb(dark ? (byte)36 : (byte)24, r, g, b);
    }

    /// <summary>
    /// The window's material, as Windows 11's own apps have one: Mica draws
    /// the desktop's wallpaper, blurred and tinted, behind the app — through
    /// the caption and the pane, which is why the page runs into the title
    /// bar. Where the system has no Mica (Windows 10, or a build before
    /// 22000), the window keeps a plain theme background, which the page
    /// draws over anyway.
    /// </summary>
    private void ApplyBackdrop()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
        catch (Exception e)
        {
            // a system without Mica: the plain background stands
            App.Log("no mica backdrop: " + e.Message);
        }
    }

    /// <summary>Opens a page by the view name the page's own links use
    /// ("usage&amp;tab=requests"): the page's name before the &amp;.</summary>
    public void Navigate(string view)
    {
        var key = view.Split('&')[0];
        if (key.Length == 0) key = "agents";
        // the whole view goes to the page, which reads a tab from it where
        // it has one ("settings&tab=about", "plugins&tab=discover")
        if (key == "settings")
        {
            Nav.SelectedItem = Nav.SettingsItem;
            Show(view);
            return;
        }
        foreach (var item in Nav.MenuItems)
        {
            if (item is NavigationViewItem { Tag: string tag } && tag == key)
            {
                Nav.SelectedItem = item;
                Show(view);
                return;
            }
        }
        // a page this window has no tab for (a community plugin's): Plugins
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string?)i.Tag == "plugins");
        Show("plugins");
    }

    /// <summary>
    /// Opens the view now. The NavigationView's SelectionChanged is not
    /// raised before its own template is up, which is how a view named on
    /// the command line (and the Settings item) reached no page at all.
    /// </summary>
    private void Show(string view)
    {
        var tag = view.Split('&')[0];
        ViewMemory.Last = tag;
        var page = PagesOf.Tag(tag);
        if (Pages.Content?.GetType() == page)
        {
            if (Pages.Content is WebHostPage web) web.Open(view);
            else if (Pages.Content is IRefreshable drawn) _ = drawn.Kit.Refresh();
            return;
        }
        Pages.Navigate(page, view, new SuppressNavigationTransitionInfo());
    }

    /// <summary>The pane's own words in the language in force: each item's
    /// text as XAML has it in English is its key in the page's translations.
    /// The page under it is walked the same way once it is drawn.</summary>
    private void TranslatePane() => L.Walk(Nav);

    /// <summary>Draws the page it is on again, for a change every page says
    /// (the language): the same view, read and drawn anew.</summary>
    public void Redraw()
    {
        TranslatePane();
        if (Pages.Content is IRefreshable drawn) _ = drawn.Kit.Refresh();
        else if (Pages.Content is WebHostPage web) web.Open(web.View);
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
        Show(tag);
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
        "library" => typeof(LibraryPage),
        "gateway" => typeof(GatewayPage),
        "routing" => typeof(RoutingPage),
        "plugins" => typeof(PluginsPage),
        "settings" => typeof(SettingsPage),
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
