using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Providers page's sign-in part: a subscription's sign-in run in a
/// dialog whose content is redrawn as the state changes, a plugin's sign-in
/// (its way to sign in, its questions, its key), and accounts brought in
/// from another tool's export. The web page draws these in the providers
/// list itself (app.js's startSignIn, followSignIn, renderSigning,
/// startPluginSignIn, startImport); here they are a dialog over the page,
/// on the same endpoints: POST /api/signin, GET /api/signin/{id} polled,
/// POST /api/signin/{id}/cancel and /callback, POST /api/plugin-signin and
/// /api/plugin-signin/prompt, POST /api/signin/import.
///
/// One flow runs at a time. The dialog's Closing is the cancel: it stops
/// the poll loop (<see cref="_signingOpen"/>) and, while a sign-in is
/// live, tells magpie to cancel it. Every step repaints the one panel.
/// </summary>
public sealed partial class ProvidersPage
{
    /// <summary>The sign-in under way, as the last read or write left it.</summary>
    private SignInStateJson? _signing;

    /// <summary>The sign-in dialog, while one is open.</summary>
    private ContentDialog? _signingDialog;

    /// <summary>The dialog's content, cleared and redrawn at each step.</summary>
    private StackPanel? _signingPanel;

    /// <summary>Whether the dialog is open and a flow is live; the poll loop
    /// and every awaited step stop when the dialog's Closing clears it.</summary>
    private bool _signingOpen;

    /// <summary>Whether the vendor's page was opened in the browser already:
    /// a poll must not open it again.</summary>
    private bool _urlOpened;

    /// <summary>Why the browser couldn't be opened, said where "opened the
    /// sign-in page" was (#2043), from POST /api/signin's answer.</summary>
    private string? _openError;

    /// <summary>What the flow is, for the dialog's title and Try again:
    /// a built-in's agent id, or a plugin's sub.</summary>
    private string? _signingAgent;
    private PluginSubJson? _signingSub;
    private int _signingMethod;
    private Dictionary<string, string> _signingInputs = new();
    private string? _signingSite;
    private string? _signingUser;

    /// <summary>An agent's name in English, for the dialog's title and the
    /// browser sentence: the web page's subOf(agent).name (app.js's subs).</summary>
    private static readonly Dictionary<string, string> SubNames = new()
    {
        ["claude"] = "Claude",
        ["codex"] = "ChatGPT",
        ["chatgpt-api"] = "ChatGPT API",
        ["cursor"] = "Cursor",
        ["grok"] = "Grok (SuperGrok)",
        ["copilot"] = "Copilot",
        ["zcode"] = "ZCode (GLM Coding Plan)",
        ["workbuddy"] = "WorkBuddy (CodeBuddy)",
        ["workbuddy-ai"] = "WorkBuddy AI (international)",
        ["commandcode-plan"] = "Command Code",
        ["qoder"] = "Qoder (international)",
        ["qoder-cn"] = "Qoder CN",
        ["devin"] = "Devin",
        ["zed"] = "Zed",
        ["factory"] = "Factory",
        ["mimo-app"] = "Xiaomi MiMo",
        ["kiro"] = "Kiro",
        ["gemini"] = "Gemini CLI",
        ["antigravity"] = "Antigravity",
    };

    private static string SubName(string agent) =>
        SubNames.TryGetValue(agent, out var name) ? name : agent;

    // ---- entry points ----------------------------------------------------

    /// <summary>Adds a subscription's account: its sign-in, from the Add
    /// sheet's tile. The whole flow runs in a dialog this page draws.</summary>
    protected internal Task StartSignInFor(string agent) => RunSignInAsync(agent, null, null);

