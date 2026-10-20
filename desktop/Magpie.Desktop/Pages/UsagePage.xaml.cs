using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Usage page, drawn here rather than by the page magpie serves: what
/// each subscription and key has left, read from the vendors
/// (GET /api/usage/quotas), and what this computer's gateway calls came to
/// over a period (GET /api/usage?period=), with each agent's and model's
/// share of them.
///
/// The two are read apart, as the page reads them: the allowances come from
/// the vendors and can be slow or refused, so the figures never wait for
/// them, and a refusal is the card's own to say.
/// </summary>
public sealed partial class UsagePage : Page, IRefreshable, ITabbed
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    /// <summary>The page's tabs, as the page names them (usageTab in
    /// page.js): the allowances and the period's figures, and the calls
    /// themselves. The page's own Sessions and Context tabs are not drawn
    /// here yet, so the sidebar lists these two.</summary>
    public IReadOnlyList<PageTab> TabList { get; } =
    [
        new("usage", "Overview"),
        new("requests", "Requests"),
    ];

    public event EventHandler? TabsChanged;

    public FrameworkElement Strip => Tabs;

    /// <summary>The tab shown: "usage" (Overview) until another is picked.</summary>
    private string _tab = "usage";

    public string Tab => _tab;

    /// <summary>The period shown, as the page's own picker names it
    /// (usage.Period, internal/usage): today until another is picked.</summary>
    private string _period = "today";
    private bool _settingPeriod;
    private bool _settingTab;
    /// <summary>The allowances last drawn, so a redraw of the page after its
    /// pictures are read draws the same cards.</summary>
    private List<QuotaJson> _quotas = [];

    // ---- the Requests tab's state, as the page keeps it: a filter, a
    // search, and the page of rows shown (100 at a time, newest first)
    private const int LedgerPage = 100;
    private string _ledAgent = "";
    private string _ledProvider = "";
    private string _ledStatus = "";
    private string _ledQuery = "";
    private int _ledOffset;
    private LedgerJson? _ledger;
    private bool _ledgerDirty = true;
    /// <summary>Set while the status strip is set in code, whose change is
    /// not the reader picking one.</summary>
    private bool _settingStatus;

    public UsagePage()
    {
        InitializeComponent();
        // the pickers show what they start on once they are in the tree:
        // their indicators are laid out from the selection when they load,
        // not when they are set from here
        Period.Loaded += (_, _) =>
        {
            _settingPeriod = true;
            try { Period.SelectedItem = Period.Items.FirstOrDefault(i => (string?)i.Tag == _period); }
            finally { _settingPeriod = false; }
        };
        Tabs.Loaded += (_, _) => ShowTab(_tab, read: false);
        // the status starts at All, which the strip says by marking it
        StatusPick.Loaded += (_, _) =>
        {
            _settingStatus = true;
            try { StatusPick.SelectedItem = StatusPick.Items.FirstOrDefault(i => (string?)i.Tag == _ledStatus); }
            finally { _settingStatus = false; }
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // the tab a link named (`usage&tab=requests`), as the page's own
        // links name one; else the page's first
        var named = TabsOf.Named(e.Parameter as string ?? "");
        if (named.Length > 0 && TabList.Any(t => t.Id == named)) _tab = named;
        await LoadAsync();
    }

    /// <summary>Shows a tab, as clicking it in the page's own strip does:
    /// the one panel in sight, and what it shows read if it has nothing
    /// drawn yet.</summary>
    public void SelectTab(string id)
    {
        if (id == _tab || TabList.All(t => t.Id != id)) return;
        ShowTab(id, read: true);
    }

    private void ShowTab(string id, bool read)
    {
        _tab = id;
        _settingTab = true;
        try { Tabs.SelectedItem = Tabs.Items.FirstOrDefault(i => (string?)i.Tag == id); }
        finally { _settingTab = false; }
        var requests = id == "requests";
        OverviewScroll.Visibility = requests ? Visibility.Collapsed : Visibility.Visible;
        RequestsPane.Visibility = requests ? Visibility.Visible : Visibility.Collapsed;
        // the cost total is the Overview's own figure
        CostTotal.Visibility = requests ? Visibility.Collapsed : Visibility.Visible;
        CostNote.Visibility = CostTotal.Visibility;
        // the tab is what the page was last on, for the row clicked in the pane
        TabMemory.Set("usage", id);
        TabsChanged?.Invoke(this, EventArgs.Empty);
        if (read && requests) _ = LoadLedgerAsync();
    }

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_settingTab) return;
        if (sender.SelectedItem?.Tag is not string id || id == _tab) return;
        ShowTab(id, read: true);
    }

    /// <summary>Reads the tab in sight: the figures and the allowances, or
    /// the period's calls.</summary>
    private async Task LoadAsync()
    {
        if (_tab == "requests")
        {
            await LoadLedgerAsync();
            return;
        }
        var period = _period;
        string? why = null;
        await PageKit.RunAsync(async () =>
        {
            // the allowances are read alongside the figures, not before them:
            // they come from the vendors and can be slow or refused, and the
            // figures mustn't wait behind a vendor that doesn't answer
            var quotas = LoadQuotasAsync();
            var usage = await App.CurrentApp.Client.GetAsync<UsageJson>("usage?period=" + Uri.EscapeDataString(period))
                        ?? new UsageJson();
            // another period was picked meanwhile, and reads its own figures:
            // the allowances it asked for are left to that read, not dropped
            if (period != _period)
            {
                Observe(quotas);
                return;
            }
            DrawUsage(usage);
            try { DrawQuotas(await quotas); }
            catch (Exception e) { why = e.Message; }
            // each card's and row's own logo, read through the signed-in
            // client and drawn on the pass after it has them
            if (period == _period)
            {
                await PageKitUI.LoadIconsAsync(
                    _quotas.Select(q => q.Icon).Concat(usage.Agents.Select(g => g.Icon)).Concat(usage.Models.Select(g => g.Icon)),
                    () => { DrawUsage(usage); DrawQuotas(_quotas); });
            }
        }, Status, Busy);

        if (why is not null)
        {
            // a read the page failed to make is worth saying in full, not
            // only in a line that may be scrolled past
            AllowancesError.Message = why;
            AllowancesError.IsOpen = true;
        }
    }

    // ---- the Requests tab -----------------------------------------------

    /// <summary>The calls the filters keep, a page at a time, as the page
    /// reads them (GET /api/usage/requests, usageapi.Ledger).</summary>
    private async Task LoadLedgerAsync()
    {
        var period = _period;
        await PageKit.RunAsync(async () =>
        {
            _ledger = await App.CurrentApp.Client.GetAsync<LedgerJson>("usage/requests?" + LedgerQuery()) ?? new LedgerJson();
            _ledgerDirty = false;
            // the period or a filter changed while this read was out: its
            // answer is not what the page now asks for
            if (period != _period) return;
            DrawLedger();
            // each row's agent's own mark, drawn on the pass after it is read
            await PageKitUI.LoadIconsAsync(_ledger.Rows.Select(r => r.Icon), () =>
            {
                if (_tab == "requests" && !_ledgerDirty) DrawLedger();
            });
        }, Status, Busy);
    }

    /// <summary>The query the ledger's filters make, as the page's own
    /// ledParams does: only what is picked, so an unfiltered read is the
    /// period's whole list.</summary>
    private string LedgerQuery()
    {
        var q = new List<string>
        {
            "period=" + Uri.EscapeDataString(_period),
            "offset=" + _ledOffset,
            "limit=" + LedgerPage,
        };
        if (_ledAgent.Length > 0) q.Add("agent=" + Uri.EscapeDataString(_ledAgent));
        if (_ledProvider.Length > 0) q.Add("provider=" + Uri.EscapeDataString(_ledProvider));
        if (_ledStatus.Length > 0) q.Add(_ledStatus + "=1");
        if (_ledQuery.Trim().Length > 0) q.Add("q=" + Uri.EscapeDataString(_ledQuery.Trim()));
        return string.Join("&", q);
    }

    /// <summary>The table's own width, as wide as its columns: the body is
    /// given it, so every row is laid out at the same width and a window
    /// narrower than it scrolls the table sideways.</summary>
    private void RequestsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = TableWidth();
        if (Math.Abs(RequestsBody.Width - width) > 0.5) RequestsBody.Width = width;
    }

    private void DrawLedger()
    {
        var l = _ledger ?? new LedgerJson();
        // the filters say what the period has, and the picks say what is on
        AgentPick.Content = _ledAgent.Length > 0 ? NameOf(l.Agents, _ledAgent) : "All agents";
        ProviderPick.Content = _ledProvider.Length > 0 ? NameOf(l.Providers, _ledProvider) : "All providers";

        var sum = new List<string> { Count(l.Total) + (l.Total == 1 ? " request" : " requests") };
        if (l.Total > 0)
        {
            sum.Add(Count(l.Input) + " in");
            sum.Add(Count(l.Output) + " out");
            sum.Add(Count(l.CacheWrite) + " cache write");
            sum.Add(Count(l.CacheRead) + " cache read");
            if (l.Errors > 0) sum.Add(l.Errors + " failed");
            if (l.Retried > 0) sum.Add(l.Retried + " answered on a retry");
            if (l.Cost > 0) sum.Add("≈" + Money(l.Cost));
        }
        RequestsSum.Text = string.Join(" · ", sum);

        RequestsBody.Children.Clear();
        RequestsNone.Visibility = l.Total == 0 ? Visibility.Visible : Visibility.Collapsed;
        RequestsNone.Text = Filtered()
            ? "No requests match these filters."
            : _period switch
            {
                "today" => "No calls today.",
                "7d" => "No calls in the last 7 days.",
                "30d" => "No calls in the last 30 days.",
                _ => "No calls yet.",
            };
        if (l.Total > 0)
        {
            RequestsBody.Children.Add(LedgerHead());
            foreach (var r in l.Rows) RequestsBody.Children.Add(LedgerRowOf(r));
        }

        // a page at a time, the newest first
        Pager.Visibility = l.Total > LedgerPage ? Visibility.Visible : Visibility.Collapsed;
        Newer.IsEnabled = l.Offset > 0;
        Older.IsEnabled = l.Offset + l.Rows.Count < l.Total;
        PagerText.Text = l.Rows.Count == 0
            ? ""
            : $"{Count(l.Offset + 1)}–{Count(l.Offset + l.Rows.Count)} of {Count(l.Total)}";
    }

    private bool Filtered() => _ledAgent.Length > 0 || _ledProvider.Length > 0 || _ledStatus.Length > 0 || _ledQuery.Trim().Length > 0;

    private static string NameOf(List<NamedJson> names, string id) =>
        names.FirstOrDefault(n => n.Id == id)?.Name is { Length: > 0 } name ? name : id;

    /// <summary>The table's columns, as the page's LED_COLS has them: the
    /// time, who asked, the model asked for, where it went, the tokens, the
    /// cost, how long it took, and how it ended. Each is as wide as what it
    /// holds: a window narrower than them all scrolls the table sideways, as
    /// the page's own table does, rather than cutting a name to two letters.
    /// </summary>
    private static readonly (string Name, double Width, bool Right)[] LedgerColumns =
    [
        ("Time", 120, false),
        ("Agent", 170, false),
        ("Requested", 160, false),
        ("Provider", 220, false),
        ("In", 70, true),
        ("Out", 70, true),
        ("Cache read", 90, true),
        ("Cost", 90, true),
        ("Duration", 90, true),
        ("Status", 130, false),
    ];

    /// <summary>How wide the table is with every column at its own width: a
    /// window narrower than this scrolls it sideways.</summary>
    private static double TableWidth() =>
        LedgerColumns.Sum(c => c.Width) + 12 * (LedgerColumns.Length - 1) + 24;

    /// <summary>One row of the table: its columns at their own widths, and the
    /// table's own width given to it, so a row's figures line up with the
    /// header's and with every other row's, scrolled sideways or not.</summary>
    private static Grid LedgerGrid() => new()
    {
        ColumnSpacing = 12,
        Padding = new Thickness(12, 6, 12, 6),
        // the row's own height: a Grid of TextBlocks in a StackPanel measures
        // them at no height at all, and every row came out invisible
        MinHeight = 30,
        Width = TableWidth(),
    };

    private static Grid Columns(Grid grid)
    {
        foreach (var (_, width, _) in LedgerColumns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        return grid;
    }

    private Grid LedgerHead()
    {
        var head = Columns(LedgerGrid());
        for (var i = 0; i < LedgerColumns.Length; i++)

        {
            var (name, _, right) = LedgerColumns[i];
            var text = new TextBlock
            {
                Text = name,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = 0.8,
                HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            };
            Grid.SetColumn(text, i);
            head.Children.Add(text);
        }
        return head;
    }

    /// <summary>One call, as a row: a failed one says its reason in the
    /// status cell and takes the critical ink, as the page's rows do.</summary>
    private Grid LedgerRowOf(LedgerRowJson r)
    {
        var failed = r.Status >= 400 || r.Error is { Length: > 0 };
        var row = Columns(LedgerGrid());
        row.Background = failed ? PageKitUI.Brush("SystemFillColorCriticalBackgroundBrush") : null;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var at = r.T.LocalDateTime;
        var agent = r.AgentName is { Length: > 0 } a ? a : r.Agent;
        if (r.ComputerName is { Length: > 0 } computer) agent += " · on " + computer;
        var where = r.ProviderName is { Length: > 0 } p ? p : r.Provider;
        if (r.Host is { Length: > 0 } host) where += " · " + host;

        var cells = new (string Text, bool Right, string Tip)[]
        {
            (at.Date == DateTime.Today ? at.ToString("HH:mm:ss", culture) : at.ToString("d MMM HH:mm", culture), false, at.ToString("g", System.Globalization.CultureInfo.CurrentCulture)),
            (agent, false, r.Session is { Length: > 0 } s ? "session " + s : ""),
            (r.Requested ?? "—", false, ""),
            (where, false, ""),
            (Count(r.Input), true, ""),
            (Count(r.Output), true, r.Reasoning > 0 ? Count(r.Reasoning) + " reasoning, inside output" : ""),
            (r.CacheRead > 0 ? Count(r.CacheRead) : "—", true, ""),
            (r.Priced ? "≈" + Money(r.Cost) : "—", true, r.Priced ? "" : "No known price for this model"),
            (r.Millis > 0 ? Took(r.Millis) : "—", true, r.Millis > 0 ? r.Millis + " ms" : ""),
            (failed ? (r.Status > 0 ? r.Status + (r.ErrType is { Length: > 0 } t ? " · " + t : "") : "Failed") : (r.Status > 0 ? r.Status.ToString() : "—"), false, failed ? r.Error ?? "" : ""),
        };
        for (var i = 0; i < cells.Length; i++)
        {
            var (text, right, tip) = cells[i];
            var block = new TextBlock
            {
                Text = text,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // the ink is only ever set, never cleared: a null set here would
            // be a local value, and a cell's own null wins over the theme's
            // brush — the text was drawn with no paint at all
            if (i == cells.Length - 1 && failed) block.Foreground = PageKitUI.Brush("SystemFillColorCriticalBrush");
            if (tip.Length > 0) ToolTipService.SetToolTip(block, tip);
            Grid.SetColumn(block, i);
            row.Children.Add(block);
        }
        return row;
    }

    /// <summary>How long a call took, as the page writes it short: seconds
    /// under a minute, then minutes.</summary>
    private static string Took(long ms) =>
        ms < 60_000 ? (ms / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s"
        : (ms / 60_000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " min";

    private void AgentPick_Click(object sender, RoutedEventArgs e)
    {
        var l = _ledger ?? new LedgerJson();
        var menu = new MenuFlyout();
        AddFilter(menu, "All agents", "", () => { _ledAgent = ""; });
        foreach (var n in l.Agents) AddFilter(menu, n.Name, n.Id, () => { _ledAgent = n.Id; });
        menu.ShowAt(AgentPick);
    }

    private void ProviderPick_Click(object sender, RoutedEventArgs e)
    {
        var l = _ledger ?? new LedgerJson();
        var menu = new MenuFlyout();
        AddFilter(menu, "All providers", "", () => { _ledProvider = ""; });
        foreach (var n in l.Providers) AddFilter(menu, n.Name, n.Id, () => { _ledProvider = n.Id; });
        menu.ShowAt(ProviderPick);
    }

    /// <summary>A filter's choice: picking it starts the list again at its
    /// first page, as the page's own filters do.</summary>
    private void AddFilter(MenuFlyout menu, string text, string id, Action pick)
    {
        var item = new MenuFlyoutItem { Text = L.T(text), Tag = id };
        item.Click += (_, _) =>
        {
            pick();
            _ledOffset = 0;
            _ledgerDirty = true;
            _ = LoadLedgerAsync();
        };
        menu.Items.Add(item);
    }

    private void StatusPick_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_settingStatus) return;
        _ledStatus = sender.SelectedItem?.Tag as string ?? "";
        _ledOffset = 0;
        _ledgerDirty = true;
        _ = LoadLedgerAsync();
    }

    private async void Find_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ledQuery = Find.Text;
        // the page waits for the typing to stop, as its own search does: a
        // read a keystroke is worth is a read per keystroke
        _findWait?.Cancel();
        var wait = _findWait = new CancellationTokenSource();
        try { await Task.Delay(250, wait.Token); }
        catch (OperationCanceledException) { return; }
        _ledOffset = 0;
        _ledgerDirty = true;
        await LoadLedgerAsync();
    }

    private CancellationTokenSource? _findWait;

    private void Newer_Click(object sender, RoutedEventArgs e)
    {
        _ledOffset = Math.Max(0, (_ledger?.Offset ?? 0) - LedgerPage);
        _ledgerDirty = true;
        _ = LoadLedgerAsync();
    }

    private void Older_Click(object sender, RoutedEventArgs e)
    {
        _ledOffset = (_ledger?.Offset ?? 0) + LedgerPage;
        _ledgerDirty = true;
        _ = LoadLedgerAsync();
    }

    /// <summary>Saves every call the filters keep, all pages, as a CSV in
    /// Downloads (POST /api/usage/requests/export), as the page's Export
    /// does; magpie opens the folder it wrote it in.</summary>
    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        await PageKit.RunAsync(async () =>
        {
            var q = LedgerQuery().Split('&').Where(p => !p.StartsWith("offset=", StringComparison.Ordinal) && !p.StartsWith("limit=", StringComparison.Ordinal));
            var saved = await App.CurrentApp.Client.PostAsync<SavedCsvJson>("usage/requests/export?" + string.Join("&", q));
            Status.Text = saved is null ? "" : $"Saved {saved.Rows} requests to {saved.Path}";
        }, Status, Busy);
    }

    /// <summary>The allowances, asked for as the page asks when the reader
    /// opens it: ?asked=1 is what has a Claude account's usage read at once
    /// by running Claude Code's own /usage.</summary>
    private static Task<List<QuotaJson>?> LoadQuotasAsync() =>
        App.CurrentApp.Client.GetAsync<List<QuotaJson>>("usage/quotas?asked=1");

    /// <summary>A read whose answer is no longer drawn still ends: its
    /// failure is taken, so it isn't raised as one nobody handled.</summary>
    private static void Observe(Task task) =>
        _ = task.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);

    private void DrawQuotas(List<QuotaJson>? quotas)
    {
        AllowancesError.IsOpen = false;
        var cards = (quotas ?? []).Select(CardOf).ToList();
        _quotas = quotas ?? [];
        Quotas.ItemsSource = cards;
        AllowancesEmpty.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static QuotaCardRow CardOf(QuotaJson q)
    {
        var account = AccountLineOf(q);
        return new QuotaCardRow
        {
            Name = q.Name.Length > 0 ? q.Name : q.Provider,
            Icon = IconOf(q.Icon),
            Account = account,
            AccountVisibility = Shown(account.Length > 0),
            Windows = q.Windows.Select(WindowOf).ToList(),
            WindowsVisibility = Shown(q.Windows.Count > 0),
            Balance = q.Balance is { Length: > 0 } b ? "Balance " + b : "",
            BalanceVisibility = Shown(q.Balance is { Length: > 0 }),
            Error = q.Error ?? "",
            ErrorVisibility = Shown(q.Error is { Length: > 0 }),
        };
    }

    /// <summary>The line under a card's name: the account it is of, the
    /// name the user gave it, and the plan — each only when there is one.</summary>
    private static string AccountLineOf(QuotaJson q)
    {
        var parts = new List<string>();
        if (q.Alias is { Length: > 0 } alias) parts.Add(alias);
        if (q.User is { Length: > 0 } user && user != q.Alias) parts.Add(user);
        if (q.Plan is { Length: > 0 } plan) parts.Add(plan);
        // the plan's paid time ends then, and whether it renews: the vendor
        // doesn't always say which. The page is in English, so its dates are
        // too, whatever the system's language.
        if (q.Until is { } until) parts.Add((q.Renew == "auto" ? "Renews " : q.Renew == "off" ? "Expires " : "Until ") + until.LocalDateTime.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture));
        return string.Join(" · ", parts);
    }

    private static QuotaWindowRow WindowOf(QuotaWindowJson w)
    {
        // how much of it is used, as the page's meters say it: the vendor's
        // own count first where it gives one, then the share
        string text;
        if (w.Unlimited) text = "Unlimited";
        else
        {
            var used = Math.Max(0, Math.Min(100, w.Used));
            var pct = (used == Math.Round(used) ? used.ToString("0") : used.ToString("0.#")) + "% used";
            text = w.Limit > 0 ? Amount(w.Amount) + " / " + Amount(w.Limit) + (w.Unit is { Length: > 0 } u ? " " + u : "") + " · " + pct
                 : w.Display is { Length: > 0 } d ? d + " · " + pct
                 : pct;
        }
        return new QuotaWindowRow
        {
            Name = w.Name,
            Text = text,
            Used = w.Unlimited ? 0 : Math.Max(0, Math.Min(100, w.Used)),
            BarVisibility = Shown(!w.Unlimited),
            Reset = ResetLineOf(w.ResetsAt),
            ResetVisibility = Shown(w.ResetsAt is not null),
        };
    }

    /// <summary>When a window starts again, on the clock and how long until
    /// then, as the page's cards say it.</summary>
    private static string ResetLineOf(DateTimeOffset? at)
    {
        if (at is not { } when) return "";
        if (when <= DateTimeOffset.Now) return "Reset time passed";
        var local = when.LocalDateTime;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var clock = local.Date == DateTime.Today
            ? local.ToString("HH:mm", culture)
            : local.ToString("d MMM HH:mm", culture);
        return "Resets " + clock + " · " + UntilText(when);
    }

    /// <summary>How long until a moment, in the page's words: minutes under
    /// an hour, then hours under two days, then days.</summary>
    private static string UntilText(DateTimeOffset at)
    {
        var mins = Math.Max(1, (int)Math.Round((at - DateTimeOffset.Now).TotalMinutes));
        if (mins < 60) return $"in {mins}m";
        var hours = (int)Math.Round(mins / 60.0);
        if (hours < 48) return $"in {hours}h";
        return $"in {(int)Math.Round(hours / 24.0)}d";
    }

    private void DrawUsage(UsageJson u)
    {
        CostTotal.Text = u.Cost > 0 || u.Unpriced > 0 ? "≈" + Money(u.Cost) : "";
        CostNote.Text = u.Unpriced > 0
            ? $"{u.Unpriced} call{(u.Unpriced == 1 ? "" : "s")} unpriced"
            : "effective prices";

        var empty = u.Calls == 0;
        Overview.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            Empty.Text = EmptyText() +
                " Point an agent at a catalog model and use it; every call through the gateway is counted here.";
            Note.Text = "";
            return;
        }

        // the period's counts, as the page's tiles put them: tokens with the
        // cache's apart, then the calls and what went wrong in them
        var prompt = u.Input + u.CacheRead + u.CacheWrite;
        var hit = u.CacheRead > 0 && prompt > 0 ? $"{Math.Round(100.0 * u.CacheRead / prompt)}% from cache" : "";
        var calls = u.Calls == 1 ? "call" : "calls";
        var failed = new List<string>();
        if (u.Errors > 0) failed.Add($"{u.Errors} failed");
        if (u.Unpriced > 0) failed.Add($"{u.Unpriced} unpriced");
        Tiles.ItemsSource = new List<TileRow>
        {
            new() { Value = Count(u.Input + u.Output), Label = "tokens", Sub = $"{Count(u.Input)} in · {Count(u.Output)} out" },
            new() { Value = Count(u.CacheRead), Label = "cache read", Sub = hit },
            new() { Value = Count(u.Reasoning), Label = "reasoning", Sub = "inside output" },
            new() { Value = u.Calls.ToString(), Label = calls, Sub = string.Join(" · ", failed) },
        };

        // each agent's and model's share of the period's tokens, the most
        // first, which is how the page lists them
        var total = Math.Max(1, u.Input + u.Output);
        AgentsList.ItemsSource = u.Agents.Select(g => ShareOf(g, total)).ToList();
        ModelsList.ItemsSource = u.Models.Select(g => ShareOf(g, total)).ToList();
        Note.Text = "Counted from the providers' own usage reports on every call through the gateway · " + u.Path;
    }

    private static ShareRow ShareOf(UsageGroupJson g, int total)
    {
        var tokens = g.Input + g.Output;
        var sub = new List<string>();
        if (g.Sub is { Length: > 0 } s) sub.Add(s);
        sub.Add(g.Calls == 1 ? "1 call" : $"{g.Calls} calls");
        if (g.Errors > 0) sub.Add($"{g.Errors} failed");
        var prompt = g.Input + g.CacheRead + g.CacheWrite;
        var detail = $"{Count(g.Input)} in · {Count(g.Output)} out";
        if (g.CacheRead > 0) detail += " · " + Count(g.CacheRead) + " cached";
        if (g.CacheRead > 0 && prompt > 0) detail += " · " + Math.Round(100.0 * g.CacheRead / prompt) + "% hit";
        return new ShareRow
        {
            Name = g.Name.Length > 0 ? g.Name : "account not recorded",
            Icon = IconOf(g.Icon),
            Sub = string.Join(" · ", sub),
            SubVisibility = Shown(sub.Count > 0),
            Share = Math.Max(1.5, 100.0 * tokens / total),
            Tokens = Count(tokens),
            Detail = detail,
            Cost = g.Cost > 0 ? "≈" + Money(g.Cost) : "",
        };
    }

    /// <summary>What an empty period says of it, in the page's words.</summary>
    private string EmptyText() => _period switch
    {
        "today" => "No calls today.",
        "7d" => "No calls in the last 7 days.",
        "30d" => "No calls in the last 30 days.",
        _ => "No calls yet.",
    };

    /// <summary>A count as the page writes it short: 133M, 68.1K, and a
    /// plain number under a thousand.</summary>
    private static string Count(int n) => n switch
    {
        >= 1_000_000_000 => (n / 1e9).ToString("0.##") + "B",
        >= 10_000_000 => Math.Round(n / 1e6).ToString("0") + "M",
        >= 1_000_000 => (n / 1e6).ToString("0.#") + "M",
        >= 100_000 => Math.Round(n / 1e3).ToString("0") + "K",
        >= 1_000 => (n / 1e3).ToString("0.#") + "K",
        _ => n.ToString(),
    };

    /// <summary>A window's own count, in the vendor's unit: credits, say,
    /// which aren't tokens and aren't short-scaled.</summary>
    private static string Amount(double n) => n == Math.Round(n) ? n.ToString("0") : n.ToString("0.#");

    /// <summary>A cost in dollars, at the page's own precision: whole
    /// dollars past a hundred, cents past one, thousandths under it.</summary>
    private static string Money(double c) => "$" + (c >= 100 ? c.ToString("0") : c >= 1 ? c.ToString("0.00") : c.ToString("0.000"));

    /// <summary>The provider's own logo, from the page's icons folder: the
    /// picture read already, or none until the read lands (LoadAsync draws
    /// the page again when it has).</summary>
    private static ImageSource? IconOf(string? icon) => PageKitUI.IconCached(icon);

    private static Visibility Shown(bool on) => on ? Visibility.Visible : Visibility.Collapsed;

    private async void Period_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_settingPeriod) return;
        if (sender.SelectedItem?.Tag is not string id || id == _period) return;
        _period = id;
        await LoadAsync();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        _ledgerDirty = true;
        _ = LoadAsync();
    }
}

