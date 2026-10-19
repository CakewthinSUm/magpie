using Magpie.Desktop.Backend;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Routing page: the gateway's own trace of each request, read again
/// every couple of seconds, beside the routing groups agents pick as one
/// model. A request picked from the list tells its story in order — every
/// account or key routing weighed, and each try with what the vendor said
/// (GET /api/gateway/trace, GET /api/groups). A manual group's pick and
/// whether magpie finds groups are written back (POST /api/groups/pick,
/// /api/groups/found), and every write answers with the whole list.
/// </summary>
public sealed partial class RoutingPage : Page, IRefreshable
{
    /// <summary>The requests the list shows; the gateway keeps 60.</summary>
    private const int Shown = 60;

    private readonly Dictionary<long, RouteJson> _routes = [];
    /// <summary>The agents' names by id, as GET /api/state has them.</summary>
    private readonly Dictionary<string, string> _agentNames = [];
    private GroupsJson _groups = new();
    /// <summary>Whether the groups were read: until they were, an empty
    /// list is "not yet", not "none".</summary>
    private bool _groupsLoaded;
    private DispatcherQueueTimer? _poll;
    private long _seq;
    /// <summary>The request the reader picked; 0 follows the newest.</summary>
    private long _picked;
    private bool _busy;
    private bool _drawingGroups;

    public RoutingPage() => InitializeComponent();