    /// <summary>The sign-in's real flow: the vendor's page in the browser,
    /// followed by a poll until the account is in, failed or canceled.</summary>
    private protected async Task RunSignInAsync(string agent, string? site, string? user)
    {
        // one flow at a time: an open one is told to cancel, as the web
        // page's startSignIn does
        if (_signingOpen) CloseSignInDialog();

        _signingAgent = agent;
        _signingSub = null;
        _signingMethod = 0;
        _signingInputs = new Dictionary<string, string>();
        _signingSite = site;
        _signingUser = user;
        _promptError = null;
        _openError = null;
        _signing = new SignInStateJson { Agent = agent, State = "starting" };
        OpenSignInDialog(SubName(agent));
        PaintSignIn();

        try
        {
            var body = new Dictionary<string, object?> { ["agent"] = agent };
            if (site is { Length: > 0 }) body["site"] = site;
            if (user is { Length: > 0 }) body["user"] = user;
            var opened = await App.CurrentApp.Client
                .PostAsync<SignInOpenedJson>("signin", body) ?? new SignInOpenedJson();
            if (!_signingOpen) return; // the dialog was closed while starting
            _openError = opened.OpenError;
            _signing = opened;
            if (opened.Url is { Length: > 0 }) await OpenSignInUrlAsync(opened.Url);
            PaintSignIn();
            if (opened.State is "waiting" or "installing") await FollowSignInAsync(opened.Id);
            else if (opened.State is "done") await SignedInAsync(opened);
            else PaintSignIn(); // failed at once: the reason, with Try again
        }
        catch (Exception e)
        {
            _signing = new SignInStateJson { Agent = agent, State = "failed", Error = e.Message };
            PaintSignIn();
        }
    }

    /// <summary>Adds a plugin provider's account: the way to sign in (a
    /// chooser when the plugin offers more than one), then the browser or a
    /// key, from the Add sheet's plugin tile.</summary>
    protected internal Task StartPluginSignInFor(PluginSubJson sub) => RunPluginSignInAsync(sub);

    /// <summary>The plugin sign-in's real flow, as app.js's
    /// startPluginSignIn runs it.</summary>
    private protected async Task RunPluginSignInAsync(PluginSubJson sub)
    {
        if (_signingOpen) CloseSignInDialog();
        var methods = sub.Methods ?? [];
        if (methods.Count == 0)
        {
            Status.Text = L.T("The plugin has no way to sign in to {name}").Replace("{name}", sub.Name);
            return;
        }
        _signingSub = sub;
        _signingAgent = null;
        _signingInputs = new Dictionary<string, string>();
        _openError = null;
        if (methods.Count > 1)
        {
            OpenSignInDialog(SubName(sub.Id));
            PaintMethodChooser(sub);
            return;
        }
        await RunPluginMethodAsync(sub, 0);
    }

    /// <summary>One way of a plugin's sign-in: its questions, then the
    /// browser (and a poll) or, for an "api" way, the key.</summary>
    private protected async Task RunPluginMethodAsync(PluginSubJson sub, int method)
    {
        _signingMethod = method;
        _signing = new SignInStateJson { Agent = sub.Id, State = "starting" };
        _promptError = null;
        OpenSignInDialog(SubName(sub.Id));
        PaintSignIn();
        try
        {
            var ask = await App.CurrentApp.Client.PostAsync<PluginPromptAnswerJson>(
                "plugin-signin/prompt", new { provider = sub.Pid, method, inputs = _signingInputs })
                ?? new PluginPromptAnswerJson();
            if (!_signingOpen) return;
            if (ask.Prompt is { } q)
            {
                _signing = new SignInStateJson { Agent = sub.Id, State = "prompt" };
                _lastPrompt = q;
                PaintPrompt(sub, q);
                return;
            }
            var type = (sub.Methods ?? [])[method].Type;
            if (type == "api")
            {
                _signing = new SignInStateJson { Agent = sub.Id, State = "key" };
                PaintKeyField(sub, (sub.Methods ?? [])[method]);
                return;
            }
            var opened = await App.CurrentApp.Client.PostAsync<SignInOpenedJson>("plugin-signin",
                new { provider = sub.Pid, method, inputs = _signingInputs }) ?? new SignInOpenedJson();
            if (!_signingOpen) return;
            _openError = opened.OpenError;
            _signing = opened;
            if (opened.Url is { Length: > 0 }) await OpenSignInUrlAsync(opened.Url);
            PaintSignIn();
            if (opened.State is "waiting" or "installing") await FollowSignInAsync(opened.Id);
            else if (opened.State is "done") await SignedInAsync(opened);
            else PaintSignIn();
        }
        catch (Exception e)
        {
            _signing = new SignInStateJson { Agent = sub.Id, State = "failed", Error = e.Message };
            PaintSignIn();
        }
    }

