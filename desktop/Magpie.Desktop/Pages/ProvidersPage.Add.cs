using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Providers page's add sheet (app.js's renderAdd): what magpie can serve
/// next, in the order the web page lists it — partners, subscriptions, a
/// plugin's provider, one the user removed being offered back, the vendors'
/// own APIs and the relays, this machine's servers, another computer's
/// magpie, and at the end the user's own URL. A search narrows the tiles as
/// the page's does.
///
/// A tile is a card, not the web page's row: its plans and hints go in its
/// tooltip, a subscription with an account already carries a green dot and
/// how many, and a vendor's China endpoint shares its global tile. A
/// subscription moved onto its plugin is not listed here at all — the plugin
/// signs in from its own tile under "From plugins".
///
/// A click here either opens a new provider's editor (<see cref="OpenNewEditor"/>)
/// or starts a subscription's sign-in, whose flow is drawn in
/// ProvidersPage.SignIn.cs. Every write answers with the whole list, so what
/// is drawn is what magpie wrote.
/// </summary>
public sealed partial class ProvidersPage
{
    /// <summary>The words typed in the sheet's own search, which narrows the
    /// tiles while the head keeps its focus.</summary>
    private string _addQuery = "";

    /// <summary>Where the sheet's sections are drawn, rebuilt on each letter
    /// the search takes, so the search box itself is not.</summary>
    private StackPanel? _tiles;

    /// <summary>Why a built-in subscription is deprecated, as the badge's
    /// tooltip says it (app.js's DEPRECATED_WHY).</summary>
    private const string DeprecatedWhy =
        "Some subscriptions are reached in ways their vendors' terms may not allow. So that magpie itself isn't banned over them, subscription providers are being decoupled from magpie and run by community plugins instead.";

    /// <summary>The subscriptions the sheet offers, in the web page's order
    /// and with its plans (app.js's SUBS). One whose built-in was moved onto
    /// its plugin is left out of the list — Data.OnPlugins has it — and the
    /// plugin's tile signs in instead.</summary>
    private static readonly (string Agent, string Name, string Icon, string Plans, string Hint, bool Single)[] Subs =
    [
        ("claude", "Claude", "claude-color", "Pro · Max · Team", "", false),
        ("codex", "ChatGPT", "openai", "Plus · Pro · Business", "", false),
        ("chatgpt-api", "ChatGPT API", "openai", "Plus · Pro · Business",
            "Sign in with ChatGPT: your plan on OpenAI's own API, with your agent's own instructions rather than Codex's.", false),
        ("cursor", "Cursor", "cursor", "Pro · Ultra · Teams", "", true),
        ("grok", "Grok (SuperGrok)", "xai", "SuperGrok · X Premium+", "", false),
        ("copilot", "Copilot", "githubcopilot", "Free · Education · Pro · Pro+ · Max · Business · Enterprise", "", false),
        ("zcode", "ZCode (GLM Coding Plan)", "zcode", "Lite · Pro · Max · Team", "", false),
        ("workbuddy", "WorkBuddy (CodeBuddy)", "workbuddy-color", "Free · Pro", "", false),
        ("workbuddy-ai", "WorkBuddy AI (international)", "workbuddy-color", "Free · Pro", "", false),
        ("commandcode-plan", "Command Code", "commandcode", "Go · Pro · GOAT · Max · Ultra", "", false),
        ("qoder", "Qoder (international)", "qoder", "Pro", "For accounts on qoder.com, the international site.", false),
        ("qoder-cn", "Qoder CN", "qoder", "Pro",
            "For accounts on qoder.cn: signed in with an Alibaba Cloud account or a phone number.", false),
        ("devin", "Devin", "devin", "Pro · Enterprise", "", false),
        ("zed", "Zed", "zed", "Pro · Student · Business", "", false),
        ("factory", "Factory", "factory", "Pro · Plus · Max", "", false),
        ("mimo-app", "Xiaomi MiMo", "mimocode", "Free · Starter · Plus · Pro · Ultra", "", false),
        ("kiro", "Kiro", "kiro-color", "Free · Pro · Pro+ · Power", "", false),
        ("gemini", "Gemini CLI", "geminicli-color", "Code Assist Standard · Enterprise", "", false),
        ("antigravity", "Antigravity", "antigravity-color", "Google AI Pro · Ultra · free", "", false),
    ];

