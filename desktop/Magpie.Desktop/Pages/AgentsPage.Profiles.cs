using Magpie.Desktop.Backend;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Agents page's profiles, as the page keeps them (app.js's
/// renderProfiles): a chip per saved setup, the one in use ticked, and a
/// click on a chip showing what it holds before it is applied. A chip's ↻ and
/// × overwrite and delete it, each asked twice, as the page asks.
///
/// It writes POST /api/profile/{save|use|delete}, whose answer is the whole
/// state with what applying changed.
/// </summary>
public sealed partial class AgentsPage
{
    private Border Profiles()
    {
        var box = new StackPanel { Spacing = 6, Padding = new Thickness(2, 6, 2, 0) };
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock
        {
            Text = L.T("Profiles"),
            FontSize = 12,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var save = new Button
        {
            Content = L.T("＋ Save current"),
            Background = null,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 12,
        };
        save.Click += (_, _) => _ = SaveProfileAsync();
        Grid.SetColumn(save, 2);
        head.Children.Add(save);
        box.Children.Add(head);

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (_state.Profiles.Count == 0)
        {
            chips.Children.Add(new TextBlock
            {
                Text = L.T("none yet · save the setup to switch back in one click"),
                FontSize = 12,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        foreach (var p in _state.Profiles)
        {
            chips.Children.Add(ProfileChip(p));
        }
        box.Children.Add(chips);

        var open = _state.Profiles.FirstOrDefault(p => p.Name == _profileOpen);
        if (open is not null) box.Children.Add(ProfileDetail(open));
        return new Border { Child = box };
    }

    private Button ProfileChip(ProfileJson p)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (p.InUse) content.Children.Add(new FontIcon { Glyph = "", FontSize = 11, Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"] });
        content.Children.Add(new TextBlock { Text = p.Name, FontSize = 12.5 });
        var chip = new Button
        {
            Content = content,
            Padding = new Thickness(10, 3, 10, 3),
            CornerRadius = new CornerRadius(999),
        };
        var tip = new List<string>();
        if (p.InUse) tip.Add(L.T("In use"));
        if (p.Summary is { Length: > 0 } summary) tip.Add(summary);
        if (p.Library is { } lib)
        {
            var parts = new List<string>();
            if (lib.Servers > 0) parts.Add(L.T("MCP servers") + " " + lib.Servers);
            if (lib.Skills > 0) parts.Add(L.T("Skills") + " " + lib.Skills);
            if (lib.Instructions) parts.Add(L.T("Instructions"));
            if (parts.Count > 0) tip.Add(string.Join(" · ", parts));
        }
        if (tip.Count > 0) ToolTipService.SetToolTip(chip, string.Join("\n", tip));
        chip.Click += (_, _) =>
        {
            _profileOpen = _profileOpen == p.Name ? null : p.Name;
            Draw();
        };
        chip.ContextFlyout = ProfileMenu(p);
        return chip;
    }

    /// <summary>A chip's menu: what the page puts on its ↻ and ×, and the
    /// details.</summary>
    private MenuFlyout ProfileMenu(ProfileJson p)
    {
        var menu = new MenuFlyout();
        var apply = new MenuFlyoutItem { Text = L.T("Apply"), IsEnabled = !p.InUse };
        apply.Click += async (_, _) => await ProfileActionAsync("use", p.Name);
        menu.Items.Add(apply);
        var update = new MenuFlyoutItem { Text = L.T("Update to the current setup") };
        update.Click += async (_, _) =>
        {
            if (!await PageKitUI.ConfirmAsync(XamlRoot, L.T("Overwrite?"), L.T("Click again to update {name} to the current setup").Replace("{name}", p.Name), L.T("Overwrite?")))
                return;
            await ProfileActionAsync("save", p.Name);
        };
        menu.Items.Add(update);
        menu.Items.Add(new MenuFlyoutSeparator());
        var delete = new MenuFlyoutItem { Text = L.T("Delete profile") };
        delete.Click += async (_, _) =>
        {
            if (!await PageKitUI.ConfirmAsync(XamlRoot, L.T("Delete?"), L.T("Click again to delete {name}").Replace("{name}", p.Name), L.T("Delete"), danger: true))
                return;
            await ProfileActionAsync("delete", p.Name);
        };
        menu.Items.Add(delete);
        return menu;
    }

    /// <summary>What a profile holds, by agent: the field rows the page's
    /// details show, a value that looked like a key left as dots.</summary>
    private Border ProfileDetail(ProfileJson p)
    {
        var stack = new StackPanel { Spacing = 8 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(new TextBlock { Text = p.Name, FontWeight = FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        if (p.InUse)
        {
            head.Children.Add(new TextBlock { Text = L.T("In use"), FontSize = 11.5, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        }
        var apply = new Button { Content = L.T("Apply"), Padding = new Thickness(10, 2, 10, 2), FontSize = 12, IsEnabled = !p.InUse };
        apply.Click += async (_, _) => await ProfileActionAsync("use", p.Name);
        head.Children.Add(apply);
        stack.Children.Add(head);

        if (p.Agents.Count == 0)
        {
            stack.Children.Add(new TextBlock { Text = L.T("No agent settings saved"), FontSize = 12, Opacity = 0.7 });
        }
        foreach (var g in p.Agents)
        {
            var group = new StackPanel { Spacing = 3 };
            var who = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (PageKitUI.IconCached(g.Icon) is { } icon) who.Children.Add(new Image { Width = 16, Height = 16, Source = icon });
            who.Children.Add(new TextBlock { Text = g.Name, FontSize = 12.5, FontWeight = FontWeights.SemiBold });
            group.Children.Add(who);
            var grid = new Grid { ColumnSpacing = 12, Margin = new Thickness(22, 0, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var row = 0;
            void Add(string label, string value, bool faded = false)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = label, FontSize = 12, Opacity = 0.65 };
                Grid.SetRow(l, row);
                grid.Children.Add(l);
                var v = new TextBlock { Text = value, FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = faded ? 0.6 : 1 };
                Grid.SetRow(v, row);
                Grid.SetColumn(v, 1);
                grid.Children.Add(v);
                row++;
            }
            foreach (var f in g.Fields)
            {
                if (f.Hidden) Add(L.T(f.Label), "••••••", true);
                else if (f.Follows is { Length: > 0 } follows)
                {
                    var main = g.Fields.FirstOrDefault(x => x.Key == follows && !x.Hidden)?.Value;
                    Add(L.T(f.Label), main is { Length: > 0 } m
                        ? L.T("follows the main model ({model})").Replace("{model}", m)
                        : L.T("follows the main model"), true);
                }
                else if (f.Value.Length == 0) Add(L.T(f.Label), L.T("agent default"), true);
                else Add(L.T(f.Label), f.Value);
            }
            if (g.Models is { } models)
            {
                Add(L.T("Model list"), models.Only
                    ? L.T("only the {n} picked").Replace("{n}", models.Picked.ToString())
                    : models.Hidden > 0 ? L.T("{n} hidden").Replace("{n}", models.Hidden.ToString())
                    : L.T("every model shown"), !models.Only && models.Hidden == 0);
            }
            if (g.Servers is { Count: > 0 } servers) Add(L.T("MCP servers"), string.Join(", ", servers));
            if (g.Skills is { Count: > 0 } skills) Add(L.T("Skills"), string.Join(", ", skills));
            if (g.Instructions) Add(L.T("Instructions"), L.T("on"));
            group.Children.Add(grid);
            stack.Children.Add(group);
        }
        if (p.Agents.Any(g => g.Fields.Any(f => f.Hidden)))
        {
            stack.Children.Add(new TextBlock
            {
                Text = L.T("•••••• looks like a key or a token, and is not shown"),
                FontSize = 11.5,
                Opacity = 0.6,
            });
        }
        return new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            Child = new ScrollViewer { Content = stack, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
    }

    /// <summary>＋ Save current: the name to save the setup under, in a box
    /// beside it, as the page opens one.</summary>
    private async Task SaveProfileAsync()
    {
        var name = new TextBox { PlaceholderText = L.T("Name"), MinWidth = 260 };
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock
        {
            Text = L.T("Saved as the setup the agents have now: applying it later puts these back."),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.75,
        });
        body.Children.Add(name);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L.T("＋ Save current"),
            Content = body,
            PrimaryButtonText = L.T("Save"),
            CloseButtonText = L.T("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var chosen = name.Text.Trim();
        if (chosen.Length == 0) return;
        await ProfileActionAsync("save", chosen);
    }

    /// <summary>A profile's use, save or delete: the answer is the whole
    /// state, so the page is drawn from what magpie wrote.</summary>
    private async Task ProfileActionAsync(string action, string name)
    {
        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<ProfileAnswerJson>("profile/" + action, new { name });
            if (answer is not null) _state = answer;
            if (action == "use") _profileOpen = null;
            Draw();
            Status.Text = action switch
            {
                "use" => L.T("{name} applied · {n} settings changed").Replace("{name}", name).Replace("{n}", (answer?.Changed ?? 0).ToString()),
                "save" => L.T("Saved {name}").Replace("{name}", name),
                _ => L.T("Deleted {name}").Replace("{name}", name),
            };
        }, Status, Busy);
    }

    // ---- manage ----------------------------------------------------------

    /// <summary>Manage agents: the whole list with a tick each, hidden or
    /// not, to hide or show them in one go. Hiding only changes this list,
    /// never an agent's connection or settings.</summary>
    private async Task ManageAsync()
    {
        var order = _state.Settings.AgentOrder ?? _state.Agents.Select(a => a.Id).ToList();
        var hidden = new HashSet<string>(_state.Settings.AgentsHidden ?? []);
        var picked = new HashSet<string>(hidden);
        var query = "";

        var body = new Grid { RowSpacing = 8, MinWidth = 560 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(new TextBlock
        {
            Text = L.T("Hiding only changes this list, not an agent's connection or settings. Unhidden agents may stay under Not set up."),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.75,
        });

        var list = new ListView { SelectionMode = ListViewSelectionMode.None, MaxHeight = 420 };
        var rows = new List<(AgentJson Agent, CheckBox Tick)>();
        void Paint()
        {
            list.Items.Clear();
            rows.Clear();
            foreach (var a in _state.Agents.OrderBy(a => order.IndexOf(a.Id) < 0 ? int.MaxValue : order.IndexOf(a.Id)))
            {
                if (query.Length > 0 && !a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                var tick = new CheckBox { IsChecked = picked.Contains(a.Id), MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
                tick.Checked += (_, _) => picked.Add(a.Id);
                tick.Unchecked += (_, _) => picked.Remove(a.Id);
                var who = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (PageKitUI.IconCached(a.Icon) is { } icon) who.Children.Add(new Image { Width = 18, Height = 18, Source = icon });
                who.Children.Add(new TextBlock { Text = a.Name, VerticalAlignment = VerticalAlignment.Center });
                if (a.Wired) who.Children.Add(new TextBlock { Text = L.T("Connected · magpie's models are in {agent}'s model list").Replace("{agent}", a.Name), FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center });
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                line.Children.Add(tick);
                line.Children.Add(who);
                list.Items.Add(line);
                rows.Add((a, tick));
            }
        }

        var search = new TextBox { PlaceholderText = L.T("Search"), Width = 240 };
        search.TextChanged += (_, _) => { query = search.Text.Trim(); Paint(); };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        tools.Children.Add(search);
        var all = new Button { Content = L.T("Select all"), Padding = new Thickness(10, 2, 10, 2), FontSize = 12 };
        all.Click += (_, _) => { foreach (var (a, t) in rows) { picked.Add(a.Id); t.IsChecked = true; } };
        var none = new Button { Content = L.T("Select none"), Padding = new Thickness(10, 2, 10, 2), FontSize = 12 };
        none.Click += (_, _) => { foreach (var (a, t) in rows) { picked.Remove(a.Id); t.IsChecked = false; } };
        tools.Children.Add(all);
        tools.Children.Add(none);
        body.Children.Add(tools);
        Grid.SetRow(list, 1);
        body.Children.Add(list);
        Paint();

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L.T("Manage agents"),
            Content = body,
            PrimaryButtonText = L.T("Save"),
            CloseButtonText = L.T("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await PageKit.RunAsync(async () =>
        {
            await App.CurrentApp.Client.PostAsync<SettingsFullJson>("agents/arrange", new { order, hidden = picked.ToList(), shown = new List<string>() });
            await ReloadAsync();
        }, Status, Busy);
    }
}
