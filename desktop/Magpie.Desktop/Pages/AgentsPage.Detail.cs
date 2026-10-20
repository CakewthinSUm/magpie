using Magpie.Desktop.Backend;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Agents page's opened row and the parts that hang off it: the agent's
/// settings on a line of their own (a label and the button that picks, as the
/// page's rows have), the model button that picks the one it starts on, and
/// the picker both open — the page's own menu, never a native select
/// (docs/code-standards.md).
/// </summary>
public sealed partial class AgentsPage
{
    // ---- the opened row --------------------------------------------------

    /// <summary>An agent's settings, built only while its row is open: a
    /// label and a button per field, the fields magpie doesn't offer while
    /// they have nothing to choose from left out, as the page leaves them.</summary>
    private FrameworkElement DetailPanel(AgentJson a)
    {
        var box = new StackPanel { Spacing = 6, Padding = new Thickness(50, 0, 14, 12) };
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var row = 0;
        // the extras go in one cell of their own at the end of the pickers,
        // as the page's squares do: a tier's model, a subagent's, the effort
        // its subagents start at
        var extras = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var f in a.Fields)
        {
            if (Grouped(a, f)) continue;
            if (None(a, f)) continue;
            var control = Extra(f) ? ExtraField(a, f) : FieldButton(a, f);
            if (Extra(f))
            {
                extras.Children.Add(control);
                continue;
            }
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = L.T(f.Label),
                Opacity = 0.7,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(label, row);
            grid.Children.Add(label);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
            row++;
        }
        // Claude Code's tiers and omp's roles are one square listing them all
        if (TierMenu(a) is { } tiers)
        {
            extras.Children.Add(TierButton(a, tiers));
        }
        // the command that starts an agent taking magpie only from its
        // environment (agy)
        if (a.Launch is { Length: > 0 } launch)
        {
            var copy = new Button
            {
                Content = L.T("Copy launch command"),
                Padding = new Thickness(10, 3, 10, 3),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            ToolTipService.SetToolTip(copy, launch);
            copy.Click += (_, _) => Copy(launch);
            extras.Children.Add(copy);
        }
        if (extras.Children.Count > 0)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(extras, row);
            Grid.SetColumn(extras, 1);
            grid.Children.Add(extras);
        }
        box.Children.Add(grid);

