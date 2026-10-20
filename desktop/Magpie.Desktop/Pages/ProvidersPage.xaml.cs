using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Providers page: the model sources on this computer, each switched on
/// or off in place, a row opened to its editor, and the sheet that adds one
/// (a preset, a subscription's sign-in, a plugin's, or the user's own).
///
/// The pieces are partials beside this file: Rows (the list), Editor (a
/// provider's settings), Add (the sheet), SignIn (a subscription's sign-in).
/// It reads GET /api/providers, and every write answers with the whole list,
/// so what is drawn is what magpie wrote.
/// </summary>
public sealed partial class ProvidersPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    protected internal ProvidersJson Data = new();

    /// <summary>The provider the editor is open on, by id; null when none is.
    /// "add" is the add sheet being open on a new provider.</summary>
    protected internal string? Editing;

    /// <summary>Whether the add sheet is open.</summary>
    protected internal bool Adding;

    /// <summary>What the search keeps: the words typed, matched on the row's
    /// text and on what it doesn't show (its id, preset, addresses).</summary>
    protected internal string Query = "";

    /// <summary>Set while the page draws: a switch takes its state from the
    /// data then, and its Toggled is not a click.</summary>
    protected internal bool Drawing;

    /// <summary>Whether the turned-off providers are unrolled.</summary>
    private bool _offOpen;

    public ProvidersPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            Data = await App.CurrentApp.Client.GetAsync<ProvidersJson>("providers") ?? new ProvidersJson();
            Draw();
            // each source's own logo, read through the signed-in client and
            // drawn on the pass after it has it; the presets' too, for the
            // tiles the sheet shows
            await PageKitUI.LoadIconsAsync(
                Data.Providers.Select(p => p.Icon)
                    .Concat(Data.Presets.Select(p => p.Icon))
                    .Concat(Data.Plugins.Select(p => p.Icon))
                    .Concat(Data.Excluded.Select(p => p.AgentIcon)),
                Draw);
        }, Status, Busy);
    }

    /// <summary>Reads the list again and draws it: what a write answers with
    /// is the whole list, so a click draws what magpie wrote.</summary>
    protected internal async Task ReloadAsync(ProvidersJson? answer = null)
    {
        Data = answer ?? await App.CurrentApp.Client.GetAsync<ProvidersJson>("providers") ?? new ProvidersJson();
        Draw();
    }

    private void Find_TextChanged(object sender, TextChangedEventArgs e)
    {
        Query = Find.Text.Trim();
        if (!Drawing) DrawRows();
    }

    private void Draw()
    {
        Drawing = true;
        try
        {
            DrawBanners();
            DrawRows();
        }
        finally
        {
            Drawing = false;
        }
    }

    // ---- banners ---------------------------------------------------------

    private void DrawBanners()
    {
        Banners.Children.Clear();
        // the providers file is there but can't be read: magpie left it as it
        // is, and nothing is saved over it until it is fixed or moved
        if (Data.FileError is { Length: > 0 } why)
        {
            Banners.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Error,
                Title = L.T("Your providers file can't be read"),
                Message = L.T("magpie left it unchanged and lists no providers from it. Fix the file or move it aside, then reopen this page; until then, changes to providers are refused.") + "\n" + why,
            });
        }
        // a retired built-in whose accounts haven't moved to its plugin yet:
        // nothing serves them until they do, so it is never hidden
        foreach (var u in Data.Unmoved ?? [])
        {
            var bar = new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Title = L.T("{name}'s built-in subscription is retired, and its accounts haven't moved to its plugin yet").Replace("{name}", u.Name),
                Message = u.Error is { Length: > 0 }
                    ? L.T("Nothing serves them until they move: {error}").Replace("{error}", u.Error)
                    : L.T("Nothing serves them until they move. Moving keeps your accounts, models and agents as they are."),
            };
            var move = new Button { Content = L.T("Move to the plugin"), Padding = new Thickness(10, 2, 10, 2) };
            ToolTipService.SetToolTip(move, u.Package);
            move.Click += async (_, _) =>
            {
                move.IsEnabled = false;
                move.Content = L.T("Installing the plugin and checking each account…");
                try
                {
                    var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>("provider/move", new { id = u.Id });
                    await ReloadAsync(answer);
                }
                catch (Exception e) { Status.Text = e.Message; move.IsEnabled = true; }
            };
            bar.Content = move;
            Banners.Children.Add(bar);
        }
    }

    // ---- rows ------------------------------------------------------------

    protected void DrawRows()
    {
        Body.Children.Clear();
        // what the page shows instead of the list: a provider's editor while
        // one is open, the add sheet while it is. A new provider's editor
        // draws itself from the sheet's OpenNewEditor, so the frame only
        // redraws the sheet while it is up.
        if (Editing is { Length: > 0 } id && Data.Providers.FirstOrDefault(p => p.Id == id) is { } open)
        {
            DrawEditor(open);
            return;
        }
        if (Adding)
        {
            DrawAddSheet();
            return;
        }
        DrawList();
    }

    private void DrawList()
    {
        bool Match(ProviderJson p) =>
            Query.Length == 0 ||
            (p.Name + " " + p.Id + " " + p.Preset + " " + p.Host).Contains(Query, StringComparison.OrdinalIgnoreCase);

        var on = Data.Providers.Where(p => !p.Off && Match(p)).ToList();
        var off = Data.Providers.Where(p => p.Off && Match(p)).ToList();

        if (Data.Providers.Count == 0)
        {
            Body.Children.Add(new TextBlock
            {
                Text = L.T(Data.FileError is { Length: > 0 }
                    ? "magpie left it unchanged and lists no providers from it. Fix the file or move it aside, then reopen this page; until then, changes to providers are refused."
                    : "Add a provider"),
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        foreach (var p in on) Body.Children.Add(ProviderRow(p));
        if (off.Count > 0)
        {
            var fold = new Expander
            {
                Header = new TextBlock { Text = L.T("Turned off") + $" ({off.Count})", FontSize = 13 },
                IsExpanded = _offOpen || Query.Length > 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            var list = new StackPanel { Spacing = 2 };
            foreach (var p in off) list.Children.Add(ProviderRow(p));
            fold.Content = list;
            fold.Expanding += (_, _) => _offOpen = true;
            fold.Collapsed += (_, _) => _offOpen = false;
            Body.Children.Add(fold);
        }
    }

    /// <summary>One provider's row: its mark, name, what it runs on, the
    /// agents pointed at it, its key or account, and the switch.</summary>
    private Border ProviderRow(ProviderJson p)
    {
        var grid = new Grid { ColumnSpacing = 12, Padding = new Thickness(14, 10, 10, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Image
        {
            Width = 22,
            Height = 22,
            VerticalAlignment = VerticalAlignment.Center,
            Source = PageKitUI.IconCached(p.Icon),
        };
        grid.Children.Add(icon);

        var who = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(who, 1);
        grid.Children.Add(who);
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        nameRow.Children.Add(new TextBlock { Text = p.Name, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        if (p.Sponsored) nameRow.Children.Add(Badge(L.T("sponsored")));
        if (p.Off) nameRow.Children.Add(Badge(L.T("Switched off")));
        if (p.Move is { } mv && mv.State != "plugin") nameRow.Children.Add(Badge(L.T("Deprecated")));
        who.Children.Add(nameRow);
        who.Children.Add(new TextBlock
        {
            Text = SubLine(p),
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });

        // the agents pointed at it, a chip each, whose click changes that
        // agent's model
        var uses = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(uses, 2);
        foreach (var a in p.Agents.Where(a => a.Current))
        {
            var chip = new Button
            {
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            if (PageKitUI.IconCached(a.AgentIcon) is { } ai) content.Children.Add(new Image { Width = 14, Height = 14, Source = ai });
            chip.Content = content;
            ToolTipService.SetToolTip(chip, a.Group is { Length: > 0 } g
                ? L.T("{name} · {model}, through the routing group {group} — click to change").Replace("{name}", a.AgentName).Replace("{model}", a.Model ?? "").Replace("{group}", g)
                : L.T("{name} · {model} — click to change").Replace("{name}", a.AgentName).Replace("{model}", a.Model ?? ""));
            uses.Children.Add(chip);
        }
        if (uses.Children.Count > 0) grid.Children.Add(uses);

        // the key or the account, as the page's pill has it
        var pill = new Border
        {
            Padding = new Thickness(10, 3, 10, 3),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"],
            Child = new TextBlock { Text = KeyPill(p), FontSize = 11.5, MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis },
        };
        ToolTipService.SetToolTip(pill, KeyTip(p));
        Grid.SetColumn(pill, 3);
        grid.Children.Add(pill);

        var toggle = new ToggleSwitch
        {
            IsOn = !p.Off,
            OnContent = "",
            OffContent = "",
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(toggle, !p.Off
            ? L.T("On · switch it off and agents no longer get its models; its keys and settings are kept")
            : L.T("Off · switch it on and agents get its models again"));
        toggle.Toggled += async (_, _) =>
        {
            if (Drawing || toggle.IsOn == !p.Off) return;
            try
            {
                var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>(toggle.IsOn ? "provider/on" : "provider/off", new { id = p.Id });
                await ReloadAsync(answer);
            }
            catch (Exception e)
            {
                Status.Text = e.Message;
                toggle.IsOn = !p.Off;
            }
        };
        Grid.SetColumn(toggle, 4);
        grid.Children.Add(toggle);

        var chev = new FontIcon { Glyph = "", FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(chev, 5);
        grid.Children.Add(chev);

        // the row opens its editor; a right-click moves it among the ones on,
        // in the order requests try them
        var row = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
        grid.Tapped += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && AgentsPage.InsideControl(d)) return;
            Editing = p.Id;
            Draw();
        };
        grid.RightTapped += (_, e) => { e.Handled = true; ShowRowMenu(grid, p); };
        return row;
    }

    private static Border Badge(string text) => new()
    {
        Padding = new Thickness(6, 1, 6, 1),
        CornerRadius = new CornerRadius(4),
        Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
        Child = new TextBlock { Text = text, FontSize = 10.5, Opacity = 0.85 },
    };

    private string SubLine(ProviderJson p)
    {
        var parts = new List<string>();
        var a = p.Account;
        if (a is not null)
        {
            var inUse = (a.Logins ?? []).Where(l => l.On || l.Active).ToList();
            parts.Add(inUse.Count > 0 && inUse.All(l => l.Lapsed is { Length: > 0 })
                ? L.T("{user} is signed out").Replace("{user}", a.User)
                : L.T("signed in as {user}").Replace("{user}", a.User));
        }
        else if (p.Host.Length > 0) parts.Add(p.Host);
        var n = p.Models.Count(m => m.On);
        parts.Add(n > 0
            ? L.T(n == 1 ? "{n} model" : "{n} models").Replace("{n}", n.ToString())
            : p.Draws > 0
                ? L.T(p.Draws == 1 ? "{n} image model" : "{n} image models").Replace("{n}", p.Draws.ToString())
                : L.T(p.NoModels ? "usage only" : "no models exposed"));
        if (p.Move is { State: "plugin" }) parts.Add(L.T("run by its plugin"));
        return string.Join(" · ", parts);
    }

    /// <summary>The pill's word: the plan of the account signed in, the keys
    /// on, or the key masked; "no key" for a local server, "needs a key"
    /// when one is wanted.</summary>
    private string KeyPill(ProviderJson p)
    {
        var a = p.Account;
        if (a is not null)
        {
            var inUse = (a.Logins ?? []).Where(l => l.On || l.Active).ToList();
            if (inUse.Count > 0 && inUse.All(l => l.Lapsed is { Length: > 0 })) return L.T("Signed out");
            return AccountPlan(a);
        }
        if (p.VertexInfo is not null)
        {
            return !p.Ready ? L.T("needs a project")
                : p.VertexInfo.Impersonate is { Length: > 0 } im ? im
                : p.VertexInfo.Credentials is { Length: > 0 } c ? System.IO.Path.GetFileName(c) : "Google ADC";
        }
        if (p.Key.Set)
        {
            var on = p.KeyList.Where(k => !k.Off).ToList();
            return on.Count > 1 ? L.T("{n} keys").Replace("{n}", on.Count.ToString()) : on.FirstOrDefault()?.Name is { Length: > 0 } name ? name : p.Key.Masked;
        }
        return p.Ready ? L.T("no key") : L.T("needs a key");
    }

    private string KeyTip(ProviderJson p)
    {
        var a = p.Account;
        if (a is not null)
        {
            var inUse = (a.Logins ?? []).Where(l => l.On || l.Active).ToList();
            if (inUse.Count > 0 && inUse.All(l => l.Lapsed is { Length: > 0 }))
                return L.T("Signed out — add this account again to use it");
            return L.T("{agent} is signed in; its models are here for every other agent").Replace("{agent}", a.AgentName);
        }
        if (p.VertexInfo is not null)
        {
            return p.Ready
                ? L.T("Signed with your Google credentials") + " · " + (p.VertexInfo.Credentials ?? "Application Default Credentials")
                : L.T("Open the row and give your Google Cloud project's id");
        }
        return p.Key.Set ? L.T("API key {masked}").Replace("{masked}", p.Key.Masked)
            : p.Ready ? L.T("Local servers need no key")
            : L.T("Open the row and paste an API key");
    }

    /// <summary>The subscription an account stands for, as the page's
    /// accountPlan names it: "Claude Pro", "ChatGPT Plus".</summary>
    private static string AccountPlan(AccountJson a)
    {
        var vendor = a.Agent switch
        {
            "codex" or "chatgpt-api" => "ChatGPT",
            "copilot" => "GitHub",
            "claude" => "Claude",
            "cursor" => "Cursor",
            "grok" => "",
            "gemini" or "antigravity" => "",
            "zcode" => "",
            "workbuddy" => "",
            _ => "",
        };
        var plan = a.Plan is { Length: > 0 } pl ? char.ToUpperInvariant(pl[0]) + pl[1..] : "";
        if (vendor.Length == 0) return plan.Length > 0 ? plan : a.Agent;
        if (plan.Length == 0) return vendor;
        // "Xiaomi MiMo" and "MiMo 高阶" overlap in "MiMo": said once
        var words = vendor.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if ((plan.ToLowerInvariant() + " ").StartsWith(string.Join(' ', words[i..]).ToLowerInvariant() + " ", StringComparison.Ordinal))
                return string.Join(' ', words[..i].Append(plan));
        }
        return vendor + " " + plan;
    }

    private void ShowRowMenu(FrameworkElement anchor, ProviderJson p)
    {
        var onIds = Data.Providers.Where(x => !x.Off).Select(x => x.Id).ToList();
        var i = onIds.IndexOf(p.Id);
        var menu = new MenuFlyout();
        void Add(string key, bool enabled, int to)
        {
            var item = new MenuFlyoutItem { Text = L.T(key), IsEnabled = enabled };
            item.Click += async (_, _) => await ArrangeAsync(p, to);
            menu.Items.Add(item);
        }
        Add("Move to top", i > 0, 0);
        Add("Move up", i > 0, i - 1);
        Add("Move down", i >= 0 && i < onIds.Count - 1, i + 1);
        Add("Move to bottom", i >= 0 && i < onIds.Count - 1, onIds.Count - 1);
        menu.ShowAt(anchor);
    }

    /// <summary>The order providers are tried in: a model several serve goes
    /// to the higher one first.</summary>
    protected internal async Task ArrangeAsync(ProviderJson p, int to)
    {
        var on = Data.Providers.Where(x => !x.Off).Select(x => x.Id).ToList();
        var from = on.IndexOf(p.Id);
        if (from < 0 || to < 0 || to >= on.Count || to == from) return;
        on.RemoveAt(from);
        on.Insert(to, p.Id);
        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>("providers/arrange", new { order = on });
            await ReloadAsync(answer);
        }, Status, Busy);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        Adding = true;
        Editing = null;
        Draw();
    }
}
