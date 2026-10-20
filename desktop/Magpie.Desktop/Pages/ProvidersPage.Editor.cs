using Magpie.Desktop.Backend;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Magpie.Desktop.Pages;

/// <summary>
/// A provider's editor, drawn in place of the list while a row is open, as
/// the web page's dialog is (app.js's drawEditor): the provider's name and
/// mark, the agents on it, its key or its accounts, its models, how requests
/// spread over them, and the settings few need under Advanced. A save sends
/// only what changed — the backend keeps what a save of an existing provider
/// omits — and the answer, the whole list, is what is drawn.
/// </summary>
public sealed partial class ProvidersPage
{
    /// <summary>The models the editor has picked, while it is open: the
    /// saved picks (Chosen) when the list had them, else the ones on.</summary>
    private HashSet<string>? _pickedModels;

    /// <summary>Whether the editor draws: a draw that sets a control's state
    /// is not a click on it.</summary>
    private bool _editorDrawing;

    /// <summary>Draws the editor on a provider, in the page's Body.</summary>
    protected internal void DrawEditor(ProviderJson p)
    {
        _pickedModels ??= p.Chosen is { Count: > 0 } chosen
            ? [.. chosen]
            : [.. p.Models.Where(m => m.On).Select(m => m.Id)];
        _editorDrawing = true;
        try
        {
            Body.Children.Clear();
            Body.Children.Add(EditorHead(p));
            if (p.Account is { } account) Body.Children.Add(AccountsCard(p, account));
            else Body.Children.Add(KeyCard(p));
            if (p.Models.Count > 0 || p.DrawIds is { Count: > 0 }) Body.Children.Add(ModelsCard(p));
            if (p.Account is not null || p.KeyList.Count > 1) Body.Children.Add(RoutingCard(p));
            Body.Children.Add(AdvancedCard(p));
            Body.Children.Add(EditorBar(p));
        }
        finally
        {
            _editorDrawing = false;
        }
    }

    /// <summary>Closes the editor: the list is drawn again.</summary>
    protected internal void CloseEditor()
    {
        _pickedModels = null;
        Editing = null;
        DrawRows();
    }

    /// <summary>Reads the list again and draws the editor's answer, as every
    /// write here does.</summary>
    private async Task SaveAsync(ProviderJson p, object body, string okSaid)
    {
        try
        {
            var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>("provider/save", body);
            await ReloadAsync(answer);
            if (Editing == p.Id) DrawEditor(Data.Providers.FirstOrDefault(x => x.Id == p.Id) ?? p);
            Status.Text = okSaid;
        }
        catch (Exception e)
        {
            Status.Text = e.Message;
        }
    }

    /// <summary>A write whose answer is the whole list, drawn from.</summary>
    private async Task WriteAsync(ProviderJson p, string path, object body)
    {
        try
        {
            var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>(path, body);
            await ReloadAsync(answer);
            if (Editing == p.Id) DrawEditor(Data.Providers.FirstOrDefault(x => x.Id == p.Id) ?? p);
        }
        catch (Exception e)
        {
            Status.Text = e.Message;
        }
    }

    // ---- the cards -------------------------------------------------------