    /// <summary>The question a plugin's way asks before the browser or the
    /// key, kept for the answer's key.</summary>
    private PluginPromptJson? _lastPrompt;

    /// <summary>What the plugin said of the last answer, said again over the
    /// question until it is answered right.</summary>
    private string? _promptError;

    /// <summary>One of a plugin way's questions answered, checked by the
    /// plugin before the next is asked, as app.js's pluginAnswer does.</summary>
    private async Task AnswerPluginPromptAsync(PluginSubJson sub, PluginPromptJson q, string value)
    {
        try
        {
            var r = await App.CurrentApp.Client.PostAsync<PluginPromptAnswerJson>("plugin-signin/prompt",
                new { provider = sub.Pid, method = _signingMethod, inputs = _signingInputs, key = q.Key, value })
                ?? new PluginPromptAnswerJson();
            if (!_signingOpen) return;
            if (r.Error is { Length: > 0 })
            {
                PaintPromptError(r.Error);
                return;
            }
            // the plugin checked it: the answer is this way's input now, as
            // the prompt endpoint folds it in on its side
            _signingInputs[q.Key] = value;
            _lastPrompt = null;
            await RunPluginMethodAsync(sub, _signingMethod);
        }
        catch (Exception e)
        {
            PaintPromptError(e.Message);
        }
    }

    /// <summary>An "api" way's key sent in, which the plugin keeps, as
    /// app.js's pluginKey does.</summary>
    private async Task SubmitPluginKeyAsync(PluginSubJson sub, string key)
    {
        try
        {
            var st = await App.CurrentApp.Client.PostAsync<SignInStateJson>("plugin-signin",
                new { provider = sub.Pid, method = _signingMethod, inputs = _signingInputs, key });
            if (!_signingOpen || st is null) return;
            _signing = st;
            await SignedInAsync(st);
        }
        catch (Exception e)
        {
            _signing = new SignInStateJson { Agent = sub.Id, State = "failed", Error = e.Message };
            PaintSignIn();
        }
    }

    /// <summary>One account import: a file (or what is pasted) checked with
    /// the vendor, each account kept or said no, as app.js's runImport does.</summary>
    protected internal Task StartImportFor(string agent) => RunImportAsync(agent);

    /// <summary>The import's real flow: choose a file or paste the export's
    /// text, send it, and draw what became of each account.</summary>
    private protected async Task RunImportAsync(string agent)
    {
        if (_signingOpen) CloseSignInDialog();

        var choose = new Button { Content = L.T("Choose files…") };
        var picked = new TextBlock { Opacity = 0.7, TextWrapping = TextWrapping.Wrap };
        var text = new TextBox
        {
            PlaceholderText = L.T("The pasted text"),
            AcceptsReturn = true,
            Height = 140,
            TextWrapping = TextWrapping.Wrap,
        };
        var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"], TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock
        {
            Text = L.T("Import accounts from a file…"),
            TextWrapping = TextWrapping.Wrap,
        });
        var pickRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        pickRow.Children.Add(choose);
        pickRow.Children.Add(picked);
        body.Children.Add(pickRow);
        body.Children.Add(text);
        body.Children.Add(error);

        string? fileName = null;
        byte[]? fileData = null;
        choose.Click += async (_, _) =>
        {
            try
            {
                var got = await App.CurrentApp.ChooseFileAsync(L.T("Choose files…"), ".json");
                if (got is { } file)
                {
                    fileName = file.Name;
                    fileData = file.Data;
                    picked.Text = file.Name;
                }
            }
            catch (Exception e) { Status.Text = e.Message; }
        };

