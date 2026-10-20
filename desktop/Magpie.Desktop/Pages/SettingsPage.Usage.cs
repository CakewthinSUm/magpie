using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop.Pages;

public sealed partial class SettingsPage
{
    /// <summary>
    /// The Allowances (usage) part of Settings, as the page has it (app.js
    /// renderTrayUsage, renderAlerts, the warm-up and check-in helpers, the
    /// Codex model picks): how the menu bar shows the allowances, the usage
    /// alerts, the warm-ups and daily check-ins, and where Codex's own
    /// little requests are answered.
    /// </summary>
    private void DrawUsage()
    {
        var s = _settings!;

        Body.Children.Add(PageKitUI.Heading("Allowance display"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Used or left",
            Description = "What the allowance pill under the icon reads",
            Control = PageKitUI.Choices([("false", "Used"), ("true", "Left")], s.QuotaLeft ? "true" : "false",
                v => _ = SaveQuotaLeftAsync(v == "true")),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Read allowances",
            Description = "At login before a proxy app, a read by itself goes out direct",
            Control = PageKitUI.Choices([("", "Automatic"), ("asked", "When I ask")], s.QuotaReads ?? "",
                v => _ = SaveQuotaReadsAsync(v)),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Currency",
            Description = "What a cost — the Usage page's, the tray panel's, the TUI's and the CLI's — is shown as; a vendor's own balance, already in its own currency, is never converted",
            Control = PageKitUI.Choices([("usd", "$ USD"), ("cny", "¥ CNY")], s.Currency is { Length: > 0 } c ? c : "usd",
                v => _ = SaveAsync(new { currency = v })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Units",
            Description = "万 and 亿 in Chinese, or K / M / B",
            Control = PageKitUI.Choices([("false", "K / M / B"), ("true", "万 / 亿")], s.ChineseUnits ? "true" : "false",
                v => _ = SaveAsync(new { chineseUnits = v == "true" })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Provider in model names",
            Description = "How magpie's own ids name a model: with its provider, or alone",
            Control = PageKitUI.Choices([("on", "On"), ("own", "Not on names I set"), ("off", "Off")], SuffixMode(s),
                v => _ = SavePlainNamesAsync(v)),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Codex subagents use other models",
            Description = "Codex's own models in multi-agent V1; where Codex's own multi_agent_v2 is on in the config magpie routes it wins, and magpie writes nothing",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.CodexAgentsV1 ? "on" : "off",
                v => _ = SaveCodexAgentsV1Async(v == "on")),
        }));

        Body.Children.Add(PageKitUI.Heading("Compact and window"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Full window or compact at",
            Description = "A 1M model's whole window, or compacting at the working one",
            Control = CompactAtControl(s),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Background compaction",
            Description = "Codex's background compaction and its threshold",
            Control = AsyncCompactControl(s),
        }));

        Body.Children.Add(PageKitUI.Heading("Tray"));
        Body.Children.Add(TrayUsageRow(s));

        Body.Children.Add(PageKitUI.Heading("Alerts"));
        Body.Children.Add(AlertRow(s, "Usage alert", "A notification when a 5-hour, weekly or monthly window reaches this share used, once each time it runs",
            s.UsageAlert, "%", n => n >= 1 && n <= 100, n => _ = SaveAsync(new { usageAlert = n }),
            () => _ = SaveAsync(new { usageAlert = s.UsageAlert > 0 ? s.UsageAlert : 80 })));
        Body.Children.Add(AlertRow(s, "Balance alert", "A notification when a balance falls to this amount, in its own currency or credits, once until it is topped up",
            s.BalanceAlert, "", n => n > 0, n => _ = SaveAsync(new { balanceAlert = n }),
            () => _ = SaveAsync(new { balanceAlert = s.BalanceAlert > 0 ? s.BalanceAlert : 5 })));
        Body.Children.Add(ResetReminderRow(s));
        Body.Children.Add(TurnDoneRow(s));

