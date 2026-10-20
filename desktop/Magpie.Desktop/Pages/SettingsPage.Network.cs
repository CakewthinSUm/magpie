using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop.Pages;

public sealed partial class SettingsPage
{
    /// <summary>
    /// The Network and sharing part of Settings, as the page has it (app.js
    /// renderProxy, renderGitHubToken, renderPort, renderBareIds, renderLAN,
    /// renderAllow, renderCORS): the proxy magpie's own requests follow, the
    /// GitHub token, the gateway's port, and the ways other computers reach
    /// it — and are let in.
    /// </summary>
    private void DrawNetwork()
    {
        var s = _settings!;

        Body.Children.Add(PageKitUI.Heading("Requests to vendors"));
        Body.Children.Add(ProxyRow(s));
        Body.Children.Add(GitHubTokenRow(s));

        Body.Children.Add(PageKitUI.Heading("Gateway"));
        Body.Children.Add(PortRow(s));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Model ids without the provider",
            Description = "The gateway's /v1/models lists each model by its own id as well, gpt-5.6-sol beside codex/gpt-5.6-sol",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.BareModelIDs ? "on" : "off",
                v => _ = SaveBareIdsAsync(v == "on")),
        }));

        Body.Children.Add(PageKitUI.Heading("Sharing"));
        Body.Children.Add(LanRow(s));
        Body.Children.Add(AllowRow(s));
        Body.Children.Add(CorsRow(s));
    }

    // ---- Proxy ----

    private FrameworkElement ProxyRow(SettingsFullJson s)
    {
        var cur = s.Proxy.Length == 0 ? "auto" : s.Proxy == "direct" ? "off" : "custom";
        var raw = s.ProxyNow;
        var line = ProxyHost(raw);
        var description = s.ProxySource switch
        {
            "settings" => "Requests to vendors without a proxy of their own, and update checks, go through " + line,
            "system" => "Following the system proxy, " + line,
            "environment" => "Following HTTPS_PROXY, " + line,
            "off" => "Off: requests to vendors without a proxy of their own, and update checks, go direct",
            "none" => "No system proxy found; requests to vendors without a proxy of their own, and update checks, go direct",
            _ => "",
        };
        return PageKitUI.Setting(new SettingRow
        {
            Title = "Proxy",
            Description = description,
            Control = PageKitUI.Choices([("auto", "Auto"), ("off", "Off"), ("custom", "Custom")], cur,
                v => _ = SaveProxyAsync(v)),
        });
    }

    private async Task SaveProxyAsync(string mode)
    {
        string? value;
        if (mode == "custom")
        {
            value = await PromptProxyAsync(_settings!.Proxy);
            if (string.IsNullOrEmpty(value)) { Draw(); return; }
        }
        else
        {
            value = mode == "off" ? "direct" : "";
        }
        if (value == _settings!.Proxy) { Draw(); return; }
        await SaveAsync(new { proxy = value });
    }

    /// <summary>A dialog typing a custom proxy: its address, and for a socks5
    /// proxy a user and password. Returns the URL, or '' when cancelled.</summary>
    private async Task<string> PromptProxyAsync(string current)
    {
        var address = new TextBox { Header = "Address", PlaceholderText = "http://127.0.0.1:7890", Text = current, Width = 320 };
        var user = new TextBox { Header = "Username (optional)", Width = 320 };
        var pass = new PasswordBox { Header = "Password (optional)", Width = 320 };
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(address);
        body.Children.Add(user);
        body.Children.Add(pass);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Custom proxy",
            Content = body,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return "";
        var v = address.Text.Trim();
        if (v.Length == 0) return "";
        if (user.Text.Length > 0 || pass.Password.Length > 0)
        {
            // a socks5 URL carries its credentials in the userinfo
            var host = v;
            if (!host.Contains("://")) host = "http://" + host;
            try
            {
                var u = new Uri(host);
                var h = u.Scheme + "://";
                var uu = Uri.EscapeDataString(user.Text);
                var pp = Uri.EscapeDataString(pass.Password);
                v = h + (uu.Length > 0 ? uu : "") + (pp.Length > 0 ? ":" + pp : "") + "@" + u.Authority + u.PathAndQuery;
            }
            catch (Exception) { /* the settings API explains an invalid address */ }
        }
        return v;
    }

    /// <summary>A proxy's credentials are kept out of the line: only the host
    /// part of the URL that is shown.</summary>
    private static string ProxyHost(string proxy) =>
        (proxy ?? "").Split('@').Last();

    // ---- GitHub token ----

    private FrameworkElement GitHubTokenRow(SettingsFullJson s)
    {
        var why = "The library checks skills for updates with it: GitHub allows 5,000 requests an hour with a token, 60 without. It also installs skills from private repositories it can read; for public ones it needs no scopes.";
        if (s.GithubTokenFrom == "settings")
        {
            var remove = new Button { Content = "Remove", VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 2, 8, 2) };
            remove.Click += async (_, _) => await SaveGitHubTokenAsync("");
            var control = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            control.Children.Add(new TextBlock { Text = s.GithubTokenMask ?? "", VerticalAlignment = VerticalAlignment.Center });
            control.Children.Add(remove);
            return PageKitUI.Setting(new SettingRow
            {
                Title = "GitHub token",
                Description = why,
                Control = control,
            });
        }
        var pass = new PasswordBox { PlaceholderText = "ghp_… / github_pat_…", Width = 220, VerticalAlignment = VerticalAlignment.Center };
        void Save()
        {
            var v = pass.Password.Trim();
            if (v.Length > 0) _ = SaveGitHubTokenAsync(v);
        }
        pass.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Save(); };
        var save = new Button { Content = "Save", VerticalAlignment = VerticalAlignment.Center };
        save.Click += (_, _) => Save();
        var tokenControl = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        tokenControl.Children.Add(pass);
        tokenControl.Children.Add(save);
        tokenControl.Children.Add(PageKitUI.LinkButton("Create one", "https://github.com/settings/tokens/new?description=magpie"));
        return PageKitUI.Setting(new SettingRow
        {
            Title = "GitHub token",
            Description = s.GithubTokenFrom is { Length: > 0 } from
                ? $"Using {from} from the environment ({s.GithubTokenMask}); a token set here comes first. " + why
                : why,
            Control = tokenControl,
        });
    }

    private async Task SaveGitHubTokenAsync(string token)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/github-token", new { token }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- Port ----

    private FrameworkElement PortRow(SettingsFullJson s)
    {
        if (s.AddrEnv is { Length: > 0 } addr)
        {
            return PageKitUI.Setting(new SettingRow
            {
                Title = "Gateway port",
                Description = "MAGPIE_ADDR=" + addr + " sets the gateway's address: unset it to set the port here",
                Control = new TextBlock { Text = addr, VerticalAlignment = VerticalAlignment.Center, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") },
            });
        }
        var now = s.Port > 0 ? s.Port : 3425;
        return PageKitUI.Setting(new SettingRow
        {
            Title = "Gateway port",
            Description = "Agents magpie connected move with it. Now " + s.Gateway,
            Control = PageKitUI.NumberField(now, "3425", p => _ = SavePortAsync(p)),
        });
    }

    private async Task SavePortAsync(int port)
    {
        if (port < 1024 || port > 65535) { Status.Text = "A port is a number from 1024 to 65535"; return; }
        if (port == (_settings!.Port > 0 ? _settings.Port : 3425)) return;
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            var answer = await App.CurrentApp.Client.PostAsync<PortAnswerJson>("settings/port", new { port });
            if (answer is not null)
            {
                _settings = answer.Settings;
                var moved = answer.Port?.Moved?.Count ?? 0;
                Status.Text = moved > 0 ? $"Gateway on port {port}; {moved} agents moved with it" : $"Gateway on port {port}";
            }
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private async Task SaveBareIdsAsync(bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/bare-ids", new { on }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- Sharing ----

    private FrameworkElement LanRow(SettingsFullJson s)
    {
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Share on local network",
            Description = "Agents on other computers on this network, or through a tunnel to this one such as Cloudflare Tunnel, can use magpie’s models with a gateway key from Gateway",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.Lan ? "on" : "off",
                v => _ = SaveLanAsync(v == "on")),
        }));
        if (s.Lan)
        {
            var urls = s.LanUrls ?? [];
            if (urls.Count == 0)
            {
                card.Children.Add(PageKitUI.Setting(new SettingRow
                {
                    Title = "Address",
                    Description = "This computer has no local network address right now",
                }));
            }
            else
            {
                card.Children.Add(PageKitUI.Setting(new SettingRow
                {
                    Title = "Address",
                    Description = "Where another computer on this network reaches the gateway",
                    Control = new TextBlock { Text = urls[0] + "/v1", VerticalAlignment = VerticalAlignment.Center, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap },
                }));
            }
            if (s.LanContainer)
            {
                card.Children.Add(PageKitUI.Setting(new SettingRow
                {
                    Title = "In a container",
                    Description = "The container's own addresses, which other devices can't reach: set MAGPIE_PUBLIC_URL to the host's",
                }));
            }
        }
        return card;
    }

    private async Task SaveLanAsync(bool on)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/lan", new { on }) ?? _settings;
            if (_settings?.Rekeyed is { Length: > 0 } rekeyed) Status.Text = rekeyed;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    /// <summary>The allowed addresses, under Share: who else may reach the
    /// gateway at all. Shown only when sharing is on or an environment list
    /// is set (app.js renderAllow: env list is shown but not edited).</summary>
    private FrameworkElement? AllowRow(SettingsFullJson s)
    {
        var env = s.AllowIPsEnv ?? "";
        if (!s.Lan && !s.OnNetwork && env.Length == 0) return null;
        var list = env.Length > 0 ? (s.AllowIPsFromEnv ?? []) : (s.AllowIPs ?? []);
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Allowed addresses",
            Description = env.Length > 0
                ? "Set by MAGPIE_ALLOW_IPS. Other computers reach magpie only from these addresses, even with a gateway key; this computer always can"
                : "Other computers reach magpie only from these addresses or ranges (CIDR), even with a gateway key; this computer always can. None listed lets every address in",
            Control = env.Length == 0 ? PageKitUI.AddRow("203.0.113.7, 10.0.0.0/8", v => _ = SaveAllowAsync(AddToList(s.AllowIPs ?? [], v))) : null,
        }));
        foreach (var a in list)
        {
            if (env.Length > 0) continue;
            var was = a;
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = a,
                Control = ItemRemover(() => _ = SaveAllowAsync((_settings!.AllowIPs ?? []).Where(x => x != was).ToList())),
            }));
        }
        return card;
    }

    private FrameworkElement ItemRemover(Action remove)
    {
        var b = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
        b.Click += (_, _) => remove();
        return b;
    }

    private static List<string> AddToList(List<string> list, string text) =>
        list.Concat(text.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0)).ToList();

    private async Task SaveAllowAsync(List<string> ips)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/allowips", new { ips }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    /// <summary>The web pages whose scripts may call the gateway from a
    /// browser, by origin — a row to add one, and one row each to take it
    /// away. None listed, no page can read what the gateway answers.</summary>
    private FrameworkElement CorsRow(SettingsFullJson s)
    {
        var origins = s.CorsOrigins ?? [];
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Web pages that may call magpie",
            Description = "Scripts on these origins can call the gateway from a browser, each request with a gateway key from Gateway. A page from any other site is refused; pages on this computer are not",
            Control = PageKitUI.AddRow("http://localhost:3000", v => _ = SaveCorsAsync(AddToList(origins, v))),
        }));
        foreach (var o in origins)
        {
            var was = o;
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = o,
                Control = ItemRemover(() => _ = SaveCorsAsync((_settings!.CorsOrigins ?? []).Where(x => x != was).ToList())),
            }));
        }
        return card;
    }

    private async Task SaveCorsAsync(List<string> origins)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/cors", new { origins }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }
}