        var import = new Button
        {
            Content = L.T("Import"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        body.Children.Add(import);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L.T("Import {name} accounts").Replace("{name}", SubName(agent)),
            Content = body,
            CloseButtonText = L.T("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        var done = new TaskCompletionSource();
        dialog.Closed += (_, _) =>
        {
            if (_signingDialog != dialog) return; // a new flow opened over this one
            _signingOpen = false;
            _signingDialog = null;
            _signingPanel = null;
            done.TrySetResult();
        };
        _signingDialog = dialog;
        _signingPanel = body;
        _signingOpen = true;
        _signingAgent = agent;
        _ = dialog.ShowAsync();

        import.Click += async (_, _) =>
        {
            var files = new List<string>();
            if (fileData is { Length: > 0 }) files.Add(System.Text.Encoding.UTF8.GetString(fileData));
            if (text.Text.Trim() is { Length: > 0 } pasted) files.Add(pasted);
            if (files.Count == 0) return;
            import.IsEnabled = false;
            error.Visibility = Visibility.Collapsed;
            try
            {
                var answer = await App.CurrentApp.Client.PostAsync<ImportAnswerJson>(
                    "signin/import", new { agent, files });
                if (!_signingOpen) return;
                await ReloadAsync(answer?.Providers);
                var results = answer?.Results ?? [];
                var added = results.Count(r => r.Status is "added" or "updated");
                var failed = results.Count(r => r.Status == "failed");
                var list = new StackPanel { Spacing = 6 };
                foreach (var r in results)
                {
                    var who = r.User is { Length: > 0 } u ? u
                        : r.File > 0 && fileName is { Length: > 0 } ? fileName : "";
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    row.Children.Add(new TextBlock { Text = who, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                    row.Children.Add(new TextBlock
                    {
                        Text = L.T(r.Status switch
                        {
                            "added" => "Added",
                            "updated" => "Updated with this sign-in",
                            "exists" => "Already in magpie",
                            "failed" => "Not added",
                            _ => r.Status,
                        }),
                        Opacity = 0.75,
                    });
                    if (r.Error is { Length: > 0 }) ToolTipService.SetToolTip(row, r.Error);
                    list.Children.Add(row);
                }
                body.Children.Clear();
                body.Children.Add(new TextBlock
                {
                    Text = L.T("Import finished") + " · " + L.T("{n} added, {m} not added")
                        .Replace("{n}", added.ToString()).Replace("{m}", failed.ToString()),
                });
                body.Children.Add(list);
                var again = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var done2 = new Button { Content = L.T("Done") };
                var more = new Button
                {
                    Content = L.T("Import more…"),
                    Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                };
                done2.Click += (_, _) => dialog.Hide();
                more.Click += (_, _) => { dialog.Hide(); _ = StartImportFor(agent); };
                again.Children.Add(done2);
                again.Children.Add(more);
                body.Children.Add(again);
            }
            catch (Exception e)
            {
                error.Text = e.Message;
                error.Visibility = Visibility.Visible;
                import.IsEnabled = true;
            }
        };
        await done.Task; // Closed cleared the dialog's fields
    }

    // ---- the dialog and the poll ------------------------------------------

    /// <summary>Opens the sign-in dialog: an empty panel its draws fill, a
    /// Cancel button whose click is the flow's cancel. It replaces one
    /// already open (Try again, a plugin's next step), not stacks on it.</summary>
    private void OpenSignInDialog(string title)
    {
        CloseSignInDialog();
        var panel = new StackPanel { Spacing = 10 };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = panel,
            CloseButtonText = L.T("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        _signingPanel = panel;
        _signingDialog = dialog;
        _signingOpen = true;
        _urlOpened = false;
        dialog.Closing += (_, _) =>
        {
            // the dialog's Closing is the cancel: the poll stops, and while
            // a sign-in is live magpie is told, as the web page's
            // cancelSignIn does; errors ignored
            _signingOpen = false;
            if (_signing is { Id: { Length: > 0 } id } && _signing.State is "waiting" or "installing")
                _ = CancelSignInQuietlyAsync(id);
        };
        dialog.Closed += (_, _) =>
        {
            if (_signingDialog != dialog) return; // a new flow opened over this one
            _signingOpen = false;
            _signingDialog = null;
            _signingPanel = null;
        };
        _ = dialog.ShowAsync();
    }

    /// <summary>Tells magpie a sign-in is over: the user closed its dialog.
    /// Nothing is said of a refusal — the sign-in lapses on its own.</summary>
    private async Task CancelSignInQuietlyAsync(string id)
    {
        try { await App.CurrentApp.Client.PostAsync("signin/" + id + "/cancel"); }
        catch (Exception) { /* the sign-in lapses on its own */ }
    }

    /// <summary>Closes the sign-in dialog, whatever stands in it: the start
    /// of a new flow, over the old one.</summary>
    private void CloseSignInDialog()
    {
        _signingOpen = false;
        if (_signingDialog is { } d)
        {
            _signingDialog = null;
            _signingPanel = null;
            d.Hide();
        }
    }

    /// <summary>The vendor's page in the user's browser, once per flow: the
    /// app opens it itself, as the web page's api("open") is a no-op here.</summary>
    private async Task OpenSignInUrlAsync(string url)
    {
        if (_urlOpened) return;
        _urlOpened = true;
        try { await PageKitUI.OpenUrl(url); }
        catch (Exception e) { _openError = e.Message; }
    }

    /// <summary>Follows a sign-in: a read every 800ms while it waits or
    /// installs, repainting the panel each time, until it is done, failed
    /// or canceled — or the dialog closed and the flow is over.</summary>
    private async Task FollowSignInAsync(string id)
    {
        while (_signingOpen && _signing is { } flow && flow.Id == id
            && flow.State is "waiting" or "installing")
        {
            await Task.Delay(800);
            if (!_signingOpen || _signing is not { } held || held.Id != id) return;
            SignInStateJson? st;
            try { st = await App.CurrentApp.Client.GetAsync<SignInStateJson>("signin/" + id); }
            catch (Exception) { continue; } // one read refused: ask again
            if (!_signingOpen || _signing is not { } at || at.Id != id || st is null) return;
            // the CLI it needed is in: now the vendor's page can open
            if (at.State == "installing" && st.State == "waiting" && st.Url is { Length: > 0 })
                await OpenSignInUrlAsync(st.Url);
            if (at.State != st.State || at.Url != st.Url || at.Code != st.Code)
            {
                _signing = st;
                PaintSignIn();
            }
            if (st.State == "done")
            {
                _signing = st;
                await SignedInAsync(st);
                return;
            }
            if (st.State is not ("waiting" or "installing"))
            {
                _signing = st;
                PaintSignIn();
                return;
            }
        }
    }

    /// <summary>A small button that copies a value to the clipboard: the
    /// device code, the sign-in link.</summary>
    private static Button CopyButton(string text, string label)
    {
        var button = new Button { Content = label, Padding = new Thickness(8, 2, 8, 2) };
        button.Click += (_, _) => CopyToClipboard(text);
        return button;
    }

    /// <summary>Copies a value to the clipboard, for the code and the link
    /// the browser page shows.</summary>
    private static void CopyToClipboard(string text)
    {
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
    }

    // ---- the drawing ------------------------------------------------------

    /// <summary>Redraws the dialog's panel for the state _signing is in now:
    /// starting, installing, waiting (with the code, the link, a paste
    /// field), failed, or a plugin's question.</summary>
    private void PaintSignIn()
    {
        if (_signingPanel is not { } box) return;
        box.Children.Clear();
        var st = _signing;
        if (st is null) return;

        if (st.State == "starting")
        {
            box.Children.Add(new ProgressRing { IsActive = true, Width = 28, Height = 28 });
            box.Children.Add(new TextBlock { Text = L.T("Starting the sign-in…"), TextWrapping = TextWrapping.Wrap });
            return;
        }
        if (st.State == "installing")
        {
            box.Children.Add(new ProgressRing { IsActive = true, Width = 28, Height = 28 });
            box.Children.Add(new TextBlock
            {
                Text = L.T("Installing {cli}…").Replace("{cli}", st.Installing ?? SubName(st.Agent)),
                TextWrapping = TextWrapping.Wrap,
            });
            box.Children.Add(new TextBlock
            {
                Text = L.T("{name} is used through its own CLI, which isn't on this computer yet. magpie is installing it with the official installer; the sign-in page opens as soon as it's done.")
                    .Replace("{name}", SubName(st.Agent)),
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        if (st.State == "failed" || st.State == "canceled")
        {
            box.Children.Add(new TextBlock
            {
                Text = L.T("Sign-in didn't finish"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            if (st.Error is { Length: > 0 } why)
                box.Children.Add(new TextBlock { Text = why, Opacity = 0.75, TextWrapping = TextWrapping.Wrap });
            var again = new Button
            {
                Content = L.T("Try again"),
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            again.Click += async (_, _) =>
            {
                var agent = _signingAgent;
                var sub = _signingSub;
                var (site, user) = (_signingSite, _signingUser);
                var method = _signingMethod;
                if (sub is not null) await RunPluginMethodAsync(sub, method); // keeps inputs
                else if (agent is not null) await RunSignInAsync(agent, site, user);
            };
            box.Children.Add(again);
            return;
        }
        if (st.State == "prompt" || st.State == "key")
        {
            if (st.State == "prompt" && _lastPrompt is { } q && _signingSub is { } sub) PaintPrompt(sub, q);
            else if (st.State == "key" && _signingSub is { } keySub && _signingMethod < (keySub.Methods ?? []).Count)
                PaintKeyField(keySub, keySub.Methods[_signingMethod]);
            return;
        }

        // waiting: the browser sentence, the code, the link, a paste field
        box.Children.Add(new TextBlock
        {
            Text = L.T("Finish signing in to {name} in your browser").Replace("{name}", SubName(st.Agent)),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var say = _openError is { Length: > 0 } open
            ? L.T("magpie couldn't open your browser: {error}. Open the sign-in link below in a browser yourself.").Replace("{error}", open)
            : st.PasteCode ? L.T("magpie opened the sign-in page. Paste the code it shows below.")
                : st.Code is { Length: > 0 } && st.Agent == "factory"
                    ? L.T("magpie opened Factory's sign-in page. Check it shows this code and confirm it; the account shows up here as soon as you're done.")
                    : st.Code is { Length: > 0 }
                        ? L.T("magpie opened GitHub's device page. Enter this code there; the account shows up here as soon as you're done.")
                        : L.T("magpie opened the sign-in page. The account shows up here as soon as you're done.");
        box.Children.Add(new TextBlock { Text = say, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });

        if (st.Code is { Length: > 0 } code)
        {
            var codeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            codeRow.Children.Add(new TextBlock
            {
                Text = code,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            });
            codeRow.Children.Add(CopyButton(code, L.T("Code")));
            box.Children.Add(codeRow);
        }
        if (st.Url is { Length: > 0 } url)
        {
            // the link itself, to copy into another browser or profile than
            // the one the app opened it in
            var link = new TextBox { Text = url, IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
            var linkRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            linkRow.Children.Add(link);
            linkRow.Children.Add(CopyButton(url, L.T("Sign-in link")));
            box.Children.Add(linkRow);
            var openAgain = new Button { Content = L.T("Open again"), HorizontalAlignment = HorizontalAlignment.Left };
            openAgain.Click += async (_, _) =>
            {
                _openError = null;
                try { await PageKitUI.OpenUrl(url); }
                catch (Exception e) { _openError = e.Message; PaintSignIn(); }
            };
            box.Children.Add(openAgain);
        }
        if (st.PasteCallback || st.PasteCode || st.PasteKey) PaintPasteField(box, st);
    }

    /// <summary>The waiting state's paste field: a callback URL, the page's
    /// code, or a key made on the keys page, sent to
    /// POST /api/signin/{id}/callback.</summary>
    private void PaintPasteField(StackPanel box, SignInStateJson st)
    {
        var what = st.PasteCode ? L.T("Code") : st.PasteKey ? L.T("API key") : L.T("Callback URL");
        if (st.PasteKey)
        {
            // a page that can't reach magpie (Docker) has no address to
            // paste, so a key made on the keys page finishes it too
            var keysLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            keysLine.Children.Add(new TextBlock
            {
                Text = L.T("If the page can't reach magpie (it runs on a server or in Docker), make an API key on {name}'s keys page and paste it here.")
                    .Replace("{name}", SubName(st.Agent)) + " ",
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap,
            });
            if (st.KeysUrl is { Length: > 0 } keysUrl)
            {
                var keys = new HyperlinkButton
                {
                    Content = L.T("Open the keys page"),
                    Padding = new Thickness(0),
                };
                keys.Click += async (_, _) =>
                {
                    try { await PageKitUI.OpenUrl(keysUrl); }
                    catch (Exception) { /* opening failed; the paste field still works */ }
                };
                keysLine.Children.Add(keys);
            }
            box.Children.Add(keysLine);
        }
        else if (st.CallbackBlocked is { Length: > 0 } blocked)
        {
            // the port the page sends the browser back to couldn't be
            // opened, so only the pasted address finishes this sign-in
            box.Children.Add(new TextBlock
            {
                Text = (blocked == "reserved"
                    ? L.T("Windows keeps port {port}, where the sign-in comes back, for Hyper-V, WSL or Docker, so the page the browser ends on won't load. Copy its whole address and paste it here.")
                    : L.T("Another program has port {port}, where the sign-in comes back, so the page the browser ends on won't finish the sign-in. Copy its whole address and paste it here."))
                    .Replace("{port}", st.CallbackPort.ToString()),
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        else if (!st.PasteCode)
        {
            box.Children.Add(new TextBlock
            {
                Text = L.T("If the page the browser ends on won't load (magpie runs on a server or in Docker), copy its whole address and paste it here."),
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        var field = new TextBox { PlaceholderText = what };
        if (st.PasteKey) // a key is typed unseen, as the web page's input does
        {
            var secret = new PasswordBox { PlaceholderText = what };
            var send = new Button
            {
                Content = L.T("Finish sign-in"),
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            send.Click += async (_, _) =>
            {
                var value = secret.Password.Trim();
                if (value.Length == 0) return;
                send.IsEnabled = false;
                try
                {
                    await App.CurrentApp.Client.PostAsync("signin/" + st.Id + "/callback", new { url = value });
                }
                catch (Exception e)
                {
                    Status.Text = e.Message;
                    send.IsEnabled = true;
                }
            };
            box.Children.Add(secret);
            box.Children.Add(send);
            return;
        }
        var finish = new Button
        {
            Content = L.T("Finish sign-in"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        void Refresh() => finish.IsEnabled = field.Text.Trim().Length > 0;
        field.TextChanged += (_, _) => Refresh();
        finish.IsEnabled = false;
        finish.Click += async (_, _) =>
        {
            var value = field.Text.Trim();
            if (value.Length == 0) return;
            finish.IsEnabled = false;
            try
            {
                await App.CurrentApp.Client.PostAsync("signin/" + st.Id + "/callback", new { url = value });
            }
            catch (Exception e)
            {
                Status.Text = e.Message;
                finish.IsEnabled = true;
            }
        };
        box.Children.Add(field);
        box.Children.Add(finish);
    }

    /// <summary>A plugin's way-to-sign-in chooser, one button per way, as
    /// app.js's renderPluginAsk draws its "method" state.</summary>
    private void PaintMethodChooser(PluginSubJson sub)
    {
        if (_signingPanel is not { } box) return;
        box.Children.Clear();
        box.Children.Add(new TextBlock
        {
            Text = L.T("How do you sign in to {name}?").Replace("{name}", sub.Name),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        box.Children.Add(new TextBlock
        {
            Text = L.T("The plugin {spec} signs in and sends {name}'s requests; magpie only passes them on.")
                .Replace("{spec}", sub.Spec).Replace("{name}", sub.Name),
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });
        var ways = new StackPanel { Spacing = 6 };
        var methods = sub.Methods ?? [];
        for (var i = 0; i < methods.Count; i++)
        {
            var index = i;
            var way = methods[i];
            var button = new Button
            {
                Content = way.Label is { Length: > 0 } label ? L.T(label)
                    : L.T(way.Type == "api" ? "API key" : "Browser"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            button.Click += async (_, _) => await RunPluginMethodAsync(sub, index);
            ways.Children.Add(button);
        }
        box.Children.Add(ways);
    }

    /// <summary>A plugin way's question, drawn from the prompt: text typed
    /// plain, a token typed unseen, or the choices as buttons.</summary>
    private void PaintPrompt(PluginSubJson sub, PluginPromptJson q)
    {
        if (_signingPanel is not { } box) return;
        box.Children.Clear();
        box.Children.Add(new TextBlock { Text = q.Message, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (_promptError is { Length: > 0 } why)
            box.Children.Add(new TextBlock { Text = why, Opacity = 0.75, TextWrapping = TextWrapping.Wrap });
        if (q.Type == "select")
        {
            var choices = new StackPanel { Spacing = 6 };
            foreach (var o in q.Options ?? [])
            {
                var button = new Button
                {
                    Content = L.T(o.Label),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                if (o.Hint is { Length: > 0 } hint) ToolTipService.SetToolTip(button, hint);
                button.Click += async (_, _) =>
                {
                    _promptError = null;
                    await AnswerPluginPromptAsync(sub, q, o.Value);
                };
                choices.Children.Add(button);
            }
            box.Children.Add(choices);
            return;
        }
        var next = new Button
        {
            Content = L.T("Next"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        async void Send(string value)
        {
            if (value.Length == 0) return;
            next.IsEnabled = false;
            _promptError = null;
            await AnswerPluginPromptAsync(sub, q, value);
        }
        if (q.Type == "password")
        {
            var secret = new PasswordBox { PlaceholderText = q.Placeholder ?? "" };
            secret.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Send(secret.Password.Trim()); };
            next.Click += (_, _) => Send(secret.Password.Trim());
            box.Children.Add(secret);
        }
        else
        {
            var field = new TextBox { PlaceholderText = q.Placeholder ?? "" };
            field.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Send(field.Text.Trim()); };
            next.Click += (_, _) => Send(field.Text.Trim());
            box.Children.Add(field);
        }
        box.Children.Add(next);
    }

    /// <summary>What the plugin said of an answer, said again over the
    /// question it was asked at.</summary>
    private void PaintPromptError(string error)
    {
        _promptError = error;
        if (_signingSub is { } sub && _lastPrompt is { } q) PaintPrompt(sub, q);
        else PaintSignIn();
    }

    /// <summary>An "api" way's key field, titled by the way's own label
    /// unless that only says "API key", as app.js's renderPluginAsk does.</summary>
    private void PaintKeyField(PluginSubJson sub, PluginMethodJson way)
    {
        if (_signingPanel is not { } box) return;
        box.Children.Clear();
        var own = (way.Label ?? "").Trim();
        box.Children.Add(new TextBlock
        {
            Text = own is { Length: > 0 } && !own.Equals("API key", StringComparison.OrdinalIgnoreCase)
                ? L.T(own)
                : L.T("{name} API key").Replace("{name}", sub.Name),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var secret = new PasswordBox { PlaceholderText = way.Placeholder ?? "" };
        var go = new Button
        {
            Content = L.T("Sign in"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        go.Click += async (_, _) =>
        {
            var key = secret.Password.Trim();
            if (key.Length == 0) return;
            go.IsEnabled = false;
            await SubmitPluginKeyAsync(sub, key);
        };
        box.Children.Add(secret);
        box.Children.Add(go);
    }

    // ---- done -------------------------------------------------------------

    /// <summary>A sign-in finished: the dialog closed, the list read again,
    /// and the account greeted on the status line — added, already listed,
    /// or signed in as, as the web page's signedIn says it.</summary>
    private async Task SignedInAsync(SignInStateJson st)
    {
        var agent = st.Agent;
        CloseSignInDialog();
        _signing = null;
        _lastPrompt = null;
        _promptError = null;
        try
        {
            await ReloadAsync(); // GET providers, as the web page reads providers?wait=1
            var listed = Data.Providers.Any(p => p.Account?.Agent == agent);
            var who = st.User is { Length: > 0 } u ? u : SubName(agent);
            Status.Text = !listed
                ? L.T("{user} signed in, but magpie can't list it — please report this").Replace("{user}", who)
                : st.Again
                    ? L.T("{user} is already listed — its sign-in was renewed").Replace("{user}", who)
                    : st.Using
                        ? L.T("Signed in as {user}").Replace("{user}", who)
                        : L.T("{user} added — switch to it any time").Replace("{user}", who);
        }
        catch (Exception e)
        {
            Status.Text = e.Message;
        }
    }
}