        Body.Children.Add(PageKitUI.Heading("Warm-up and check-in"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Codex warm-up",
            Description = "One tiny request starts the next window at once",
            Control = PageKitUI.Choices([("off", "Off"), ("week", "Weekly"), ("all", "Weekly and 5-hour")], !string.IsNullOrEmpty(s.CodexWarmup) ? s.CodexWarmup : "off",
                v => _ = SaveAsync(new { codexWarmup = v == "off" ? "" : v })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Claude warm-up",
            Description = "One tiny request through Claude Code (Haiku) starts the next window at once",
            Control = PageKitUI.Choices([("off", "Off"), ("week", "Weekly"), ("all", "Weekly and 5-hour")], !string.IsNullOrEmpty(s.ClaudeWarmup) ? s.ClaudeWarmup : "off",
                v => _ = SaveAsync(new { claudeWarmup = v == "off" ? "" : v })),
        }));
        Body.Children.Add(WarmAtRow(s, "Codex warm-up times", "Starts each account's 5-hour window at this time every day",
            s.CodexWarmAts ?? [], ats => _ = SaveAsync(new { codexWarmAts = ats, codexWarmAt = ats.FirstOrDefault() ?? "" })));
        Body.Children.Add(WarmAtRow(s, "Claude warm-up times", "Starts each account's 5-hour window at this time every day",
            s.ClaudeWarmAts ?? [], ats => _ = SaveAsync(new { claudeWarmAts = ats, claudeWarmAt = ats.FirstOrDefault() ?? "" })));
        foreach (var u in (s.CodexUsers ?? []).Concat(s.CodexWarmAtOf?.Keys ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Body.Children.Add(OwnWarmAtRow(s, u));
        }
        Body.Children.Add(AutoResetRow(s));
        Body.Children.Add(CheckinRows(s));
        Body.Children.Add(PluginCheckinRows(s));

        Body.Children.Add(PageKitUI.Heading("Codex's own little requests"));
        Body.Children.Add(CodexModelRow(s, "Thread titles", "Where Codex's requests for a thread's title go",
            s.CodexTitles, "off", v => _ = SaveCodexRouteAsync("codex-titles", v)));
        Body.Children.Add(CodexModelRow(s, "Thread descriptions", "Where Codex's requests for a thread's description go",
            s.CodexDescriptions, null, v => _ = SaveCodexRouteAsync("codex-descriptions", v)));
        Body.Children.Add(CodexModelRow(s, "Composer predictions", "Where Codex's guess at your next message goes",
            s.CodexPredictions, "off", v => _ = SaveCodexRouteAsync("codex-predictions", v)));
        Body.Children.Add(CodexModelRow(s, "Auto-review", "The model Codex's auto-review runs on",
            s.CodexAutoReview, null, v => _ = SaveCodexRouteAsync("codex-auto-review", v)));
    }

    private static string SuffixMode(SettingsFullJson s) => s.PlainNames ? "off" : s.PlainOwnNames ? "own" : "on";

    // ---- helper: a menu of the models, for the Codex picks ----

    private FrameworkElement CodexModelRow(SettingsFullJson s, string title, string sub, string value, string? offLabel, Action<string> save)
    {
        var models = s.TitleModels ?? [];
        var options = new List<(string, string)> { ("", "Codex's own (ChatGPT)") };
        if (offLabel is not null) options.Add(("off", "Off"));
        options.AddRange(models.Select(m => (m.Id, (m.Name.Length > 0 ? m.Name : m.Id) + " · " + m.ProviderName)));
        return PageKitUI.Setting(new SettingRow
        {
            Title = title,
            Description = sub,
            Control = PageKitUI.Menu(options, value, save),
        });
    }

    private async Task SaveCodexRouteAsync(string route, string model)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/" + route, new { model }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- alerts ----

    private FrameworkElement AlertRow(SettingsFullJson s, string title, string sub, double value, string unit,
        Func<double, bool> ok, Action<double> setValue, Action turnOn)
    {
        var on = value > 0;
        var segs = PageKitUI.Choices([("off", "Off"), ("on", "On")], on ? "on" : "off", v =>
        {
            if (v == "on" && !on) turnOn();
            else if (v == "off") setValue(0);
        });
        var field = on
            ? PageKitUI.NumberField((int)value, "", n => { if (ok(n)) setValue(n); }, "Set")
            : null;
        if (field is null) return PageKitUI.Setting(new SettingRow { Title = title, Description = sub, Control = segs });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(segs);
        row.Children.Add(field);
        return PageKitUI.Setting(new SettingRow { Title = title, Description = sub, Control = row });
    }

