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
public sealed partial class UsagePage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    /// <summary>The period shown, as the page's own picker names it
    /// (usage.Period, internal/usage): today until another is picked.</summary>
    private string _period = "today";
    private bool _settingPeriod;
    /// <summary>The allowances last drawn, so a redraw of the page after its
    /// pictures are read draws the same cards.</summary>
    private List<QuotaJson> _quotas = [];

    public UsagePage()
    {
        InitializeComponent();
        // the picker shows the period it starts on once it is in the tree:
        // its indicator is laid out from the selection when it loads, not
        // when it is set from here
        Period.Loaded += (_, _) =>
        {
            _settingPeriod = true;
            try { Period.SelectedItem = Period.Items.FirstOrDefault(i => (string?)i.Tag == _period); }
            finally { _settingPeriod = false; }
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    /// <summary>Reads both halves: the figures for the period, and the
    /// allowances. A refusal of the allowances is said on the page and left
    /// the figures standing.</summary>
    private async Task LoadAsync()
    {
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

    private void Reload_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();
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
