using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop.Pages;

public sealed partial class SettingsPage
{
    /// <summary>
    /// The Models part of Settings, as the page has it (app.js renderImages,
    /// renderImageGen, renderSearcher, renderSearch, renderReplies,
    /// renderDisabledModels): which models see images, draw them and search
    /// the web, the search APIs, and the models turned off everywhere.
    /// </summary>
    private void DrawModels()
    {
        var s = _settings!;

        Body.Children.Add(PageKitUI.Heading("Seeing and drawing"));
        Body.Children.Add(ImageRecognitionRow(s));
        Body.Children.Add(ImageGenRow(s));

        Body.Children.Add(PageKitUI.Heading("Search"));
        Body.Children.Add(SearcherRow(s));
        Body.Children.Add(SearchApisRow(s));

        Body.Children.Add(PageKitUI.Heading("Replies"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Name the member in replies",
            Description = "A reply's model says magpie's provider/model that answered (workbuddy/glm-5.3-flash), not the vendor's own name, for agents that count usage by it. Claude Code, Claude Desktop and Codex keep the vendor's; the X-Magpie-Model header says it to every agent",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.MemberModel ? "on" : "off",
                v => _ = SaveAsync(new { memberModel = v == "on" })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Stop looping replies",
            Description = "A reply whose reasoning or text goes round the same few lines is ended with an error the agent can retry, not left to run to its output limit",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.NoLoopGuard ? "off" : "on",
                v => _ = SaveAsync(new { noLoopGuard = v == "off" })),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Ask again after a refusal",
            Description = "When a vendor's safety filter refuses a request before anything is said, how many more accounts or routing group members are asked. Never ask again hands the refusal straight back: Claude Code then switches to its own fallback model, as it does on Anthropic's API",
            Control = RefusalRetriesControl(s),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Short Claude system prompt",
            Description = "A Claude subscription answers with the agent's own instructions as its system prompt, in place of Claude Code's, about 1K fewer tokens each request. Still the real Claude Code, but its requests look less like Claude Code's own, which can put the account more at risk",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], s.ClaudeLeanPrompt ? "on" : "off",
                v => _ = SaveAsync(new { claudeLeanPrompt = v == "on" })),
        }));

        Body.Children.Add(PageKitUI.Heading("Turned off"));
        Body.Children.Add(DisabledModelsRow(s));
    }

    // ---- Image recognition ----

    private FrameworkElement ImageRecognitionRow(SettingsFullJson s)
    {
        var models = s.VisionModels ?? [];
        var v = s.Vision ?? "";
        var description = v == "off"
            ? "A model that can't see images is sent none: a request with one in its latest message is turned away"
            : "When the model in use can't see images, this one describes them to it, once for each image";
        var picked = v switch
        {
            "" => s.VisionAuto is { Length: > 0 } auto ? "Automatic · " + NameOf(models, auto) : "Automatic",
            "off" => "Off",
            _ when v == s.VisionMissing => v + " · missing",
            _ => NameOf(models, v),
        };
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Image recognition",
            Description = description,
            Control = PageKitUI.Menu(VisionOptions(models, s.VisionAuto, v), v, pick => _ = SaveAsync(new { vision = pick })),
        }));
        if (v != "off")
        {
            card.Children.Add(EffortRow("the least it has", s.VisionEfforts ?? [], s.VisionEffort ?? "", s.VisionEffortNow,
                e => _ = SaveAsync(new { visionEffort = e })));
        }
        if (v is { Length: > 0 } g && g == s.VisionMissing)
        {
            card.Children.Add(ProblemLine(s.VisionAuto is { Length: > 0 } auto
                ? $"{g}, picked here, isn't set up any more: its provider was removed or turned off, or no longer has it. {NameOf(models, auto)}, the automatic choice, describes images in its place. Pick another model here."
                : $"{g}, picked here, isn't set up any more: its provider was removed or turned off, or no longer has it. No other model sees, so images are turned away. Pick another model here."));
        }
        return card;
    }

    private FrameworkElement ImageGenRow(SettingsFullJson s)
    {
        var models = s.ImageGenModels ?? [];
        var v = s.ImageGen ?? "";
        var shown = s.ImageGenShown is { Length: > 0 } sv ? sv : v;
        var description = v == "off"
            ? "Agents given Magpie Image can't generate images or videos: the tool says it is off"
            : "The model Magpie Image draws with. Agents get the Magpie Image tool from the Library; images are saved in their project";
        var picked = v switch
        {
            "" => s.ImageGenAuto is { Length: > 0 } auto ? "Automatic · " + NameOf(models, auto) : "Automatic",
            "off" => "Off",
            _ when v == s.ImageGenMissing => v + " · missing",
            _ => NameOf(models, shown),
        };
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Image generation",
            Description = description,
            Control = PageKitUI.Menu(ImageGenOptions(models, s.ImageGenAuto), shown, pick => _ = SaveAsync(new { imageGen = pick })),
        }));
        if (models.Any(m => m.Id == (v == "off" || v == s.ImageGenMissing ? "" : shown) && m.VendorPicks))
        {
            card.Children.Add(ProblemLine("ChatGPT's image backend picks the model itself, whichever is named, so one GPT Image is listed", bad: false));
        }
        if (v is { Length: > 0 } g && g == s.ImageGenMissing)
        {
            card.Children.Add(ProblemLine(s.ImageGenAuto is { Length: > 0 } auto
                ? $"{g}, picked here, isn't set up any more: its provider was removed or turned off, or no longer has it. {NameOf(models, auto)}, the automatic choice, draws in its place. Pick another model here."
                : $"{g}, picked here, isn't set up any more: its provider was removed or turned off, or no longer has it. No other model draws, so a request that names no model is turned away. Pick another model here."));
        }
        return card;
    }

    private FrameworkElement SearcherRow(SettingsFullJson s)
    {
        var choices = s.SearchChoices ?? [];
        var v = s.Searcher ?? "";
        var description = v == "off"
            ? "Provider search is off; only these APIs are used"
            : "A model that can't search the web is searched for with these providers, in the order they are checked".Replace("Checked", "checked");
        var card = new StackPanel { Spacing = 8 };
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Searches for other models",
            Description = description,
            Control = PageKitUI.Menu(SearcherOptions(choices, s.SearchAuto), v, pick => _ = SaveAsync(new { searcher = pick })),
        }));
        if (v != "off")
        {
            card.Children.Add(EffortRow("the least it has", s.SearchEfforts ?? [], s.SearcherEffort ?? "", s.SearchEffortNow,
                e => _ = SaveAsync(new { searcherEffort = e })));
        }
        if (s.SearchUnused is { Length: > 0 } unused)
        {
            card.Children.Add(ProblemLine(unused, bad: false));
        }
        return card;
    }

    // ---- Search APIs ----

    private FrameworkElement SearchApisRow(SettingsFullJson s)
    {
        var apis = s.SearchAPIs ?? [];
        var card = new StackPanel { Spacing = 8 };
        var vendors = s.SearchVendors ?? [];
        var dVendor = vendors.Count > 0 ? vendors[0].Id : "";
        if (vendors.Count > 0 && !vendors.Any(x => x.Id == dVendor)) dVendor = "tavily";
        var vendor = vendors.FirstOrDefault(x => x.Id == dVendor);
        var by = s.Searcher == "off" ? "Provider search is off; only these APIs are used"
            : s.SearchProvider is not { Length: > 0 } ? "No provider can search, so these are asked"
            : s.SearchFirst == "api" && apis.Any(a => !a.Off) ? "Asked before " + s.SearchProvider + ", which searches when these fail"
            : "Now done by " + s.SearchProvider + "; these come after it";

        // the add-row: which API, its key, its base URL
        var keyBox = new TextBox { PlaceholderText = "API key", Width = 180, VerticalAlignment = VerticalAlignment.Center };
        var urlTextBox = new TextBox { PlaceholderText = "Base URL", Width = 150, VerticalAlignment = VerticalAlignment.Center };
        var pickVendor = PageKitUI.Menu(vendors.Select(x => (x.Id, x.Name)).ToList(), dVendor, id => { dVendor = id; Draw(); });
        var add = new Button { Content = "Add", VerticalAlignment = VerticalAlignment.Center };
        void SendAdd()
        {
            var url = urlTextBox.Text.Trim();
            var key = keyBox.Text.Trim();
            _ = SaveSearchApiAsync(new { vendor = dVendor, key, url });
            keyBox.Text = "";
            urlTextBox.Text = "";
        }
        add.Click += (_, _) => SendAdd();
        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        addRow.Children.Add(pickVendor);
        addRow.Children.Add(keyBox);
        addRow.Children.Add(urlTextBox);
        addRow.Children.Add(add);
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Search APIs",
            Description = "When a model can't search the web, magpie searches for it with these, in this order, and gives it what they found · " + by,
            Control = addRow,
        }));

        foreach (var (a, n) in apis.Select((x, i) => (x, i)).ToList())
        {
            var was = a;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var sw = PageKitUI.Switch(!a.Off, on => _ = SaveSearchApiAsync(new { vendor = was.Vendor, on }));
            sw.VerticalAlignment = VerticalAlignment.Center;
            var rm = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
            rm.Click += (_, _) => _ = SaveSearchApiAsync(new { vendor = was.Vendor, remove = true });
            buttons.Children.Add(sw);
            buttons.Children.Add(rm);
            var what = new List<string>();
            if (a.Key is { Length: > 0 } key) what.Add(key);
            else what.Add("needs its key");
            if (a.Url is { Length: > 0 } url) what.Add(url);
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = $"{n + 1}. {a.Name}",
                Description = a.Off ? "Off · " + string.Join(" · ", what) : string.Join(" · ", what),
                Control = buttons,
            }));
        }
        if (apis.Count > 1)
        {
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = "Order",
                Description = "The APIs are asked in this order",
                Control = UpDownRow(apis.Count, i => _ = SaveSearchApiAsync(new { arrange = Rearranged(apis, i).Select(x => x.Vendor).ToList() })),
            }));
        }

        // which goes first when both a provider and a search API can search
        if (s.SearchProvider is { Length: > 0 } && apis.Any(a => !a.Off))
        {
            var apiFirst = s.SearchFirst == "api";
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = "Search first with",
                Description = "The other is asked when it fails",
                Control = PageKitUI.Choices([("model", "Model search"), ("api", "Search APIs")], apiFirst ? "api" : "model",
                    v => _ = SaveAsync(new { searchFirst = v == "api" ? "api" : "" })),
            }));
        }
        return card;
    }

    private FrameworkElement UpDownRow(int count, Action<int> move)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (var i = 0; i < count; i++)
        {
            var n = i;
            var b = new Button { Content = $"↑{n + 1}", Padding = new Thickness(8, 2, 8, 2) };
            b.Click += (_, _) => move(n);
            row.Children.Add(b);
        }
        return row;
    }

    private static List<SearchApiJson> Rearranged(List<SearchApiJson> apis, int to)
    {
        // the item at index 0 moved to `to` (an up-move of the first is a
        // no-op that the caller's drawing handles)
        var list = new List<SearchApiJson>(apis);
        if (to <= 0 || to >= list.Count) return list;
        var first = list[0];
        list.RemoveAt(0);
        list.Insert(to - 1, first);
        return list;
    }

    private async Task SaveSearchApiAsync(object body)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/search-api", body) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- Disabled models ----

    private FrameworkElement DisabledModelsRow(SettingsFullJson s)
    {
        var list = s.DisabledModels ?? [];
        var card = new StackPanel { Spacing = 8 };
        var models = (s.TitleModels ?? []).Where(m => m.Provider.Length > 0).ToList();
        var options = models.Select(m => (m.Id, NameOf(models, m.Id))).ToList();
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Disable a model",
            Description = "A disabled model is refused on every route through magpie (a request, a session, a routing group, a fallback, a sub-agent, a decision model) and left out of magpie's lists and agents' pickers. Only requests through magpie are covered. Type a pattern such as *fable* to disable every model it matches",
            Control = PageKitUI.Menu(options, "", pick => _ = SaveDisabledModelsAsync(SaveDisabledModels(list, pick)), "Disable…"),
        }));
        foreach (var pat in list)
        {
            var was = pat;
            var hits = s.DisabledHits is not null && s.DisabledHits.TryGetValue(pat, out var h) ? h : [];
            var desc = hits.Count > 0
                ? "Matches " + hits.Count + ": " + string.Join(", ", hits.Take(4)) + (hits.Count > 4 ? ", …" : "")
                : "Matches no model now; it is refused if one appears";
            var enable = new Button { Content = "Enable", Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
            enable.Click += (_, _) => _ = SaveDisabledModelsAsync((_settings!.DisabledModels ?? []).Where(p => p != was).ToList());
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = pat,
                Description = desc,
                Control = enable,
            }));
        }
        return card;
    }

    private static List<string> SaveDisabledModels(List<string> list, string id)
    {
        id = (id ?? "").Trim();
        if (id.Length == 0) return list;
        if (list.Any(p => p.Equals(id, StringComparison.OrdinalIgnoreCase))) return list;
        return [.. list, id];
    }

    private async Task SaveDisabledModelsAsync(List<string> models)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _settings = await App.CurrentApp.Client.PostAsync<SettingsFullJson>("settings/disabled-models", new { models }) ?? _settings;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    // ---- small pieces ----

    /// <summary>A model's picker options: Automatic and Off at the top, then
    /// each model, named with its provider.</summary>
    private static List<(string, string)> VisionOptions(List<ModelRefJson> models, string? auto, string picked)
    {
        var options = new List<(string, string)> { ("", auto is { Length: > 0 } a ? "Automatic · " + NameOf(models, a) : "Automatic"), ("off", "Off") };
        options.AddRange(models.Select(m => (m.Id, NameOf(models, m.Id))));
        return options;
    }

    private static List<(string, string)> ImageGenOptions(List<ModelRefJson> models, string? auto)
    {
        var options = new List<(string, string)> { ("", auto is { Length: > 0 } a ? "Automatic · " + NameOf(models, a) : "Automatic"), ("off", "Off") };
        options.AddRange(models.Select(m => (m.Id, m.VendorPicks ? m.Name + " · ChatGPT picks the model" : NameOf(models, m.Id))));
        return options;
    }

    private static List<(string, string)> SearcherOptions(List<SearchChoiceJson> choices, string? auto)
    {
        var options = new List<(string, string)> { ("", auto is { Length: > 0 } a ? "Automatic · " + a : "Automatic"), ("off", "Off") };
        foreach (var c in choices)
        {
            // its small model, or the provider's name when it has no model
            var label = c.SmallName is { Length: > 0 } sn ? sn : c.Name;
            options.Add((c.Id, c.Name + " · " + label));
            foreach (var m in c.Models ?? [])
            {
                if (c.Small is { Length: > 0 } sm && m.Id == c.Id + "/" + sm) continue;
                options.Add((m.Id, NameOf(c.Models ?? [], m.Id)));
            }
        }
        return options;
    }

    private static string NameOf(List<ModelRefJson> models, string id)
    {
        var m = models.FirstOrDefault(x => x.Id == id);
        return m is not null ? (m.Name.Length > 0 ? m.Name : m.Id) + " · " + m.ProviderName : id;
    }

    private static FrameworkElement EffortRow(string note, List<string> efforts, string picked, string? current, Action<string> save)
    {
        // a thinking level of the model now: "the least it has" when none is
        // picked, else the picked one; the picker lists the levels
        var options = new List<(string, string)> { ("", current is { Length: > 0 } c ? c : note) };
        options.AddRange(efforts.Select(e => (e, e switch
        {
            "low" => "Low",
            "medium" => "Medium",
            "high" => "High",
            _ => e,
        })));
        return PageKitUI.Setting(new SettingRow
        {
            Title = "Effort",
            Description = "The reasoning level the pick is asked at",
            Control = PageKitUI.Menu(options, picked, save),
        });
    }

    private static FrameworkElement ProblemLine(string text, bool bad = true)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity = bad ? 0.9 : 0.7,
            Margin = new Thickness(2, 0, 2, 6),
        };
        if (bad) t.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
        return t;
    }

    private FrameworkElement RefusalRetriesControl(SettingsFullJson s)
    {
        var counts = new List<(string, string)> { ("0", "Never ask again"), ("1", "1"), ("2", "2"), ("3", "3") };
        var n = s.RefusalRetries is { } r && r >= 0 ? r.ToString() : "";
        if (n.Length > 0 && !counts.Any(c => c.Item1 == n)) counts.Add((n, n));
        counts.Add(("", "All"));
        return PageKitUI.Choices(counts, n, v => _ = SaveAsync(new { refusalRetries = v.Length == 0 ? (int?)null : int.Parse(v) }));
    }
}