    private FrameworkElement ResetReminderRow(SettingsFullJson s)
    {
        var r = s.ResetReminder;
        var hours = new List<(string, string)> { ("0", "Off"), ("12", "12 h before"), ("24", "24 h before"), ("48", "48 h before") };
        if (r > 0 && !hours.Any(h => h.Item1 == r.ToString())) hours.Add((r.ToString(), r + " h before"));
        return PageKitUI.Setting(new SettingRow
        {
            Title = "Reset reminder",
            Description = "A notification this long before a weekly or monthly window renews with under 85% of it used, or before unused resets expire",
            Control = PageKitUI.Choices(hours, r.ToString(), v => _ = SaveAsync(new { resetReminder = int.Parse(v) })),
        });
    }

    private FrameworkElement TurnDoneRow(SettingsFullJson s)
    {
        var d = s.TurnDoneAlert;
        var mins = new List<(string, string)> { ("0", "Off"), ("60", "1 min"), ("180", "3 min"), ("600", "10 min") };
        if (d > 0 && !mins.Any(m => m.Item1 == d.ToString())) mins.Add((d.ToString(), d < 60 ? d + " s" : (d / 60) + " min"));
        return PageKitUI.Setting(new SettingRow
        {
            Title = "Task done",
            Description = "A notification when Claude Code, Codex, OpenCode or another agent going through magpie finishes a task that ran at least this long",
            Control = PageKitUI.Choices(mins, d.ToString(), v => _ = SaveAsync(new { turnDoneAlert = int.Parse(v) })),
        });
    }

    private FrameworkElement CompactAtControl(SettingsFullJson s)
    {
        var compact = s.CompactAt > 0 ? s.CompactAt : 272000;
        var at = compact >= 1000 ? (compact / 1000.0).ToString("0.#") + "k" : compact.ToString();
        var card = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var segs = PageKitUI.Choices([("off", "Compact at " + at), ("on", "Full window")], s.FullContext ? "on" : "off",
            v => _ = SaveFullContextAsync(v == "on", s.FullContext ? null : s.CompactAt));
        card.Children.Add(segs);
        if (!s.FullContext)
        {
            card.Children.Add(PageKitUI.NumberField(s.CompactAt, "272k", n => _ = SaveFullContextAsync(false, n)));
        }
        return card;
    }

    private async Task SaveFullContextAsync(bool on, int? at)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/full-context", on ? new { on } : new { on = false, at }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private FrameworkElement AsyncCompactControl(SettingsFullJson s)
    {
        var card = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        card.Children.Add(PageKitUI.Choices([("off", "Off"), ("on", "On")], s.CodexAsyncCompact ? "on" : "off",
            v => _ = SaveAsyncCompactAsync(v == "on", s.CodexAsyncCompact ? s.CodexAsyncCompactPercent : null)));
        if (s.CodexAsyncCompact)
        {
            card.Children.Add(PageKitUI.NumberField(s.CodexAsyncCompactPercent, "65%", n => _ = SaveAsyncCompactAsync(true, n)));
        }
        return card;
    }

    private async Task SaveAsyncCompactAsync(bool on, int? percent)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/codex-async-compact", on ? new { on, percent } : new { on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- tray usage ----

    private FrameworkElement TrayUsageRow(SettingsFullJson s)
    {
        var ids = s.TrayUsages ?? [];
        var label = ids.Count == 0 ? "Off" : ids.Count > 1 ? ids.Count + " shown" : ids[0];
        var totals = new List<(string, string)> { ("usage:today", "Tokens and cost today"), ("usage:7d", "Tokens and cost, last 7 days") };
        var options = new List<(string, string)> { ("", "Off") };
        options.AddRange(totals);
        options.AddRange((s.TitleModels ?? []).Select(m => (
            m.Id,
            (m.Name.Length > 0 ? m.Name : m.Id) + " · " + m.ProviderName)));

        return PageKitUI.Setting(new SettingRow
        {
            Title = "Show beside the icon",
            Description = "Show your subscriptions' allowances, or the tokens and cost of today or the last 7 days, when pointing at magpie's tray icon, refreshed every few minutes",
            Control = PageKitUI.Menu(options, ids.Count > 0 ? ids[0] : "",
                v => _ = SaveAsync(new { trayUsages = v.Length == 0 ? new List<string>() : new List<string> { v } })),
        });
    }

