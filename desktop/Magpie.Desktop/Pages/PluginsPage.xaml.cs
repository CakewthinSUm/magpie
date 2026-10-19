using System.Text.RegularExpressions;
using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Plugins page, drawn here rather than by the page magpie serves: the
/// plugins installed (each switched on or off, updated, removed), magpie's
/// own built-in plugins with their switches, and the market of OpenCode
/// plugins to install (GET /api/plugins and its builtin, market and search
/// routes; every write answers with the whole list, as the page's do).
/// </summary>
public sealed partial class PluginsPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    /// <summary>Which part is shown: "installed", "builtin" or "discover".</summary>
    private string _part = "installed";

    /// <summary>Discover's hand-typed package, kept with its field across a
    /// draw so a search under it doesn't lose what was typed.</summary>
    private string _spec = "";

    /// <summary>The words npm was last answered for, so its "nothing else"
    /// waits for the answer rather than coming with the keystroke.</summary>
    private string _searched = "";

    private PluginsJson? _installed;
    private List<BuiltinJson>? _builtins;
    private PluginMarketJson? _market;
    private readonly List<PluginHitJson> _hits = [];

    /// <summary>One write at a time: a card's buttons are off while it runs,
    /// and a second click is not a second install.</summary>
    private bool _busy;

    public PluginsPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // the part is as wide as the view it is in: a ScrollViewer measures
        // what it holds at an unbounded width, and a card's buttons would sit
        // past the window
        Scroller.SizeChanged += (_, args) => Body.Width = Math.Max(0, args.NewSize.Width - 2);
        // a part named on the command line (`plugins&tab=discover`), as the
        // page's own links name one; the bar's own answer says which it is
        var named = (e.Parameter as string ?? "").Split('&').FirstOrDefault(s => s.StartsWith("tab=", StringComparison.Ordinal)) is { Length: > 4 } t ? t[4..] : "";
        Tabs.SelectedItem = named switch
        {
            "builtin" => BuiltinTab,
            "discover" => DiscoverTab,
            _ => InstalledTab,
        };
        // the bar raises no change for what is picked before its template is
        // up, so the part is set here too
        _part = named is "builtin" or "discover" ? named : "installed";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            _installed = await App.CurrentApp.Client.GetAsync<PluginsJson>("plugins") ?? new PluginsJson();
            _builtins = await App.CurrentApp.Client.GetAsync<List<BuiltinJson>>("plugins/builtin") ?? [];
            Draw();
            // the market is asked after the page is drawn: npm is waited for,
            // and what is installed shouldn't wait on it
            _ = LoadMarketAsync();
        }, Status, Busy);
    }

    private async Task LoadMarketAsync()
    {
        try
        {
            _market = await App.CurrentApp.Client.GetAsync<PluginMarketJson>("plugins/market");
            if (_part == "discover") Draw();
        }
        catch (Exception e)
        {
            PageKitUI.Say(Problem, e.Message);
        }
    }

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        _part = ReferenceEquals(sender.SelectedItem, BuiltinTab) ? "builtin"
            : ReferenceEquals(sender.SelectedItem, DiscoverTab) ? "discover"
            : "installed";
        Draw();
    }

    private void Draw()
    {
        Body.Children.Clear();
        PageKitUI.Say(Problem, _installed?.Error);
        FootBar.Visibility = _part == "installed" ? Visibility.Visible : Visibility.Collapsed;
        switch (_part)
        {
            case "builtin":
                DrawBuiltins();
                break;
            case "discover":
                DrawDiscover();
                break;
            default:
                DrawInstalled();
                break;
        }
    }

    // ---- installed ----

    private void DrawInstalled()
    {
        var plugins = _installed?.Plugins ?? [];
        Foot.Text = _installed is { Bun: true }
            ? "Plugins run on Bun " + _installed.BunVersion
            : "Plugins run on Bun, downloaded with the first plugin";

        if (plugins.Count == 0)
        {
            Body.Children.Add(new TextBlock
            {
                Text = "No plugins yet. Install a subscription or a middleware from Discover; a subscription signs in from here.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                Margin = new Thickness(2, 8, 2, 8),
            });
            var go = new Button { Content = "Discover plugins", HorizontalAlignment = HorizontalAlignment.Left };
            go.Click += (_, _) =>
            {
                Tabs.SelectedItem = DiscoverTab;
                _part = "discover";
                Draw();
            };
            Body.Children.Add(go);
            return;
        }

        var outdated = plugins.Where(p => !p.Off && UpdateOut(p)).ToList();
        if (outdated.Count > 0)
        {
            var bar = new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Informational,
                Title = outdated.Count == 1
                    ? "A newer version of " + ShownName(outdated[0]) + " is out"
                    : $"Newer versions of {outdated.Count} plugins are out: " + string.Join(", ", outdated.Select(ShownName)),
            };
            var all = new Button { Content = $"Update all ({outdated.Count})", Margin = new Thickness(0, 6, 0, 0) };
            all.Click += async (_, _) => await ActAsync("update", new { });
            bar.Content = all;
            Body.Children.Add(bar);
        }

        foreach (var p in plugins.OrderBy(ShownName, StringComparer.CurrentCultureIgnoreCase))
        {
            Body.Children.Add(InstalledCard(p));
        }
    }

    /// <summary>One installed plugin's card: its picture, name and version,
    /// what it signs in to or does, and the buttons that change it.</summary>
    private Border InstalledCard(PluginEntryJson p)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = PageKitUI.IconOf(IconOf(p));
        if (icon is not null)
        {
            grid.Children.Add(new Image
            {
                Width = 28,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Top,
                Source = icon,
            });
        }
        else
        {
            // a plugin magpie has no picture for: a plug, in the same place
            grid.Children.Add(new FontIcon
            {
                Glyph = "",
                FontSize = 20,
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Top,
            });
        }

        // the text wraps rather than holding the card wider than the window:
        // a row of chips beside the name measured the card at its own width,
        // and the buttons went off the window's edge
        var who = new StackPanel { Spacing = 3 };
        Grid.SetColumn(who, 1);
        who.Children.Add(new TextBlock
        {
            Text = ShownName(p),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var facts = new List<string>();
        if (p.Version is { Length: > 0 } v) facts.Add("v" + v);
        if (UpdateOut(p))
        {
            facts.Add(p.Latest is { Length: > 0 } latest ? "v" + latest + " out" : "new commits");
        }
        else if (p.AutoUpdated is { } auto && auto.To == p.Version)
        {
            facts.Add("auto-updated");
        }
        var line = LineOf(p);
        if (line.Length > 0) facts.Add(line);
        who.Children.Add(new TextBlock
        {
            Text = string.Join(" · ", facts),
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        });
        if (p.Error is { Length: > 0 } why)
        {
            who.Children.Add(new TextBlock
            {
                Text = "Didn't load: " + why,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            });
        }
        grid.Children.Add(who);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetColumn(actions, 2);

        if (UpdateOut(p))
        {
            var update = new Button { Content = "Update", Style = PrimaryStyle() };
            update.Click += async (_, _) => await ActAsync("upgrade", new { spec = p.Spec });
            actions.Children.Add(update);
        }

        var toggle = new ToggleSwitch
        {
            IsOn = !p.Off,
            OnContent = "",
            OffContent = "",
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toggle.Toggled += async (_, _) =>
        {
            if (_busy) return;
            await ActAsync("off", new { spec = p.Spec, off = toggle.IsOn == false });
        };
        actions.Children.Add(toggle);

        var remove = new Button { Content = "Remove" };
        remove.Click += async (_, _) => await RemoveAsync(p);
        actions.Children.Add(remove);
        grid.Children.Add(actions);

        return new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = grid,
        };
    }

    /// <summary>What a plugin's row says under its name: what it signs in to,
    /// what of magpie it runs, and what it carries.</summary>
    private string LineOf(PluginEntryJson p)
    {
        if (p.Off) return "Off";
        var parts = new List<string>();
        if (p.Providers.Count > 0) parts.Add("Signs in to " + string.Join(", ", p.Providers));
        else if (p.Named && !p.IsMiddleware && !p.IsGateway && !p.IsAgent && !p.InMagpieOnly) parts.Add("Signs in to nothing magpie can use");
        if (p.IsGateway) parts.Add("a request hook in the gateway");
        else if (p.IsMiddleware) parts.Add("middleware in the gateway");
        if (p.IsAgent) parts.Add("an agent");
        if (p.InMagpieOnly && !p.IsMiddleware && !p.IsGateway && !p.IsAgent) parts.Add("runs in magpie itself");
        if (p.Page is { Title: { Length: > 0 } page }) parts.Add("the " + page + " page");
        if (p.Moved.Count > 0) parts.Add("serves " + string.Join(", ", p.Moved) + " in place of the built-in");
        if (p.Commit is { Length: > 0 } commit) parts.Add("at commit " + commit);
        return string.Join(" · ", parts);
    }

    /// <summary>A plugin's name as its card shows it: its package's, the
    /// market's, else the folder's last part.</summary>
    private string ShownName(PluginEntryJson p)
    {
        var package = PackageOf(p.Spec);
        var listed = _market?.Listings.FirstOrDefault(l => l.Package == package);
        if (listed is { Name.Length: > 0 }) return listed.Name;
        if (p.Package is { Length: > 0 } own) return own;
        return LabelOf(p.Spec);
    }

    /// <summary>Whether npm or a git repository has something newer than what
    /// is installed, as the page's Update button says it: a git plugin's
    /// repository at another commit than the one installed.</summary>
    private static bool UpdateOut(PluginEntryJson p)
    {
        if (p.Off) return false;
        if (p.Latest is { Length: > 0 } latest && p.Version is { Length: > 0 } version && Newer(latest, version)) return true;
        return p.Commit is { Length: > 0 } have && p.Head is { Length: > 0 } head && !SameCommit(have, head);
    }

    private async Task RemoveAsync(PluginEntryJson p)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Remove " + ShownName(p) + "?",
            Content = p.Moved.Count > 0
                ? string.Join(", ", p.Moved) + " runs on this plugin: removed, it stops working until the plugin is installed again. Its accounts are kept."
                : "Removes the plugin and what it installed; its sign-ins are kept until you sign out.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await ActAsync("remove", new { spec = p.Spec });
    }

    /// <summary>A plugin's picture: its own, else the market's for its
    /// package, else none.</summary>
    private string? IconOf(PluginEntryJson p)
    {
        if (p.Icon is { Length: > 0 } icon) return icon;
        var listed = _market?.Listings.FirstOrDefault(l => l.Package == PackageOf(p.Spec));
        return listed?.Icon ?? listed?.Npm?.Icon;
    }

    // ---- built-in ----

    private void DrawBuiltins()
    {
        Body.Children.Add(new TextBlock
        {
            Text = "Parts of magpie not everyone needs. Switched off, a part leaves the sidebar, its pages and Settings; magpie works the same either way.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Margin = new Thickness(2, 0, 2, 8),
        });

        var broken = _builtins?.FirstOrDefault(b => b.Broken is { Length: > 0 })?.Broken;
        if (broken is { Length: > 0 })
        {
            Body.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Error,
                Title = "plugins.yml can't be read",
                Message = "Every built-in plugin stays as it was: " + broken,
            });
        }

        foreach (var b in _builtins ?? [])
        {
            // one in use stays on: nothing else provides it
            var toggle = new ToggleSwitch
            {
                IsOn = b.On,
                OnContent = "",
                OffContent = "",
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = b.InUse == 0 && broken is not { Length: > 0 },
            };
            if (b.InUse > 0) ToolTipService.SetToolTip(toggle, "In use, so it stays on");
            var id = b.Id;
            toggle.Toggled += async (_, _) => await ActBuiltinAsync(id, toggle.IsOn);

            var lines = new List<string>();
            if (b.Sub is { Length: > 0 } sub) lines.Add(sub);
            if (b.InUse > 0) lines.Add(UseLine(b));
            if (b.On && b.Why is { Length: > 0 } why) lines.Add(why);
            Body.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = b.Name,
                Description = string.Join(" · ", lines),
                Control = toggle,
            }));
        }
    }

    /// <summary>What uses a built-in plugin, as its row says it: pluginrt's
    /// own sentence with the count in it ("{n}" for one or several).</summary>
    private static string UseLine(BuiltinJson b)
    {
        var use = b.Use is { Length: > 0 } u ? u : "In use";
        return use.Contains("{n}", StringComparison.Ordinal) ? use.Replace("{n}", b.InUse.ToString()) : use;
    }

    private async Task ActBuiltinAsync(string id, bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _builtins = await App.CurrentApp.Client.PostAsync<List<BuiltinJson>>("plugins/builtin", new { id, on });
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- discover ----

    /// <summary>What was typed into Discover's field: what the market's cards
    /// are narrowed to, and what npm is searched for.</summary>
    private string _find = "";

    private void DrawDiscover()
    {
        // the field narrows what is drawn under it; the list it narrows is
        // made again there, so typing never makes a second field
        var find = new AutoSuggestBox
        {
            PlaceholderText = "Search plugins and npm…",
            QueryIcon = null,
            Text = _find,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
        };
        find.TextChanged += (box, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            _find = box.Text.Trim();
            if (_find.Length < 2)
            {
                _hits.Clear();
                _searched = "";
                DrawResults(find);
                return;
            }
            _ = SearchAsync(_find, find);
        };
        DrawResults(find);
    }

    /// <summary>The cards under the field: what the market lists matching
    /// what was typed, npm's hits, and the field for a package typed by
    /// hand. The field itself is kept, and given the focus after a draw, so
    /// typing goes on where it was.</summary>
    private void DrawResults(AutoSuggestBox find)
    {
        Body.Children.Clear();
        Body.Children.Add(find);

        if (_market is null)
        {
            Body.Children.Add(new TextBlock { Text = "Looking for plugins…", Opacity = 0.7, Margin = new Thickness(2, 8, 2, 8) });
            return;
        }

        var findText = find.Text.Trim();
        var installed = (_market.State.Plugins ?? []);
        bool Match(PluginListingJson l) =>
            findText.Length == 0 ||
            (l.Name + " " + l.Package + " " + SummaryOf(l) + " " + string.Join(" ", l.Providers)).Contains(findText, StringComparison.OrdinalIgnoreCase);

        var listed = _market.Listings.Where(Match).ToList();
        var community = listed.Where(l => l.Community).ToList();
        if (community.Count > 0)
        {
            Body.Children.Add(PageKitUI.Heading("Written for magpie"));
            foreach (var l in community) Body.Children.Add(MarketCard(l));
        }
        var others = listed.Where(l => !l.Community).ToList();
        if (others.Count > 0)
        {
            Body.Children.Add(PageKitUI.Heading("More plugins"));
            foreach (var l in others) Body.Children.Add(MarketCard(l));
        }

        // npm's own search, for what the market doesn't list
        Body.Children.Add(PageKitUI.Heading("On npm"));
        Body.Children.Add(new TextBlock
        {
            Text = "OpenCode plugins and pi packages anyone published — read what one does before you install it.",
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 2, 6),
        });
        if (findText.Length < 2)
        {
            Body.Children.Add(new TextBlock { Text = "Type two letters or more to search npm", Opacity = 0.7, Margin = new Thickness(2, 0, 2, 8) });
        }
        else
        {
            var known = _market.Listings.Select(l => l.Package).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hits = _hits.Where(h => !known.Contains(h.Package) && !installed.Any(p => PackageOf(p.Spec).Equals(h.Package, StringComparison.OrdinalIgnoreCase))).ToList();
            if (hits.Count == 0)
            {
                // as the page has it: the answer is only "none" once npm has
                // given one for these words
                Body.Children.Add(new TextBlock
                {
                    Text = _searched == findText ? "Nothing else on npm" : "Searching npm…",
                    Opacity = 0.7,
                    Margin = new Thickness(2, 0, 2, 8),
                });
            }
            foreach (var h in hits)
            {
                Body.Children.Add(NpmCard(h));
            }
        }

        // a package, repository or folder typed by hand
        Body.Children.Add(PageKitUI.Heading("Have one in mind?"));
        var spec = new TextBox { PlaceholderText = "npm package, GitHub repo (github:owner/repo) or a folder", Text = _spec };
        spec.TextChanged += (box, _) => _spec = ((TextBox)box).Text;
        var install = new Button { Content = "Install" };
        install.Click += async (_, _) =>
        {
            var s = spec.Text.Trim();
            if (s.Length == 0) return;
            _spec = "";
            await ActAsync("add", new { spec = s });
        };
        var manual = new Grid { ColumnSpacing = 8, Margin = new Thickness(2, 0, 2, 8) };
        manual.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        manual.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        manual.Children.Add(spec);
        Grid.SetColumn(install, 1);
        manual.Children.Add(install);
        Body.Children.Add(manual);
    }

    private async Task SearchAsync(string q, AutoSuggestBox find)
    {
        try
        {
            var answer = await App.CurrentApp.Client.GetAsync<PluginSearchJson>("plugins/search?q=" + Uri.EscapeDataString(q));
            // an answer for words typed before the last ones isn't this list
            if (_part != "discover" || q != _find) return;
            _hits.Clear();
            if (answer is not null) _hits.AddRange(answer.Hits);
            _searched = q;
            DrawResults(find);
            find.Focus(FocusState.Programmatic);
        }
        catch (MagpieException e)
        {
            Status.Text = e.Message;
        }
    }

    /// <summary>A plugin the market lists: its name, what it does, what npm
    /// says of it, and Install (or what it is already).</summary>
    private Border MarketCard(PluginListingJson l)
    {
        var installed = _market?.State.Plugins.FirstOrDefault(p => PackageOf(p.Spec).Equals(l.Package, StringComparison.OrdinalIgnoreCase));
        var who = new StackPanel { Spacing = 3 };
        who.Children.Add(new TextBlock
        {
            Text = l.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var facts = new List<string>();
        if (l.Community) facts.Add("magpie community");
        if (l.Npm?.Version is { Length: > 0 } ver) facts.Add("v" + ver);
        if (installed is { Off: true }) facts.Add("off");
        if (l.Kind is { Length: > 0 } kind) facts.Add(kind);
        if (l.Replaces is { Length: > 0 }) facts.Add("replaces the built-in");
        who.Children.Add(new TextBlock
        {
            Text = (l.Npm?.Publisher is { Length: > 0 } publisher ? publisher : l.Package) +
                   (facts.Count > 0 ? " · " + string.Join(" · ", facts) : ""),
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
        });
        var summary = SummaryOf(l);
        if (summary.Length > 0)
        {
            who.Children.Add(new TextBlock { Text = summary, FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
        }

        var action = new Button { VerticalAlignment = VerticalAlignment.Top };
        if (installed is { Off: false })
        {
            action.Content = "Installed";
            action.IsEnabled = false;
        }
        else if (installed is { Off: true })
        {
            action.Content = "Switch on";
            action.Click += async (_, _) => await ActAsync("off", new { spec = installed.Spec, off = false });
        }
        else
        {
            action.Content = "Install";
            action.Style = PrimaryStyle();
            action.Click += async (_, _) => await ActAsync("add", new { spec = l.Package });
        }
        return CardWith(who, action, PageKitUI.IconOf(l.Icon ?? l.Npm?.Icon));
    }

    /// <summary>One of npm's search hits: the package, what npm says of it,
    /// and Install.</summary>
    private Border NpmCard(PluginHitJson h)
    {
        var who = new StackPanel { Spacing = 3 };
        who.Children.Add(new TextBlock
        {
            Text = h.Package,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var by = h.Publisher ?? "";
        if (h.Version is { Length: > 0 } v) by += (by.Length > 0 ? " · " : "") + "v" + v;
        if (by.Length > 0)
        {
            who.Children.Add(new TextBlock { Text = by, FontSize = 12, Opacity = 0.6, TextWrapping = TextWrapping.Wrap });
        }
        if (h.Description is { Length: > 0 } d)
        {
            who.Children.Add(new TextBlock { Text = d, FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
        }
        var install = new Button { Content = "Install", Style = PrimaryStyle(), VerticalAlignment = VerticalAlignment.Top };
        install.Click += async (_, _) => await ActAsync("add", new { spec = h.Package });
        return CardWith(who, install);
    }

    private Border CardWith(FrameworkElement who, FrameworkElement action, ImageSource? icon = null)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (icon is not null)
        {
            grid.Children.Add(new Image
            {
                Width = 28,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Top,
                Source = icon,
            });
        }
        else
        {
            grid.Children.Add(new FontIcon
            {
                Glyph = "",
                FontSize = 20,
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Top,
            });
        }
        Grid.SetColumn(who, 1);
        grid.Children.Add(who);
        Grid.SetColumn(action, 2);
        grid.Children.Add(action);
        return new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = grid,
        };
    }

    private static string SummaryOf(PluginListingJson l)
    {
        if (l.Summary is null) return l.Npm?.Description ?? "";
        return l.Summary.TryGetValue("en", out var en) ? en : l.Summary.Values.FirstOrDefault() ?? l.Npm?.Description ?? "";
    }

    // ---- writes ----

    /// <summary>
    /// A write against the plugin list, which answers with the whole list:
    /// the page is drawn from what magpie wrote, not from what was clicked.
    /// </summary>
    private async Task ActAsync(string op, object body)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _installed = await App.CurrentApp.Client.PostAsync<PluginsJson>("plugins/" + op, body) ?? _installed;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            Check.IsEnabled = false;
            try
            {
                // npm asked now; the answer carries the list as it then stands
                var checkedNow = await App.CurrentApp.Client.PostAsync<PluginCheckJson>("plugins/check", new { });
                if (checkedNow?.State is { } state) _installed = state;
                Draw();
            }
            finally
            {
                Check.IsEnabled = true;
            }
        }, Status, Busy);
        _busy = false;
    }

    // ---- small helpers ----

    /// <summary>A small word on a card, as the page's chips are: a version, a
    /// state, or what a plugin replaces.</summary>
    private static Border Chip(string text, bool up = false, bool soft = false)
    {
        var color = up ? "SystemFillColorAttentionBrush" : "TextFillColorSecondaryBrush";
        return new Border
        {
            Padding = new Thickness(6, 1, 6, 2),
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Opacity = soft ? 0.7 : 0.9,
                Foreground = Application.Current.Resources.TryGetValue(color, out var b) ? b as Brush : null,
            },
        };
    }

    /// <summary>The accent button style, for the one action a card is for.</summary>
    private static Style PrimaryStyle() =>
        (Style)Application.Current.Resources["AccentButtonStyle"];

    /// <summary>A plugin's package name, as the page's name(): a folder is its
    /// path whole, anything else is the spec without its version.</summary>
    private static string PackageOf(string spec)
    {
        if (IsPath(spec)) return spec;
        var i = spec.LastIndexOf('@');
        return i > 0 && spec.IndexOf('/', i) < 0 ? spec[..i] : spec;
    }

    /// <summary>A plugin added from a folder is known by the folder's name,
    /// not its whole path.</summary>
    private static string LabelOf(string spec)
    {
        var name = PackageOf(spec);
        if (!IsPath(name)) return name;
        var parts = name.TrimEnd('\\', '/').Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : name;
    }

    private static bool IsPath(string p) =>
        p.StartsWith("file://", StringComparison.Ordinal) || p.StartsWith('.') ||
        p.StartsWith('/') || p.StartsWith('\\') || Regex.IsMatch(p, @"^[A-Za-z]:[\\/]");

    /// <summary>Whether version a comes after b, in semver's order as the page
    /// reads it: a pre-release before its release, numbers as numbers. A
    /// version that isn't x.y.z is never newer.</summary>
    private static bool Newer(string a, string b)
    {
        static (int[] N, string Pre)? Parse(string v)
        {
            var m = Regex.Match(v.Trim(), @"^v?(\d+)\.(\d+)\.(\d+)(?:-([^+]*))?(?:\+.*)?$");
            if (!m.Success) return null;
            return ([int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)], m.Groups[4].Value);
        }
        if (Parse(a) is not { } x || Parse(b) is not { } y) return false;
        for (var i = 0; i < 3; i++)
        {
            if (x.N[i] != y.N[i]) return x.N[i] > y.N[i];
        }
        if (x.Pre == y.Pre) return false;
        if (x.Pre.Length == 0 || y.Pre.Length == 0) return x.Pre.Length == 0;
        var xs = x.Pre.Split('.');
        var ys = y.Pre.Split('.');
        for (var i = 0; i < xs.Length && i < ys.Length; i++)
        {
            var xd = int.TryParse(xs[i], out var xn);
            var yd = int.TryParse(ys[i], out var yn);
            if (xd && yd) { if (xn != yn) return xn > yn; }
            else if (xd != yd) return yd;
            else if (xs[i] != ys[i]) return string.CompareOrdinal(xs[i], ys[i]) > 0;
        }
        return xs.Length > ys.Length;
    }

    /// <summary>Whether two abbreviated commit hashes name the same commit.</summary>
    private static bool SameCommit(string a, string b) =>
        a.Length > 0 && b.Length > 0 &&
        (a.Length > b.Length ? a.StartsWith(b, StringComparison.Ordinal) : b.StartsWith(a, StringComparison.Ordinal));
}