    // ---- the sheet -------------------------------------------------------

    /// <summary>Draws the sheet into Body, in place of the list: its head
    /// (title, search, import, close) and the sections' tiles under it. The
    /// main partial calls this while Adding is true.</summary>
    protected internal void DrawAddSheet()
    {
        Body.Children.Clear();

        var head = new Grid { ColumnSpacing = 8 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        head.Children.Add(new TextBlock
        {
            Text = L.T(Data.Providers.Count > 0 ? "Add a provider" : "Add your first provider"),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var q = new TextBox
        {
            PlaceholderText = L.T("Find a vendor…"),
            MinWidth = 240,
            MaxWidth = 320,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = _addQuery,
        };
        Grid.SetColumn(q, 1);
        q.TextChanged += (_, _) => { _addQuery = q.Text; RedrawAddTiles(); };
        head.Children.Add(q);

        // which subscription the Import button brings accounts in for: the
        // one the reader last looked at, else the first plugin's
        _addImportAgent = _addImportAgent ?? Data.Providers
            .Where(p => p.Account?.Agent is { Length: > 0 } a && _importable.Contains(a))
            .Select(p => p.Account!.Agent).FirstOrDefault();

        // accounts brought over from another tool's export, the web page's
        // Import: the files (or what is pasted) are checked with the vendor
        // before each account is kept
        var imp = new Button { Content = L.T("Import…"), VerticalAlignment = VerticalAlignment.Center };
        imp.Click += (_, _) => _ = StartImportFor(_addImportAgent ?? Data.Plugins.FirstOrDefault()?.Id ?? "antigravity");
        Grid.SetColumn(imp, 2);
        head.Children.Add(imp);

        // the first provider's sheet has no close: adding one is the way on
        if (Data.Providers.Count > 0)
        {
            var close = new Button { Content = L.T("Close"), VerticalAlignment = VerticalAlignment.Center };
            close.Click += (_, _) => { Adding = false; DrawRows(); };
            Grid.SetColumn(close, 3);
            head.Children.Add(close);
        }

        _tiles = new StackPanel { Spacing = 2 };
        Body.Children.Add(head);
        Body.Children.Add(_tiles);
        RedrawAddTiles();
    }

    /// <summary>The sheet's sections, rebuilt from the search: a section with
    /// nothing that matches is left out, and a search matching nothing at all
    /// says so, with the custom provider one click away.</summary>
    private void RedrawAddTiles()
    {
        if (_tiles is null) return;
        _tiles.Children.Clear();
        var f = _addQuery.Trim();
        bool Matches(string? s) => f.Length == 0 || (s?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false);
        bool SubNamed(string name, string agent) => Matches(name) || agent.Contains(f, StringComparison.OrdinalIgnoreCase);
        bool Hit(PresetJson pr) => Matches(pr.Name) || pr.Id.Contains(f, StringComparison.OrdinalIgnoreCase)
            || PresetHost(pr).Contains(f, StringComparison.OrdinalIgnoreCase) || Matches(pr.Note);
        bool any = false;

        // partners pay to be listed first, under a heading that says so; each
        // names the languages it is shown in, and one listed in none is
        // listed in every one — its tagline is the partner's own words
        var partners = Data.Presets
            .Where(p => p.Kind == "partner" && (p.Langs is not { Count: > 0 } || p.Langs.Contains("en"))
                && (Hit(p) || (p.Notes is { } notes && notes.TryGetValue("en", out var en) && Matches(en))))
            .ToList();
        if (partners.Count > 0)
        {
            any = true;
            var grid = Section(_tiles, "Partners", "magpie's sponsors");
            foreach (var pr in partners) grid.Children.Add(PartnerTile(pr));
        }

        // a subscription moved onto its plugin is not offered here: the
        // plugin signs in from its own tile under "From plugins"
        var subs = Subs.Where(s => !(Data.OnPlugins?.Contains(s.Agent) ?? false) && SubNamed(s.Name, s.Agent)).ToList();
        if (subs.Count > 0)
        {
            any = true;
            var grid = Section(_tiles, "Subscriptions", "sign in, no key");
            foreach (var s in subs) grid.Children.Add(SubTile(s));
            // the way to more subscriptions, which plugins sign in to: a
            // search naming one finds it through this tile
            if (f.Length == 0 || Subs.Any(s => (Data.OnPlugins?.Contains(s.Agent) ?? false) && SubNamed(s.Name, s.Agent)))
            {
                grid.Children.Add(Tile(L.T("More in Plugins"), null,
                    L.T("Subscriptions magpie doesn't sign in to itself: install a plugin for one in the Plugins tab"),
                    () => App.CurrentApp.ShowMain("plugins")));
            }
        }

        var plugged = Data.Plugins.Where(x => SubNamed(x.Name, x.Id)).ToList();
        if (plugged.Count > 0)
        {
            any = true;
            var grid = Section(_tiles, "From plugins", "signed in by an OpenCode plugin or a pi package");
            foreach (var x in plugged) grid.Children.Add(Tile(L.T(x.Name), x.Icon, x.Spec, () => _ = StartPluginSignInFor(x)));
        }

        // the removed ones the user asked not to be reminded of, each with
        // its two ways out side by side: added back as it was, or signed out
        // for good
        var gone = Data.Excluded.Where(x => x.Quiet && x.Provider is { Length: > 0 } && SubNamed(x.AgentName, x.Agent)).ToList();
        if (gone.Count > 0)
        {
            any = true;
            var grid = Section(_tiles, "Removed from magpie", "still signed in");
            foreach (var x in gone) grid.Children.Add(RemovedTile(x));
        }

        // another computer's magpie is no relay: a heading of its own, after
        // this machine's. A vendor's China endpoint is a preset of its own,
        // one tile with the global one.
        foreach (var (kind, title, hint) in new[]
        {
            ("vendor", "Vendors", "the makers' own APIs"),
            ("relay", "Relays", "one key, many vendors"),
            ("local", "On this machine", ""),
            ("remote", "Another computer's magpie", "shared on its network"),
        })
        {
            var rows = Data.Presets
                .Where(p => p.Kind == kind && !p.Id.EndsWith("-cn", StringComparison.Ordinal))
                .Select(p => (P: p, Cn: Data.Presets.FirstOrDefault(c => c.Id == p.Id + "-cn")))
                .Where(r => Hit(r.P) || (r.Cn is { } cn && Hit(cn)))
                .ToList();
            if (rows.Count == 0) continue;
            any = true;
            var grid = Section(_tiles, title, hint.Length > 0 ? hint : null);
            foreach (var (p, cn) in rows) grid.Children.Add(cn is null ? PresetTile(p) : PairTile(p, cn));
        }

        if (!any)
        {
            var none = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(2, 12, 0, 0) };
            none.Children.Add(new TextBlock
            {
                Text = L.T("Nothing called “{q}”. ").Replace("{q}", f),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });
            var custom = new HyperlinkButton { Content = L.T("Add it as a custom provider"), Padding = new Thickness(0) };
            custom.Click += (_, _) => OpenNewEditor(null);
            none.Children.Add(custom);
            _tiles.Children.Add(none);
        }
        else if (f.Length == 0)
        {
            // a vendor not listed: one line under them all
            var foot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(2, 14, 0, 0) };
            var custom = new Button
            {
                Padding = new Thickness(12, 8, 12, 8),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = "+", VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = L.T("Custom provider"), VerticalAlignment = VerticalAlignment.Center },
                    },
                },
            };
            custom.Click += (_, _) => OpenNewEditor(null);
            foot.Children.Add(custom);
            foot.Children.Add(new TextBlock
            {
                Text = L.T("any OpenAI or Anthropic compatible URL"),
                FontSize = 12,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
            });
            _tiles.Children.Add(foot);
        }
    }

    /// <summary>A section of the sheet: its name and what it is in a word,
    /// then the tiles under it. The tiles sit in a horizontal StackPanel
    /// inside a horizontally scrolling row, rather than a WrapGrid — one
    /// whose width is unbounded (a StackPanel's measure) is a catastrophic
    /// failure, and the sheet's rows are short enough to scroll past.</summary>
    private static StackPanel Section(StackPanel tiles, string title, string? hint)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(2, 10, 0, 4) };
        head.Children.Add(new TextBlock
        {
            Text = L.T(title),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (hint is { Length: > 0 })
        {
            head.Children.Add(new TextBlock
            {
                Text = L.T(hint),
                FontSize = 12,
                Opacity = 0.55,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        tiles.Children.Add(head);
        var grid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        tiles.Children.Add(grid);
        return grid;
    }

    /// <summary>One tile of the sheet: its mark and name on a card. What more
    /// there is to say goes in its tooltip, and what else it carries (a
    /// count, a badge) rides after its name.</summary>
    private static Button Tile(string name, string? icon, string? tip, Action click, FrameworkElement? extra = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        if (PageKitUI.IconCached(icon) is { } pic)
        {
            row.Children.Add(new Image { Width = 20, Height = 20, Source = pic, VerticalAlignment = VerticalAlignment.Center });
        }
        row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        if (extra is not null) row.Children.Add(extra);
        var b = new Button
        {
            Content = row,
            Padding = new Thickness(12, 8, 12, 8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        if (tip is { Length: > 0 }) ToolTipService.SetToolTip(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>The green dot a tile with an account or a provider already
    /// added carries, as the web page's markAdded does.</summary>
    private static FrameworkElement AddedDot() => new Microsoft.UI.Xaml.Shapes.Ellipse
    {
        Width = 7,
        Height = 7,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = PageKitUI.Brush("SystemFillColorSuccessBrush"),
    };

    // ---- the tiles -------------------------------------------------------

    /// <summary>A partner's tile: its own section says it is sponsored, so no
    /// badge. Added, it opens the provider it made; else the new provider's
    /// editor.</summary>
    private Button PartnerTile(PresetJson pr)
    {
        var note = pr.Notes?.TryGetValue("en", out var got) == true ? got : null;
        var tip = pr.Name + (note is { Length: > 0 } ? " · " + note : "") + "\n" + PresetHost(pr);
        var name = pr.Short is { Length: > 0 } s ? s : pr.Name;
        return Tile(name, pr.Icon, tip, () =>
        {
            if (pr.Added)
            {
                Editing = Data.Providers.FirstOrDefault(p => p.Preset == pr.Id)?.Id ?? pr.Id;
                DrawRows();
            }
            else OpenNewEditor(pr);
        }, pr.Added ? AddedDot() : null);
    }

    /// <summary>A subscription's tile: one more account when the agent has
    /// some — a green dot and how many, a Deprecated badge on one whose
    /// plugin it hasn't moved onto — and a click starting its sign-in.</summary>
    private Button SubTile((string Agent, string Name, string Icon, string Plans, string Hint, bool Single) s)
    {
        var have = Data.Providers.FirstOrDefault(p => p.Account?.Agent == s.Agent);
        var n = have is null ? 0 : have.Account?.Logins?.Count ?? 1;
        var tip = L.T("{name} subscription").Replace("{name}", L.T(s.Name)) + " · " + s.Plans;
        if (s.Hint.Length > 0) tip += "\n" + L.T(s.Hint);
        var extra = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        if ((Data.Movable?.Contains(s.Agent) ?? false) && !(Data.OnPlugins?.Contains(s.Agent) ?? false))
        {
            tip += "\n" + L.T("Deprecated") + " · " + L.T(DeprecatedWhy);
            extra.Children.Add(Badge(L.T("Deprecated")));
        }
        if (n > 0)
        {
            tip += "\n" + (s.Single
                    ? L.T("Signed in")
                    : L.T(n == 1 ? "1 account" : "{n} accounts").Replace("{n}", n.ToString()))
                + " · " + L.T(s.Single ? "signed in · click to switch account" : "click to add another account");
            extra.Children.Add(AddedDot());
            if (n > 1)
            {
                extra.Children.Add(new TextBlock { Text = n.ToString(), FontSize = 11, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center });
            }
        }
        return Tile(L.T(s.Name), s.Icon, tip, () =>
        {
            _addImportAgent = s.Agent; // the Import button follows the one looked at
            _ = StartSignInFor(s.Agent);
        }, extra.Children.Count > 0 ? extra : null);
    }

    /// <summary>A preset's tile: its short name on the card, its long one and
    /// its note or host in the tooltip. Added, it opens the provider it made;
    /// else the new provider's editor.</summary>
    private Button PresetTile(PresetJson pr)
    {
        var name = pr.Short is { Length: > 0 } s ? s : pr.Name;
        string tip;
        FrameworkElement? extra = null;
        if (pr.Added)
        {
            tip = L.T("{name} is already added — open it").Replace("{name}", pr.Name);
            extra = AddedDot();
        }
        else
        {
            tip = (pr.Short is { Length: > 0 } ? pr.Name + " · " : "")
                + (pr.Note is { Length: > 0 } n ? L.T(n) : PresetHost(pr));
            if (pr.Sponsored) extra = Badge(L.T("sponsored"));
        }
        return Tile(name, pr.Icon, tip, () =>
        {
            if (pr.Added)
            {
                Editing = Data.Providers.FirstOrDefault(p => p.Preset == pr.Id)?.Id ?? pr.Id;
                DrawRows();
            }
            else OpenNewEditor(pr);
        }, extra);
    }

    /// <summary>A vendor's global and China presets as one tile, named as the
    /// global one's short name: the editor is opened on the one not added
    /// yet, and when both are, on the provider the global one made.</summary>
    private Button PairTile(PresetJson pr, PresetJson cn)
    {
        var tip = L.T("Global") + " " + PresetHost(pr) + (pr.Added ? " · " + L.T("Added") : "") + "\n"
            + L.T("China") + " " + PresetHost(cn) + (cn.Added ? " · " + L.T("Added") : "");
        var name = pr.Short is { Length: > 0 } s ? s : pr.Name;
        return Tile(name, pr.Icon, tip, () =>
        {
            var next = !pr.Added ? pr : !cn.Added ? cn : null;
            if (next is { } go) OpenNewEditor(go);
            else
            {
                Editing = Data.Providers.FirstOrDefault(p => p.Preset == pr.Id)?.Id ?? pr.Id;
                DrawRows();
            }
        }, pr.Added || cn.Added ? AddedDot() : null);
    }

    /// <summary>A removed account's tile: its mark and name, and its two ways
    /// out side by side — added back as it was, or signed out for good, asked
    /// first, since what magpie keeps of its sign-ins goes with it.</summary>
    private Border RemovedTile(ExcludedJson x)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (PageKitUI.IconCached(x.AgentIcon) is { } pic)
        {
            row.Children.Add(new Image { Width = 20, Height = 20, Source = pic, VerticalAlignment = VerticalAlignment.Center });
        }
        row.Children.Add(new TextBlock { Text = x.AgentName, VerticalAlignment = VerticalAlignment.Center });

        var back = new Button { Content = L.T("Add it back"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(back, L.T("its accounts and model picks as they were"));
        back.Click += async (_, _) =>
        {
            back.IsEnabled = false;
            try
            {
                var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>("provider/show", new { id = x.Provider });
                await ReloadAsync(answer);
            }
            catch (Exception e) { Status.Text = e.Message; back.IsEnabled = true; }
        };
        row.Children.Add(back);

        var signOut = new Button { Content = L.T("Sign out…"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(signOut, L.T("magpie forgets its accounts; adding it again signs in afresh"));
        signOut.Click += async (_, _) =>
        {
            if (!await PageKitUI.ConfirmAsync(XamlRoot,
                    L.T("Sign {name} out of magpie?").Replace("{name}", x.AgentName),
                    L.T("magpie signs out the accounts it keeps for {name} and forgets them; adding {name} again signs in afresh. The account itself is left as it is.").Replace("{name}", x.AgentName),
                    L.T("Sign out"), L.T("Cancel"), danger: true)) return;
            try
            {
                var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>("provider/forget", new { id = x.Provider });
                await ReloadAsync(answer);
            }
            catch (Exception e) { Status.Text = e.Message; }
        };
        row.Children.Add(signOut);

        return new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = row,
        };
    }

    // ---- the new provider's editor --------------------------------------

    /// <summary>Opens the editor on a provider being made: a name, an API
    /// key and a base URL, with the preset's own as where they start (and an
    /// endpoint asked for when the preset needs one, as Azure's does). Adding
    /// is put away — the editor takes the page's Body itself.</summary>
    protected internal void OpenNewEditor(PresetJson? pr)
    {
        Adding = false;
        Body.Children.Clear();

        var card = new StackPanel { Spacing = 10, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left };
        card.Children.Add(new TextBlock
        {
            Text = pr is { Name.Length: > 0 } ? pr.Name : L.T("Custom provider"),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        static StackPanel Field(string label, FrameworkElement box)
        {
            var p = new StackPanel { Spacing = 4 };
            p.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 });
            p.Children.Add(box);
            return p;
        }

        var name = new TextBox { Text = pr?.Name ?? "", PlaceholderText = L.T("Name") };
        var key = new PasswordBox { PlaceholderText = pr?.KeyHint is { Length: > 0 } kh ? kh : L.T("API key") };
        var chat = new TextBox
        {
            PlaceholderText = L.T("Base URL"),
            Text = pr?.Chat is { Length: > 0 } c ? c
                : pr?.Responses is { Length: > 0 } r ? r
                : pr?.Anthropic ?? "",
        };
        // Azure: the resource's own address, which the key alone doesn't say
        TextBox? endpoint = null;
        if (pr?.Endpoint is not null)
        {
            endpoint = new TextBox
            {
                PlaceholderText = pr.EndpointNeeded is { Length: > 0 } need ? need : L.T("Your resource's endpoint is needed"),
            };
        }

        var add = new Button { Content = L.T("Add") };
        add.Click += async (_, _) =>
        {
            var nm = name.Text.Trim();
            if (nm.Length == 0) { Status.Text = L.T("Name"); return; }
            var at = endpoint is null ? "" : endpoint.Text.Trim();
            if (endpoint is not null && at.Length == 0)
            {
                Status.Text = pr!.EndpointNeeded is { Length: > 0 } need ? need : L.T("Your resource's endpoint is needed");
                return;
            }
            add.IsEnabled = false;
            try
            {
                var url = chat.Text.Trim();
                var which = pr?.Chat is { Length: > 0 } ? "chat"
                    : pr?.Responses is { Length: > 0 } ? "responses"
                    : pr?.Anthropic is { Length: > 0 } ? "anthropic" : "chat";
                var body = new Dictionary<string, object?>
                {
                    ["id"] = Slug(nm),
                    ["name"] = nm,
                    ["preset"] = pr?.Id ?? "",
                    ["key"] = key.Password.Trim(),
                    ["new"] = true,
                };
                if (url.Length > 0) body[which] = url;
                if (endpoint is not null) body["endpoint"] = at;
                var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>("provider/save", body);
                await ReloadAsync(answer);
                _addQuery = "";
            }
            catch (Exception e)
            {
                Status.Text = e.Message;
                add.IsEnabled = true;
            }
        };

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var cancel = new Button { Content = L.T("Cancel") };
        cancel.Click += (_, _) => DrawRows();
        bar.Children.Add(cancel);
        bar.Children.Add(add);

        card.Children.Add(Field(L.T("Name"), name));
        card.Children.Add(Field(L.T("API key"), key));
        card.Children.Add(Field(L.T("Base URL"), chat));
        if (endpoint is not null) card.Children.Add(Field(L.T("Endpoint"), endpoint));
        card.Children.Add(bar);
        Body.Children.Add(card);
    }

    /// <summary>The id magpie gives a provider of a name (app.js's slug):
    /// lower case, runs of anything but letters and digits one dash, trimmed
    /// of dashes at the ends.</summary>
    private static string Slug(string s)
    {
        var id = new System.Text.StringBuilder();
        var dash = false;
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (dash && id.Length > 0) id.Append('-');
                dash = false;
                id.Append(c);
            }
            else dash = id.Length > 0;
        }
        return id.ToString();
    }

    /// <summary>The host of a base URL, as the web page's hostOf says it: ""
    /// when it parses as none.</summary>
    private static string HostOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        return Uri.TryCreate(url.Contains("://", StringComparison.Ordinal) ? url : "https://" + url, UriKind.Absolute, out var u)
            ? u.Host : "";
    }

    /// <summary>The host a preset is asked on: its first base URL that is
    /// filled in.</summary>
    private static string PresetHost(PresetJson pr) =>
        HostOf(pr.Chat is { Length: > 0 } ? pr.Chat : pr.Responses is { Length: > 0 } ? pr.Responses : pr.Anthropic);

    // ---- sign-ins --------------------------------------------------------

    // The sign-in flow itself is drawn in ProvidersPage.SignIn.cs; these two
    // forward to it, so the sheet's tiles have one way in.

    /// <summary>The subscription the sheet's Import button brings accounts in
    /// for: the one whose tile the reader last clicked, else a signed-in one,
    /// else the first plugin's.</summary>
    private string? _addImportAgent;

    /// <summary>The agents whose accounts can be brought in from another
    /// tool's export (provider's signin/import).</summary>
    private static readonly HashSet<string> _importable = ["claude", "codex", "factory", "antigravity"];
}
