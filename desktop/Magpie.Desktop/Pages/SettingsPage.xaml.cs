using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Settings page, drawn here rather than by the page magpie serves: the
/// choices on General (appearance, language, text size, colorblind mode) and
/// the facts on About (the version, the folder, the gateway's address and a
/// check for a newer magpie). Each part is a SelectorBar's tab, as the page
/// has them; every save posts only what changed to the route that sets it
/// (POST /api/settings and the per-setting routes), and the page is drawn
/// again from magpie's answer.
/// </summary>
public sealed partial class SettingsPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    /// <summary>The part shown: the page's own SET_TABS names.</summary>
    private string _part = "general";

    private SettingsFullJson? _settings;
    private UpdateJson? _update;
    private bool _busy;

    public SettingsPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // the part is as wide as the view it is in: a ScrollViewer measures
        // what it holds at an unbounded width, and a row's control would sit
        // past the window
        Scroller.SizeChanged += (_, args) => Body.Width = Math.Max(0, args.NewSize.Width - 2);
        // the part a link named (`settings&tab=about`), else General; the
        // bar's own answer says which it is
        var part = (e.Parameter as string ?? "").Split('&').FirstOrDefault(s => s.StartsWith("tab=", StringComparison.Ordinal))?[4..] ?? "";
        _part = part is "usage" or "network" or "models" or "privacy" or "otel" or "sync" or "about" ? part : "general";
        Tabs.SelectedItem = part switch
        {
            "usage" => UsageTab,
            "network" => NetworkTab,
            "models" => ModelsTab,
            "privacy" => PrivacyTab,
            "otel" => ObservabilityTab,
            "sync" => SyncTab,
            "about" => AboutTab,
            _ => GeneralTab,
        };
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            _settings = await App.CurrentApp.Client.GetAsync<SettingsFullJson>("settings") ?? new SettingsFullJson();
            Draw();
            // the version's own state is asked after the page is drawn: it
            // may wait on the feed
            _ = LoadUpdateAsync();
        }, Status, Busy);
    }

    private async Task LoadUpdateAsync()
    {
        try
        {
            _update = await App.CurrentApp.Client.GetAsync<UpdateJson>("update");
            if (_part == "about") Draw();
        }
        catch (MagpieException e)
        {
            // the version row says it, with the check beside it still there
            Status.Text = e.Message;
        }
    }

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        _part = ReferenceEquals(sender.SelectedItem, UsageTab) ? "usage"
            : ReferenceEquals(sender.SelectedItem, NetworkTab) ? "network"
            : ReferenceEquals(sender.SelectedItem, ModelsTab) ? "models"
            : ReferenceEquals(sender.SelectedItem, PrivacyTab) ? "privacy"
            : ReferenceEquals(sender.SelectedItem, ObservabilityTab) ? "otel"
            : ReferenceEquals(sender.SelectedItem, SyncTab) ? "sync"
            : ReferenceEquals(sender.SelectedItem, AboutTab) ? "about"
            : "general";
        Draw();
    }

    private void Draw()
    {
        if (_settings is null) return;
        Body.Children.Clear();
        PageKitUI.Say(Problem, _settings.Notice);
        switch (_part)
        {
            case "about":
                DrawAbout();
                break;
            case "general":
                DrawGeneral();
                break;
            default:
                DrawElsewhere();
                break;
        }
    }

    // ---- General ----

    private void DrawGeneral()
    {
        var s = _settings!;

        Body.Children.Add(PageKitUI.Heading("Appearance"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Appearance",
            Description = "System follows Windows' light or dark setting.",
            Control = PageKitUI.Choices(
                [("system", "System"), ("light", "Light"), ("dark", "Dark")],
                s.Theme is { Length: > 0 } theme ? theme : "system",
                v => _ = SaveAsync(new { theme = v })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Colorblind mode",
            Description = "Colorblind-safe chart colors, and each allowance window's line in a dash of its own.",
            Control = PageKitUI.Choices(
                [("off", "Off"), ("on", "On")],
                s.Colorblind ? "on" : "off",
                v => _ = SaveAsync(new { colorblind = v == "on" })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Language",
            Description = "The language of magpie's windows, its page and the menu bar.",
            Control = PageKitUI.Menu(LanguageChoices(), s.Lang is { Length: > 0 } lang ? lang : "system",
                v => _ = SaveAsync(new { lang = v })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Text size",
            Description = "Everything in magpie's windows, larger.",
            Control = PageKitUI.Choices(
                TextSizeChoices(), s.TextSize.ToString(),
                v => _ = SaveTextSizeAsync(int.Parse(v))),
        }));

        Body.Children.Add(PageKitUI.Heading("Window"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Click the tray icon to",
            Description = "The quick panel under the icon, or magpie's window.",
            Control = PageKitUI.Choices(
                [("panel", "Open the panel"), ("window", "Open the window")],
                s.Tray is { Length: > 0 } tray ? tray : "panel",
                v => _ = SaveAsync(new { tray = v })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Open at login",
            Description = "Opens in the tray with no window when you sign in to Windows.",
            Control = PageKitUI.Choices(
                [("off", "Off"), ("on", "On")],
                s.Login ? "on" : "off",
                v => _ = SaveLoginAsync(v == "on")),
        }));

        // what magpie has read of the agents' own sign-ins: off, they aren't
        // served as subscriptions
        Body.Children.Add(PageKitUI.Heading("Agents' sign-ins"));
        if (s.NoAccountsEnv)
        {
            Body.Children.Add(new TextBlock
            {
                Text = "MAGPIE_NO_ACCOUNTS is set, so the agents' sign-ins aren't read for this run whatever this says.",
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 2, 6),
            });
        }
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Read agents' sign-ins",
            Description = "Serves the accounts your agents are signed in to as subscriptions, like the ones added here.",
            Control = PageKitUI.Choices(
                [("on", "On"), ("off", "Off")],
                s.NoAccountDiscovery ? "off" : "on",
                v => _ = SaveAccountDiscoveryAsync(v == "off")),
        }));
    }

    /// <summary>The languages magpie offers, as the page's menu names them.</summary>
    private static IEnumerable<(string, string)> LanguageChoices() =>
    [
        ("system", "System"),
        ("en", "English"),
        ("zh", "简体中文"),
        ("zh-TW", "繁體中文"),
        ("ja", "日本語"),
        ("de", "Deutsch"),
    ];

    private static IEnumerable<(string, string)> TextSizeChoices() =>
        new[] { 100, 110, 125, 150 }.Select(n => (n.ToString(), n + "%"));

    // ---- About ----

    private void DrawAbout()
    {
        var s = _settings!;

        Body.Children.Add(PageKitUI.Heading("Version"));
        var version = new TextBlock
        {
            Text = s.Version is { Length: > 0 } v ? v : "dev",
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        };
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Version",
            Description = UpdateLine(),
            Control = version,
        }));

        var check = new Button { Content = "Check for updates" };
        check.Click += async (_, _) => await CheckAsync();
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Check for updates",
            Description = "Asks for the newest magpie now; a newer one downloads by itself when automatic updates are on.",
            Control = check,
        }));

        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Automatic updates",
            Description = "Checks for a newer magpie and downloads it.",
            Control = PageKitUI.Choices(
                [("on", "On"), ("off", "Off")],
                s.NoAutoUpdate ? "off" : "on",
                v => _ = SaveAsync(new { noAutoUpdate = v == "off" })),
        }));

        Body.Children.Add(PageKitUI.Heading("Where magpie keeps its files"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Config folder",
            Description = s.Portable
                ? "Portable: everything magpie keeps, in the data folder beside it."
                : "providers, profiles and these settings",
            Control = new TextBlock
            {
                Text = s.Dir,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
            },
        }));
        var reveal = new Button { Content = "Open the folder" };
        reveal.Click += async (_, _) =>
        {
            await PageKit.RunAsync(
                () => App.CurrentApp.Client.PostAsync("settings/reveal"),
                Status, Busy);
        };
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Open the folder",
            Description = "Shows the config folder in File Explorer.",
            Control = reveal,
        }));

        Body.Children.Add(PageKitUI.Heading("Gateway"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Gateway URL",
            Description = "the address every agent is pointed at",
            Control = CopyButton(s.Gateway, "Gateway URL"),
        }));
        if (s.PublicUrl is { Length: > 0 } publicUrl)
        {
            Body.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = "Public address",
                Description = "MAGPIE_PUBLIC_URL: where other computers reach the gateway, with a gateway key.",
                Control = CopyButton(publicUrl, "Public address"),
            }));
        }
        if (s.Lan && s.LanUrls is { Count: > 0 } urls)
        {
            foreach (var url in urls)
            {
                Body.Children.Add(PageKitUI.Setting(new SettingRow
                {
                    Title = "On this network",
                    Description = "Where another computer on this network reaches the gateway.",
                    Control = CopyButton(url, "Network address"),
                }));
            }
        }
    }

    /// <summary>What the version's row says of an update: what is out, what
    /// is downloading, or why the check failed.</summary>
    private string UpdateLine()
    {
        if (_update is null) return "";
        return _update.State switch
        {
            "ready" => _update.Latest + " is downloaded; restart magpie to update",
            "found" => _update.Latest + " is out" + (_update.Size > 0 ? $" · {_update.Size / 1e6:F1} MB" : ""),
            "downloading" => "Downloading " + _update.Latest + (_update.Total > 0 ? $" · {_update.Done * 100 / _update.Total}%" : ""),
            "available" => _update.Latest + " is out",
            "latest" => "Up to date",
            "checking" => "Checking for updates…",
            "error" => "Couldn't check for updates" + (_update.Error is { Length: > 0 } e ? ": " + e : ""),
            "blocked" => _update.Latest + " didn't start on this computer, so magpie stays on " + _update.Current,
            _ => "",
        };
    }

    private async Task CheckAsync()
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _update = await App.CurrentApp.Client.PostAsync<UpdateJson>("update/check", new { });
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- the parts not drawn here ----

    /// <summary>
    /// A part whose settings this page doesn't draw yet: its name, and the
    /// settings it holds, so a reader knows they exist and where they are
    /// drawn (the page magpie serves, until each is drawn here).
    /// </summary>
    private void DrawElsewhere()
    {
        var (title, what) = _part switch
        {
            "usage" => ("Allowances", "what the menu bar shows, how often, the allowance display and reads, currency, and the warm-ups and daily check-ins."),
            "network" => ("Network and sharing", "the proxy, the GitHub token, the gateway's port and local-network sharing, and the web pages allowed to reach it."),
            "models" => ("Models", "the model that describes images, the one that searches, the one that draws, and the models turned off."),
            "privacy" => ("Privacy", "what is masked before a request goes out, and whether the agents' sign-ins are read."),
            "otel" => ("Observability", "OpenTelemetry: where traces and metrics go, what is sent, and whether request bodies go with them."),
            "sync" => ("Sync and backup", "a remote that keeps the setup the same on every computer, and a sealed file to carry by hand."),
            _ => ("Settings", ""),
        };
        Body.Children.Add(PageKitUI.Heading(title));
        Body.Children.Add(new TextBlock
        {
            Text = "Not drawn here yet: " + what,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Margin = new Thickness(2, 0, 2, 8),
        });
        Body.Children.Add(new TextBlock
        {
            Text = "They are in Settings in magpie's own page; open " + title + " there until this page draws them.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.6,
            Margin = new Thickness(2, 0, 2, 8),
        });
    }

    // ---- writes ----

    /// <summary>
    /// A save of what changed: POST /api/settings reads the body as the whole
    /// settings file and keeps what other pages own, so only the changed
    /// values are sent; the answer is the page as magpie saved it.
    /// </summary>
    private async Task SaveAsync(object change)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings", change) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    /// <summary>The text size is set on its own route: the keyboard's zoom
    /// sets it too, and a save of the page's other choices must not undo one
    /// made meanwhile.</summary>
    private async Task SaveTextSizeAsync(int size)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/text-size", new { size }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    /// <summary>Open at login is the system's record, not a setting, so it has
    /// its own route.</summary>
    private async Task SaveLoginAsync(bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/login", new { on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    /// <summary>Reading the agents' sign-ins is set on its own route, which
    /// forgets what was read as it is turned off.</summary>
    private async Task SaveAccountDiscoveryAsync(bool off)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/account-discovery", new { off }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    /// <summary>A value to copy, on a button: the clipboard through the page's
    /// own route, as the page copies it, with the value in a tooltip.</summary>
    private static Button CopyButton(string text, string what)
    {
        var copy = new Button
        {
            Content = "Copy",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTipService.SetToolTip(copy, text);
        copy.Click += async (_, _) =>
        {
            try { await App.CurrentApp.Client.PostAsync("copy", new { text }); }
            catch (Exception) { /* the clipboard is the page's; nothing to do */ }
        };
        return copy;
    }
}
