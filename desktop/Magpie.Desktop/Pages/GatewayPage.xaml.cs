using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Gateway page: whether magpie serves the gateway and at which address,
/// how a client connects in each API, the caller keys with their switches
/// and usage, and the calls served lately, each with its wire bodies a fold
/// away (GET /api/providers, /api/caller-keys, /api/gateway/call).
/// </summary>
public sealed partial class GatewayPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    private ProvidersJson _providers = new();
    private List<CallerKeyJson> _keys = [];
    /// <summary>The address and gateway key Connect's examples use, picked in
    /// their menus and kept across runs as the page keeps them.</summary>
    private string _address = "";
    private string _keyId = "";
    /// <summary>The picked key's secret, read for the examples only.</summary>
    private string _secret = "";
    private int _proto;
    /// <summary>True while Draw writes the switches: their Toggled fires then
    /// too, and must not be sent back as a change.</summary>
    private bool _drawing;

    public GatewayPage()
    {
        InitializeComponent();
        _proto = ReadPick("proto", 0);
        _address = ReadPick("address", "");
        _keyId = ReadPick("key", "");
    }

    /// <summary>A pick kept across runs, as the page keeps them in its own
    /// storage (magpie.connectAddress, magpie.connectKey, magpie.flavor).</summary>
    private static string ReadPick(string name, string fallback)
    {
        try
        {
            var file = Path.Combine(BackendProcess.AppDataDir(), "gateway-" + name);
            return File.Exists(file) ? File.ReadAllText(file).Trim() : fallback;
        }
        catch (IOException) { return fallback; }
    }

    private static int ReadPick(string name, int fallback) =>
        int.TryParse(ReadPick(name, ""), out var n) && n is >= 0 and <= 3 ? n : fallback;

    private static void KeepPick(string name, string value)
    {
        try
        {
            Directory.CreateDirectory(BackendProcess.AppDataDir());
            File.WriteAllText(Path.Combine(BackendProcess.AppDataDir(), "gateway-" + name), value);
        }
        catch (IOException) { }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await PageKit.RunAsync(ReadAsync, Status, Busy);
    }

    /// <summary>Reads the gateway's state and the caller keys together, then
    /// draws the page from what magpie answered.</summary>
    private async Task ReadAsync()
    {
        var providers = App.CurrentApp.Client.GetAsync<ProvidersJson>("providers");
        var keys = App.CurrentApp.Client.GetAsync<CallerKeysJson>("caller-keys");
        _providers = await providers ?? new ProvidersJson();
        _keys = (await keys)?.Keys ?? [];
        // a picked key that is gone, or a fresh list after a rotation, drops
        // the secret read before: it may no longer be the one
        if (!_keys.Any(k => k.Id == _keyId && !k.Off)) _keyId = "";
        _secret = "";
        Draw();
        await ReadSecretAsync();
    }

    private GatewayJson Gateway => _providers.Gateway;

    private void Draw()
    {
        _drawing = true;
        try
        {
            var g = Gateway;
            Share.IsOn = g.Lan;
            var shown = g.PublicUrl.Length > 0 ? g.PublicUrl : g.Url;
            HeaderUrl.Text = shown;
            if (_address.Length == 0 || !Addresses().Contains(_address)) _address = shown;
            DrawState(g);
            DrawConnect();
            DrawKeys();
            DrawCalls();
        }
        finally { _drawing = false; }
    }

    /// <summary>The card: running here, served by an older magpie, or not
    /// running, in the page's own words.</summary>
    private void DrawState(GatewayJson g)
    {
        var older = g.Running && !g.Mine && g.Older;
        DotRunning.Visibility = g.Running && !older ? Visibility.Visible : Visibility.Collapsed;
        DotOlder.Visibility = older ? Visibility.Visible : Visibility.Collapsed;
        DotOff.Visibility = !g.Running ? Visibility.Visible : Visibility.Collapsed;
        StateWord.Text = older
            ? $"running · served by magpie {g.Version}, older than this one"
            : g.Running ? (g.Mine ? "running" : "running · served by another magpie") : "not running";
        StateLine.Text = older
            ? "Agents' requests go through that magpie and are sent as it sends them, without this version's fixes."
            : g.Running
                ? $"{g.Models} models · five APIs, one URL"
                : "Start it with magpie serve, or open magpie at login.";
    }

    // ---- Connect ----

    /// <summary>The addresses a client may point at: this computer's, and the
    /// local network's while sharing is on.</summary>
    private List<string> Addresses()
    {
        var all = new List<string> { Gateway.Url };
        all.AddRange(Gateway.LanUrls.Where(u => u.Length > 0 && u != Gateway.Url));
        return all;
    }

    /// <summary>Whether the address picked is one another computer reaches,
    /// where a gateway key is required.</summary>
    private bool Remote => _address != Gateway.Url || Gateway.OnNetwork;

    private string BaseUrlFor(int proto) =>
        proto is 0 or 1 ? _address + "/v1" : _address;

    private void DrawConnect()
    {
        BaseUrl.Text = BaseUrlFor(_proto);
        PickAddress.Visibility = Addresses().Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        // the key's own name when one is picked; this computer trusts every
        // key on loopback, so no key is needed there
        var key = _keys.FirstOrDefault(k => k.Id == _keyId);
        var keyShown = key?.Name ?? (Remote ? "Choose a gateway key" : "This computer");
        KeyShown.Text = _secret.Length > 0 && Reveal.IsChecked != true ? Mask(_secret) : keyShown;
        PickKey.Visibility = Gateway.Lan ? Visibility.Visible : Visibility.Collapsed;
        SnippetNote.Text = _proto switch
        {
            0 => "Chat Completions, the API most tools speak.",
            1 => "OpenAI's newer API: reasoning and built-in tools. Codex speaks this.",
            2 => "The Messages API. Claude Code reads ANTHROPIC_AUTH_TOKEN instead of the key.",
            _ => "Google's generateContent API, v1beta. Gemini CLI speaks this.",
        };
        Snippet.Text = SnippetFor(_proto);
    }

    /// <summary>The masked secret: its first and last few characters, as the
    /// page's own key list shows one.</summary>
    private static string Mask(string secret) =>
        secret.Length <= 10 ? new string('•', secret.Length) : secret[..6] + new string('•', 8) + secret[^4..];

    private string SnippetFor(int proto)
    {
        var secret = _secret.Length > 0 ? _secret : Remote ? "" : "magpie";
        var model = ExampleModel();
        var b = BaseUrlFor(proto);
        if (secret.Length == 0) return "Choose a gateway key to connect.";
        // a request in PowerShell's own words: the curl.exe Windows ships,
        // with the body in single quotes so its JSON needs no escaping
        return proto switch
        {
            0 => $$"""
curl.exe "{{b}}/chat/completions" `
  -H "Authorization: Bearer {{secret}}" `
  -H "Content-Type: application/json" `
  -d '{"model":"{{model}}","messages":[{"role":"user","content":"hi"}]}'
""",
            1 => $$"""
curl.exe "{{b}}/responses" `
  -H "Authorization: Bearer {{secret}}" `
  -H "Content-Type: application/json" `
  -d '{"model":"{{model}}","input":"hi"}'
""",
            2 => $$"""
curl.exe "{{b}}/v1/messages" `
  -H "x-api-key: {{secret}}" `
  -H "anthropic-version: 2023-06-01" `
  -H "Content-Type: application/json" `
  -d '{"model":"{{model}}","max_tokens":1024,"messages":[{"role":"user","content":"hi"}]}'
""",
            _ => $$"""
curl.exe "{{b}}/v1beta/models/{{model}}:generateContent" `
  -H "x-goog-api-key: {{secret}}" `
  -H "Content-Type: application/json" `
  -d '{"contents":[{"parts":[{"text":"hi"}]}]}'
""",
        };
    }

    /// <summary>The first model an agent may use, as the snippets name it, or
    /// a placeholder while none is exposed.</summary>
    private string ExampleModel()
    {
        foreach (var p in _providers.Providers)
        {
            if (p.Off) continue;
            var m = p.Models.FirstOrDefault(m => m.On);
            if (m is not null) return p.Id + "/" + m.Id;
        }
        return "provider/model";
    }

    private void Proto_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
    {
        if (sender.SelectedItem is not { } item) return;
        _proto = sender.Items.IndexOf(item);
        KeepPick("proto", _proto.ToString());
        if (_drawing) return;
        DrawConnect();
    }

    private void PickAddress_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        foreach (var u in Addresses())
        {
            var item = new MenuFlyoutItem { Text = u, Tag = u };
            if (u == _address) item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            item.Click += (_, _) => { _address = u; KeepPick("address", u); DrawConnect(); };
            menu.Items.Add(item);
        }
        menu.ShowAt(PickAddress);
    }

    private void PickKey_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        if (!Remote)
        {
            var here = new MenuFlyoutItem { Text = "This computer" };
            if (_keyId.Length == 0) here.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            here.Click += (_, _) => { _keyId = ""; _secret = ""; KeepPick("key", ""); DrawConnect(); };
            menu.Items.Add(here);
            menu.Items.Add(new MenuFlyoutSeparator());
        }
        foreach (var k in _keys.Where(k => !k.Off))
        {
            var item = new MenuFlyoutItem { Text = k.Name, Tag = k.Id };
            if (k.Id == _keyId) item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            item.Click += (_, _) => _ = PickKeyAsync(k.Id);
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuFlyoutItem { Text = "Create a gateway key first", IsEnabled = false });
        menu.ShowAt(PickKey);
    }

    private async Task PickKeyAsync(string id)
    {
        _keyId = id;
        _secret = "";
        KeepPick("key", id);
        DrawConnect();
        await ReadSecretAsync();
    }

    /// <summary>Reads the picked key's secret to put in the examples; the
    /// list itself never carries one (POST /api/caller-keys/copy-key).</summary>
    private async Task ReadSecretAsync()
    {
        if (_keyId.Length == 0)
        {
            // loopback: the gateway trusts every key, so the examples show
            // the page's own word for one
            _secret = "magpie";
            DrawConnect();
            return;
        }
        var id = _keyId;
        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<CallerKeysJson>("caller-keys/copy-key", new { key = id });
            if (_keyId != id) return;
            _secret = answer?.Secret ?? "";
            DrawConnect();
        }, Status);
    }

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (Reveal.IsChecked == true) KeyShown.Text = _secret.Length > 0 ? _secret : "(choose a key)";
        else DrawConnect();
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e) => CopyText(HeaderUrl.Text);
    private void CopyBase_Click(object sender, RoutedEventArgs e) => CopyText(BaseUrl.Text);
    private void CopyKey_Click(object sender, RoutedEventArgs e) => CopyText(_secret);
    private void CopySnippet_Click(object sender, RoutedEventArgs e) => CopyText(Snippet.Text);

    // ---- Caller keys ----

    private void DrawKeys()
    {
        KeysNote.Text = Gateway.Lan
            ? "Create a gateway key for each client. Usage is counted separately for each key."
            : "Gateway keys are for other computers, and for a tunnel to this one: turn on Share on local network first. Agents on this computer need none.";
        AddKey.Visibility = Gateway.Lan ? Visibility.Visible : Visibility.Collapsed;
        KeyList.Visibility = Gateway.Lan ? Visibility.Visible : Visibility.Collapsed;
        KeysEmpty.Visibility = Gateway.Lan && _keys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        KeyList.ItemsSource = Gateway.Lan ? _keys.Select(KeyRowOf).ToList() : null;
    }

    private CallerKeyRow KeyRowOf(CallerKeyJson k) => new()
    {
        Key = k,
        On = !k.Off,
        Name = k.Name,
        Masked = k.Masked,
        Usage = UsageOf(k),
        UsageVisibility = UsageOf(k).Length > 0 ? Visibility.Visible : Visibility.Collapsed,
    };

    /// <summary>What a key has used of its limits, as its row says it: the
    /// tokens or cost of each, and the window it resets in.</summary>
    private static string UsageOf(CallerKeyJson k)
    {
        var parts = new List<string>();
        foreach (var u in k.UsedAll)
        {
            var one = new List<string>();
            if (u.TokenLimit > 0) one.Add($"{Count(u.Tokens)} / {Count(u.TokenLimit)} tokens");
            if (u.CostLimit > 0) one.Add($"{Usd(u.Cost)} / {Usd(u.CostLimit)}");
            if (one.Count == 0) continue;
            one.Add(Window(u));
            if (u.Spent) one.Insert(0, "Spent");
            parts.Add(string.Join(" · ", one));
        }
        return string.Join("\n", parts);
    }

    private static string Window(KeyUsageJson u) => u.Period switch
    {
        "days" => $"every {u.Days} days",
        "hours" => $"every {u.Hours} hours",
        "day" => "per day",
        "week" => "per week",
        "month" => "per month",
        _ => u.Period,
    };

    private static string Count(long n) => n >= 1_000_000 ? (n / 1_000_000.0).ToString("0.#") + "M"
        : n >= 1_000 ? (n / 1_000.0).ToString("0.#") + "K" : n.ToString();

    private static string Usd(double n) => "$" + (n >= 100 ? n.ToString("0") : n.ToString("0.00"));

    private async void Key_Toggled(object sender, RoutedEventArgs e)
    {
        if (_drawing) return;
        if (sender is not ToggleSwitch { Tag: CallerKeyRow row } toggle) return;
        await KeyActionAsync(toggle.IsOn ? "on-key" : "off-key", new { key = row.Key.Id });
    }

    private async void KeyCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CallerKeyRow row }) return;
        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<CallerKeysJson>("caller-keys/copy-key", new { key = row.Key.Id });
            if (answer is not null) CopyText(answer.Secret);
        }, Status);
    }

    private void KeyMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CallerKeyRow row } button) return;
        var menu = new MenuFlyout();
        var rename = new MenuFlyoutItem { Text = "Rename" };
        rename.Click += (_, _) => _ = RenameAsync(row);
        var rotate = new MenuFlyoutItem { Text = "Rotate key" };
        rotate.Click += (_, _) => _ = AskAsync(row, rotate: true);
        var remove = new MenuFlyoutItem { Text = "Remove" };
        remove.Click += (_, _) => _ = AskAsync(row, rotate: false);
        menu.Items.Add(rename);
        menu.Items.Add(rotate);
        menu.Items.Add(remove);
        menu.ShowAt(button);
    }

    private async Task KeyActionAsync(string action, object body)
    {
        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<CallerKeysJson>("caller-keys/" + action, body);
            if (answer is null) return;
            // the write answers with the whole list, as every write does
            _keys = answer.Keys;
            if (!_keys.Any(k => k.Id == _keyId && !k.Off)) _keyId = "";
            _secret = "";
            DrawKeys();
            DrawConnect();
        }, Status, Busy);
    }

    private async void AddKey_Click(object sender, RoutedEventArgs e)
    {
        var name = new TextBox { Header = "Name", PlaceholderText = "Key name, e.g. Laptop" };
        var own = new PasswordBox { Header = "Your own key (optional)", PlaceholderText = "Leave empty and magpie makes one" };
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(name);
        body.Children.Add(own);
        body.Children.Add(new TextBlock
        {
            Text = "Or enter a key your clients already send, from another gateway, so they keep working.",
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        });
        var dialog = new ContentDialog
        {
            Title = "Add gateway key",
            Content = body,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var sent = own.Password.Length > 0
            ? new { name = name.Text, secret = own.Password }
            : (object)new { name = name.Text };
        await KeyActionAsync("add-key", sent);
    }

    private async Task RenameAsync(CallerKeyRow row)
    {
        var name = new TextBox { Text = row.Key.Name, Header = "Name" };
        var dialog = new ContentDialog
        {
            Title = "Rename gateway key",
            Content = name,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (name.Text.Trim() == row.Key.Name) return;
        await KeyActionAsync("rename-key", new { key = row.Key.Id, name = name.Text });
    }

    /// <summary>A rotate or a remove asks first: either takes the key away
    /// from the clients that send it.</summary>
    private async Task AskAsync(CallerKeyRow row, bool rotate)
    {
        var dialog = new ContentDialog
        {
            Title = rotate ? "Rotate gateway key?" : "Remove gateway key?",
            Content = rotate
                ? $"Clients using {row.Key.Name} will need the new key. Its name and usage history stay the same."
                : $"Clients using {row.Key.Name} will lose access from other computers. Its usage history is kept.",
            PrimaryButtonText = rotate ? "Rotate key" : "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await KeyActionAsync(rotate ? "rotate-key" : "remove-key", new { key = row.Key.Id });
    }

    // ---- Recent calls ----

    private void DrawCalls()
    {
        var calls = Gateway.Calls;
        CallsNote.Text = Gateway.Running && !Gateway.Mine ? "shown by the magpie that serves the gateway" : "";
        CallList.ItemsSource = calls.Select(CallRowOf).ToList();
        CallsEmpty.Visibility = calls.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static CallRow CallRowOf(GatewayCallJson c) => new()
    {
        Call = c,
        Time = c.Time.ToLocalTime().ToString("HH:mm:ss"),
        Agent = c.Agent.Length > 0 ? c.Agent : "—",
        Model = c.Model,
        Provider = c.From == c.To || c.To.Length == 0 ? c.Provider : $"{c.Provider} · {c.From} → {c.To}",
        Result = c.Error.Length > 0 ? $"{c.Status} {c.Error}" : $"{c.Status} · {c.Ms} ms" + (c.Ttft > 0 ? $" · TTFT {c.Ttft} ms" : ""),
    };

    /// <summary>A call's bodies are read when its fold opens, not with every
    /// refresh: forty calls' bodies made the whole state megabytes.</summary>
    private async void Call_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.Tag is not CallRow row || sender.Content is not null) return;
        var busy = new TextBlock { Text = "Fetching…", Opacity = 0.7, FontSize = 12 };
        sender.Content = busy;
        try
        {
            var body = await App.CurrentApp.Client.GetAsync<GatewayCallBodyJson>(
                "gateway/call?id=" + row.Call.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sender.Content = BodiesOf(body);
        }
        catch (Exception e)
        {
            var failed = new StackPanel { Spacing = 6 };
            failed.Children.Add(new TextBlock { Text = e.Message, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            var retry = new Button { Content = "Try again", Padding = new Thickness(8, 2, 8, 2) };
            retry.Click += (_, _) => { sender.Content = null; Call_Expanding(sender, args); };
            failed.Children.Add(retry);
            sender.Content = failed;
        }
    }

    private static StackPanel BodiesOf(GatewayCallBodyJson? body)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(BodyBox("Request body", body?.RequestBody ?? "", body?.RequestTruncated ?? false));
        panel.Children.Add(BodyBox("Response body", body?.ResponseBody ?? "", body?.ResponseTruncated ?? false));
        return panel;
    }

    private static StackPanel BodyBox(string title, string text, bool truncated)
    {
        var box = new StackPanel { Spacing = 4 };
        box.Children.Add(new TextBlock { Text = title + (truncated ? " (cut short)" : ""), FontSize = 12, Opacity = 0.7 });
        box.Children.Add(new TextBox
        {
            Text = text.Length > 0 ? text : "(empty)",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 12,
            MinHeight = 80,
            MaxHeight = 320,
        });
        return box;
    }

    // ---- Sharing ----

    /// <summary>Settings' Share on local network, from the same switch's
    /// write: magpie answers with the new settings, and the page is drawn
    /// from what it wrote.</summary>
    private async void Share_Toggled(object sender, RoutedEventArgs e)
    {
        if (_drawing) return;
        var on = Share.IsOn;
        await PageKit.RunAsync(async () =>
        {
            var s = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/lan", new { on });
            if (s is not null && (s.Notice ?? s.Rekeyed) is { Length: > 0 } notice)
            {
                Notice.Title = notice;
                Notice.Severity = InfoBarSeverity.Warning;
                Notice.IsOpen = true;
            }
            await ReadAsync();
        }, Status, Busy);
    }

    private void CopyText(string text)
    {
        if (text.Length == 0) return;
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        Status.Text = "Copied.";
    }
}

/// <summary>One caller key as the list draws it.</summary>
public sealed class CallerKeyRow
{
    public CallerKeyJson Key { get; set; } = new();
    public bool On { get; set; }
    public string Name { get; set; } = "";
    public string Masked { get; set; } = "";
    public string Usage { get; set; } = "";
    public Visibility UsageVisibility { get; set; } = Visibility.Collapsed;
}

/// <summary>One recent call as the list draws it.</summary>
public sealed class CallRow
{
    public GatewayCallJson Call { get; set; } = new();
    public string Time { get; set; } = "";
    public string Agent { get; set; } = "";
    public string Model { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Result { get; set; } = "";
}
