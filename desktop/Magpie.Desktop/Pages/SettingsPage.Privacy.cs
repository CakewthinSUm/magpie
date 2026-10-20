using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop.Pages;

public sealed partial class SettingsPage
{
    /// <summary>The kinds of personal data that can be masked while Mask
    /// personal data is on, as the page lists them (app.js REDACT_KINDS):
    /// id, what it is, when it is masked, and whether it is on by default.</summary>
    private static readonly (string Id, string Name, string Sub, bool On)[] RedactKinds =
    [
        ("ssn", "Social Security numbers", "US SSNs, in the ranges that are given out", true),
        ("passport", "Passport numbers", "After the word passport, 护照 or パスポート", true),
        ("iban", "IBANs", "Bank account numbers whose checksum adds up", true),
        ("birthday", "Birthdays", "A date after birthday, DOB, born or 生日; a date alone stays", true),
        ("mac", "MAC addresses", "A network card's hardware address", true),
        ("serial", "Serial numbers", "After S/N, Serial or 序列号", true),
        ("home", "User name in home folders", "The name in /Users/name, /home/name and C:\\Users\\name. Off by default: agents send their folder with every request", false),
        ("userhost", "User and computer names", "user@host in a shell prompt or an ssh command, and hostname or user lines", false),
        ("ip", "Public IP addresses", "Private, local and documentation addresses stay. Off by default: logs and configs are full of them", false),
        ("bucket", "Cloud storage buckets", "The bucket in s3://, gs://, oss://, cos:// and storage URLs; the path in it stays", false),
    ];

