using Magpie.Desktop.Backend;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Agents page, drawn as magpie's own page draws it (app.js's
/// renderAgents): one line of lead, then a card holding a compact row per
/// agent — its name, what it runs on, the model it starts on and the switch
/// that connects it to magpie. A row opens in place to its settings, and its
/// menu moves, hides or disconnects it. Below the card are the agents nothing
/// is set on (folded), the ones magpie knows but this computer lacks (with
/// their install commands) and the profiles.
///
/// It reads GET /api/state, and every write answers with the whole state, so
/// the page is drawn from what magpie wrote rather than from what was
/// clicked.
/// </summary>
public sealed partial class AgentsPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    private StateJson _state = new();
    private CliAnswerJson _cli = new();
    private List<InstallJson> _installs = [];

    /// <summary>The one row opened to its settings, by agent id; null when
    /// none is.</summary>
    private string? _open;

    /// <summary>Whether the agents nothing is set on are unrolled.</summary>
    private bool _showAll;

    /// <summary>Whether the agents hidden by hand are unrolled, under their
    /// own line.</summary>
    private bool _showHidden;

    /// <summary>The profile whose details are shown, by name.</summary>
    private string? _profileOpen;

    /// <summary>Set while the page draws: a switch takes its state from the
    /// data then, and its Toggled is not a click.</summary>
    private bool _drawing;

    /// <summary>Whether the CLI answer is still coming; the page asks again
    /// until it has every version.</summary>
    private bool _cliPending;

    public AgentsPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            _state = await App.CurrentApp.Client.GetAsync<StateJson>("state") ?? new StateJson();
            Draw();
            // the versions after the rows, as the page asks for them: asking
            // can be slow, and the rows shouldn't wait
            _ = LoadClisAsync();
            _ = LoadInstallsAsync();
            await PageKitUI.LoadIconsAsync(_state.Agents.Select(a => a.Icon), Draw);
        }, Status, Busy);
    }

    /// <summary>Reads the agents' CLIs, and again while some version is still
    /// being asked for.</summary>
    private async Task LoadClisAsync()
    {
        try
        {
            _cli = await App.CurrentApp.Client.GetAsync<CliAnswerJson>("agents/cli") ?? new CliAnswerJson();
        }
        catch (Exception)
        {
            return; // the rows draw without versions, as the page's do
        }
        _cliPending = _cli.Pending;
        if (IsLoaded) Draw();
        // a version still being asked for comes with the next read: the page
        // asks again until they are all in
        if (_cliPending && IsLoaded)
        {
            await Task.Delay(1500);
            if (IsLoaded) await LoadClisAsync();
        }
    }

    private async Task LoadInstallsAsync()
    {
        try
        {
            _installs = await App.CurrentApp.Client.GetAsync<List<InstallJson>>("agents/install") ?? [];
        }
        catch (Exception)
        {
            _installs = [];
        }
        if (IsLoaded) Draw();
    }

    /// <summary>Reads the state again and draws it: what a write answers with
    /// is the whole state, so a click draws what magpie wrote.</summary>
    private async Task ReloadAsync()
    {
        _state = await App.CurrentApp.Client.GetAsync<StateJson>("state") ?? new StateJson();
        Draw();
    }

    // ---- drawing ---------------------------------------------------------

    private void Draw()
    {
        _drawing = true;
        try
        {
            Body.Children.Clear();
            var arranged = Arranged();
            if (_state.Agents.Count == 0)
            {
                Body.Children.Add(EmptyState());
                return;
            }
            Body.Children.Add(Lead());
            if (_state.Agents.Count(UpdateOut) > 1) Body.Children.Add(UpdateAllBar());
            Body.Children.Add(AgentCard(arranged));
            if (_installs.Count > 0) Body.Children.Add(InstallFold());
            Body.Children.Add(Profiles());
        }
        finally
        {
            _drawing = false;
        }
    }

    /// <summary>The agents magpie knows and this computer lacks, with their
    /// vendors' install commands to copy, as the page lists them under the
    /// agents it found.</summary>
    private Expander InstallFold()
    {
        var fold = new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsExpanded = _state.Agents.Count == 0,
        };
        fold.Header = new TextBlock
        {
            Text = L.T("Install another agent ({n})").Replace("{n}", _installs.Count.ToString()),
            FontSize = 13,
        };
        var body = new StackPanel { Spacing = 8, Padding = new Thickness(0, 4, 0, 4) };
        foreach (var i in _installs)
        {
            var card = new StackPanel { Spacing = 4 };
            var who = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (PageKitUI.IconCached(i.Icon) is { } icon) who.Children.Add(new Image { Width = 18, Height = 18, Source = icon });
            who.Children.Add(new TextBlock { Text = i.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            if (i.Missing)
            {
                who.Children.Add(new TextBlock
                {
                    Text = L.T("CLI not found"),
                    FontSize = 11,
                    Opacity = 0.65,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }
            card.Children.Add(who);
            foreach (var c in i.Commands)
            {
                var line = new Grid { ColumnSpacing = 8 };
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var code = new TextBox
                {
                    Text = c.Command,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11.5,
                    TextWrapping = TextWrapping.NoWrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                line.Children.Add(code);
                var copy = new Button { Content = L.T("Copy"), Padding = new Thickness(8, 2, 8, 2), FontSize = 12 };
                copy.Click += (_, _) => Copy(c.Command);
                Grid.SetColumn(copy, 1);
                line.Children.Add(copy);
                card.Children.Add(line);
            }
            body.Children.Add(new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                Child = card,
            });
        }
        fold.Content = body;
        return fold;
    }

    /// <summary>The rows in view, and the rest folded: the ones hidden by
    /// hand, and the ones nothing is set on (as app.js's arrangeAgents).</summary>
    private (List<AgentJson> Shown, List<AgentJson> Folded, List<AgentJson> ByHand) Arranged()
    {
        var order = _state.Settings.AgentOrder ?? [];
        var hidden = new HashSet<string>(_state.Settings.AgentsHidden ?? []);
        int Rank(AgentJson a) { var i = order.IndexOf(a.Id); return i < 0 ? order.Count : i; }
        var all = _state.Agents.Select((a, i) => (a, i)).OrderBy(x => Rank(x.a)).ThenBy(x => x.i).Select(x => x.a).ToList();
        // with a switch to use, folding starts once one is on magpie, not for
        // a model an agent not connected has in its own files
        var anyUsed = all.Any(a => !hidden.Contains(a.Id) && Used(a));
        bool InView(AgentJson a) => !hidden.Contains(a.Id) && (!anyUsed || Used(a));
        return (all.Where(InView).ToList(), all.Where(a => !InView(a) && !hidden.Contains(a.Id)).ToList(), all.Where(a => !InView(a) && hidden.Contains(a.Id)).ToList());
    }

    /// <summary>Whether an agent has something set on it: connected, or a
    /// setting of its own that magpie isn't the source of.</summary>
    private bool Used(AgentJson a)
    {
        if (a.CliMissing) return a.Wired;
        if (a.Wired) return true;
        if (!Connectable(a) && a.Fields.Any(f => f.Value.Length > 0 && !IsTweak(f))) return true;
        return a.Launch is { Length: > 0 } && a.Fields.Count == 0 && string.IsNullOrEmpty(a.Import);
    }

    /// <summary>A field whose value magpie itself set, rather than the user
    /// (agent.tweak): an unconnected agent with only these has nothing set on
    /// it.</summary>
    private static bool IsTweak(FieldJson f) => f.Key == "effort" || f.Label == "effort";

    private Border Lead()
    {
        var parts = new List<string>();
        var anyWired = _state.Agents.Any(a => a.Wired);
        var text = anyWired
            ? L.T("Switch an agent on and the models you set up in magpie show up as the {magpie} provider in its own model list; or pick its model right here.")
            : L.T("Let your agents use the models you set up in magpie");
        if (!anyWired) text = L.T("Switch an agent on and your models show up as the magpie provider in its own model list, to change in the agent. Or pick a model right here: the agent is connected and starts on it. Switch it off and it goes back to how it was.");
        parts.Add(text.Replace("{magpie}", "magpie"));
        var row = new StackPanel { Spacing = 6 };
        row.Children.Add(new TextBlock
        {
            Text = parts[0],
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            FontSize = 13,
        });
        if (_state.Served == 0 && !_state.Agents.Any(a => a.Fields.Any(f => f.Options.Any(o => o.Ref is { Length: > 0 }))))
        {
            var add = new Button { Content = L.T("Add a provider"), HorizontalAlignment = HorizontalAlignment.Left };
            add.Click += (_, _) => App.CurrentApp.ShowMain("providers");
            row.Children.Add(add);
        }
        return new Border
        {
            Padding = new Thickness(2, 0, 2, 2),
            Child = row,
        };
    }

    private Border AgentCard((List<AgentJson> Shown, List<AgentJson> Folded, List<AgentJson> ByHand) arranged)
    {
        var stack = new StackPanel { Spacing = 0 };
        foreach (var a in arranged.Shown)
        {
            stack.Children.Add(AgentRow(a, inFold: false));
        }
        if (arranged.Folded.Count > 0 || arranged.ByHand.Count > 0)
        {
            stack.Children.Add(new Border
            {
                Height = new Thickness(1).Top,
                Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Margin = new Thickness(0, 4, 0, 0),
            });
            stack.Children.Add(FoldFoot(arranged));
        }
        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = stack,
        };
    }

    /// <summary>The card's foot: what is folded, and Manage.</summary>
    private Grid FoldFoot((List<AgentJson> Shown, List<AgentJson> Folded, List<AgentJson> ByHand) arranged)
    {
        var grid = new Grid { Padding = new Thickness(14, 8, 8, 8), ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var more = new Button
        {
            Content = MoreLabel(arranged),
            Background = null,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        more.Click += (_, _) => { _showAll = !_showAll; if (_showAll) _showHidden = true; Draw(); };
        grid.Children.Add(more);

        var manage = new Button
        {
            Content = L.T("Manage agents"),
            Background = null,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
        };
        manage.Click += (_, _) => _ = ManageAsync();
        Grid.SetColumn(manage, 1);
        grid.Children.Add(manage);
        return grid;
    }

    private string MoreLabel((List<AgentJson> Shown, List<AgentJson> Folded, List<AgentJson> ByHand) arranged)
    {
        if (_showAll) return L.T("Show less");
        var n = arranged.Folded.Count + arranged.ByHand.Count;
        return arranged.Folded.Count == 0
            ? L.T("Hidden ({n})").Replace("{n}", arranged.ByHand.Count.ToString())
            : L.T("Show {n} more").Replace("{n}", n.ToString());
    }

    /// <summary>One agent's row: the compact line, and its settings when it is
    /// the one opened.</summary>
    private FrameworkElement AgentRow(AgentJson a, bool inFold)
    {
        var outer = new StackPanel { Spacing = 0 };
        var line = new Grid { Padding = new Thickness(14, 10, 10, 10), ColumnSpacing = 12 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // icon
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // link
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // model
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // switch

        // the agent's own mark, which is also the row's handle: a right-click
        // on it opens the menu the page's does
        var icon = new Image
        {
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Top,
            Source = PageKitUI.IconCached(a.Icon),
        };
        ToolTipService.SetToolTip(icon, a.Path);
        icon.RightTapped += (_, e) => { e.Handled = true; ShowRowMenu(icon, a, inFold); };
        line.Children.Add(icon);

        var who = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(who, 1);
        line.Children.Add(who);
        who.Children.Add(NameLine(a, inFold));
        who.Children.Add(StatusLine(a));

        // the link to the settings, as the page's row has: Tiers for Claude
        // Code, Models when the row's list is the menu, Details otherwise
        if (!a.CliMissing)
        {
            var link = new Button
            {
                Content = LinkLabel(a),
                Background = null,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            };
            link.Click += (_, _) => { _open = _open == a.Id ? null : a.Id; Draw(); };
            Grid.SetColumn(link, 2);
            line.Children.Add(link);
        }

        // the model it starts on, one click away
        if (!a.CliMissing && ConnectKind(a) == "ok" && !ListsMenu(a))
        {
            var start = StartField(a);
            if (start is not null)
            {
                var pick = ModelButton(a, start);
                Grid.SetColumn(pick, 3);
                line.Children.Add(pick);
            }
        }

        // the switch, or what stands in its place
        var right = SwitchCell(a, inFold);
        if (right is not null)
        {
            Grid.SetColumn(right, 4);
            line.Children.Add(right);
        }

        line.RightTapped += (_, e) => { e.Handled = true; ShowRowMenu(line, a, inFold); };
        line.Tapped += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && InButton(d)) return;
            if (a.CliMissing) return;
            _open = _open == a.Id ? null : a.Id;
            Draw();
        };
        outer.Children.Add(line);

        // opened: its settings, on a line of their own under the name
        if (!a.CliMissing && _open == a.Id) outer.Children.Add(DetailPanel(a));

        // the rows are told apart by a hairline, as the page's list rows are
        var box = new Border { Child = outer, BorderThickness = new Thickness(0, 0, 0, 0) };
        return box;
    }

    /// <summary>Whether an element is a button or inside one, so a click on it
    /// is the button's, not the row's.</summary>
    private static bool InButton(DependencyObject? d) => InsideControl(d);

    /// <summary>Whether an element is a control or inside one, so a click on
    /// it is the control's, not the row's. Shared with the Providers page's
    /// rows, which read it the same way.</summary>
    internal static bool InsideControl(DependencyObject? d)
    {
        for (var at = d; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is Button or ToggleSwitch or CheckBox or HyperlinkButton or TextBox) return true;
        }
        return false;
    }

    private string LinkLabel(AgentJson a) =>
        a.Id == "claude" ? L.T("Tiers") : a.Models is not null && !ListsMenu(a) ? L.T("Models") : L.T("Details");

    /// <summary>The name, its CLI version, the update it is behind, and the
    /// app beside it.</summary>
    private StackPanel NameLine(AgentJson a, bool inFold)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock
        {
            Text = a.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (inFold)
        {
            var show = new Button
            {
                Content = L.T("Show"),
                Padding = new Thickness(8, 1, 8, 1),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            show.Click += async (_, _) => await SetHiddenAsync(a, false);
            row.Children.Add(show);
        }
        if (a.CliMissing)
        {
            row.Children.Add(new TextBlock
            {
                Text = L.T("CLI not found"),
                FontSize = 11.5,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return row;
        }
        var c = _cli.Agents.GetValueOrDefault(a.Id);
        if (c is null) return row;
        if (c.Version is { Length: > 0 } v)
        {
            var ver = new TextBlock
            {
                Text = c.App is { Length: > 0 } ? L.T("CLI {v}").Replace("{v}", v) : v,
                FontSize = 11.5,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(ver, c.Via is null
                ? L.T("{agent} {v} · magpie can't tell how it was installed — update it the way you installed it").Replace("{agent}", a.Name).Replace("{v}", v)
                : c.Update
                    ? L.T("{agent} {v} is installed · {latest} is out").Replace("{agent}", a.Name).Replace("{v}", v).Replace("{latest}", c.Latest ?? "")
                    : L.T("{agent} {v} · up to date").Replace("{agent}", a.Name).Replace("{v}", v));
            row.Children.Add(ver);
            if (c.OffPath && c.Path is { Length: > 0 } path)
            {
                var off = new TextBlock
                {
                    Text = L.T("Not on your terminal's PATH"),
                    FontSize = 11.5,
                    Opacity = 0.6,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTipService.SetToolTip(off, L.T("magpie found {agent} {v} at {path}. That folder is one magpie looks in, but your terminal's PATH doesn't have it, so a terminal won't find {agent} there. If it was left behind by an uninstall, delete it.")
                    .Replace("{agent}", a.Name).Replace("{v}", v).Replace("{path}", path));
                row.Children.Add(off);
            }
        }
        if (c.App is { Length: > 0 } app)
        {
            var appTag = new TextBlock
            {
                Text = L.T("App {v}").Replace("{v}", app),
                FontSize = 11.5,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(appTag, L.T("The {agent} app {v}. It reads the same settings as the CLI: connecting {agent} here connects both")
                .Replace("{agent}", a.Name).Replace("{v}", app));
            row.Children.Add(appTag);
        }
        if (c.Update)
        {
            var up = new Button
            {
                Content = L.T("Update to {v}").Replace("{v}", c.Latest ?? ""),
                Padding = new Thickness(8, 1, 8, 1),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            };
            ToolTipService.SetToolTip(up, c.Command is { Length: > 0 } cmd
                ? L.T("Updates with {cmd}").Replace("{cmd}", cmd)
                : L.T("Update to {v}").Replace("{v}", c.Latest ?? ""));
            up.Click += async (_, _) => await UpdateCliAsync(a, up);
            row.Children.Add(up);
        }
        return row;
    }

    /// <summary>What the row says under the name: whether it goes through
    /// magpie, and what it runs on when it doesn't.</summary>
    private StackPanel StatusLine(AgentJson a)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 7,
            Height = 7,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = (Brush)Application.Current.Resources[a.Wired ? "SystemFillColorSuccessBrush" : "TextFillColorTertiaryBrush"],
        };
        row.Children.Add(dot);
        var text = new TextBlock
        {
            Text = StatusSaid(a),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = a.Drift is not null ? 1 : 0.8,
        };
        if (a.Drift is not null) text.Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
        row.Children.Add(text);
        if (a.Drift is not null && ConnectKind(a) == "ok")
        {
            var fix = new Button
            {
                Content = L.T("Apply again"),
                Padding = new Thickness(8, 1, 8, 1),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            fix.Click += async (_, _) => await ReapplyAsync(a);
            row.Children.Add(fix);
        }
        return row;
    }

    private string StatusSaid(AgentJson a)
    {
        if (a.CliMissing) return L.T("{agent}'s settings are still here ({path}), but its command-line program isn't found: it may have been uninstalled. Install another agent, below the list, has its install command.")
            .Replace("{agent}", a.Name).Replace("{path}", a.Path);
        if (a.Drift is { } d)
        {
            var want = a.Fields.FirstOrDefault(f => f.Key == d.Field)?.Options.FirstOrDefault(o => o.Value == d.Want)?.Label ?? d.Want;
            return L.T(DriftWhy(d.Kind)).Replace("{agent}", a.Name).Replace("{model}", want);
        }
        var kind = ConnectKind(a);
        if (kind == "no") return L.T(NativeOnly(a.Id) ?? "");
        if (kind == "link")
        {
            return a.Added == true
                ? L.T("Added · magpie is a provider in {agent}").Replace("{agent}", a.Name)
                : L.T("Not added · {agent} asks to add magpie when its link opens").Replace("{agent}", a.Name);
        }
        if (kind == "empty") return L.T("Add a key or a subscription first; then there are models to connect");
        if (!a.Wired)
        {
            if (a.Native is { } n && n.Provider != "disconnected" && n.Detail is { Length: > 0 }) return L.T(n.Detail);
            var src = a.Source ?? "";
            if (a.Id == "agy") return L.T("Not connected · once connected, start it with magpie's command");
            if (a.Failover) return L.T("Not connected · goes through magpie only to fail over to your other ChatGPT accounts");
            if (src == "sub") return L.T("Not connected · now on its Claude subscription");
            if (src == "chatgpt") return L.T("Not connected · now signed in with ChatGPT · OpenAI's models stay available once connected");
            if (src == "key") return L.T("Not connected · now on an API key of its own");
            if (src.StartsWith("providers:", StringComparison.Ordinal)) return L.T("Not connected · now has {n} providers of its own").Replace("{n}", src[10..]);
            return L.T("Not connected · {agent} uses its own settings").Replace("{agent}", a.Name);
        }
        if (a.Native is not null) return ConnectSaid(a);
        if (a.Stale > 0)
        {
            return a.Id == "claude" ? L.T("Connected · new sessions take it")
                : L.T("Connected · takes effect once {agent} is reopened").Replace("{agent}", a.Name);
        }
        if (ListsMenu(a) && a.Models is not null) return ConnectSaid(a);
        if (a.Id == "claude") return L.T("Connected · switch the opus · sonnet · haiku tiers in /model");
        if (NoPicker(a.Id)) return L.T("Connected · starts on the model picked here");
        var at = PicksIn(a.Id);
        if (at is not null && a.Models is not null) return L.T("Connected · {n} models in {agent}'s {cmd}").Replace("{n}", a.Models.Shown.ToString()).Replace("{agent}", a.Name).Replace("{cmd}", at);
        return ConnectSaid(a);
    }

    private string ConnectSaid(AgentJson a)
    {
        if (!a.Wired) return L.T("Not connected · {agent} uses its own settings").Replace("{agent}", a.Name);
        if (ListsMenu(a) && a.Models is not null) return L.T("Connected · {n} models in {agent}'s model menu").Replace("{n}", a.Models.Shown.ToString()).Replace("{agent}", a.Name);
        var at = PicksIn(a.Id);
        return at is not null
            ? L.T("Connected · pick magpie's models with {cmd} in {agent}").Replace("{cmd}", at).Replace("{agent}", a.Name)
            : L.T("Connected · magpie's models are in {agent}'s model list").Replace("{agent}", a.Name);
    }

    private static string DriftWhy(string kind) => kind switch
    {
        "replaced" => "{agent} was switched off {model} outside magpie",
        "bypassed" => "{agent} was used without going through magpie — restart it after applying",
        "unreachable" => "{agent} is set up, but nothing answers at the address it reaches magpie by",
        "proxied" => "{agent} reaches magpie through a proxy that doesn't pass its requests on",
        _ => "{agent} no longer goes through magpie — its config was changed",
    };

    private static string? NativeOnly(string id) => id switch
    {
        "cursor" => "Uses only Cursor's own models, so magpie can't connect it",
        "copilot" => "Uses only GitHub Copilot's own models, so magpie can't connect it",
        "goose" => "Uses only Goose's own providers, so magpie can't connect it",
        "devin" => "Uses only Devin's own models, so magpie can't connect it",
        _ => null,
    };

    /// <summary>Agents that pick no model once started: the one they start on
    /// stays in the row.</summary>
    private static bool NoPicker(string id) => id is "gemini" or "hermes" or "morph" or "agy" or "muse";

    /// <summary>Where an agent picks a model itself, for the words under it.</summary>
    private static string? PicksIn(string id) => id switch
    {
        "codex" or "claude" or "pi" or "droid" or "kimi" or "grok" or "minimax-code" or "dsh" or "omp" or "qwen" or "mimocode" => "/model",
        "opencode" or "deveco" => "/models",
        "crush" => "ctrl+p",
        _ => null,
    };

    /// <summary>Agents whose own model menu is the list picked here (Claude
    /// Desktop, Cursor Private Inference), or that the server says list
    /// magpie's models.</summary>
    private static bool ListsMenu(AgentJson a) => a.Menu || a.Id is "claude-desktop" or "cursor-local";

    // ---- connecting ------------------------------------------------------

    /// <summary>What the row's switch is: "ok" (a switch), "empty" (a switch
    /// for when magpie has models), "no" (never), "link" (an app magpie is
    /// added to by a link of its own), "" (none).</summary>
    private string ConnectKind(AgentJson a)
    {
        if (a.CliMissing) return "no";
        if (!string.IsNullOrEmpty(a.Import)) return "link";
        if (Connectable(a)) return "ok";
        if (NativeOnly(a.Id) is not null) return "no";
        if (a.Fields.Count > 0 && !AnyMagpieModels()) return "empty";
        return "";
    }

    private static bool Connectable(AgentJson a) =>
        string.IsNullOrEmpty(a.Import) && (ConnectField(a) is not null || a.Models?.Listed > 0);

    /// <summary>The field magpie is picked in: the first, or Gemini CLI's
    /// model, its first being how it signs in.</summary>
    private static FieldJson? ConnectField(AgentJson a) =>
        a.Fields.FirstOrDefault(f => f.Options.Any(o => !string.IsNullOrEmpty(o.Ref)))
        ?? a.Fields.FirstOrDefault(f => f.Options.Any(o => o.Value == "magpie"));

    /// <summary>The field an agent's model is picked in (Gemini CLI's model,
    /// not how it signs in).</summary>
    private static FieldJson? StartField(AgentJson a) =>
        a.Fields.FirstOrDefault(f => f.Key == "model" && f.Options.Count > 0) ?? ConnectField(a);

    private bool AnyMagpieModels() =>
        _state.Agents.Any(a => ConnectField(a)?.Options.Any(o => !string.IsNullOrEmpty(o.Ref)) == true);

    /// <summary>The row's rightmost cell: the switch that connects it, what
    /// stands in for one, or nothing.</summary>
    private FrameworkElement? SwitchCell(AgentJson a, bool inFold)
    {
        var kind = ConnectKind(a);
        if (inFold) return null;
        if (a.CliMissing) return null;
        if (kind == "no") return null;
        if (kind == "link")
        {
            var add = new Button
            {
                Content = a.Added == true ? "magpie" : L.T("Add magpie"),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
            };
            add.Click += async (_, _) =>
            {
                try { await PageKitUI.OpenUrl(a.Import!); }
                catch (Exception e) { Status.Text = e.Message; }
            };
            return add;
        }
        if (kind == "empty")
        {
            var add = new Button { Content = L.T("Add a provider"), VerticalAlignment = VerticalAlignment.Center };
            add.Click += (_, _) => App.CurrentApp.ShowMain("providers");
            return add;
        }
        if (kind != "ok") return null;
        var toggle = new ToggleSwitch
        {
            IsOn = a.Wired,
            OnContent = "",
            OffContent = "",
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(toggle, a.Wired
            ? L.T("Connected · switch off to put back what {agent} had before magpie").Replace("{agent}", a.Name)
            : L.T("Switch on and {agent}'s model list gets magpie's models").Replace("{agent}", a.Name));
        toggle.Toggled += async (_, _) =>
        {
            if (_drawing) return;
            if (toggle.IsOn == a.Wired) return;
            toggle.IsEnabled = false;
            try
            {
                if (toggle.IsOn) await ConnectAsync(a);
                else await DisconnectAsync(a);
            }
            finally { toggle.IsEnabled = true; }
        };
        return toggle;
    }

    private async Task ConnectAsync(AgentJson a)
    {
        await PageKit.RunAsync(async () =>
        {
            _state = await App.CurrentApp.Client.PostAsync<StateJson>("agents/connect/" + Uri.EscapeDataString(a.Id)) ?? _state;
            Draw();
            Status.Text = _state.Notice ?? L.T("Connected · magpie's models are in {agent}'s model list").Replace("{agent}", a.Name);
        }, Status, Busy);
    }

    /// <summary>Asks first, showing what disconnecting changes in the agent's
    /// files (GET /api/agents/preview/{id}), as the page's dialog does.</summary>
    private async Task DisconnectAsync(AgentJson a)
    {
        var preview = new StackPanel { Spacing = 8, MaxWidth = 620 };
        preview.Children.Add(new TextBlock
        {
            Text = L.T("magpie takes out everything it wrote into {agent}'s config — its endpoint, key, models and effort — and puts back the settings {agent} had before. magpie's providers and accounts stay as they are.")
                .Replace("{agent}", a.Name),
            TextWrapping = TextWrapping.Wrap,
        });
        var changes = new StackPanel { Spacing = 4 };
        preview.Children.Add(changes);
        var body = new ScrollViewer { Content = changes, MaxHeight = 280 };
        preview.Children.Add(body);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L.T("Disconnect {agent} from magpie?").Replace("{agent}", a.Name),
            Content = preview,
            PrimaryButtonText = L.T("Disconnect and restore"),
            CloseButtonText = L.T("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        // what changes, read while the dialog is up; a preview that can't be
        // had says so and leaves the words above in its place
        _ = LoadPreviewAsync(a, changes);
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            Draw();
            return;
        }
        await PageKit.RunAsync(async () =>
        {
            _state = await App.CurrentApp.Client.PostAsync<StateJson>("agents/disconnect/" + Uri.EscapeDataString(a.Id)) ?? _state;
            Draw();
            Status.Text = L.T("{agent} no longer goes through magpie; its own settings are back").Replace("{agent}", a.Name);
        }, Status, Busy);
    }

    private async Task LoadPreviewAsync(AgentJson a, StackPanel into)
    {
        try
        {
            var answer = await App.CurrentApp.Client.GetAsync<PreviewJson>("agents/preview/" + Uri.EscapeDataString(a.Id));
            if (answer?.Changes is not { Count: > 0 })
            {
                if (answer?.Error is { Length: > 0 } why)
                    into.Children.Add(new TextBlock { Text = why, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, FontSize = 12 });
                return;
            }
            foreach (var c in answer.Changes)
            {
                into.Children.Add(new TextBlock
                {
                    Text = c.Path + (c.Removed ? " · " + L.T("removed") : c.Created ? " · " + L.T("created") : ""),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                });
                foreach (var l in c.Lines ?? [])
                {
                    into.Children.Add(new TextBlock
                    {
                        Text = (l.Op == "-" ? "− " : l.Op == "+" ? "+ " : "~ ") + l.Text,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11.5,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)Application.Current.Resources[l.Op == "-" ? "SystemFillColorCriticalBrush" : l.Op == "+" ? "SystemFillColorSuccessBrush" : "TextFillColorSecondaryBrush"],
                    });
                }
            }
        }
        catch (Exception)
        {
            // no preview for this agent: the words above stand
        }
    }

    private async Task ReapplyAsync(AgentJson a)
    {
        await PageKit.RunAsync(async () =>
        {
            _state = await App.CurrentApp.Client.PostAsync<StateJson>("agents/reapply/" + Uri.EscapeDataString(a.Id)) ?? _state;
            Draw();
            Status.Text = _state.Notice ?? L.T("{agent} goes through magpie again").Replace("{agent}", a.Name);
        }, Status, Busy);
    }

    private async Task SetHiddenAsync(AgentJson a, bool hide)
    {
        var order = _state.Settings.AgentOrder ?? _state.Agents.Select(x => x.Id).ToList();
        var hidden = new HashSet<string>(_state.Settings.AgentsHidden ?? []);
        if (hide) hidden.Add(a.Id); else hidden.Remove(a.Id);
        await PageKit.RunAsync(async () =>
        {
            await App.CurrentApp.Client.PostAsync<SettingsFullJson>("agents/arrange", new { order, hidden = hidden.ToList(), shown = new List<string>() });
            await ReloadAsync();
        }, Status, Busy);
    }

    private void ShowRowMenu(FrameworkElement anchor, AgentJson a, bool inFold)
    {
        var menu = new MenuFlyout();
        var shown = Arranged().Shown;
        var at = shown.FindIndex(x => x.Id == a.Id);
        if (inFold)
        {
            var show = new MenuFlyoutItem { Text = L.T("Show") };
            show.Click += async (_, _) => await SetHiddenAsync(a, false);
            menu.Items.Add(show);
        }
        else
        {
            var up = new MenuFlyoutItem { Text = L.T("Move up"), IsEnabled = at > 0 };
            up.Click += async (_, _) => await MoveAsync(a, at - 1);
            var down = new MenuFlyoutItem { Text = L.T("Move down"), IsEnabled = at >= 0 && at < shown.Count - 1 };
            down.Click += async (_, _) => await MoveAsync(a, at + 1);
            menu.Items.Add(up);
            menu.Items.Add(down);
            var reapply = a.Drift is not null || a.Fields.Any(f => !string.IsNullOrEmpty(f.Options.FirstOrDefault(o => o.Value == f.Value)?.Ref));
            if (reapply)
            {
                menu.Items.Add(new MenuFlyoutSeparator());
                var again = new MenuFlyoutItem { Text = L.T("Apply again") };
                again.Click += async (_, _) => await ReapplyAsync(a);
                menu.Items.Add(again);
            }
            if (a.Drift?.Kind == "replaced")
            {
                var keep = new MenuFlyoutItem { Text = L.T("Keep current settings") };
                keep.Click += async (_, _) =>
                {
                    await PageKit.RunAsync(async () =>
                    {
                        _state = await App.CurrentApp.Client.PostAsync<StateJson>("agents/keep/" + Uri.EscapeDataString(a.Id)) ?? _state;
                        Draw();
                    }, Status, Busy);
                };
                menu.Items.Add(keep);
            }
            if (a.Wired)
            {
                menu.Items.Add(new MenuFlyoutSeparator());
                var off = new MenuFlyoutItem { Text = L.T("Disconnect from magpie") };
                off.Click += async (_, _) => await DisconnectAsync(a);
                menu.Items.Add(off);
            }
            menu.Items.Add(new MenuFlyoutSeparator());
            var hide = new MenuFlyoutItem { Text = L.T("Hide") };
            hide.Click += async (_, _) => await SetHiddenAsync(a, true);
            menu.Items.Add(hide);
        }
        menu.ShowAt(anchor);
    }

    private async Task MoveAsync(AgentJson a, int to)
    {
        var shown = Arranged().Shown.Select(x => x.Id).ToList();
        var folded = Arranged().Folded.Select(x => x.Id).Concat(Arranged().ByHand.Select(x => x.Id)).ToList();
        var from = shown.IndexOf(a.Id);
        if (from < 0 || to < 0 || to >= shown.Count || to == from) return;
        shown.RemoveAt(from);
        shown.Insert(to, a.Id);
        var order = shown.Concat(folded).ToList();
        var hidden = _state.Settings.AgentsHidden ?? [];
        await PageKit.RunAsync(async () =>
        {
            await App.CurrentApp.Client.PostAsync<SettingsFullJson>("agents/arrange", new { order, hidden, shown = new List<string>() });
            await ReloadAsync();
        }, Status, Busy);
    }

    private async Task UpdateCliAsync(AgentJson a, Button button)
    {
        button.IsEnabled = false;
        button.Content = L.T("Updating…");
        try
        {
            var c = await App.CurrentApp.Client.PostAsync<CliJson>("agents/cli/" + Uri.EscapeDataString(a.Id));
            if (c is not null) _cli.Agents[a.Id] = c;
            Status.Text = c?.Version is { Length: > 0 } v
                ? L.T("{agent} updated to {v}").Replace("{agent}", a.Name).Replace("{v}", v)
                : L.T("{agent} updated to {v}").Replace("{agent}", a.Name).Replace("{v}", "");
            Draw();
        }
        catch (Exception e)
        {
            Status.Text = e.Message;
            button.IsEnabled = true;
            button.Content = L.T("Update to {v}").Replace("{v}", _cli.Agents.GetValueOrDefault(a.Id)?.Latest ?? "");
        }
    }

    /// <summary>Whether a newer version of an agent's CLI is out and magpie
    /// knows how to get it.</summary>
    private bool UpdateOut(AgentJson a) => !a.CliMissing && _cli.Agents.GetValueOrDefault(a.Id) is { Update: true };

    private Border UpdateAllBar()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var behind = _state.Agents.Where(UpdateOut).ToList();
        row.Children.Add(new TextBlock
        {
            Text = behind.Count == 1
                ? L.T("A newer version of {name} is out").Replace("{name}", behind[0].Name)
                : L.T("Newer versions of {n} agents are out").Replace("{n}", behind.Count.ToString()),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
        });
        var all = new Button { Content = L.T("Update all ({n})").Replace("{n}", behind.Count.ToString()), Padding = new Thickness(10, 2, 10, 2) };
        all.Click += async (_, _) =>
        {
            all.IsEnabled = false;
            foreach (var a in behind)
            {
                try
                {
                    var c = await App.CurrentApp.Client.PostAsync<CliJson>("agents/cli/" + Uri.EscapeDataString(a.Id));
                    if (c is not null) _cli.Agents[a.Id] = c;
                }
                catch (Exception e) { Status.Text = e.Message; }
            }
            Draw();
        };
        row.Children.Add(all);
        return new Border { Padding = new Thickness(2, 0, 2, 0), Child = row };
    }

    private Border EmptyState() => new()
    {
        Padding = new Thickness(16, 20, 16, 20),
        Child = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = L.T("No agents found"), FontWeight = FontWeights.SemiBold },
                new TextBlock
                {
                    Text = L.T("Install Claude Code, Codex, Gemini CLI, OpenCode… and magpie will list them here."),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.75,
                    FontSize = 12,
                },
            },
        },
    };
}