/// <summary>Where the page's Export wrote the file, and how many rows it
/// holds (gui's usage/requests/export answer).</summary>
public sealed class SavedCsvJson
{
    public string Path { get; set; } = "";
    public int Rows { get; set; }
}

/// <summary>One subscription's or key's card, as the allowances draw it.</summary>
public sealed class QuotaCardRow
{
    public string Name { get; set; } = "";
    public string Account { get; set; } = "";
    public ImageSource? Icon { get; set; }
    public List<QuotaWindowRow> Windows { get; set; } = [];
    public string Balance { get; set; } = "";
    public string Error { get; set; } = "";
    public Visibility AccountVisibility { get; set; }
    public Visibility WindowsVisibility { get; set; }
    public Visibility BalanceVisibility { get; set; }
    public Visibility ErrorVisibility { get; set; }
}

/// <summary>One window of an allowance: its name, how much of it is used,
/// and when it starts again.</summary>
public sealed class QuotaWindowRow
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public double Used { get; set; }
    public string Reset { get; set; } = "";
    public Visibility BarVisibility { get; set; }
    public Visibility ResetVisibility { get; set; }
}

/// <summary>One of the period's counts, as a tile shows it.</summary>
public sealed class TileRow
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public string Sub { get; set; } = "";
    public Visibility SubVisibility => Sub.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>One agent's or model's share of the period, as the overview's
/// lists draw it.</summary>
public sealed class ShareRow
{
    public string Name { get; set; } = "";
    public string Sub { get; set; } = "";
    public ImageSource? Icon { get; set; }
    public double Share { get; set; }
    public string Tokens { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Cost { get; set; } = "";
    public Visibility SubVisibility { get; set; }
}