    /// <summary>
    /// The Privacy part of Settings, as the page has it (app.js renderRedact,
    /// renderDiscovery, renderRedactRules): what is masked before a request
    /// goes out, whether the agents' sign-ins are read, and whether the
    /// anonymous usage event leaves this computer.
    /// </summary>
    private void DrawPrivacy()
    {
        var s = _settings!;

        Body.Children.Add(PageKitUI.Heading("What is masked before it goes out"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Mask secrets",
            Description = "API keys, private keys, tokens and passwords go to vendors as placeholders, and come back as they were",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.Redact ? "on" : "off",
                v => _ = SaveAsync(new { redact = v == "on" })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Mask personal data",
            Description = "Emails, phone numbers, ID and bank card numbers too",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.RedactPersonal ? "on" : "off",
                v => _ = SaveAsync(new { redactPersonal = v == "on" })),
        }));
        // each other kind of personal data, on or off of its own while
        // personal data is masked; only the user's choices are kept
        if (s.RedactPersonal)
        {
            var kinds = s.RedactKinds ?? new Dictionary<string, bool>();
            foreach (var (id, name, sub, on) in RedactKinds)
            {
                var chosen = kinds.TryGetValue(id, out var c) ? c : on;
                Body.Children.Add(PageKitUI.Setting(new SettingRow
                {
                    Title = name,
                    Description = sub,
                    Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], chosen ? "on" : "off",
                        v => _ = SaveAsync(new { redactKinds = Patched(kinds, id, v == "on") })),
                }));
            }
        }

        var words = string.Join(", ", s.RedactWords ?? []);
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Masked words",
            Description = "Your own words to keep from vendors, separated by commas",
            Control = PageKitUI.TextField(words, "names, codenames, hosts",
                v => _ = SaveAsync(new { redactWords = ParseWords(v) })),
        }));

        Body.Children.Add(MaskingRulesRow(s));

        Body.Children.Add(PageKitUI.Heading("What magpie says of you"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Count me as a user",
            Description = "Once a day, a random id for this computer with magpie's version and system",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.NoStats ? "off" : "on",
                v => _ = SaveAsync(new { noStats = v == "off" })),
        }));
        // rides on that event: nothing goes without it
        if (!s.NoStats)
        {
            Body.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = "Share the agents, providers and models I use",
                Description = "Sent with that event, by magpie's own ids, with how many of each, and how often each partner was shown, opened and added; a provider you added yourself is only \u201ccustom\u201d. No names, addresses, accounts, keys or usage",
                Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.NoUsageStats ? "off" : "on",
                    v => _ = SaveAsync(new { noUsageStats = v == "off" })),
            }));
        }

        Body.Children.Add(PageKitUI.Heading("Agents' sign-ins"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Subscription accounts",
            Description = s.NoAccountsEnv ? "Off for this run: MAGPIE_NO_ACCOUNTS is set"
                : "Read the accounts your agents are signed in to (Claude Code, Codex, Copilot, Cursor and others: their files and the Keychain) and serve them. Off, magpie reads none of them; API-key providers work as before",
            Control = DiscoveryControl(),
        }));
    }

    /// <summary>The masking rules as one card: each rule with a Remove button,
    /// and below it the row that adds one by a prefix or a regular
    /// expression. Adding or removing sends the whole list, so a rule magpie
    /// can't use is said and the rest stay as they were.</summary>
    private FrameworkElement MaskingRulesRow(SettingsFullJson s)
    {
        var card = new StackPanel { Spacing = 8 };
        var rules = s.RedactRules ?? [];

        foreach (var rule in rules)
        {
            var remove = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
            // each rule as it was when drawn: removing it takes that one, not
            // whichever is at its index after an earlier removal
            var was = rule;
            remove.Click += async (_, _) =>
            {
                var next = (_settings!.RedactRules ?? []).Where(r => !RulesEqual(r, was)).ToList();
                await SaveRedactRulesAsync(next);
            };
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = rule.Kind,
                Description = rule.Prefix.Length > 0 ? "Starts with " + rule.Prefix : "Matches " + rule.Regex,
                Control = remove,
            }));
        }

        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Masking rules",
            Description = "Secrets magpie doesn't know, such as a gateway's own keys: what they start with, or a regular expression. Masked while Mask secrets is on",
            Control = RuleAddRow(),
        }));
        return card;
    }

    /// <summary>A copy of the kinds map with one id's value changed, sent
    /// whole so the other kinds stay as the user left them.</summary>
    private static Dictionary<string, bool> Patched(Dictionary<string, bool> kinds, string id, bool on)
    {
        var out_ = new Dictionary<string, bool>(kinds);
        out_[id] = on;
        return out_;
    }

    /// <summary>Comma or newline separated words into the list the settings
    /// read, trimmed and empty ones dropped.</summary>
    private static List<string> ParseWords(string text) =>
        text.Split([',', '，', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).Where(w => w.Length > 0).ToList();

    private static bool RulesEqual(RedactRuleJson a, RedactRuleJson b) =>
        a.Kind == b.Kind && a.Prefix == b.Prefix && a.Regex == b.Regex;

    private async Task SaveRedactRulesAsync(List<RedactRuleJson> rules)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/redact-rules", new { rules }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    /// <summary>The row that adds a rule: its name, Prefix or Regex, the match,
    /// and Add. A rule with neither a prefix nor a regex isn't sent.</summary>
    private FrameworkElement RuleAddRow()
    {
        var kind = new TextBox { PlaceholderText = "API_KEY", Width = 130, VerticalAlignment = VerticalAlignment.Center };
        var byPrefix = true;
        var match = new TextBox { PlaceholderText = "oc_sk_", Width = 170, VerticalAlignment = VerticalAlignment.Center };
        var by = PageKitUI.Choices([("prefix", "Prefix"), ("regex", "Regex")], "prefix",
            v => { byPrefix = v == "prefix"; match.PlaceholderText = byPrefix ? "oc_sk_" : "oc_sk_[A-Za-z0-9]{20,}"; });
        var add = new Button { Content = "Add", VerticalAlignment = VerticalAlignment.Center };
        void Send()
        {
            var m = match.Text.Trim();
            if (m.Length == 0) return;
            var rule = new RedactRuleJson { Kind = kind.Text.Trim(), Prefix = byPrefix ? m : "", Regex = byPrefix ? "" : m };
            var next = new List<RedactRuleJson>(_settings!.RedactRules ?? []) { rule };
            _ = SaveRedactRulesAsync(next);
            kind.Text = "";
            match.Text = "";
        }
        add.Click += (_, _) => Send();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(kind);
        row.Children.Add(by);
        row.Children.Add(match);
        row.Children.Add(add);
        return row;
    }

    /// <summary>The subscription-accounts switch. Turning it off asks first,
    /// naming the subscriptions that stop, then writes the account-discovery
    /// route, which forgets what was read as it is turned off.</summary>
    private FrameworkElement DiscoveryControl()
    {
        var s = _settings!;
        var on = !s.NoAccountDiscovery && !s.NoAccountsEnv;
        return PageKitUI.Choices([("off", "Off"), ("on", "On")], on ? "on" : "off", v => _ = ToggleDiscoveryAsync(v == "on", on));
    }

    private async Task ToggleDiscoveryAsync(bool target, bool current)
    {
        if (target == current) return;
        if (!target && !(await PageKitUI.ConfirmAsync(XamlRoot, "Turn off subscription accounts?",
            await DiscoveryOffWarningAsync(), "Turn them off", "Cancel", danger: true)))
        {
            Draw();
            return;
        }
        await SaveAccountDiscoveryAsync(!target);
    }

    /// <summary>What turning discovery off stops: the subscriptions signed in
    /// now, by name, read from the Providers list.</summary>
    private async Task<string> DiscoveryOffWarningAsync()
    {
        try
        {
            var list = await App.CurrentApp.Client.GetAsync<ProvidersJson>("providers");
            var subs = (list?.Providers ?? []).Where(p => p.Account is not null && !p.Off).Select(p => p.Name).ToList();
            return subs.Count > 0
                ? $"{string.Join(", ", subs)} stop working in magpie until this is on again: magpie reads none of the sign-ins they come from. API-key providers work as before."
                : "Every subscription stops working in magpie until this is on again: magpie reads none of the sign-ins they come from. API-key providers work as before.";
        }
        catch (Exception)
        {
            return "Every subscription stops working in magpie until this is on again: magpie reads none of the sign-ins they come from. API-key providers work as before.";
        }
    }
}