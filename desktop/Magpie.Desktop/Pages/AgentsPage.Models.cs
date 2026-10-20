using Magpie.Desktop.Backend;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Magpie.Desktop.Pages;

/// <summary>
/// An agent's model list, as the page's own dialog draws it (app.js's
/// openAgentModels): which of magpie's models its picker shows, one at a
/// time, and the order they come in. A model turned off leaves the agent's
/// picker alone; every other agent still has it.
///
/// It reads GET /api/agent-models/{id} and writes POST /api/agent-models/{id}
/// with hidden, shown, only, order or ownHidden, as the page does.
/// </summary>
public sealed partial class AgentsPage
{
    /// <summary>One row of the dialog, as it is drawn.</summary>
    private sealed class ModelRow
    {
        public AgentModelJson Model { get; init; } = new();
        public bool On { get; set; }
        public bool InUse => Model.InUse;
        public string Title => Model.Name.Length > 0 ? Model.Name : Model.Id;
        public ImageSource? Icon { get; init; }
        public string Line { get; init; } = "";
    }

    private async Task OpenModelsAsync(AgentJson a)
    {
        AgentModelsJson list;
        try
        {
            list = await App.CurrentApp.Client.GetAsync<AgentModelsJson>("agent-models/" + Uri.EscapeDataString(a.Id)) ?? new AgentModelsJson();
        }
        catch (Exception e)
        {
            Status.Text = e.Message;
            return;
        }

        // what the dialog holds: the list as read, and what the reader changed
        var hidden = new HashSet<string>(list.Models.Where(m => m.Hidden).Select(m => m.Id));
        var order = list.Models.Select(m => m.Id).ToList();
        var only = list.Only;
        var ownHidden = list.OwnHidden;
        var changed = false;
        var query = "";
        var view = "all";

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L.T("{agent}'s model list").Replace("{agent}", a.Name),
            PrimaryButtonText = L.T("Done"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var body = new Grid { RowSpacing = 8, MinWidth = 700 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // the words the page puts at the top of the list
        var head = new StackPanel { Spacing = 8 };
        head.Children.Add(new TextBlock
        {
            Text = L.T("Models turned off don't show in {agent}'s model picker; other agents can still use them.").Replace("{agent}", a.Name),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            FontSize = 12,
        });
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var search = new TextBox { PlaceholderText = L.T("Search models"), Width = 260 };
        tools.Children.Add(search);
        var segs = new SelectorBar();
        var allTab = new SelectorBarItem { Text = L.T("All"), Tag = "all" };
        var shownTab = new SelectorBarItem { Text = L.T("Shown"), Tag = "shown" };
        var orderTab = new SelectorBarItem { Text = L.T("Order"), Tag = "order" };
        segs.Items.Add(allTab);
        segs.Items.Add(shownTab);
        segs.Items.Add(orderTab);
        segs.SelectedItem = allTab;
        tools.Children.Add(segs);
        head.Children.Add(tools);
        Grid.SetRow(head, 0);
        body.Children.Add(head);

        var listBox = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 420,
            IsItemClickEnabled = false,
        };
        // a row is built by ModelItem as its container comes up, so the tick
        // it holds is the one the reader sees
        listBox.ContainerContentChanging += (_, e) =>
        {
            if (e.Item is not ModelRow row || e.InRecycleQueue) return;
            e.Handled = true;
            e.ItemContainer.ContentTemplate = null;
            e.ItemContainer.Content = ModelItem(row, hidden, () => { changed = true; Paint(); });
        };
        Grid.SetRow(listBox, 1);
        body.Children.Add(listBox);

        // the foot: what the whole list does at once, and the two switches
        var foot = new StackPanel { Spacing = 6 };
        var footRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var hideAll = new Button { Content = L.T("Hide all"), Padding = new Thickness(10, 3, 10, 3), FontSize = 12 };
        var showAll = new Button { Content = L.T("Show all"), Padding = new Thickness(10, 3, 10, 3), FontSize = 12 };
        var unorder = new Button { Content = L.T("Default order"), Padding = new Thickness(10, 3, 10, 3), FontSize = 12 };
        footRow.Children.Add(hideAll);
        footRow.Children.Add(showAll);
        footRow.Children.Add(unorder);
        foot.Children.Add(footRow);

        // whether a model that comes later is shown, or stays off until it is
        // ticked, and whether the agent's own models sit beside magpie's
        var onlySwitch = new ToggleSwitch
        {
            IsOn = only,
            Header = L.T("Only models I pick"),
            MinWidth = 0,
        };
        ToolTipService.SetToolTip(onlySwitch, L.T("Off: new models are shown. On: only the models ticked here are shown, and a model added later, of any provider, stays off until you tick it"));
        onlySwitch.Toggled += async (_, _) =>
        {
            if (onlySwitch.IsOn == only) return;
            only = onlySwitch.IsOn;
            await WriteAsync(a, new { only });
        };
        foot.Children.Add(onlySwitch);
        if (list.Own)
        {
            var ownSwitch = new ToggleSwitch
            {
                IsOn = !ownHidden,
                Header = L.T("Show {agent}'s own models").Replace("{agent}", a.Name),
                MinWidth = 0,
            };
            ToolTipService.SetToolTip(ownSwitch, L.T("The models {agent} reaches itself, not through magpie. Off: its model picker offers magpie's alone; the one it is set to stays").Replace("{agent}", a.Name));
            ownSwitch.Toggled += async (_, _) =>
            {
                if ((!ownSwitch.IsOn) == ownHidden) return;
                ownHidden = !ownSwitch.IsOn;
                await WriteAsync(a, new { ownHidden });
            };
            foot.Children.Add(ownSwitch);
        }
        Grid.SetRow(foot, 2);
        body.Children.Add(foot);
        dialog.Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        hideAll.Click += (_, _) =>
        {
            // every one at once, the model in use aside
            foreach (var m in list.Models.Where(m => !m.InUse)) hidden.Add(m.Id);
            changed = true;
            Paint();
        };
        showAll.Click += (_, _) =>
        {
            hidden.Clear();
            changed = true;
            Paint();
        };
        unorder.Click += async (_, _) =>
        {
            order = list.Models
                .OrderBy(m => m.Group, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(m => m.Name.Length > 0 ? m.Name : m.Id, StringComparer.CurrentCultureIgnoreCase)
                .Select(m => m.Id)
                .ToList();
            await WriteAsync(a, new { order });
        };

        void Paint()
        {
            var rows = new List<ModelRow>();
            var shown = list.Models.Where(m => !hidden.Contains(m.Id));
            var source = view == "shown" ? shown : list.Models;
            if (view == "order")
            {
                var byId = list.Models.ToDictionary(m => m.Id);
                source = order.Where(byId.ContainsKey).Select(id => byId[id]);
            }
            foreach (var m in source)
            {
                if (query.Length > 0 &&
                    !(m.Name + " " + m.Id + " " + m.Group).Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                rows.Add(new ModelRow
                {
                    Model = m,
                    On = !hidden.Contains(m.Id),
                    Icon = PageKitUI.IconCached(m.Logo ?? m.Icon ?? (m.Icons is { Count: > 0 } icons ? icons[0] : null)),
                    Line = m.Context > 0 ? m.Group + " · " + Context(m.Context) : m.Group,
                });
            }
            listBox.ItemsSource = rows;
        }
        search.TextChanged += (_, _) => { query = search.Text.Trim(); Paint(); };
        segs.SelectionChanged += (_, _) =>
        {
            view = segs.SelectedItem?.Tag as string ?? "all";
            Paint();
        };

        // the write, and the list as magpie answered it
        async Task WriteAsync(AgentJson agent, object change)
        {
            try
            {
                await App.CurrentApp.Client.PostAsync("agent-models/" + Uri.EscapeDataString(agent.Id), change);
                changed = true;
            }
            catch (Exception e)
            {
                Status.Text = e.Message;
            }
        }

        dialog.PrimaryButtonClick += async (_, _) =>
        {
            if (!changed) return;
            try
            {
                if (only || list.Only)
                {
                    var picks = list.Models.Where(m => !hidden.Contains(m.Id) || m.InUse).Select(m => m.Id).ToList();
                    await App.CurrentApp.Client.PostAsync("agent-models/" + Uri.EscapeDataString(a.Id), new { shown = picks });
                }
                else
                {
                    await App.CurrentApp.Client.PostAsync("agent-models/" + Uri.EscapeDataString(a.Id), new { hidden = hidden.ToList() });
                }
                await ReloadAsync();
            }
            catch (Exception e)
            {
                Status.Text = e.Message;
            }
        };

        Paint();
        await dialog.ShowAsync();
    }

    /// <summary>One model's row: a tick, its logo and name, what it is, and
    /// whether it is the one in use (which can't be taken out).</summary>
    private static FrameworkElement ModelItem(ModelRow row, HashSet<string> hidden, Action changed)
    {
        var grid = new Grid { ColumnSpacing = 10, Padding = new Thickness(4, 4, 4, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // the tick is set before its handlers, which would read the draw as a
        // click
        var tick = new CheckBox
        {
            IsChecked = row.On,
            IsEnabled = !row.InUse,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(tick, row.InUse ? L.T("In use, so it stays on") : "");
        tick.Checked += (_, _) => { if (row.On) return; row.On = true; hidden.Remove(row.Model.Id); changed(); };
        tick.Unchecked += (_, _) => { if (!row.On) return; row.On = false; hidden.Add(row.Model.Id); changed(); };
        grid.Children.Add(tick);

        if (row.Icon is not null)
        {
            var image = new Image { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
            image.Source = row.Icon;
            Grid.SetColumn(image, 1);
            grid.Children.Add(image);
        }

        var who = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(who, 2);
        who.Children.Add(new TextBlock { Text = row.Title, FontSize = 13 });
        who.Children.Add(new TextBlock { Text = row.Line, FontSize = 11.5, Opacity = 0.65 });
        grid.Children.Add(who);

        if (row.InUse)
        {
            var now = new TextBlock
            {
                Text = L.T("Current"),
                FontSize = 11,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(now, 3);
            grid.Children.Add(now);
        }
        return grid;
    }
}
