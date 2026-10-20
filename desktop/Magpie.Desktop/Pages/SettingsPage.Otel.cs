using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop.Pages;

public sealed partial class SettingsPage
{
    /// <summary>
    /// The Observability (OTel) part of Settings, as the page has it
    /// (app.js renderOTel): the collector's endpoint and headers, and what
    /// of the calls goes out with the traces. The whole `otel` object is
    /// sent at every change (a save of one field keeps the others), through
    /// the bulk route; there is no otel route of its own (settings.save
    /// validates it: the endpoint, and that headers aren't Content-Type
    /// etc.). When MAGPIE_OTEL_* is set, a line says the environment wins.
    /// </summary>
    private void DrawOtel()
    {
        var s = _settings!;
        var otel = s.Otel;

        Body.Children.Add(PageKitUI.Heading("Collector connection"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "OTLP export",
            Description = "Send traces to your collector. Choose what to include below; account credentials stay local",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], otel.Enabled ? "on" : "off",
                v => _ = SaveOtelAsync(patch(o => o.Enabled = v == "on"))),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "OTLP endpoint",
            Description = "Base URL of your collector, or Langfuse's /api/public/otel endpoint",
            Control = PageKitUI.TextField(otel.Endpoint, "http://localhost:4318",
                v => _ = SaveOtelAsync(patch(o => o.Endpoint = v.TrimEnd('/'))), applyLabel: "Save"),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "OTLP headers",
            Description = "Comma-separated name=value pairs; percent-encode spaces and commas in values",
            Control = PageKitUI.TextField(HeadersText(otel.Headers), "Authorization=Bearer%20token",
                v => _ = SaveOtelAsync(patch(o => o.Headers = ParseHeaders(v))), password: true, applyLabel: "Save"),
        }));

        Body.Children.Add(PageKitUI.Heading("Export scope"));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Export metrics",
            Description = "Also send duration and token histograms. Leave off for a traces-only service such as Langfuse",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], otel.Metrics ? "on" : "off",
                v => _ = SaveOtelAsync(patch(o => o.Metrics = v == "on"))),
        }));
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Trace agent conversations",
            Description = "Reads all local sessions from Codex, Pi, Oh My Pi, Claude Code, Cowork, OpenCode and Gemini CLI, including calls not routed through Magpie. When content is included below, exports prompts, replies, tool arguments and tool output (including file contents and command output), with secrets masked",
            Control = PageKitUI.Choices([("off", "Off"), ("on", "On")], otel.Sessions ? "on" : "off",
                v => _ = SaveOtelAsync(patch(o => o.Sessions = v == "on"))),
        }));

        Body.Children.Add(PageKitUI.Heading("Trace content"));
        var mode = !otel.Bodies ? "metadata" : otel.BodiesWhole ? "full" : "limited";
        var description = mode switch
        {
            "metadata" => "Send model, token, status and timing metadata only. Prompts, replies and tool content stay local",
            "limited" => "Include requests and replies as trace input and output, with secrets masked. Each body is limited to 256 KB",
            _ => "Include complete requests and replies, with secrets masked. Large content may be refused by your collector",
        };
        Body.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Request and response content",
            Description = description,
            Control = PageKitUI.Choices([("metadata", "Metadata only"), ("limited", "Up to 256 KB"), ("full", "Full content")], mode,
                v => _ = SaveOtelAsync(patch(o => { o.Bodies = v != "metadata"; o.BodiesWhole = v == "full"; }))),
        }));

        if (s.OtelEnv)
        {
            Body.Children.Add(new TextBlock
            {
                Text = "Environment variables override these saved OTLP preferences",
                FontSize = 12,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 8, 2, 0),
            });
        }
    }

    /// <summary>Saves the otel object `next` as the one changed field, on the
    /// bulk route with the settings snapshot beside it (a save of one field
    /// keeps the others). The answer redraws the page.</summary>
    private async Task SaveOtelAsync(OTelJson next)
    {
        if (_busy) return;
        if (!await SaveWithRedrawAsync("settings", next)) Draw();
    }

    /// <summary>A copy of the current otel with one field changed, so a save
    /// of one field keeps the rest as the user left them.</summary>
    private OTelJson patch(Action<OTelJson> set)
    {
        var o = Clone(_settings!.Otel);
        set(o);
        return o;
    }

    private static OTelJson Clone(OTelJson o) => new()
    {
        Enabled = o.Enabled,
        Endpoint = o.Endpoint,
        Headers = new Dictionary<string, string>(o.Headers),
        Metrics = o.Metrics,
        Sessions = o.Sessions,
        Bodies = o.Bodies,
        BodiesWhole = o.BodiesWhole,
    };

    /// <summary>The saved headers as the page shows them, or '' when none.</summary>
    private static string HeadersText(Dictionary<string, string> headers) =>
        string.Join(",", headers.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

    /// <summary>Comma-separated name=value pairs back into the map the
    /// settings read; a value that can't be decoded is kept as typed.</summary>
    private static Dictionary<string, string> ParseHeaders(string text)
    {
        var out_ = new Dictionary<string, string>();
        foreach (var part in text.Split(','))
        {
            var p = part.Trim();
            if (p.Length == 0) continue;
            var i = p.IndexOf('=');
            if (i < 1) continue;
            var k = p[..i].Trim();
            if (k.Length == 0) continue;
            var v = p[(i + 1)..].Trim();
            try { v = Uri.UnescapeDataString(v); } catch (Exception) { /* kept as typed */ }
            out_[k] = v;
        }
        return out_;
    }
}