    private Border EditorHead(ProviderJson p)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var back = new Button
        {
            Content = L.T("Back"),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(back, L.T("Providers"));
        back.Click += (_, _) => CloseEditor();
        grid.Children.Add(back);

        var who = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(who, 1);
        if (PageKitUI.IconCached(p.Icon) is { } icon) who.Children.Add(new Image { Width = 24, Height = 24, Source = icon });
        who.Children.Add(new TextBlock { Text = p.Name, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (p.Website is { Length: > 0 } site)
        {
            who.Children.Add(PageKitUI.LinkButton(HostOf(site), site));
        }
        grid.Children.Add(who);

        var toggle = new ToggleSwitch
        {
            IsOn = !p.Off,
            OnContent = "",
            OffContent = "",
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toggle.Toggled += async (_, _) =>
        {
            if (_editorDrawing || toggle.IsOn == !p.Off) return;
            await WriteAsync(p, toggle.IsOn ? "provider/on" : "provider/off", new { id = p.Id });
        };
        Grid.SetColumn(toggle, 2);
        grid.Children.Add(toggle);

        var close = new Button { Content = L.T("Close"), Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
        close.Click += (_, _) => CloseEditor();
        Grid.SetColumn(close, 3);
        grid.Children.Add(close);

        var box = new StackPanel { Spacing = 6 };
        if (p.Off)
        {
            box.Children.Add(new TextBlock
            {
                Text = L.T("Switched off: agents aren't given its models and no request goes to it. Its keys and settings are kept; switch it on to use it again."),
                FontSize = 12,
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        if (p.Agents.Any(a => a.Current))
        {
            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var a in p.Agents.Where(a => a.Current))
            {
                var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                if (PageKitUI.IconCached(a.AgentIcon) is { } ai) chip.Children.Add(new Image { Width = 14, Height = 14, Source = ai });
                chip.Children.Add(new TextBlock { Text = a.AgentName, FontSize = 11.5, Opacity = 0.8 });
                chips.Children.Add(new Border
                {
                    Padding = new Thickness(8, 2, 8, 2),
                    CornerRadius = new CornerRadius(999),
                    Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                    Child = chip,
                });
            }
            box.Children.Add(chips);
        }
        var card = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = box,
        };
        var outer = new StackPanel { Spacing = 8 };
        outer.Children.Add(card);
        outer.Children.Add(grid);
        return new Border { Child = outer };
    }

    /// <summary>The key card, for a provider of keys: the key masked, shown
    /// on demand (POST provider/key), and the vendor's keys page.</summary>
    private Border KeyCard(ProviderJson p)
    {
        var box = new StackPanel { Spacing = 8 };
        box.Children.Add(new TextBlock { Text = L.T("API key"), FontSize = 13, FontWeight = FontWeights.SemiBold });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var key = new PasswordBox { Password = p.Key.Masked, PlaceholderText = L.T("API key"), Width = 280 };
        row.Children.Add(key);
        var show = new Button { Content = L.T("Show"), Padding = new Thickness(8, 2, 8, 2) };
        show.Click += async (_, _) =>
        {
            try
            {
                var answer = await App.CurrentApp.Client.PostAsync<Dictionary<string, string>>("provider/key", new { id = p.Id });
                if (answer?.TryGetValue("key", out var real) == true) key.Password = real;
            }
            catch (Exception e) { Status.Text = e.Message; }
        };
        row.Children.Add(show);
        if (p.KeysUrl is { Length: > 0 } keys) row.Children.Add(PageKitUI.LinkButton(L.T("Get a key"), keys));
        box.Children.Add(row);
        if (p.Key.Optional)
        {
            box.Children.Add(new TextBlock
            {
                Text = L.T("Local servers need no key"),
                FontSize = 12,
                Opacity = 0.7,
            });
        }

        // the key goes with Save: a save that leaves it out keeps it
        var save = new Button { Content = L.T("Save"), Padding = new Thickness(10, 2, 10, 2), HorizontalAlignment = HorizontalAlignment.Left };
        save.Click += async (_, _) =>
        {
            var typed = key.Password;
            var body = new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id };
            // the masked form stands for the saved key: only a different one is sent
            if (typed.Length > 0 && typed != p.Key.Masked) body["key"] = typed;
            await SaveAsync(p, body, L.T("{name} saved").Replace("{name}", p.Name));
        };
        box.Children.Add(save);
        return Card(box);
    }

    /// <summary>The accounts card, for a subscription: one row per account
    /// magpie remembers, ticked when it is on, signed in again when its
    /// vendor refused it.</summary>
    private Border AccountsCard(ProviderJson p, AccountJson account)
    {
        var box = new StackPanel { Spacing = 6 };
        box.Children.Add(new TextBlock { Text = L.T("Accounts"), FontSize = 13, FontWeight = FontWeights.SemiBold });
        var logins = account.Logins ?? [];
        if (logins.Count == 0)
        {
            box.Children.Add(new TextBlock
            {
                Text = L.T("signed in as {user}").Replace("{user}", account.User),
                FontSize = 12,
                Opacity = 0.75,
            });
        }
        foreach (var l in logins)
        {
            var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tick = new CheckBox
            {
                IsChecked = !l.Lapsed.IsNullOrEmpty() ? false : (l.Active && !l.Paused) || l.On,
                IsEnabled = l.Lapsed.IsNullOrEmpty(),
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(tick, (l.Active && !l.Paused) || l.On
                ? L.T("Stop using this account")
                : L.T("Use this account too"));
            tick.Checked += (_, _) => { if (!_editorDrawing) _ = WriteAsync(p, "login/on", new { agent = account.Agent, user = l.User }); };
            tick.Unchecked += (_, _) => { if (!_editorDrawing) _ = WriteAsync(p, "login/off", new { agent = account.Agent, user = l.User }); };
            grid.Children.Add(tick);

            var who = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(who, 1);
            who.Children.Add(new TextBlock { Text = l.User, FontSize = 13 });
            var plan = l.Plan is { Length: > 0 } pl ? pl : "";
            if (l.Lapsed is { Length: > 0 } why) plan = why;
            if (plan.Length > 0)
            {
                who.Children.Add(new TextBlock { Text = plan, FontSize = 11.5, Opacity = l.Lapsed.IsNullOrEmpty() ? 0.65 : 1.0,
                    Foreground = l.Lapsed.IsNullOrEmpty() ? null : (Brush)Application.Current.Resources["SystemFillColorCautionBrush"] });
            }
            grid.Children.Add(who);

            if (l.Lapsed is { Length: > 0 })
            {
                var again = new Button { Content = L.T("Sign in again"), Padding = new Thickness(8, 2, 8, 2) };
                again.Click += (_, _) => _ = StartSignInFor(account.Agent);
                Grid.SetColumn(again, 2);
                grid.Children.Add(again);
            }
            else if (!l.Active && (l.On || l.Active))
            {
                var first = new Button { Content = L.T("Make first"), Padding = new Thickness(8, 2, 8, 2) };
                var others = logins.Where(x => x != l).Select(x => x.User).ToList();
                first.Click += async (_, _) => await WriteAsync(p, "provider/arrange", new { id = p.Id, accountOrder = new List<string> { l.User }.Concat(others).ToList() });
                Grid.SetColumn(first, 2);
                grid.Children.Add(first);
            }
            else if (!l.Active)
            {
                var use = new Button { Content = L.T("Use"), Padding = new Thickness(8, 2, 8, 2) };
                use.Click += async (_, _) => await WriteAsync(p, "login/switch", new { agent = account.Agent, user = l.User });
                Grid.SetColumn(use, 2);
                grid.Children.Add(use);
            }

            var forget = new Button { Content = L.T("Remove"), Padding = new Thickness(8, 2, 8, 2) };
            forget.Click += async (_, _) =>
            {
                if (!await PageKitUI.ConfirmAsync(XamlRoot,
                        L.T("Remove"),
                        L.T("magpie forgets this account's sign-in; the account itself is untouched"),
                        L.T("Remove"), L.T("Cancel"), danger: true)) return;
                await WriteAsync(p, "login/forget", new { agent = account.Agent, user = l.User });
            };
            Grid.SetColumn(forget, 3);
            grid.Children.Add(forget);
            box.Children.Add(grid);
        }

        // Codex or Claude Code stays signed in to the first account rather
        // than moving on when it runs low, or to one of the user's choosing
        if ((account.Agent == "codex" || account.Agent == "claude") && logins.Count(l => (l.Active && !l.Paused) || l.On) > 1)
        {
            var keep = new ToggleSwitch
            {
                IsOn = p.KeepLogin,
                Header = L.T("Keep {agent} signed in to").Replace("{agent}", account.AgentName),
                MinWidth = 0,
            };
            ToolTipService.SetToolTip(keep, L.T("magpie won't sign {agent} in to another account when the first runs low; requests through magpie still go to the other ticked accounts as Routing says")
                .Replace("{agent}", account.AgentName));
            keep.Toggled += async (_, _) =>
            {
                if (_editorDrawing || keep.IsOn == p.KeepLogin) return;
                var body = new Dictionary<string, object?> { ["id"] = p.Id, ["keepLogin"] = keep.IsOn };
                if (keep.IsOn && p.KeepLoginAs is { Length: > 0 }) body["keepLoginAs"] = p.KeepLoginAs;
                await WriteAsync(p, "provider/keeplogin", body);
            };
            box.Children.Add(keep);
            if (p.KeepLogin)
            {
                var asButton = PageKitUI.Menu(
                    new List<(string, string)> { ("", L.T("the first account")) }
                        .Concat(logins.Where(l => l.Lapsed.IsNullOrEmpty()).Select(l => (l.User, l.User))),
                    p.KeepLoginAs ?? "",
                    async chosen => await WriteAsync(p, "provider/keeplogin", new Dictionary<string, object?> { ["id"] = p.Id, ["keepLogin"] = true, ["keepLoginAs"] = chosen }));
                box.Children.Add(asButton);
            }
        }
        // magpie only moves Codex's own sign-in, its requests going straight
        // to OpenAI, when this is on
        if (account.Agent == "codex" && logins.Count(l => (l.Active && !l.Paused) || l.On) > 1)
        {
            var only = new ToggleSwitch
            {
                IsOn = p.SwitchOnly,
                Header = L.T("Only switch {agent}'s sign-in").Replace("{agent}", account.AgentName),
                MinWidth = 0,
            };
            ToolTipService.SetToolTip(only, L.T("magpie signs {agent} in to the next account with room when the one it is on is used up, and back once that has room again, by writing {agent}'s own sign-in; {agent}'s requests go straight to OpenAI, never through magpie. A {agent} app or session already open stays on its account until it is opened again. Connected to magpie, {agent}'s requests go through it as before.")
                .Replace("{agent}", account.AgentName));
            only.Toggled += async (_, _) =>
            {
                if (_editorDrawing || only.IsOn == p.SwitchOnly) return;
                await WriteAsync(p, "provider/switchonly", new { id = p.Id, switchOnly = only.IsOn });
            };
            box.Children.Add(only);
        }
        return Card(box);
    }

    /// <summary>The models card: a tick per model, the ones the agents are
    /// offered, with their context and price; Select all and none; a test,
    /// from a model's own menu.</summary>
    private Border ModelsCard(ProviderJson p)
    {
        var box = new StackPanel { Spacing = 6 };
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = L.T("Models"), FontSize = 13, FontWeight = FontWeights.SemiBold });
        var all = new Button { Content = L.T("Select all"), Padding = new Thickness(8, 2, 8, 2), FontSize = 12 };
        all.Click += (_, _) =>
        {
            _pickedModels = p.Models.Where(m => !m.NoApi).Select(m => m.Id).ToHashSet();
            DrawEditor(p);
        };
        Grid.SetColumn(all, 1);
        head.Children.Add(all);
        var none = new Button { Content = L.T("Select none"), Padding = new Thickness(8, 2, 8, 2), FontSize = 12 };
        none.Click += async (_, _) =>
        {
            // Select none is saved with no model, as the page's does: the
            // provider keeps its settings and serves nothing
            _pickedModels = [];
            await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["models"] = new List<string>(), ["noModels"] = true },
                L.T("{name} saved").Replace("{name}", p.Name));
        };
        Grid.SetColumn(none, 2);
        head.Children.Add(none);
        box.Children.Add(head);

        var list = new StackPanel { Spacing = 2 };
        foreach (var m in p.Models)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tick = new CheckBox { IsChecked = _pickedModels?.Contains(m.Id) == true, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
            var on = tick.IsChecked == true;
            tick.Checked += (_, _) => { if (_editorDrawing || on) return; _pickedModels?.Add(m.Id); _ = PushModelsAsync(p); };
            tick.Unchecked += (_, _) => { if (_editorDrawing || !on) return; _pickedModels?.Remove(m.Id); _ = PushModelsAsync(p); };
            grid.Children.Add(tick);

            if (PageKitUI.IconCached(m.Icon) is { } icon)
            {
                var image = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, Source = icon };
                Grid.SetColumn(image, 1);
                grid.Children.Add(image);
            }

            var who = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(who, 2);
            who.Children.Add(new TextBlock { Text = m.Name is { Length: > 0 } n ? n : m.Id, FontSize = 12.5 });
            var facts = new List<string>();
            if (m.Context > 0) facts.Add(ContextSaid(m.Context));
            if (m.Free) facts.Add("free");
            if (m.Rate > 0) facts.Add("×" + m.Rate.ToString("0.##"));
            if (m.Api is { Length: > 0 } api) facts.Add(api);
            if (m.Kind is { Length: > 0 } kind) facts.Add(kind);
            if (facts.Count > 0)
            {
                who.Children.Add(new TextBlock { Text = string.Join(" · ", facts), FontSize = 11, Opacity = 0.6 });
            }
            grid.Children.Add(who);

            if (m.NoApi)
            {
                var said = new TextBlock { Text = L.T("no models exposed"), FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(said, 3);
                grid.Children.Add(said);
            }
            var chip = new Border
            {
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                Child = grid,
            };
            chip.ContextFlyout = ModelMenu(p, m);
            list.Children.Add(chip);
        }
        box.Children.Add(new ScrollViewer { Content = list, MaxHeight = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return Card(box);
    }

    /// <summary>The picks as one write: the whole list, the backend keeping
    /// the one the agent is on.</summary>
    private async Task PushModelsAsync(ProviderJson p)
    {
        if (_pickedModels is null) return;
        await WriteAsync(p, "provider/models", new { id = p.Id, models = _pickedModels.ToList() });
    }

    /// <summary>A model's menu: test it alone, copy its id, say what it
    /// makes.</summary>
    private MenuFlyout ModelMenu(ProviderJson p, ModelJson m)
    {
        var menu = new MenuFlyout();
        var test = new MenuFlyoutItem
        {
            Text = L.T("Test this model"),
            IsEnabled = p.ModelTest is not { Length: > 0 },
        };
        test.Click += async (_, _) =>
        {
            try
            {
                await App.CurrentApp.Client.PostAsync("provider/test", new { id = p.Id, test = new List<string> { m.Id } });
                Status.Text = L.T("Answered");
            }
            catch (Exception e) { Status.Text = e.Message; }
        };
        menu.Items.Add(test);
        var copy = new MenuFlyoutItem { Text = L.T("Copy model ID") };
        copy.Click += (_, _) =>
        {
            try
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                package.SetText(p.Id + "/" + m.Id);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            }
            catch (Exception) { /* the clipboard is the system's */ }
        };
        menu.Items.Add(copy);
        if (p.MarksKinds)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            var image = new MenuFlyoutItem { Text = L.T("Image model") };
            image.Click += async (_, _) => await SetKindAsync(p, m, "image");
            menu.Items.Add(image);
            var video = new MenuFlyoutItem { Text = L.T("Video model") };
            video.Click += async (_, _) => await SetKindAsync(p, m, "video");
            menu.Items.Add(video);
        }
        return menu;
    }

    private async Task SetKindAsync(ProviderJson p, ModelJson m, string kind)
    {
        var prefs = new Dictionary<string, ModelPrefJson> { [m.Id] = new() { Kind = kind } };
        await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["modelPrefs"] = prefs },
            L.T("{name} saved").Replace("{name}", p.Name));
    }

    /// <summary>How requests spread over the keys or accounts, how long a
    /// conversation stays with the one that answered, and what one rate
    /// limited while it still has quota does.</summary>
    private Border RoutingCard(ProviderJson p)
    {
        var box = new StackPanel { Spacing = 8 };
        box.Children.Add(new TextBlock { Text = L.T("Routing"), FontSize = 13, FontWeight = FontWeights.SemiBold });

        box.Children.Add(PageKitUI.Choices(new List<(string, string)>
        {
            ("", L.T("Smart")),
            ("order", L.T("In order")),
            ("rotate", L.T("In turn")),
            ("usage", L.T("Least used first")),
            ("pace", L.T("Weekly pace")),
            ("weight", L.T("By weight")),
        }, p.Routing ?? "", async routing => await WriteAsync(p, "provider/route", new { id = p.Id, routing })));

        box.Children.Add(new TextBlock { Text = L.T("Stays"), FontSize = 12, Opacity = 0.7 });
        box.Children.Add(PageKitUI.Choices(new List<(string, string)>
        {
            ("", L.T("Auto")),
            ("session", L.T("Session")),
            ("turn", L.T("Within a turn")),
            ("off", L.T("Off")),
            ("strict", L.T("Strict")),
        }, p.Affinity ?? "", async affinity => await WriteAsync(p, "provider/affinity", new { id = p.Id, affinity })));

        box.Children.Add(new TextBlock { Text = L.T("Rate limited"), FontSize = 12, Opacity = 0.7 });
        box.Children.Add(PageKitUI.Choices(new List<(string, string)>
        {
            ("", L.T("Keeps its place")),
            ("sink", L.T("Goes to the back")),
        }, p.Sink ? "sink" : "", async choice => await WriteAsync(p, "provider/sink", new { id = p.Id, sink = choice == "sink" })));
        return Card(box);
    }

    /// <summary>The settings few need, folded: the context window, the
    /// proxy, the limits, the headers, the web search.</summary>
    private Expander AdvancedCard(ProviderJson p)
    {
        var box = new StackPanel { Spacing = 8 };

        box.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = L.T("Context window"),
            Description = L.T("How long a request the models take, told to the agents; empty leaves it to the vendor and models.dev"),
            Control = PageKitUI.TextField(ContextsSaid(p.Contexts), "e.g. 128k · or gpt-6=1m, comma separated",
                async value =>
                {
                    var map = ParseContexts(value);
                    if (map is null) { Status.Text = L.T("Context window: {v} is not a length like 128k or 1m").Replace("{v}", value); return; }
                    await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["contexts"] = map },
                        L.T("{name} saved").Replace("{name}", p.Name));
                }),
        }));

        box.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = L.T("Proxy"),
            Description = L.T("Where its requests go: the global one, none, or an address"),
            Control = PageKitUI.TextField(p.Proxy, "http://127.0.0.1:7890",
                async value => await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["proxy"] = value },
                    L.T("{name} saved").Replace("{name}", p.Name))),
        }));

        box.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = L.T("Concurrency"),
            Description = L.T("How many requests each of its keys or accounts has out at once; 0 is no limit"),
            Control = PageKitUI.NumberField(p.MaxConcurrency ?? 0, "0",
                async n => await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["maxConcurrency"] = n },
                    L.T("{name} saved").Replace("{name}", p.Name)), L.T("Save")),
        }));

        box.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = L.T("Requests per minute"),
            Description = L.T("How many requests a minute each of its keys or accounts sends the vendor; 0 is no limit"),
            Control = PageKitUI.NumberField(p.MaxRpm, "0",
                async n => await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["maxRPM"] = n },
                    L.T("{name} saved").Replace("{name}", p.Name)), L.T("Save")),
        }));

        box.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = L.T("Price rate"),
            Description = L.T("What it charges against the official price; 0 is that"),
            Control = PageKitUI.TextField(p.PriceRate > 0 ? p.PriceRate.ToString("0.##") : "", "0",
                async value =>
                {
                    if (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var rate))
                    {
                        Status.Text = L.T("{v} is not a number").Replace("{v}", value);
                        return;
                    }
                    await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["priceRate"] = rate },
                        L.T("{name} saved").Replace("{name}", p.Name));
                }, applyLabel: L.T("Save")),
        }));

        if (p.Searches || p.Preset.Length > 0)
        {
            var searches = new ToggleSwitch { IsOn = p.Searches, Header = L.T("Web search"), MinWidth = 0 };
            ToolTipService.SetToolTip(searches, L.T("The vendor searches the web by itself"));
            searches.Toggled += async (_, _) =>
            {
                if (_editorDrawing || searches.IsOn == p.Searches) return;
                await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id, ["searches"] = searches.IsOn },
                    L.T("{name} saved").Replace("{name}", p.Name));
            };
            box.Children.Add(searches);
        }
        var fold = new Expander
        {
            Header = new TextBlock { Text = L.T("Advanced"), FontSize = 13 },
            Content = box,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        return fold;
    }

    private Border EditorBar(ProviderJson p)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var remove = new Button { Content = L.T("Remove"), Padding = new Thickness(10, 2, 10, 2) };
        remove.Click += async (_, _) =>
        {
            if (!await PageKitUI.ConfirmAsync(XamlRoot, L.T("Remove"),
                    L.T("{agent} stays signed in; magpie just stops offering it").Replace("{agent}", p.Account?.AgentName ?? p.Name),
                    L.T("Remove"), L.T("Cancel"), danger: true)) return;
            try
            {
                var answer = await App.CurrentApp.Client.PostAsync<ProvidersJson>("provider/delete", new { id = p.Id });
                CloseEditor();
                await ReloadAsync(answer);
            }
            catch (Exception e) { Status.Text = e.Message; }
        };
        grid.Children.Add(remove);

        var cancel = new Button { Content = L.T("Cancel"), Padding = new Thickness(10, 2, 10, 2) };
        cancel.Click += (_, _) => CloseEditor();
        Grid.SetColumn(cancel, 2);
        grid.Children.Add(cancel);

        var save = new Button
        {
            Content = L.T("Save"),
            Padding = new Thickness(14, 2, 14, 2),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        save.Click += async (_, _) => await SaveAsync(p, new Dictionary<string, object?> { ["id"] = p.Id, ["from"] = p.Id },
            L.T("{name} saved").Replace("{name}", p.Name));
        Grid.SetColumn(save, 3);
        grid.Children.Add(save);

        return new Border { Padding = new Thickness(0, 4, 0, 8), Child = grid };
    }

    // ---- small helpers ---------------------------------------------------

    private static Border Card(FrameworkElement content) => new()
    {
        Padding = new Thickness(14, 10, 14, 10),
        CornerRadius = new CornerRadius(8),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        Child = content,
    };

    /// <summary>A model's context window as the picker says it: 1M, 200K.</summary>
    private static string ContextSaid(int n) => n >= 1_000_000
        ? (n % 1_000_000 == 0 ? (n / 1_000_000).ToString() : (n / 1_000_000.0).ToString("0.0")) + "M"
        : (n / 1000) + "K";

    /// <summary>A provider's contexts as the editor's one box holds them: the
    /// one for all its models first, then model=size.</summary>
    private static string ContextsSaid(Dictionary<string, int>? contexts)
    {
        if (contexts is null || contexts.Count == 0) return "";
        var parts = new List<string>();
        if (contexts.TryGetValue("*", out var all)) parts.Add(ContextSaid(all));
        foreach (var (id, n) in contexts.Where(x => x.Key != "*")) parts.Add(id + "=" + ContextSaid(n));
        return string.Join(", ", parts);
    }

    /// <summary>The one box as a map of sizes: a bare number or length is
    /// every model's, model=length pairs are one each. Null says the text
    /// isn't one.</summary>
    private static Dictionary<string, int>? ParseContexts(string text)
    {
        var map = new Dictionary<string, int>();
        foreach (var raw in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var (key, value) = raw.Contains('=', StringComparison.Ordinal) ? (raw[..raw.IndexOf('=')].Trim(), raw[(raw.IndexOf('=') + 1)..].Trim()) : ("*", raw);
            var n = SizeOf(value);
            if (n is null) return null;
            map[key] = n.Value;
        }
        return map;
    }

    private static int? SizeOf(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().ToLowerInvariant();
        if (text.EndsWith("k", StringComparison.Ordinal) && double.TryParse(text[..^1], System.Globalization.CultureInfo.InvariantCulture, out var k)) return (int)(k * 1000);
        if (text.EndsWith("m", StringComparison.Ordinal) && double.TryParse(text[..^1], System.Globalization.CultureInfo.InvariantCulture, out var m)) return (int)(m * 1_000_000);
        return int.TryParse(text, out var plain) ? plain : null;
    }
}

/// <summary>A string the API leaves out or empty reads the same here: a
/// lapsed account's reason is "" when it isn't lapsed.</summary>
internal static class ProviderStringExtensions
{
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
}
