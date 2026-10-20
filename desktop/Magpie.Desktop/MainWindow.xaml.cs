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
    /// <summary>Set while a row is picked in code, whose page Show opens
    /// itself: the SelectionChanged that comes with it is not a click.</summary>
    private bool _selecting;

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
        ApplyLook();
        // the pane's own words, in the language Settings picked (L): the
        // XAML has the English, which is the page's key for it
        TranslatePane();

        // the sidebar lists the open page's own tabs under it, as the page
        // does, and the page's strip of them steps aside while it does
        Nav.DisplayModeChanged += (_, _) => ApplyStrip();

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
        // the root's theme, else the system's: what the page beside it draws in
        var dark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
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
    /// The caption's ink and the window's material, for the theme in force:
    /// the app's own when Settings picked one, else Windows'. Applied at
    /// start, and again when Settings changes it — with the root's own theme,
    /// which is what a page drawn after it follows.
    /// </summary>
    public void ApplyLook(ElementTheme? theme = null)
    {
        // none is the look App holds, for a window made after Settings named one
        var wanted = theme ?? (App.Dark ? ElementTheme.Dark : ElementTheme.Default);
        if (Content is FrameworkElement root && root.RequestedTheme != wanted) root.RequestedTheme = wanted;
        ApplyBackdrop();
        TitleBarTheme();
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
        var key = TabsOf.Page(view);
        if (key.Length == 0) key = "agents";
        // the whole view goes to the page, which reads a tab from it where
        // it has one ("settings&tab=about", "plugins&tab=discover")
        if (ParentRow(key) is { } row)
        {
            Select(row, view);
            return;
        }
        // a page this window has no tab for (a community plugin's): Plugins
        Select(ParentRow("plugins"), "plugins");
    }

    /// <summary>
    /// Selects the pane's row for a view and opens it. Selecting a row raises
    /// SelectionChanged, which opens the page; the flag keeps the page from
    /// being opened twice.
    /// </summary>
    private void Select(NavigationViewItem? item, string view)
    {
        if (item is null) return;
        _selecting = true;
        try { Nav.SelectedItem = item; }
        finally { _selecting = false; }
        Show(view);
    }

    /// <summary>
    /// Opens the view now. The NavigationView's SelectionChanged is not
    /// raised before its own template is up, which is how a view named on
    /// the command line (and the Settings item) reached no page at all.
    ///
    /// A page already open is not read again for another of its tabs: its
    /// tab is picked, and only a view that names none (the row clicked in
    /// the pane) reads the page again, as the title bar's refresh does. A
    /// tab the view doesn't name is the one the page was last on.
    /// </summary>
    private void Show(string view)
    {
        var tag = TabsOf.Page(view);
        var named = TabsOf.Named(view);
        // the tab the page was last on, for a view that names none: the row
        // clicked in the pane opens the page as the reader left it
        var tab = named.Length > 0 ? named : TabMemory.Get(tag);
        var whole = tab.Length > 0 ? TabsOf.View(tag, tab) : tag;
        ViewMemory.Last = whole;
        // a tab picked is the one the page opens on from now
        if (named.Length > 0) TabMemory.Set(tag, named);
        var page = PagesOf.Tag(tag);
        if (Pages.Content?.GetType() == page)
        {
            switch (Pages.Content)
            {
                case ITabbed tabbed when tab.Length > 0:
                    // the same tab it is on: nothing to change
                    if (tabbed.Tab != tab) tabbed.SelectTab(tab);
                    break;
                case WebHostPage web:
                    web.Open(whole);
                    return;
                case IRefreshable drawn:
                    _ = drawn.Kit.Refresh();
                    return;
            }
            // the pane says where the reader is: the row of the tab on
            // screen, and only when the page has none, the page's own row
            SyncSelection(tag, (Pages.Content as ITabbed)?.Tab ?? "");
            return;
        }
        Pages.Navigate(page, whole, new SuppressNavigationTransitionInfo());
        AttachTabs();
        // a tab the page has none of (a stale one it remembers) falls back to
        // its own first, and the view follows it
        if (tab.Length > 0 && Pages.Content is ITabbed opened) ViewMemory.Last = TabsOf.View(tag, opened.Tab);
        SyncSelection(tag, (Pages.Content as ITabbed)?.Tab ?? "");
    }

    /// <summary>
    /// Takes the page just opened's tabs for the sidebar, and lists them.
    /// The page says when they change (a count read, an update found), and
    /// the sidebar's rows are drawn again then.
    /// </summary>
    private void AttachTabs()
    {
        if (Pages.Content is ITabbed tabbed)
        {
            tabbed.TabsChanged -= PageTabsChanged;
            tabbed.TabsChanged += PageTabsChanged;
        }
        RebuildSubs();
        ApplyStrip();
    }

    /// <summary>Every row of the pane this window made: its pages, and
    /// Settings at the foot of them.</summary>
    private IEnumerable<NavigationViewItem> Rows() => Nav.MenuItems.OfType<NavigationViewItem>();

    /// <summary>The pane's row for a page: the one its tabs hang under.</summary>
    private NavigationViewItem? ParentRow(string tag) => Rows().FirstOrDefault(i => (string?)i.Tag == tag);

    /// <summary>
    /// Picks the pane's row for where the reader is: the tab on screen when
    /// the page has one listed, else the page's own row. It is how the pane
    /// marks the tab in sight, as magpie's page marks it under the page's
    /// row. Selecting a row raises SelectionChanged, which opens what it
    /// names; the flag keeps this pick from opening it twice.
    /// </summary>
    private void SyncSelection(string tag, string tab)
    {
        NavigationViewItem? row = null;
        if (tab.Length > 0)
        {
            row = ParentRow(tag)?.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => string.Equals(i.Tag as string, TabsOf.View(tag, tab), StringComparison.OrdinalIgnoreCase));
        }
        row ??= ParentRow(tag);
        if (row is null) return;
        // its tabs are in sight, as the page's own row is on opening it
        if (row.MenuItems.Count > 0) row.IsExpanded = true;
        if (ReferenceEquals(Nav.SelectedItem, row)) return;
        _selecting = true;
        try { Nav.SelectedItem = row; }
        finally { _selecting = false; }
    }

    /// <summary>
    /// Lists the open page's own tabs in the sidebar under its row, as
    /// magpie's page does: each is a row of the item's own MenuItems, picked
    /// by the view it opens ("usage&amp;tab=requests"). A page with no tabs
    /// leaves its row as it was, and the rows of the page left behind are
    /// cleared, so one page's tabs never stand under another's.
    /// </summary>
    private void RebuildSubs()
    {
        // every row this window gave tabs to, cleared but the open page's
        foreach (var item in Rows())
        {
            item.MenuItems.Clear();
            item.IsExpanded = false;
        }
        if (Pages.Content is not ITabbed tabbed || tabbed.TabList.Count < 2) return;
        // the row the tabs hang under is the page's, not the tab's
        var tag = PagesOf.NameOf(Pages.Content.GetType());
        var parent = ParentRow(tag);
        if (parent is null) return;
        foreach (var t in tabbed.TabList)
        {
            var row = new NavigationViewItem
            {
                Content = L.T(t.Label),
                Tag = TabsOf.View(tag, t.Id),
            };
            // its English, for a language picked after it was made
            tabWords[row.Tag as string ?? ""] = t.Label;
            // what it lists, and what of it has an update, as the page's own
            // sidebar rows say them ("Installed · 3", "2 updates")
            if (t.Updates > 0)
            {
                // the accent colour the page's own "N updates" is written in
                row.InfoBadge = new InfoBadge
                {
                    Value = t.Updates,
                    Background = PageKitUI.Brush("AccentFillColorDefaultBrush"),
                    Foreground = PageKitUI.Brush("TextOnAccentFillColorPrimaryBrush"),
                };
                ToolTipService.SetToolTip(row, L.T("Newer versions of your plugins are out: update them under Installed"));
            }
            else if (t.Count is int count && count > 0)
            {
                row.InfoBadge = new InfoBadge { Value = count };
            }
            parent.MenuItems.Add(row);
        }
        parent.IsExpanded = true;
        // the NavigationView folds a row again as the click that opened the
        // page is handled, after this: the row is opened once more when the
        // click is done, so the tabs are in sight
        DispatcherQueue.TryEnqueue(() =>
        {
            if (parent.MenuItems.Count > 0 && Nav.DisplayMode != NavigationViewDisplayMode.Minimal) parent.IsExpanded = true;
        });
    }

    /// <summary>
    /// The page's own strip of tabs steps aside while the sidebar lists them
    /// in its place, as magpie's page hides it (app.css's side-nav rule); in
    /// a pane too narrow for the sidebar's rows, the strip comes back.
    /// </summary>
    private void ApplyStrip()
    {
        if (Pages.Content is not ITabbed tabbed) return;
        var listed = Nav.DisplayMode == NavigationViewDisplayMode.Expanded && tabbed.TabList.Count > 1;
        tabbed.Strip.Visibility = listed ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// The pane's own words in the language in force: each item's text as
    /// XAML has it in English is its key in the page's translations. The
    /// items are set here rather than left to L.Walk, which walks the visual
    /// tree — and the NavigationView makes no visual for an item until its
    /// own template is up, so a walk before that finds none of them and the
    /// pane stayed English. The English is kept from the first walk: a word
    /// already translated is not a key, so a second language would find none.
    /// </summary>
    private void TranslatePane()
    {
        foreach (var (item, key) in PaneKeys())
        {
            item.Content = L.T(key);
            // a row's own tabs are translated as they are made (RebuildSubs)
            if (item is not NavigationViewItem row) continue;
            foreach (var tab in row.MenuItems.OfType<NavigationViewItem>())
            {
                if (tab.Tag is not string tag || !tabWords.TryGetValue(tag, out var word)) continue;
                tab.Content = L.T(word);
            }
        }
    }

    /// <summary>The pane's own rows and headers, with the English each was
    /// made with — kept the first time they are seen.</summary>
    private List<(NavigationViewItemBase Item, string Key)> PaneKeys()
    {
        if (_paneKeys.Count > 0) return _paneKeys;
        foreach (var item in Rows()) if (item.Content is string text && text.Length > 0) _paneKeys.Add((item, text));
        foreach (var head in Nav.MenuItems.OfType<NavigationViewItemHeader>())
        {
            if (head.Content is string text && text.Length > 0) _paneKeys.Add((head, text));
        }
        return _paneKeys;
    }

    private readonly List<(NavigationViewItemBase Item, string Key)> _paneKeys = [];

    /// <summary>A tab's English, by the view it opens: its rows are made
    /// again with each page, so the word is kept here rather than with them.</summary>
    private readonly Dictionary<string, string> tabWords = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Draws the page it is on again, for a change every page says
    /// (the language): the same view, read and drawn anew, and the pane's
    /// rows and the page's tabs under them in the new words — the tabs are
    /// made again with the page's own, so they are walked here as the pane
    /// is, or a page's tab kept the word it was made in.</summary>
    public void Redraw()
    {
        TranslatePane();
        if (Pages.Content is IRefreshable drawn) _ = drawn.Kit.Refresh();
        else if (Pages.Content is WebHostPage web) web.Open(web.View);
        RebuildSubs();
        L.Walk(Nav);
    }

    /// <summary>The page on screen has drawn itself, and its tabs say what
    /// they list now: the sidebar's rows under it are drawn again, and the
    /// one for the tab in sight picked again — the rows are new ones, so the
    /// selection the old row had went with it.</summary>
    private void PageTabsChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, Pages.Content)) return;
        RebuildSubs();
        ApplyStrip();
        SyncSelection(PagesOf.NameOf(Pages.Content.GetType()), (Pages.Content as ITabbed)?.Tab ?? "");
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // the row a pick in code selected (SyncSelection), whose view Show has
        // opened or is opening: it is not a click
        if (_selecting) return;
        // the pane's own items carry their view as their Tag ("agents"); the
        // Settings item NavigationView makes carries "Settings", with a
        // capital, so the tag is matched case-insensitively — a capital it
        // didn't match sent the click to a page with no case for it, which is
        // the page in a WebView2 (the one place Settings came out as the web
        // page rather than this app's own)
        var tag = args.SelectedItem switch
        {
            NavigationViewItem { Tag: string t } => t.ToLowerInvariant(),
            _ when ReferenceEquals(args.SelectedItem, sender.SettingsItem) => "settings",
            _ => "",
        };
        if (tag.Length == 0) return;
        Show(tag);
    }

    /// <summary>
    /// A row of the pane clicked, a page's tab under it too: a tab's row
    /// carries its view as its tag ("usage&amp;tab=requests") and opens that
    /// tab, and a page's row opens the page as the reader left it. Only a
    /// tab is handled here: a page's own row comes through SelectionChanged,
    /// which this leaves to it.
    /// </summary>
    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem { Tag: string tag }) return;
        if (!tag.Contains("&tab=", StringComparison.Ordinal)) return;
        Show(tag.ToLowerInvariant());
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

    /// <summary>The view name a page is opened by: Tag's other way round,
    /// for the page on screen, whose row in the pane its tabs hang under.</summary>
    public static string NameOf(Type page) => page switch
    {
        _ when page == typeof(AgentsPage) => "agents",
        _ when page == typeof(SessionsPage) => "sessions",
        _ when page == typeof(ProvidersPage) => "providers",
        _ when page == typeof(UsagePage) => "usage",
        _ when page == typeof(LibraryPage) => "library",
        _ when page == typeof(GatewayPage) => "gateway",
        _ when page == typeof(RoutingPage) => "routing",
        _ when page == typeof(PluginsPage) => "plugins",
        _ when page == typeof(SettingsPage) => "settings",
        _ => "",
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

/// <summary>
/// The tab each page was last on, kept across runs as magpie's page keeps
/// them in the webview's localStorage (magpie.usageTab, magpie.libTab,
/// magpie.pluginTab, magpie.settingsTab): a page opened from the pane comes
/// back to the tab the reader left it on.
/// </summary>
internal static class TabMemory
{
    private static readonly Dictionary<string, string> _tabs = Read();

    public static string Get(string page) => _tabs.GetValueOrDefault(page, "");

    public static void Set(string page, string tab)
    {
        if (page.Length == 0 || tab.Length == 0) return;
        if (_tabs.GetValueOrDefault(page) == tab) return;
        _tabs[page] = tab;
        Write();
    }

    private static string FilePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "magpie");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "desktop-tabs");
    }

    /// <summary>One line a page: "usage=requests". A line that isn't one is
    /// left out rather than read as a tab.</summary>
    private static Dictionary<string, string> Read()
    {
        var tabs = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(FilePath())) return tabs;
            foreach (var line in File.ReadAllLines(FilePath()))
            {
                var at = line.IndexOf('=');
                if (at <= 0 || at == line.Length - 1) continue;
                tabs[line[..at].Trim()] = line[(at + 1)..].Trim();
            }
        }
        catch (IOException) { /* none kept: every page opens on its own first */ }
        return tabs;
    }

    private static void Write()
    {
        try { File.WriteAllLines(FilePath(), _tabs.Select(t => t.Key + "=" + t.Value)); }
        catch (IOException) { /* this time only */ }
    }
}
