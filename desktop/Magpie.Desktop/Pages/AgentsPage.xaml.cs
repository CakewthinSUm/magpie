using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Agents page, drawn here rather than by the page magpie serves: each
/// agent's fields and their values, and a click on one opening the app's own
/// menu of what magpie can set (GET /api/state, POST /api/set, which answers
/// with the whole new state, as it does for the page).
/// </summary>
public sealed partial class AgentsPage : Page, IRefreshable
{
    public AgentsPage() => InitializeComponent();

    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        Busy.IsActive = true;
        Status.Text = "";
        try
        {
            var state = await App.CurrentApp.Client.GetAsync<StateJson>("state");
            if (state is null) return;
            Draw(state);
            // each agent's own logo, read through the signed-in client and
            // drawn on the pass after it has it
            await PageKitUI.LoadIconsAsync(state.Agents.Select(a => a.Icon), () => Draw(state));
        }
        catch (MagpieException e)
        {
            Status.Text = e.Message;
        }
        catch (Exception e)
        {
            Status.Text = e.Message;
        }
        finally
        {
            Busy.IsActive = false;
        }
    }

    private void Draw(StateJson state)
    {
        var rows = new List<AgentRow>();
        foreach (var a in state.Agents)
        {
            var fields = new List<FieldRow>();
            foreach (var f in a.Fields)
            {
                var current = f.Options.FirstOrDefault(o => o.Value == f.Value);
                fields.Add(new FieldRow
                {
                    Agent = a.Id,
                    Field = f,
                    Label = f.Label,
                    Shown = current?.Label is { Length: > 0 } label ? label : (f.Value.Length > 0 ? f.Value : "—"),
                });
            }
            rows.Add(new AgentRow
            {
                Name = a.Name,
                Line = LineOf(a),
                IconSource = IconOf(a.Icon),
                Fields = fields,
            });
        }
        List.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Catalog.Text = state.Catalog.Length > 0 ? "models from " + state.Catalog : "";
        if (state.Notice is { Length: > 0 } notice) Status.Text = notice;
    }

    /// <summary>The line under an agent's name, as the page's is: what it is
    /// connected to, whether magpie is in its config, and what it runs on when
    /// it isn't.</summary>
    private static string LineOf(AgentJson a)
    {
        var parts = new List<string>();
        if (a.Wired) parts.Add("through magpie");
        else if (a.Failover) parts.Add("magpie's failover");
        if (a.Source is { Length: > 0 } src) parts.Add("on " + src);
        if (a.Stale > 0) parts.Add($"{a.Stale} running on an older list");
        if (a.CliMissing) parts.Add("its CLI isn't installed");
        if (a.Menu) parts.Add("its own model list");
        if (a.Models is { } m) parts.Add($"showing {m.Shown} / {m.Listed} models");
        return string.Join(" · ", parts);
    }

    /// <summary>The agent's own logo, from the page's icons folder: the
    /// picture read already, or none until the read lands (LoadAsync draws
    /// the page again when it has).</summary>
    private static ImageSource? IconOf(string icon) => PageKitUI.IconCached(icon);

    private async void Field_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FieldRow row } button) return;
        var menu = new MenuFlyout();
        var options = row.Field.Options;
        if (options.Count == 0)
        {
            menu.Items.Add(new MenuFlyoutItem { Text = "Nothing to choose from", IsEnabled = false });
        }
        string? group = null;
        foreach (var o in options)
        {
            // a group's name is a header, as the page's menu shows it
            if (!string.IsNullOrEmpty(o.Group) && o.Group != group)
            {
                group = o.Group;
                menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(new MenuFlyoutItem { Text = group, IsEnabled = false });
            }
            var item = new MenuFlyoutItem
            {
                Text = o.Label is { Length: > 0 } l ? l : o.Value,
                Tag = o.Value,
            };
            if (o.Value == row.Field.Value) item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            if (o.Note is { Length: > 0 } note) ToolTipService.SetToolTip(item, note);
            item.Click += (_, _) => _ = PickAsync(row, o.Value);
            menu.Items.Add(item);
        }
        menu.ShowAt(button);
    }

    /// <summary>A pick: POST /api/set answers with the whole state, so the
    /// page is drawn from what magpie wrote, not from what was clicked.</summary>
    private async Task PickAsync(FieldRow row, string value)
    {
        if (value == row.Field.Value) return;
        Busy.IsActive = true;
        Status.Text = "";
        try
        {
            var state = await App.CurrentApp.Client.PostAsync<StateJson>("set",
                new { agent = row.Agent, field = row.Field.Key, value });
            if (state is not null) Draw(state);
        }
        catch (MagpieException e)
        {
            Status.Text = e.Message;
        }
        catch (Exception e)
        {
            Status.Text = e.Message;
        }
        finally
        {
            Busy.IsActive = false;
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();
}

/// <summary>One agent's row, as the list draws it.</summary>
public sealed class AgentRow
{
    public string Name { get; set; } = "";
    public string Line { get; set; } = "";
    public ImageSource? IconSource { get; set; }
    public List<FieldRow> Fields { get; set; } = [];
}

/// <summary>One field of an agent, and what its button shows.</summary>
public sealed class FieldRow
{
    public string Agent { get; set; } = "";
    public FieldJson Field { get; set; } = new();
    public string Label { get; set; } = "";
    public string Shown { get; set; } = "";
}