        // the models its lists show, as the page's line under the name says
        if (a.Models is { } m && !a.CliMissing)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(new TextBlock
            {
                Text = L.T("Model list"),
                Opacity = 0.7,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var open = new Button
            {
                Content = m.Shown >= m.Listed
                    ? L.T("All {n} models").Replace("{n}", m.Listed.ToString())
                    : L.T("Showing {shown} / {listed} models").Replace("{shown}", m.Shown.ToString()).Replace("{listed}", m.Listed.ToString()),
                Padding = new Thickness(10, 3, 10, 3),
                FontSize = 12,
            };
            ToolTipService.SetToolTip(open, L.T("Pick which models {agent} lists").Replace("{agent}", a.Name));
            open.Click += async (_, _) => await OpenModelsAsync(a);
            line.Children.Add(open);
            box.Children.Add(line);
        }

        if (a.Path is { Length: > 0 } path)
        {
            box.Children.Add(new TextBlock
            {
                Text = path,
                Opacity = 0.55,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }
        return box;
    }

    /// <summary>A field's button: what it is set to, and the app's own menu of
    /// what magpie can set.</summary>
    private Button FieldButton(AgentJson a, FieldJson f)
    {
        var current = f.Options.FirstOrDefault(o => o.Value == f.Value);
        var text = current?.Label is { Length: > 0 } label ? label
            : f.Value.Length > 0 ? f.Value
            : L.T(FollowsModel(f.Label) ? "same as model" : "default");
        var button = new Button
        {
            Content = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis },
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinWidth = 160,
        };
        var tip = L.T("{label}: {value}").Replace("{label}", L.T(f.Label)).Replace("{value}", f.Value.Length > 0 ? f.Value : L.T("default"));
        if (current?.Note is { Length: > 0 } note) tip += " · " + note;
        // a model the agent asks for itself, magpie not in the way, says so
        if (f.Value.Length > 0 && current?.Direct is { Length: > 0 } direct)
        {
            tip += "\n" + L.T("Not through magpie: {agent} asks {vendor} for it directly, with its own sign-in or key, so {path} has no magpie endpoint — that is expected.")
                .Replace("{agent}", a.Name).Replace("{vendor}", direct).Replace("{path}", a.Path);
        }
        if (a.Native?.Fields?.GetValueOrDefault(f.Key)?.Detail is { Length: > 0 } detail) tip += "\n" + detail;
        ToolTipService.SetToolTip(button, tip);
        button.Click += (_, _) => ShowFieldMenu(button, a, f);
        return button;
    }

    /// <summary>The field's menu: a reset at the top when magpie has one, its
    /// options grouped under their headers, the one in force in bold. A model
    /// picker filters as the reader types, and a value typed in that isn't
    /// listed is offered as it is, as the page's does.</summary>
    private void ShowFieldMenu(FrameworkElement anchor, AgentJson a, FieldJson f)
    {
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        if (f.Options.Count == 0)
        {
            menu.Items.Add(new MenuFlyoutItem { Text = L.T("Nothing to choose from"), IsEnabled = false });
            menu.ShowAt(anchor);
            return;
        }
        // the agent's own default comes first, as the page's picker puts it
        if (f.Key == "model" && !f.Options.Any(o => o.Value.Length == 0))
        {
            var def = new MenuFlyoutItem { Text = L.T("Default") };
            if (f.Value.Length == 0) def.FontWeight = FontWeights.SemiBold;
            def.Click += async (_, _) => await PickAsync(a, f, "");
            menu.Items.Add(def);
            menu.Items.Add(new MenuFlyoutSeparator());
        }
        string? group = null;
        foreach (var o in f.Options)
        {
            if (o.Group is { Length: > 0 } g && g != group)
            {
                group = g;
                if (menu.Items.Count > 0) menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(new MenuFlyoutItem { Text = L.T(g), IsEnabled = false });
            }
            var item = new MenuFlyoutItem
            {
                Text = o.Label is { Length: > 0 } l ? l : o.Value,
                Tag = o.Value,
            };
            if (o.Value == f.Value) item.FontWeight = FontWeights.SemiBold;
            var tip = o.Note;
            if (o.Context > 0) tip = (tip is { Length: > 0 } ? tip + " · " : "") + Context(o.Context);
            if (o.Free) tip = "free" + (tip is { Length: > 0 } ? " · " + tip : "");
            if (tip is { Length: > 0 }) ToolTipService.SetToolTip(item, tip);
            item.Click += async (_, _) => await PickAsync(a, f, o.Value);
            menu.Items.Add(item);
        }
        menu.ShowAt(anchor);
    }

    /// <summary>A field's extra square: the ones that follow the model, a
    /// sign-in, the effort subagents start at, the model Codex writes its
    /// memories with.</summary>
    private Button ExtraField(AgentJson a, FieldJson f)
    {
        var set = f.Value.Length > 0;
        var current = f.Options.FirstOrDefault(o => o.Value == f.Value);
        var said = current?.Label is { Length: > 0 } label ? label
            : f.Value.Length > 0 ? f.Value
            : L.T(f.Label == "subagent model" ? "the model the lead asks for" : FollowsModel(f.Label) ? "same as model" : "not set");
        var button = new Button
        {
            Content = new FontIcon { Glyph = ExtraGlyph(f.Label), FontSize = 13 },
            Width = 32,
            Height = 30,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        ToolTipService.SetToolTip(button, L.T("{label}: {value}").Replace("{label}", L.T(f.Label)).Replace("{value}", said));
        button.Click += (_, _) => ShowFieldMenu(button, a, f);
        return button;
    }

    /// <summary>Whether a field is one of the squares at the end of a row:
    /// the ones that follow the model, a sign-in, the effort its subagents
    /// start at, the model it writes memories with.</summary>
    private static bool Extra(FieldJson f) =>
        f.Key == "tiers" || FollowsModel(f.Label) || f.Label == "sign-in" || f.Key == "ultracode"
        || f.Label == "subagent effort" || f.Label == "subagent model" || f.Label == "memories";

    /// <summary>Whether a field belongs to the tier square (Claude Code's
    /// tiers and their efforts, omp's roles) rather than a row of its own.</summary>
    private static bool Grouped(AgentJson a, FieldJson f) =>
        Roles(a).Contains(f.Label) || RoleLevel(f.Label);

    /// <summary>A field the page doesn't draw while it has nothing to choose
    /// from and nothing set: an effort the model has none of, a sign-in with
    /// no other to take.</summary>
    private static bool None(AgentJson a, FieldJson f) =>
        (f.Key is "effort" or "ultracode" || f.Label is "subagents" or "subagent effort" or "subagent model" or "memories" or "sign-in" || f.Key == "small")
        && f.Options.Count == 0 && f.Value.Length == 0;

    /// <summary>Fields that fall back to the agent's model when unset: omp's
    /// smol and slow roles take the session's, as its subagents do.</summary>
    private static bool FollowsModel(string label) =>
        label is "opus" or "sonnet" or "haiku" or "fable" or "subagents" or "smol" or "slow";

    /// <summary>Claude Code's tiers, or omp's roles: the fields one square
    /// lists.</summary>
    private static string[] Roles(AgentJson a) =>
        OmpAgent(a) ? ["subagents", "smol", "slow", "plan", "vision", "advisor", "commit", "tiny", "memory"]
        : ["opus", "sonnet", "haiku", "fable"];

    private static bool OmpAgent(AgentJson a) => a.Id.Split("@wsl:")[0].Split('#')[0] == "omp";

    private static bool RoleLevel(string label) =>
        label.EndsWith(" effort", StringComparison.Ordinal) && RolesOf(label).Contains(label[..^7])
        || label.EndsWith(" thinking", StringComparison.Ordinal) && RolesOf(label).Contains(label[..^9]);

    private static string[] RolesOf(string label) => ["opus", "sonnet", "haiku", "fable", "subagents", "smol", "slow", "plan", "vision", "advisor", "commit", "tiny", "memory"];

    /// <summary>The one square Claude Code's tiers (or omp's roles) share: a
    /// menu of each, and of each one's effort under it.</summary>
    private sealed class TierPick
    {
        public List<(FieldJson Model, FieldJson? Level)> Rows { get; } = [];
        public string Summary { get; set; } = "";
    }

    private TierPick? TierMenu(AgentJson a)
    {
        var roles = Roles(a);
        var tiers = a.Fields.Where(f => roles.Contains(f.Label)).ToList();
        if (tiers.Count == 0 || !tiers.Any(f => f.Options.Count > 0)) return null;
        var main = a.Fields.FirstOrDefault(f => f.Key == "model");
        var mainName = main is null ? "" : main.Options.FirstOrDefault(o => o.Value == main.Value)?.Label ?? main.Value;
        var pick = new TierPick();
        foreach (var f in tiers)
        {
            var level = a.Fields.FirstOrDefault(e => (e.Label == f.Label + " effort" || e.Label == f.Label + " thinking") && (e.Options.Count > 0 || e.Value.Length > 0));
            pick.Rows.Add((f, level));
        }
        var set = pick.Rows.Where(r => r.Model.Value.Length > 0 || r.Level?.Value is { Length: > 0 }).Select(r => L.T(r.Model.Label)).ToList();
        pick.Summary = set.Count > 0 ? string.Join(", ", set) : main is not null ? L.T("same as model") : L.T("not set");
        _ = mainName;
        return pick;
    }

    /// <summary>The tier square: each tier's model, and its effort under it.</summary>
    private Button TierButton(AgentJson a, TierPick tiers)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = tiers.Summary, TextTrimming = TextTrimming.CharacterEllipsis },
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 160,
        };
        ToolTipService.SetToolTip(button, L.T("Tiers"));
        button.Click += (_, _) =>
        {
            var menu = new MenuFlyout();
            foreach (var (model, level) in tiers.Rows)
            {
                var item = new MenuFlyoutItem
                {
                    Text = model.Value.Length > 0
                        ? L.T(model.Label) + " · " + (model.Options.FirstOrDefault(o => o.Value == model.Value)?.Label ?? model.Value)
                        : L.T(model.Label),
                };
                item.Click += (_, _) => ShowFieldMenu(button, a, model);
                menu.Items.Add(item);
                if (level is not null)
                {
                    var lvl = new MenuFlyoutItem
                    {
                        Text = "   " + L.T(level.Label) + " · " + (level.Value.Length > 0
                            ? level.Options.FirstOrDefault(o => o.Value == level.Value)?.Label ?? level.Value
                            : L.T("the effort Claude Code asks for")),
                    };
                    lvl.Click += (_, _) => ShowFieldMenu(button, a, level);
                    menu.Items.Add(lvl);
                }
            }
            menu.ShowAt(button);
        };
        return button;
    }

    /// <summary>The model the agent starts on, beside the switch: a click
    /// picks one, and a model of magpie's picked while it isn't connected
    /// connects it first.</summary>
    private Button ModelButton(AgentJson a, FieldJson f)
    {
        var current = f.Options.FirstOrDefault(o => o.Value == f.Value);
        var text = !a.Wired && f.Key != "model" && !f.Options.Any(o => !string.IsNullOrEmpty(o.Ref))
            ? L.T("Pick a model")
            : current?.Label is { Length: > 0 } label ? label
            : !a.Wired ? L.T("Pick a model")
            : f.Value.Length == 0 && a.Id != "claude" ? L.T("Its last pick")
            : f.Value.Length > 0 ? f.Value : L.T("Default");
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var icon = PageKitUI.IconCached(current?.Icon ?? (f.Value.Length == 0 ? a.Icon : null));
        if (icon is not null) content.Children.Add(new Image { Width = 16, Height = 16, Source = icon });
        content.Children.Add(new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 200 });
        // the chevron that says a menu opens here, as the page's pickers carry
        content.Children.Add(new FontIcon { Glyph = "", FontSize = 9, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(10, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(button, L.T("Pick one of magpie's models: {agent} is connected and starts on it").Replace("{agent}", a.Name));
        button.Click += (_, _) => ShowFieldMenu(button, a, f);
        return button;
    }

    // ---- picking ---------------------------------------------------------

    /// <summary>A pick: POST /api/set answers with the whole state, so the
    /// page is drawn from what magpie wrote.</summary>
    private async Task PickAsync(AgentJson a, FieldJson f, string value)
    {
        if (value == f.Value) return;
        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<StateJson>("set", new { agent = a.Id, field = f.Key, value });
            if (answer is not null) _state = answer;
            Draw();
            if (_state.Notice is { Length: > 0 } notice) Status.Text = notice;
        }, Status, Busy);
    }

    /// <summary>A model's context window as the picker says it: 1M, 200K.</summary>
    private static string Context(int n) => n >= 1_000_000
        ? (n % 1_000_000 == 0 ? (n / 1_000_000).ToString() : (n / 1_000_000.0).ToString("0.0")) + "M"
        : (n / 1000) + "K";

    /// <summary>The glyph a field's extra square carries, as the page's
    /// EXTRA_GLYPH does.</summary>
    private static string ExtraGlyph(string label) => label switch
    {
        "subagents" => "",
        "smol" => "",
        "slow" => "",
        "sign-in" => "",
        "ultracode" => "",
        "subagent effort" => "",
        "subagent model" => "",
        "memories" => "",
        _ => "",
    };

    /// <summary>Puts text on the clipboard, as every page's copy button
    /// does.</summary>
    private static void Copy(string text)
    {
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
        catch (Exception)
        {
            // the clipboard is the system's; nothing to do
        }
    }
}