    // ---- warm-up times ----

    private FrameworkElement WarmAtRow(SettingsFullJson s, string title, string sub, List<string> ats, Action<List<string>> save)
    {
        var card = new StackPanel { Spacing = 8 };
        var times = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (at, n) in ats.Select((x, i) => (x, i)).ToList())
        {
            var box = new TextBox { Text = at, PlaceholderText = "06:00", Width = 80 };
            box.LostFocus += (_, _) =>
            {
                if (box.Text.Trim().Length > 0 && box.Text.Trim() != at)
                {
                    var next = new List<string>(ats);
                    next[n] = box.Text.Trim();
                    save(next);
                }
            };
            times.Children.Add(box);
            if (ats.Count > 1)
            {
                var rm = new Button { Content = "×", Padding = new Thickness(6, 0, 6, 0) };
                var k = n;
                rm.Click += (_, _) => save(ats.Where((_, i) => i != k).ToList());
                times.Children.Add(rm);
            }
        }
        if (ats.Count > 0)
        {
            var add = new Button { Content = "+", Padding = new Thickness(6, 0, 6, 0) };
            add.Click += (_, _) => save([.. ats, NextWarmTime(ats)]);
            times.Children.Add(add);
        }
        var segs = PageKitUI.Choices([("off", "Off"), ("on", "On")], ats.Count > 0 ? "on" : "off",
            v => save(v == "on" ? (ats.Count > 0 ? ats : ["06:00"]) : []));
        card.Children.Add(PageKitUI.Setting(new SettingRow { Title = title, Description = sub, Control = segs }));
        if (ats.Count > 0) card.Children.Add(PageKitUI.Setting(new SettingRow { Title = "Times", Description = sub, Control = times }));
        return card;
    }

    private static string NextWarmTime(List<string> ats)
    {
        var last = ats.LastOrDefault() ?? "06:00";
        var parts = last.Split(':');
        var h = int.TryParse(parts[0], out var hh) ? hh : 6;
        var m = parts.Length > 1 && int.TryParse(parts[1], out var mm) ? mm : 0;
        var at = ((h + 5) % 24).ToString("00") + ":" + m.ToString("00");
        for (var i = 0; ats.Contains(at) && i < 24; i++) at = i.ToString("00") + ":00";
        return at;
    }

    private FrameworkElement OwnWarmAtRow(SettingsFullJson s, string user)
    {
        var own = s.CodexWarmAtOf ?? new Dictionary<string, string>();
        var key = user.ToLower();
        var at = own.TryGetValue(key, out var t) ? t : "";
        var mode = at is null ? "same" : at == "off" ? "off" : "own";
        var card = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var segs = PageKitUI.Choices([("same", "Same"), ("own", "Own"), ("off", "Off")], mode, v =>
        {
            var value = v switch { "same" => "", "off" => "off", _ => (s.CodexWarmAt is { Length: > 0 } wa ? wa : "06:00") };
            _ = SaveWarmAtAsync(user, value);
        });
        card.Children.Add(segs);
        if (mode == "own")
        {
            var box = new TextBox { Text = at, PlaceholderText = "06:00", Width = 80 };
            box.LostFocus += (_, _) =>
            {
                var typed = (box.Text ?? "").Trim();
                if (typed.Length > 0) _ = SaveWarmAtAsync(user, typed);
            };
            card.Children.Add(box);
        }
        return PageKitUI.Setting(new SettingRow
        {
            Title = user,
            Description = "Give accounts different times and one window starts as another runs out",
            Control = card,
        });
    }

    private async Task SaveWarmAtAsync(string user, string at)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/codex-warm-at", new { user, at }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private FrameworkElement AutoResetRow(SettingsFullJson s)
    {
        var on = s.CodexAutoReset ?? [];
        var users = new List<string>(s.CodexUsers ?? []);
        foreach (var u in on) if (!users.Any(x => x.Equals(u, StringComparison.OrdinalIgnoreCase))) users.Add(u);
        var card = new StackPanel { Spacing = 8 };
        foreach (var user in users)
        {
            var only = s.CodexAutoResetOnly ?? new Dictionary<string, string>();
            var key = user.ToLower();
            var when = only.TryGetValue(key, out var w) ? w : "";
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = user,
                Description = "Whether a reset is spent by itself, and at which time",
                Control = PageKitUI.Menu([("", "Off"), ("week", "When its week runs out"), ("expiring", "Before one expires")], when,
                    v => _ = SaveCodexAutoResetAsync(user, v)),
            }));
        }
        if (users.Count == 0)
        {
            card.Children.Add(new TextBlock { Text = "Shown with a Codex account signed in", FontSize = 12, Opacity = 0.6 });
        }
        return card;
    }

    private async Task SaveCodexAutoResetAsync(string user, string when)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/codex-auto-reset", new { user, on = when.Length > 0, when }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- check-ins ----

    private FrameworkElement CheckinRows(SettingsFullJson s)
    {
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "WorkBuddy daily check-in",
            Description = "Claims each signed-in China account's check-in credits once a day",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.WorkBuddyCheckin ? "on" : "off",
                v => _ = SaveCheckinRouteAsync("workbuddy-checkin", v == "on")),
        }));
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Trae CN check-in",
            Description = "Claims each signed-in Trae CN account's check-in credits once a day",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.TraeCheckin ? "on" : "off",
                v => _ = SaveCheckinRouteAsync("trae-checkin", v == "on")),
        }));
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "MiniMax Code check-in",
            Description = "Claims each signed-in MiniMax Code account's check-in credits once a day",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.MiniMaxCheckin ? "on" : "off",
                v => _ = SaveCheckinRouteAsync("minimax-checkin", v == "on")),
        }));
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Qoder check-in",
            Description = "Claims each signed-in Qoder account's daily credits once a day",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.QoderCheckin ? "on" : "off",
                v => _ = SaveCheckinRouteAsync("qoder-checkin", v == "on")),
        }));
        // each account, when a vendor has more than one
        foreach (var a in (s.CheckinAccounts ?? []).Where(a => a.On || !a.On))
        {
            var acct = a;
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = acct.User.Length > 0 ? acct.User : acct.Key,
                Description = acct.On ? "Checked in once a day while Daily check-in is on" : "Left out of the daily check-in",
                Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], acct.On ? "on" : "off",
                    v => _ = SaveCheckinAccountAsync(acct.Key, v == "on")),
            }));
        }
        return card;
    }

    private async Task SaveCheckinRouteAsync(string route, bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/" + route, new { on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private async Task SaveCheckinAccountAsync(string key, bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/checkin-account", new { key, on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private FrameworkElement PluginCheckinRows(SettingsFullJson s)
    {
        var card = new StackPanel { Spacing = 8 };
        foreach (var p in s.CheckinPlugins ?? [])
        {
            var plug = p;
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = (p.Name.Length > 0 ? p.Name : p.Id) + " · Daily check-in",
                Description = "Has the " + (p.Name.Length > 0 ? p.Name : p.Id) + " plugin check each signed-in account in once a day",
                Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], p.On ? "on" : "off",
                    v => _ = SavePluginCheckinAsync(plug.Id, v == "on")),
            }));
        }
        return card;
    }

    private async Task SavePluginCheckinAsync(string provider, bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/plugin-checkin", new { provider, on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- dedicated routes ----

    private async Task SaveQuotaLeftAsync(bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/quota-left", new { on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private async Task SaveQuotaReadsAsync(string reads)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/quota-reads", new { reads }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private async Task SavePlainNamesAsync(string mode)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/plain-names", new { mode }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private async Task SaveCodexAgentsV1Async(bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/codex-agents-v1", new { on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }
}