    /// <summary>What the title bar's refresh asks of this page: both
    /// halves, as the page's own refresh does.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
        // the trace is read on its own timer, as the page polls it: a
        // request shows up as it happens, not when the reader asks again
        _poll = DispatcherQueue.CreateTimer();
        _poll.Interval = TimeSpan.FromSeconds(2);
        _poll.Tick += (_, _) => _ = PollAsync();
        _poll.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // a timer left running reads a page that is gone
        _poll?.Stop();
        _poll = null;
    }

    private async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            // a page opened before magpie is up (a second launch waking this
            // app while it starts) reads again a moment later rather than
            // saying the list is empty
            for (var tries = 0; ; tries++)
            {
                try
                {
                    // the agents' names come with the state, as the page reads them
                    var state = await App.CurrentApp.Client.GetAsync<StateJson>("state");
                    foreach (var a in state?.Agents ?? []) _agentNames[a.Id] = a.Name;
                    _groups = await App.CurrentApp.Client.GetAsync<GroupsJson>("groups") ?? new GroupsJson();
                    _groupsLoaded = true;
                    break;
                }
                catch (InvalidOperationException) when (tries < 10)
                {
                    await Task.Delay(600);
                }
            }
            DrawGroups();
            await PollAsync();
        }, Status, Busy);
    }

    /// <summary>One read of the trace: what changed since the last one,
    /// with the counts. The page asks with the seq it has, so a quiet
    /// gateway sends nothing, and then the list is left as it is.</summary>
    private async Task PollAsync()
    {
        // a slow read while another is out would stack them up
        if (_busy) return;
        _busy = true;
        try
        {
            var trace = await App.CurrentApp.Client.GetAsync<TraceJson>($"gateway/trace?after={_seq}");
            if (trace is null) return;
            // the gateway started over: what the list had is no longer its
            if (trace.Seq < _seq) _routes.Clear();
            var changed = trace.Seq != _seq || trace.Routes.Count > 0;
            _seq = trace.Seq;
            foreach (var r in trace.Routes) _routes[r.Id] = r;
            foreach (var id in _routes.Keys.OrderBy(id => id).SkipLast(Shown).ToList()) _routes.Remove(id);
            if (changed) DrawRoutes(trace);
            else DrawTotals(trace);
        }
        catch (MagpieException e)
        {
            Status.Text = e.Message;
        }
        catch (Exception e)
        {
            // magpie went away between two reads: said, and read again
            Status.Text = e.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    private void DrawRoutes(TraceJson trace)
    {
        var rows = _routes.Values.OrderByDescending(r => r.Id).Select(RowOf).ToList();
        var selected = _picked;
        Routes.ItemsSource = rows;
        NoRoutes.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DrawTotals(trace);
        GatewayOff.IsOpen = !trace.Mine;
        GatewayOff.Message = trace.Mine ? "" : "This magpie doesn't serve the gateway, so its routing is another magpie's to show.";

        // the picked request keeps its place as the list is drawn again,
        // and follows the live one until the reader picks one
        var row = selected != 0 ? rows.FirstOrDefault(x => x.Id == selected) : rows.FirstOrDefault();
        if (row is null && selected != 0)
        {
            // the picked request left the list: back to live, as the page does
            _picked = 0;
            row = rows.FirstOrDefault();
        }
        if (row is not null && !ReferenceEquals(Routes.SelectedItem, row))
        {
            // selecting draws the story; the guard keeps the change from
            // being read as the reader's pick
            _drawingGroups = true;
            try { Routes.SelectedItem = row; }
            finally { _drawingGroups = false; }
        }
        DrawStory();
    }

    /// <summary>The counts since the gateway started, and the sentence the
    /// page puts under the stage: who serves it, and who it is watching.</summary>
    private void DrawTotals(TraceJson trace)
    {
        Totals.Text = $"{trace.Totals.Requests} requests · {trace.Totals.Rerouted} rerouted · {trace.Totals.Errors} errors";
        LiveDot.Opacity = trace.Mine ? 1 : 0.3;
        LiveText.Text = trace.Mine ? "live, as the gateway decides" : "the gateway is another magpie's";
    }

    /// <summary>One request as its row says it: who asked for what, who
    /// answered, and how it went.</summary>
    private RouteRow RowOf(RouteJson r)
    {
        var (how, where, model, fail) = Outcome(r);
        var line = "→ " + where + (model.Length > 0 ? " · " + model : "");
        // the model the reply says answered, when it isn't the one asked for
        if (r.Served is { Length: > 0 } served && r.Swapped) line += " · served " + served;
        var meta = new List<string>();
        if (r.Done) meta.Add(Took(r.Ms));
        if (r.Tokens > 0) meta.Add(Tokens(r.Tokens) + " tokens");
        if (r.Group is { } g) meta.Add("group " + g.Name);
        if (r.Folder is { Length: > 0 } folder) meta.Add(folder);
        return new RouteRow
        {
            Id = r.Id,
            Title = AgentName(r.Agent) + " · " + r.Model,
            Line = line,
            Meta = string.Join(" · ", meta),
            When = r.Time.ToLocalTime().ToString("HH:mm:ss"),
            FailStatus = how == "bad" ? fail : "",
        };
    }

    /// <summary>How a request ended: who answered it, or what its agent
    /// got. "ok" is one try, "moved" one after a failure, "bad" an error,
    /// "wait" one still out.</summary>
    private static (string How, string Where, string Model, string Fail) Outcome(RouteJson r)
    {
        if (!r.Done)
        {
            var last = r.Tries.LastOrDefault();
            var w = last is null ? null : Tried(r, last);
            return ("wait", w is null ? r.Provider : Where(w), w is null ? "" : (last!.Model ?? w.Model), "");
        }
        var ok = r.Tries.FirstOrDefault(t => t.Done && Ok(t));
        var seat = ok is null ? null : Tried(r, ok);
        if (r.Status < 400 && (ok is not null || r.Tries.Count == 0))
            return (r.Tries.Count > 1 ? "moved" : "ok",
                seat is null ? r.Provider : Where(seat),
                seat is null || ok is null ? "" : (ok.Model ?? seat.Model), "");
        var bad = r.Tries.LastOrDefault();
        var bw = bad is null ? null : Tried(r, bad);
        var fail = bad is null
            ? (r.Status > 0 ? r.Status + " " : "") + (r.Error ?? "")
            : $"{r.Status} · {FailWord(bad.Gone ? "canceled" : bad.Fail)}";
        return ("bad", bw is null ? r.Provider : Where(bw), bad is null ? "" : (bad.Model ?? bw?.Model ?? ""), fail);
    }

    /// <summary>The seat a try went to: its account or key, by the id, the
    /// model and the effort fixed on it, as the page matches them.</summary>
    private static WeighedJson? Tried(RouteJson r, TryJson tr) =>
        r.Order.FirstOrDefault(w => Seat(w.Id, w.Model, w.Fixed) == Seat(tr.Id, tr.Model ?? "", tr.Fixed))
        ?? r.Order.FirstOrDefault(w => w.Id == tr.Id && (w.Model ?? "") == (tr.Model ?? ""))
        ?? r.Order.FirstOrDefault(w => w.Id == tr.Id);

    private static string Seat(string id, string? model, string? fixedOn) =>
        id + "\u0000" + (model ?? "") + (fixedOn is { Length: > 0 } f ? ":" + f : "");

    /// <summary>Who an account, key or provider is on the page: the
    /// account, the key's name, or the provider.</summary>
    private static string Who(WeighedJson w) =>
        w.Kind == "provider" ? w.Name : (w.Who is { Length: > 0 } who ? who : w.Name);

    /// <summary>The story of the request picked (or the newest): the way it
    /// was routed, then each try in order with what the vendor said, the
    /// accounts routing weighed folded under it.</summary>
    private void DrawStory()
    {
        var r = _picked != 0 && _routes.TryGetValue(_picked, out var pinned) ? pinned : Newest();
        StoryTitle.Text = _picked != 0 && r is not null
            ? $"How the request at {r.Time.ToLocalTime():HH:mm} was routed"
            : "How the last request was routed";
        StorySub.Text = r is null ? "" : r.Group is { } g
            ? $"{AgentName(r.Agent)} asked for the routing group {g.Name}"
            : $"{AgentName(r.Agent)} asked for {r.Model}";
        StoryPanel.Children.Clear();
        if (r is null) return;

        if (r.Group is { } group)
            AddStoryLine($"The group has {group.Members.Count} members: " + string.Join(" · ", group.Members) + ".", MarkKind.Why);
        foreach (var p in r.Group?.Paused ?? [])
            AddStoryLine($"{p.Member} is paused by rule {p.Rule} of {p.Group}, so it is left out until that ends.", MarkKind.Why);

        for (var i = 0; i < r.Tries.Count; i++)
        {
            var tr = r.Tries[i];
            var w = Tried(r, tr);
            var name = w is null ? tr.Id : Who(w) + " (" + (tr.Model ?? w.Model) + ")"
                + (tr.Effort is { Length: > 0 } e ? " at " + e + " reasoning" : "");
            var ok = tr.Done && Ok(tr);
            var line = tr.Done
                ? ok
                    ? $"{name} answered in {Took(tr.Ms)}"
                        + (i > 0 ? $"; {AgentName(r.Agent)} got one clean reply and never saw the {i} that failed first." : ".")
                    : $"{name} answered {tr.Status} · {FailWord(tr.Gone ? "canceled" : tr.Fail)}"
                : $"{name} is answering…";
            AddStoryLine(line, !tr.Done ? MarkKind.Wait : ok ? MarkKind.Ok : MarkKind.Bad);
            // what the vendor said, word for word: the line above is
            // magpie's reading of it
            if (tr.Done && !ok && tr.Error is { Length: > 0 } said)
                AddStoryLine("It said: " + (said.Length > 600 ? said[..600] + "…" : said), MarkKind.Bad, quoted: true);
            if (tr.Rest is { } rest)
                AddStoryLine(RestHow(rest), MarkKind.Why);
        }
        if (r.Done && r.Tries.Count == 0)
            AddStoryLine("Nothing was tried: " + (r.Error ?? r.Status.ToString()), MarkKind.Bad);

        // the accounts and keys routing weighed, folded: the tries above are
        // the ones that were asked, which is the story the page leads with
        if (r.Order.Count > 0) StoryPanel.Children.Add(WeighedFold(r));
    }

    /// <summary>One line of the story: a mark whose colour says how it went,
    /// and the sentence, or the vendor's own words set apart in a monospace
    /// face.</summary>
    private void AddStoryLine(string text, MarkKind kind, bool quoted = false)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10), ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 0, 2),
            Background = BrushOf(kind),
        };
        var line = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Opacity = quoted ? 0.85 : 1,
        };
        if (quoted) line.FontFamily = new FontFamily("Consolas");
        Grid.SetColumn(line, 1);
        row.Children.Add(mark);
        row.Children.Add(line);
        StoryPanel.Children.Add(row);
    }

    /// <summary>The accounts and keys routing weighed, in its order, folded
    /// where a request has more than a handful.</summary>
    private static Expander WeighedFold(RouteJson r)
    {
        var rows = new StackPanel { Spacing = 4 };
        foreach (var w in r.Order)
        {
            var line = Where(w) + " · " + w.Model + (w.Fallback ? " · fallback" : "");
            if (w.Used > 0) line += " · " + Math.Round(w.Used) + "% used";
            if (w.Rest is { } rest) line += " · rests until " + rest.Until.ToLocalTime().ToString("HH:mm");
            rows.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75 });
        }
        return new Expander
        {
            Header = $"Where routing looked ({r.Order.Count})",
            IsExpanded = false,
            Content = rows,
            Margin = new Thickness(0, 4, 0, 0),
        };
    }

    /// <summary>How long a failed account or key sits out, as the page says
    /// it (gateway.Rest.By, routing.go).</summary>
    private static string RestHow(RestJson rest)
    {
        var time = rest.Until.ToLocalTime().ToString("HH:mm");
        return rest.By switch
        {
            "retry-after" => $"It rests until {time}, as the vendor's Retry-After says.",
            "credit" => "It sits out until someone tops it up.",
            "window" => $"Its allowance is used up: it rests until that renews, at {time}.",
            "resets" => $"It rests until {time}, when the vendor says the limit resets.",
            "quota" => "It rests: out of quota, with no word of when it resets.",
            "verify" => "The vendor wants the account verified first: it rests until you say it's verified.",
            "off" => "It doesn't rest: its provider is set to ask it again after an error like this.",
            "restricted" => "The vendor restricted the account: it rests until that is lifted.",
            "backoff" => "It rests longer each time it fails again.",
            "cooldown" => "It cools down: the vendor didn't say for how long.",
            _ => $"It rests until {time}.",
        };
    }

    /// <summary>The colour a story mark says how a line went: answered,
    /// failed, still out, or the reason routing did what it did.</summary>
    private static Brush BrushOf(MarkKind kind) => new SolidColorBrush(kind switch
    {
        MarkKind.Ok => Microsoft.UI.Colors.SeaGreen,
        MarkKind.Bad => Microsoft.UI.Colors.IndianRed,
        MarkKind.Wait => Microsoft.UI.Colors.Goldenrod,
        _ => Microsoft.UI.Colors.Gray,
    });

    /// <summary>Where an account or key sits, as the page names it: the
    /// provider, and the account or key when it has one.</summary>
    private static string Where(WeighedJson w) =>
        w.Kind == "provider" || string.IsNullOrEmpty(w.Who) ? (w.Name.Length > 0 ? w.Name : w.Provider) : $"{w.Name} · {w.Who}";

    private static bool Ok(TryJson tr) => tr.Status < 400 && string.IsNullOrEmpty(tr.Fail);

    private RouteJson? Newest() => _routes.Values.OrderByDescending(r => r.Id).FirstOrDefault();

    private void DrawGroups()
    {
        _drawingGroups = true;
        try
        {
            Found.IsOn = _groups.Found;
            var rows = _groups.Groups
                .Where(g => !g.Hidden)
                .OrderByDescending(g => !g.Auto)
                .Select(GroupRowOf)
                .ToList();
            Groups.ItemsSource = rows;
            NoGroups.Visibility = rows.Count == 0 && _groupsLoaded ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _drawingGroups = false; }
    }

    private GroupRow GroupRowOf(GroupJson g)
    {
        var info = g.MemberInfo.ToDictionary(m => m.Id);
        string NameOf(string id) =>
            info.TryGetValue(id, out var m) && m.Name is { Length: > 0 } n
                ? n + (m.Model is { Length: > 0 } model && !m.Group ? " · " + model : "")
                    + (m.Effort is { Length: > 0 } e ? " · " + e + " reasoning" : "")
                : id;
        var manual = g.Routing == "manual";
        var sep = g.Routing == "order" ? " → " : " · ";
        var members = g.Members.Count == 0
            ? (g.Patterns.Count > 0 ? string.Join(" · ", g.Patterns.Select(p => p.Pattern + " (" + p.Models + " models)")) : "no member")
            : string.Join(sep, g.Members.Select(NameOf));
        var note = "";
        if (g.ClassifierFailed is { } cf) note = "Classifier failed: " + cf.Error;
        else if (g.Disabled) note = "switched off";
        else if (!g.Ready) note = "no member ready";
        return new GroupRow
        {
            Id = g.Id,
            Group = g,
            Name = g.Name,
            Id_ = "group/" + g.Id,
            Members = members,
            Mode = ModeWord(g.Routing),
            Affinity = AffinityWord(g.Affinity),
            AffinityVisibility = string.IsNullOrEmpty(g.Affinity) ? Visibility.Collapsed : Visibility.Visible,
            FoundVisibility = g.Auto ? Visibility.Visible : Visibility.Collapsed,
            Note = note,
            NoteVisibility = note.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
            PickVisibility = manual && g.Members.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
            PickLabel = manual && g.Members.Count > 0 ? "Every request: " + NameOf(g.Picked ?? g.Members[0]) : "",
            Icon = ProviderIcon(g.MemberInfo.Select(m => m.Icon).FirstOrDefault(i => !string.IsNullOrEmpty(i))),
        };
    }

    private static string ModeWord(string? routing) => routing switch
    {
        "order" => "In order",
        "rotate" => "In turn",
        "usage" => "Least used",
        "pace" => "Weekly pace",
        "weight" => "By weight",
        "manual" => "Manual",
        _ => "Smart",
    };

    private static string AffinityWord(string? affinity) => affinity switch
    {
        "session" => "Session affinity",
        "turn" => "Within a turn",
        "off" => "No affinity",
        "strict" => "Strict affinity",
        _ => "",
    };

    /// <summary>A member's provider logo, from the page's icons
    /// (/api/icons serves them); none when magpie has none. A name that
    /// already carries its extension is used as it is: a provider's icon
    /// may be a plugin's .png, as the page asks for it.</summary>
    private static ImageSource? ProviderIcon(string? icon)
    {
        if (string.IsNullOrEmpty(icon)) return null;
        var app = App.CurrentApp;
        if (app.Client.BaseUrl.Length == 0) return null;
        var name = icon.EndsWith(".svg") || icon.EndsWith(".png") ? icon : icon + ".svg";
        return new BitmapImage(new Uri(app.Client.BaseUrl + "icons/" + Uri.EscapeDataString(name)));
    }

    private void Routes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_drawingGroups) return;
        if (Routes.SelectedItem is not RouteRow row) return;
        _picked = row.Id;
        BackToLive.Visibility = Visibility.Visible;
        DrawStory();
    }

    private void BackToLive_Click(object sender, RoutedEventArgs e)
    {
        _picked = 0;
        BackToLive.Visibility = Visibility.Collapsed;
        DrawStory();
    }

    /// <summary>A manual group's pick: the member every request goes to.
    /// The write answers with the whole list, so the page is drawn from
    /// what magpie wrote.</summary>
    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GroupRow row } button) return;
        var menu = new MenuFlyout();
        var picked = row.Group.Picked ?? row.Group.Members.FirstOrDefault();
        foreach (var id in row.Group.Members)
        {
            var info = row.Group.MemberInfo.FirstOrDefault(m => m.Id == id);
            var item = new MenuFlyoutItem
            {
                Text = (info?.Name is { Length: > 0 } n ? n : id)
                    + (info?.Model is { Length: > 0 } model ? " · " + model : ""),
                Tag = id,
            };
            if (id == picked) item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            if (info is { ProviderOff: true }) ToolTipService.SetToolTip(item, "Its provider is switched off: the group skips it until it is on again");
            else if (info is { Ready: false }) ToolTipService.SetToolTip(item, "No provider serves it now; it is skipped");
            item.Click += (_, _) => _ = PickAsync(row.Group.Id, id);
            menu.Items.Add(item);
        }
        menu.ShowAt(button);
    }

    private async Task PickAsync(string group, string pick)
    {
        await PageKit.RunAsync(async () =>
        {
            _groups = await App.CurrentApp.Client.PostAsync<GroupsJson>("groups/pick",
                new { id = group, pick }) ?? new GroupsJson();
            DrawGroups();
        }, Status, Busy);
    }

    /// <summary>Whether magpie finds groups on its own: one model several
    /// providers serve becomes a group of them.</summary>
    private async void Found_Toggled(object sender, RoutedEventArgs e)
    {
        if (_drawingGroups) return;
        var on = Found.IsOn;
        await PageKit.RunAsync(async () =>
        {
            _groups = await App.CurrentApp.Client.PostAsync<GroupsJson>("groups/found",
                new { on }) ?? new GroupsJson();
            DrawGroups();
        }, Status, Busy);
    }

    /// <summary>Making a group is its editor, which is the page's own form
    /// and not drawn here yet: the list says where it is (desktop/README.md).</summary>
    private void NewGroup_Click(object sender, RoutedEventArgs e) =>
        Status.Text = "Making a routing group is still the page's own editor: open Routing in magpie web for now.";

    /// <summary>An agent's name as the state gives it; its id when magpie
    /// doesn't know one.</summary>
    private string AgentName(string id) =>
        _agentNames.TryGetValue(id, out var name) && name.Length > 0 ? name
        : id switch
        {
            "other" => "another agent",
            "" => "an agent",
            _ => id,
        };

    /// <summary>A request's or a try's time, as the page says it: ms under
    /// a second, then seconds with a decimal under ten.</summary>
    private static string Took(long ms) => ms < 1000
        ? ms + " ms"
        : (ms / 1000.0).ToString(ms < 10000 ? "0.0" : "0") + " s";

    private static string Tokens(int n) => n >= 1_000_000
        ? (n / 1_000_000.0).ToString("0.0") + "M"
        : n >= 1000 ? (n / 1000.0).ToString("0.0") + "k" : n.ToString();

    /// <summary>Why a try failed, as the page's own words for it
    /// (gateway's fail* names, trace.go).</summary>
    private static string FailWord(string? why) => why switch
    {
        "rate" => "rate limited",
        "credit" => "out of credit",
        "quota" => "quota used up",
        "auth" => "sign-in required",
        "canceled" => "canceled",
        "foreign" => "another account's reasoning",
        "floor" => "reply too short",
        "verify" => "needs verification",
        "refused" => "refused (safety filter)",
        "shape" => "request not understood",
        "proxy" => "proxy not reachable",
        "effort" => "reasoning effort not in its plan",
        "overflow" => "too long for its model",
        "slow" => "slow to start",
        "prompt" => "agent's prompt turned away",
        "restricted" => "account restricted by the vendor",
        _ => "failed",
    };
}

/// <summary>One request in the list, as its row draws it.</summary>
public sealed class RouteRow
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Line { get; set; } = "";
    public string Meta { get; set; } = "";
    public string When { get; set; } = "";
    /// <summary>A failure's status and why, in red; empty for one that
    /// answered.</summary>
    public string FailStatus { get; set; } = "";
}

/// <summary>How a story's line went: answered, failed, still out, or the
/// reason routing did what it did.</summary>
public enum MarkKind
{
    Why,
    Ok,
    Bad,
    Wait,
}

/// <summary>One routing group's card, as the list draws it.</summary>
public sealed class GroupRow
{
    public string Id { get; set; } = "";
    public GroupJson Group { get; set; } = new();
    public string Name { get; set; } = "";
    /// <summary>The id the card shows ("group/&lt;id&gt;").</summary>
    public string Id_ { get; set; } = "";
    public string Members { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Affinity { get; set; } = "";
    public string Note { get; set; } = "";
    public string PickLabel { get; set; } = "";
    public Visibility AffinityVisibility { get; set; }
    public Visibility FoundVisibility { get; set; }
    public Visibility NoteVisibility { get; set; }
    public Visibility PickVisibility { get; set; }
    public ImageSource? Icon { get; set; }
}
