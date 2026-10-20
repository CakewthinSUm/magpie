using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Sessions page: the sessions of one agent on this computer, by the day
/// they last ran, and the one picked beside them with its facts, what it
/// spent and the command that picks it up again (GET /api/sessions/manage).
/// A delete asks first in a dialog and moves the session's files to magpie's
/// trash, where Trash lists it with a Restore (POST /api/sessions/delete and
/// /api/sessions/restore); nothing is erased for good here.
///
/// The page's own list groups by folder and nests subagents. Windows' list
/// for this is by day, a master/detail, and a session's conversation is the
/// page's for now: the pane says so rather than leaving the reader guessing.
/// </summary>
public sealed partial class SessionsPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    private SessionsManageJson _data = new();
    private string _agent = "";
    private SessionJson? _picked;
    private string _find = "";
    private bool _trash;
    private bool _drawing;
    /// <summary>The one line the page says about the list when nothing
    /// failed: what it can't do with some of the sessions shown, or that
    /// there are none yet. Empty when there is nothing to say.</summary>
    private string _note = "";
    /// <summary>The note the reader closed: a redraw doesn't put it back, as
    /// an InfoBar's × means it.</summary>
    private string _noteClosed = "";

    public SessionsPage()
    {
        InitializeComponent();
        ListNotice.CloseButtonClick += (_, _) => _noteClosed = ListNotice.Message;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            _data = await App.CurrentApp.Client.GetAsync<SessionsManageJson>(
                "sessions/manage?agent=" + Uri.EscapeDataString(_agent)) ?? new SessionsManageJson();
            // magpie answers with the agent it showed, the one with the most
            // sessions when none was named
            _agent = _data.Agent;
            Draw();
            // a row's icon is a picture of its own, read through the signed-in
            // client and drawn on the next pass
            await PageKitUI.LoadIconsAsync(_data.Sessions.Select(s => s.Icon)
                .Concat(_data.Trash.Select(t => t.Icon))
                .Concat(_data.Agents.Select(a => a.Icon)), Draw);
        }, Status, Busy);
    }

    private void Draw()
    {
        _drawing = true;
        try
        {
            DrawAgentPick();
            DrawList();
            DrawTrash();
            DrawDetail();
        }
        finally { _drawing = false; }
        // the one line: what a write just did; the list's own note is an
        // InfoBar above it
        Status.Text = "";
    }

    /// <summary>The agent's button: its name and how many sessions, and the
    /// menu of every agent magpie has sessions for.</summary>
    private void DrawAgentPick()
    {
        var current = _data.Agents.FirstOrDefault(a => a.Agent == _data.Agent);
        AgentPickIcon.Source = IconCached(current?.Icon ?? "");
        AgentPickName.Text = current?.Name is { Length: > 0 } name ? name : "Sessions";
        AgentPickCount.Text = current is null ? "" : current.Count.ToString();
        AgentPick.IsEnabled = _data.Agents.Count > 1;
        TrashCount.Text = _data.Trash.Count > 0 ? _data.Trash.Count.ToString() : "";
        EmptyTrash.IsEnabled = _data.Trash.Count > 0;
    }

    private void DrawList()
    {
        TrashPane.Visibility = _trash ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = _trash ? Visibility.Collapsed : Visibility.Visible;
        if (TrashToggle.IsChecked != _trash) TrashToggle.IsChecked = _trash;

        var rows = new List<SessionRow>();
        // every row is of the one agent shown, and magpie names no icon for
        // a session itself: its agent's mark is the row's
        var agentIcon = IconCached(_data.Agents.FirstOrDefault(a => a.Agent == _data.Agent)?.Icon ?? "");
        var shown = Shown();
        string? day = null;
        foreach (var s in shown)
        {
            var d = DayOf(s.Last);
            rows.Add(new SessionRow
            {
                Session = s,
                Header = d == day ? "" : d,
                HeaderVisibility = d == day ? Visibility.Collapsed : Visibility.Visible,
                Title = TitleOf(s),
                Line = LineOf(s),
                Tokens = s.Input + s.Output > 0 ? FmtN(s.Input + s.Output) : "—",
                Cost = CostOf(s),
                Icon = agentIcon,
            });
            day = d;
        }
        List.ItemsSource = rows;
        Empty.Visibility = shown.Count == 0 && !_trash ? Visibility.Visible : Visibility.Collapsed;
        Empty.Text = _find.Length > 0 ? "No session matches." : "No sessions yet.";

        // what the list can't do, said above it, as the page says it
        _note = "";
        if (!_trash && _data.Agents.Count == 0)
            _note = "No sessions yet: the sessions of Claude Code, Codex, OpenCode and the other agents on this computer show up here.";
        else if (!_trash && _data.Sessions.Any(s => s.Gateway))
            _note = "Sessions marked gateway have no files of their agent's on this computer, so they can't be resumed here. Delete takes one off this list and moves any conversation magpie recorded of it to its trash; Usage keeps what it spent.";
        else if (!_trash && _data.Sessions.Any(s => !s.Deletable))
            _note = "Some sessions here are read only: magpie can list them, but cannot delete them.";

        // the note is a line of its own above the list, never the header's
        if (!ListNotice.IsOpen && ListNotice.Message != _note && _noteClosed != _note)
        {
            ListNotice.Message = _note;
            ListNotice.IsOpen = _note.Length > 0;
        }
        if (_note.Length == 0) _noteClosed = "";
    }

    private void DrawTrash()
    {
        var rows = new List<TrashRow>();
        foreach (var t in _data.Trash)
        {
            var parts = new List<string>();
            if (t.Name.Length > 0) parts.Add(t.Name);
            if (t.Gateway) parts.Add("seen through the gateway");
            else if (t.Cwd.Length > 0) parts.Add(ShortPath(t.Cwd));
            parts.Add("deleted " + Ago(t.Deleted));
            parts.Add(t.Gateway && t.Size == 0 ? "no conversation kept" : FmtBytes(t.Size));
            rows.Add(new TrashRow
            {
                Trashed = t,
                Title = t.Title is { Length: > 0 } title ? title : t.Id,
                Line = string.Join(" · ", parts),
                Icon = IconCached(t.Icon),
            });
        }
        TrashList.ItemsSource = rows;
        TrashEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DrawDetail()
    {
        var s = _picked;
        DetailHint.Visibility = s is null ? Visibility.Visible : Visibility.Collapsed;
        DetailCard.Visibility = s is null ? Visibility.Collapsed : Visibility.Visible;
        if (s is null) return;

        DetailIcon.Source = IconCached(_data.Agents.FirstOrDefault(a => a.Agent == s.Agent)?.Icon ?? s.Icon);
        DetailTitle.Text = TitleOf(s);
        DetailAgent.Text = AgentName(s.Agent) + (s.Wsl is { Length: > 0 } wsl ? " · WSL " + wsl : "");
        DetailRan.Text = Stamp(s.Start == default ? s.Last : s.Start) + " – " + Stamp(s.Last) + " (" + Ago(s.Last) + ")";
        DetailFolder.Text = s.Cwd is { Length: > 0 } cwd ? cwd : (s.Gateway ? "no folder on this computer (seen through the gateway)" : "—");
        DetailId.Text = s.Id;
        DetailFile.Text = s.Path is { Length: > 0 } path ? path + (s.Files > 1 ? $" +{s.Files - 1} more" : "") : "—";

        ResumeBox.Visibility = s.Resume is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        DetailResume.Text = s.Resume;

        var models = new List<ModelRow>();
        foreach (var m in s.Models)
        {
            var parts = new List<string> { FmtN(m.Input) + " in · " + FmtN(m.Output) + " out" };
            if (m.CacheRead > 0) parts.Add(FmtN(m.CacheRead) + " cached");
            if (m.CacheWrite > 0) parts.Add(FmtN(m.CacheWrite) + " written");
            models.Add(new ModelRow
            {
                Name = m.Model is { Length: > 0 } n ? n : "a model with no name",
                Line = string.Join(" · ", parts),
                Cost = m.Priced ? "≈" + Money(m.Cost) : "—",
            });
        }
        ModelList.ItemsSource = models;
        ModelsNone.Visibility = models.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var vias = new List<ViaRow>();
        foreach (var v in s.Via)
        {
            vias.Add(new ViaRow
            {
                Line = v.Provider + "/" + v.Model + (v.Effort is { Length: > 0 } e ? " · " + e : "")
                    + " · " + v.Calls + (v.Calls == 1 ? " call" : " calls") + " · " + FmtN(v.Tokens) + " tokens",
            });
        }
        ViaList.ItemsSource = vias;
        ViaBox.Visibility = vias.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        DeleteOne.IsEnabled = s.Deletable;
    }

    /// <summary>The sessions the filter leaves: its words in the title, the
    /// folder, the id or a model's name, as the page narrows its list.</summary>
    private List<SessionJson> Shown()
    {
        var q = _find.Trim();
        if (q.Length == 0) return _data.Sessions;
        return _data.Sessions.Where(s =>
            s.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            s.Cwd.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            s.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            s.Models.Any(m => m.Model.Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private string TitleOf(SessionJson s) => s.Title is { Length: > 0 } title ? title : "(no prompt)";

    /// <summary>The line under a session's title, as the page's row has it:
    /// the folder, when it last ran, what it spent and how big it is.</summary>
    private static string LineOf(SessionJson s)
    {
        var parts = new List<string>();
        if (s.Gateway) parts.Add("gateway");
        else if (s.Cwd is { Length: > 0 } cwd) parts.Add(ShortPath(cwd));
        parts.Add(Ago(s.Last));
        if (s.Models.Count > 0) parts.Add(ModelNames(s));
        if (s.Messages > 0) parts.Add(s.Messages + (s.Messages == 1 ? " message" : " messages"));
        if (s.Size > 0) parts.Add(FmtBytes(s.Size) + (s.Gateway ? " recorded" : ""));
        return string.Join(" · ", parts);
    }

    /// <summary>The models it spent through, the first two and how many more,
    /// and where the gateway sent its calls.</summary>
    private static string ModelNames(SessionJson s)
    {
        var names = s.Models.Take(2).Select(m => m.Model is { Length: > 0 } n ? n : "a model with no name").ToList();
        var text = string.Join(", ", names);
        if (s.Models.Count > 2) text += " +" + (s.Models.Count - 2);
        if (s.Via.Count > 0)
        {
            var v = s.Via[0];
            text += " → " + v.Model + (v.Effort is { Length: > 0 } e ? " · " + e : "")
                + (s.Via.Count > 1 ? " +" + (s.Via.Count - 1) : "");
        }
        return text;
    }

    /// <summary>A session's cost, "—" when none of its models has a known
    /// price, and "+" when some of what it spent is unpriced.</summary>
    private static string CostOf(SessionJson s)
    {
        if (!s.Models.Any(m => m.Priced && (m.Input > 0 || m.Output > 0 || m.CacheRead > 0 || m.CacheWrite > 0))) return "—";
        return "≈" + Money(s.Cost) + (s.Unpriced > 0 ? "+" : "");
    }

    /// <summary>The day a session is filed under: today, yesterday, or its
    /// date, in the reader's own time.</summary>
    private static string DayOf(DateTimeOffset when)
    {
        var day = when.ToLocalTime().Date;
        var today = DateTime.Today;
        if (day == today) return "Today";
        if (day == today.AddDays(-1)) return "Yesterday";
        return day.ToString("ddd, MMM d", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>How long ago, in the reader's own words: "2 hours ago".</summary>
    private static string Ago(DateTimeOffset when)
    {
        if (when == default) return "never";
        var span = DateTimeOffset.Now - when.ToLocalTime();
        var (n, unit) = span.TotalMinutes switch
        {
            < 1 => (0, ""),
            < 60 => ((int)span.TotalMinutes, "minute"),
            < 60 * 24 => ((int)span.TotalHours, "hour"),
            < 60 * 24 * 7 => ((int)span.TotalDays, "day"),
            < 60 * 24 * 30 => ((int)(span.TotalDays / 7), "week"),
            < 60 * 24 * 365 => ((int)(span.TotalDays / 30), "month"),
            _ => ((int)(span.TotalDays / 365), "year"),
        };
        if (unit.Length == 0) return "just now";
        if (n <= 0) n = 1;
        return n + " " + unit + (n == 1 ? "" : "s") + " ago";
    }

    /// <summary>"Aug 12, 14:05", as the page stamps a session's times.</summary>
    private static string Stamp(DateTimeOffset when) =>
        when == default ? "—" : when.ToLocalTime().ToString("MMM d, HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A folder's last two parts, which is what tells two projects
    /// apart in a row; the whole path is in the session's details.</summary>
    private static string ShortPath(string path)
    {
        var parts = path.TrimEnd('\\', '/').Split('\\', '/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return path;
        if (parts.Length == 1) return parts[0];
        return parts[^2] + "\\" + parts[^1];
    }

    private static string FmtN(int n)
    {
        if (n >= 1_000_000_000) return (n / 1e9).ToString("0.00") + "B";
        if (n >= 10_000_000) return Math.Round(n / 1e6) + "M";
        if (n >= 1_000_000) return (n / 1e6).ToString("0.0") + "M";
        if (n >= 100_000) return Math.Round(n / 1e3) + "K";
        if (n >= 1_000) return (n / 1e3).ToString("0.0") + "K";
        return n.ToString();
    }

    private static string Money(double cost) =>
        cost >= 100 ? cost.ToString("0") : cost >= 1 ? cost.ToString("0.00") : cost.ToString("0.000");

    private static string FmtBytes(long n)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double v = n;
        var i = 0;
        while (v >= 1000 && i < units.Length - 1) { v /= 1000; i++; }
        return (i > 0 && v < 10 ? v.ToString("0.0") : Math.Round(v).ToString()) + " " + units[i];
    }

    private string AgentName(string id) =>
        _data.Agents.FirstOrDefault(a => a.Agent == id)?.Name is { Length: > 0 } name ? name : id;

    /// <summary>An agent's own logo, as every page's rows share it: the
    /// picture read already, or none until the read lands (LoadAsync draws
    /// the page again when it has).</summary>
    private static ImageSource? IconCached(string icon) => PageKitUI.IconCached(icon);

    /// <summary>The window's width decides the layout: the list and the
    /// picked session side by side when there is room for both, one above
    /// the other when there isn't, so neither is ever cut off.</summary>
    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 32,24 padding and the 20 between the columns are already out of e.NewSize
        var side = e.NewSize.Width >= 760;
        if (side == (Grid.GetColumn(DetailPane) == 1)) return;
        Grid.SetColumn(DetailPane, side ? 1 : 0);
        Grid.SetRow(DetailPane, side ? 3 : 4);
        DetailRow.Height = side ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        DetailColumn.Width = side ? new GridLength(1.1, GridUnitType.Star) : new GridLength(0);
        DetailPane.Margin = side ? new Thickness(0) : new Thickness(0, 12, 0, 0);
    }

    // ---- clicks ----

    private void AgentPick_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        foreach (var a in _data.Agents)
        {
            var item = new MenuFlyoutItem { Text = a.Name is { Length: > 0 } n ? n : a.Agent, Tag = a.Agent };
            // its own logo where magpie has one, and how many sessions
            if (IconCached(a.Icon) is { } icon) item.Icon = new ImageIcon { Source = icon };
            item.Text += "   " + a.Count;
            if (a.Agent == _data.Agent) item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            item.Click += (_, _) => _ = PickAgentAsync(a.Agent);
            menu.Items.Add(item);
        }
        if (_data.Agents.Count == 0)
            menu.Items.Add(new MenuFlyoutItem { Text = "No agent has sessions", IsEnabled = false });
        menu.ShowAt(AgentPick);
    }

    /// <summary>A pick reads that agent's sessions again: magpie lists one
    /// agent at a time, and the answer names the one it showed.</summary>
    private async Task PickAgentAsync(string agent)
    {
        if (agent == _data.Agent) return;
        _agent = agent;
        _picked = null;
        _trash = false;
        await LoadAsync();
    }

    private void Find_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // only what the reader typed narrows the list, not the box setting
        // its own text
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _find = sender.Text;
        if (_drawing) return;
        DrawList();
    }

    private void TrashToggle_Click(object sender, RoutedEventArgs e)
    {
        _trash = TrashToggle.IsChecked == true;
        DrawList();
    }

    private void Row_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SessionRow row }) return;
        _picked = _picked?.Id == row.Session.Id ? null : row.Session;
        DrawDetail();
    }

    private async void CopyResume_Click(object sender, RoutedEventArgs e)
    {
        if (_picked?.Resume is not { Length: > 0 } command) return;
        var pack = new DataPackage();
        pack.SetText(command);
        Clipboard.SetContent(pack);
        // a short confirmation on the button itself, as the page's says Copied
        CopyResume.Content = "Copied";
        await Task.Delay(1400);
        CopyResume.Content = "Copy";
    }

    /// <summary>A write: its answer as the page's one line, then the list
    /// read again so what is drawn is what magpie wrote. A write that failed
    /// (and a read that then fails) keeps the red in the same line.</summary>
    private async Task WriteAsync(Func<Task<string>> work)
    {
        var after = "";
        await PageKit.RunAsync(async () => after = await work(), Status, Busy);
        if (Status.Text.Length > 0) after = Status.Text;
        await LoadAsync();
        if (Status.Text.Length == 0) Status.Text = after;
    }

    private async void DeleteOne_Click(object sender, RoutedEventArgs e)
    {
        if (_picked is { } s) await DeleteAsync([s]);
    }

    /// <summary>A delete asks first, then posts the ids: magpie moves their
    /// files to its trash, and one written to in the last minute is left
    /// alone and said so.</summary>
    private async Task DeleteAsync(List<SessionJson> list)
    {
        if (list.Count == 0) return;
        var names = string.Join("\n", list.Take(5).Select(s => "· " + TitleOf(s)));
        if (list.Count > 5) names += $"\n… and {list.Count - 5} more";
        var dialog = new ContentDialog
        {
            Title = list.Count == 1 ? "Delete this session?" : $"Delete {list.Count} sessions?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = names + "\n\n"
                    + $"Their files are moved to magpie's trash ({_data.TrashDir}), not erased: Trash puts them back. "
                    + "A session written to in the last minute is left alone, as its agent may still be running it.",
            },
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await WriteAsync(async () =>
        {
            var out_ = await App.CurrentApp.Client.PostAsync<SessionsDeleteJson>("sessions/delete",
                new { agent = _data.Agent, ids = list.Select(s => s.Id).ToList() }) ?? new SessionsDeleteJson();
            if (out_.Deleted.Contains(_picked?.Id ?? "")) _picked = null;
            if (out_.Refused.Count > 0)
            {
                var first = out_.Refused[0];
                var s = list.FirstOrDefault(x => x.Id == first.Id);
                var active = out_.Refused.Count(r => r.Active);
                return active > 0
                    ? $"{TitleOf(s ?? new SessionJson())} is still being written to; close it in {AgentName(_data.Agent)} and try again in a minute"
                    : (s is null ? first.Id : TitleOf(s)) + ": " + first.Error;
            }
            return out_.Deleted.Count == 1 ? "Moved to magpie's trash" : $"{out_.Deleted.Count} sessions moved to magpie's trash";
        });
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TrashRow row }) return;
        await WriteAsync(async () =>
        {
            await App.CurrentApp.Client.PostAsync("sessions/restore", new { key = row.Trashed.Key });
            return (row.Trashed.Title is { Length: > 0 } t ? t : row.Trashed.Id) + " restored";
        });
    }

    /// <summary>Emptying the trash erases for good, so it asks first: the
    /// page keeps that dialog too.</summary>
    private async void EmptyTrash_Click(object sender, RoutedEventArgs e)
    {
        if (_data.Trash.Count == 0) return;
        var dialog = new ContentDialog
        {
            Title = "Empty magpie's trash?",
            Content = $"Every session in magpie's trash ({_data.Trash.Count}) is erased for good: it can't be restored.",
            PrimaryButtonText = "Empty trash",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await WriteAsync(async () =>
        {
            await App.CurrentApp.Client.PostAsync("sessions/purge", new { all = true });
            return "Trash emptied";
        });
    }
}

/// <summary>One session in the list, and the day header that may come above
/// it: the list is one flat run, as ItemsControl draws it.</summary>
public sealed class SessionRow
{
    public SessionJson Session { get; set; } = new();
    public string Header { get; set; } = "";
    public Visibility HeaderVisibility { get; set; } = Visibility.Collapsed;
    public string Title { get; set; } = "";
    public string Line { get; set; } = "";
    public string Tokens { get; set; } = "";
    public string Cost { get; set; } = "";
    public ImageSource? Icon { get; set; }
}

/// <summary>A session in the trash, with its Restore.</summary>
public sealed class TrashRow
{
    public TrashedJson Trashed { get; set; } = new();
    public string Title { get; set; } = "";
    public string Line { get; set; } = "";
    public ImageSource? Icon { get; set; }
}

/// <summary>One model's share of the picked session.</summary>
public sealed class ModelRow
{
    public string Name { get; set; } = "";
    public string Line { get; set; } = "";
    public string Cost { get; set; } = "";
}

/// <summary>One place the gateway sent the picked session's calls.</summary>
public sealed class ViaRow
{
    public string Line { get; set; } = "";
}
