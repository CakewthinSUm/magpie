using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop.Pages;

public sealed partial class SettingsPage
{
    /// <summary>The sync view read with the tab, kept while the tab is drawn
    /// (its own GET /api/davsync, not part of settingsJSON).</summary>
    private DavSyncViewJson? _sync;

    /// <summary>
    /// The Sync and backup part of Settings, as the page has it (app.js
    /// renderSync): a WebDAV, S3 or GitHub folder keeping this computer's
    /// setup the same as the others', and a sealed file to carry by hand.
    /// The view is GET /api/davsync; every change is a POST to
    /// /api/davsync/{action}, which answers with the view again.
    /// </summary>
    private void DrawSync()
    {
        var v = _sync ?? new DavSyncViewJson { Keys = true, Agents = true, Library = true };
        var card = new StackPanel { Spacing = 8 };

        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = KindTitle(v),
            Description = SyncStatusLine(v),
            Control = SyncActions(v),
        }));

        if (v.On)
        {
            card.Children.Add(SyncErrorLines(v));
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = "Sync by itself",
                Description = v.Auto > 0 ? $"Every {v.Auto} minutes while magpie runs" : "Off: only Sync now syncs",
                Control = PageKitUI.Menu(
                    [("0", "Off"), ("3", "3 min"), ("15", "15 min"), ("30", "30 min"), ("60", "1 hour")],
                    v.Auto.ToString(),
                    m => _ = SyncActionAsync("auto", new { minutes = m == "0" ? -1 : int.Parse(m) })),
            }));
            card.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = "Restore from the server",
                Description = "This computer's setup becomes the one on the server. What is here now is kept first, to undo.",
                Control = ButtonOf("Restore…", () => _ = RestoreAsync(v)),
            }));
        }

        if (v.Notice is not null)
        {
            card.Children.Add(NoticeCard(v));
        }

        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Export",
            Description = "Everything above in one file, sealed with a passphrase, to carry to another computer",
            Control = ButtonOf("Export…", () => _ = ExportAsync()),
        }));
        card.Children.Add(PageKitUI.Setting(new SettingRow
        {
            Title = "Import",
            Description = "Bring in a file exported from magpie",
            Control = ButtonOf("Import…", () => _ = ImportAsync()),
        }));

        Body.Children.Add(PageKitUI.Heading("Sync"));
        Body.Children.Add(card);
    }

    private static string KindTitle(DavSyncViewJson v) => v.On
        ? v.Kind switch { "s3" => "S3 sync", "github" => "GitHub sync", _ => "WebDAV sync" }
        : "WebDAV, S3 or GitHub sync";

    private static string SyncStatusLine(DavSyncViewJson v)
    {
        if (!v.On) return "Keeps providers, settings and profiles the same on every computer";
        if (v.Error is { Length: > 0 } err) return "Couldn't sync: " + err;
        var host = v.Url ?? "";
        if (v.Kind == "s3") host = string.Join(" · ", new[] { v.Url, v.Endpoint }.Where(x => x is { Length: > 0 }));
        if (v.Kind == "github") host = string.Join(" · ", new[] { v.Url, v.Branch }.Where(x => x is { Length: > 0 }));
        return v.Last == default ? "Not synced yet · " + host : "Synced " + When(v.Last) + " · " + host;
    }

    private static string When(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? local.ToString("HH:mm") : local.ToString("yyyy-MM-dd HH:mm");
    }

    private FrameworkElement SyncActions(DavSyncViewJson v)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (v.On)
        {
            row.Children.Add(ButtonOf("Sync now", () => _ = SyncActionAsync("now", new { })));
            row.Children.Add(ButtonOf("Edit", () => _ = EditSyncAsync(v)));
        }
        else
        {
            row.Children.Add(ButtonOf("Set up", () => _ = EditSyncAsync(v)));
        }
        return row;
    }

    private FrameworkElement SyncErrorLines(DavSyncViewJson v)
    {
        var stack = new StackPanel { Spacing = 4 };
        if (v.UsageError is { Length: > 0 } usage)
        {
            stack.Children.Add(ProblemLine("Couldn't share usage: " + usage));
        }
        if (v.SignIn is { Count: > 0 } names)
        {
            stack.Children.Add(ProblemLine("Your synced settings use " + string.Join(", ", names) +
                ", which isn't signed in on this computer. Sync carries settings, not sign-ins: to use it here, sign in in Providers → Add a provider; if you don't, nothing needs doing.", bad: false));
        }
        if (v.Error is { Length: > 0 } && v.ServerFile is { Replace: true })
        {
            stack.Children.Add(PageKitUI.Setting(new SettingRow
            {
                Title = "Upload this computer's setup",
                Description = "It replaces the file on the server. That file is kept in the sync folder first.",
                Control = ButtonOf("Upload…", () => _ = UploadAsync()),
            }));
        }
        return stack;
    }

    private FrameworkElement NoticeCard(DavSyncViewJson v)
    {
        var n = v.Notice!;
        var lines = new List<string>();
        if (n.Restored) lines.Add("Restored from the server: " + string.Join(", ", n.Here ?? []));
        else if (n.Here is { Count: > 0 }) lines.Add("Replaced here by newer ones from another computer: " + string.Join(", ", n.Here));
        if (n.There is { Count: > 0 }) lines.Add("Replaced on the server by this computer's newer ones: " + string.Join(", ", n.There));
        lines.Add(n.Restored ? "This computer's setup from before is kept in the sync folder." : "The copies replaced are kept in the sync folder.");

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (n.Restored && v.Undo)
        {
            buttons.Children.Add(ButtonOf("Undo", () => _ = SyncActionAsync("undo", new { })));
        }
        buttons.Children.Add(ButtonOf("Show", () => _ = SyncActionAsync("reveal", new { })));
        buttons.Children.Add(ButtonOf("OK", () => _ = SyncActionAsync("dismiss", new { })));

        var stack = new StackPanel { Spacing = 6 };
        foreach (var line in lines)
        {
            stack.Children.Add(new TextBlock { Text = line, FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
        }
        stack.Children.Add(buttons);
        return new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(8),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = stack,
        };
    }

    private static Button ButtonOf(string label, Action click)
    {
        var b = new Button { Content = label, VerticalAlignment = VerticalAlignment.Center };
        b.Click += (_, _) => click();
        return b;
    }

    // ---- actions ----

    private async Task SyncActionAsync(string action, object body)
    {
        if (_busy) return;
        await PageKit.RunAsync(async () =>
        {
            _busy = true;
            _sync = await App.CurrentApp.Client.PostAsync<DavSyncViewJson>("davsync/" + action, body) ?? _sync;
            Draw();
        }, Status, Busy);
        _busy = false;
    }

    private async Task RestoreAsync(DavSyncViewJson v)
    {
        var parts = new List<string> { "providers", "settings", "profiles" };
        if (v.Agents) parts.Add("agents' models");
        if (v.Library) parts.Add("library");
        if (!await PageKitUI.ConfirmAsync(XamlRoot, "Restore from the server?",
            "This computer's " + string.Join(", ", parts) + " become the ones on the server, as your other computers last synced them. " +
            "Whatever was changed here since is replaced.\n\nWhat is here now is sealed into the sync folder first, and Undo puts it back.",
            "Restore setup", "Cancel", danger: true)) return;
        await SyncActionAsync("restore", new { });
    }

    private async Task UploadAsync()
    {
        if (!await PageKitUI.ConfirmAsync(XamlRoot, "Replace the file on the server?",
            "This computer's setup goes up in place of the file on the server, and your other computers sync with it from then on.\n\n" +
            "The file on the server is kept in the sync folder first.",
            "Upload this computer's setup", "Cancel", danger: true)) return;
        await SyncActionAsync("upload", new { });
    }

    // ---- set up / edit ----

    private async Task EditSyncAsync(DavSyncViewJson v)
    {
        var active = v.On ? (v.Kind ?? "webdav") : "";
        var kind = active.Length > 0 ? active : "webdav";

        var kindBar = PageKitUI.Choices([("webdav", "WebDAV"), ("s3", "S3"), ("github", "GitHub")], kind, k => kind = k);

        // webdav
        var url = new TextBox { Header = "Address", Text = v.Kind is null or "webdav" ? v.Url ?? "" : "", PlaceholderText = "https://dav.jianguoyun.com/dav/", Width = 360 };
        var user = new TextBox { Header = "User", Text = v.Kind is null or "webdav" ? v.User ?? "" : "", Width = 360 };
        var pass = new PasswordBox { Header = "Password", PlaceholderText = v.PasswordSet ? "saved · type a new one to replace it" : "password, or an app password", Width = 360 };

        // s3
        var bucketParts = SplitS3(v.Kind == "s3" ? v.Url : null);
        var endpoint = new TextBox { Header = "Endpoint", Text = v.Kind == "s3" ? v.Endpoint ?? "" : "", PlaceholderText = "https://<account>.r2.cloudflarestorage.com", Width = 360 };
        var bucket = new TextBox { Header = "Bucket", Text = v.Kind == "s3" ? bucketParts.Bucket : "", Width = 360 };
        var prefix = new TextBox { Header = "Prefix", Text = v.Kind == "s3" ? bucketParts.Prefix : "", Width = 360 };
        var region = new TextBox { Header = "Region", Text = v.Kind == "s3" ? v.Region ?? "" : "", PlaceholderText = "us-east-1, or auto for R2", Width = 360 };
        var keyID = new TextBox { Header = "Access key", Text = v.Kind == "s3" ? v.User ?? "" : "", Width = 360 };
        var secret = new PasswordBox { Header = "Secret", PlaceholderText = v.PasswordSet && v.Kind == "s3" ? "saved · type a new one to replace it" : "secret access key", Width = 360 };
        var pathStyle = new CheckBox { Content = "Path-style: the bucket in the path, not the host name", IsChecked = v.Kind == "s3" && v.PathStyle };

        // github
        var ghParts = SplitGitHub(v.Kind == "github" ? v.Url : null);
        var repo = new TextBox { Header = "Repository", Text = v.Kind == "github" ? ghParts.Repo : "", PlaceholderText = "owner/repo", Width = 360 };
        var branch = new TextBox { Header = "Branch", Text = v.Kind == "github" ? v.Branch ?? "" : "", PlaceholderText = "default branch", Width = 360 };
        var ghFolder = new TextBox { Header = "Folder", Text = v.Kind == "github" ? ghParts.Prefix : "", PlaceholderText = "optional", Width = 360 };
        var token = new PasswordBox { Header = "GitHub token", PlaceholderText = v.PasswordSet && v.Kind == "github" ? "saved · type a new one to replace it" : "GitHub token", Width = 360 };

        var phrase = new PasswordBox { Header = "Passphrase", PlaceholderText = v.PassphraseSet ? "saved · type a new one to replace it" : "the same on every computer", Width = 360 };
        var keys = new CheckBox { Content = "Providers' API keys", IsChecked = v.Keys };
        var agents = new CheckBox { Content = "Agents' models", IsChecked = v.Agents };
        var library = new CheckBox { Content = "Library: instructions, MCP servers and skills", IsChecked = v.Library };
        var usage = new CheckBox { Content = "Usage: this computer's calls, for the Usage page on the others", IsChecked = v.Usage };

        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(kindBar);
        body.Children.Add(url);
        body.Children.Add(user);
        body.Children.Add(pass);
        body.Children.Add(endpoint);
        body.Children.Add(bucket);
        body.Children.Add(prefix);
        body.Children.Add(region);
        body.Children.Add(keyID);
        body.Children.Add(secret);
        body.Children.Add(pathStyle);
        body.Children.Add(repo);
        body.Children.Add(branch);
        body.Children.Add(ghFolder);
        body.Children.Add(token);
        body.Children.Add(phrase);
        body.Children.Add(new TextBlock { Text = "Also sync", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        body.Children.Add(keys);
        body.Children.Add(agents);
        body.Children.Add(library);
        body.Children.Add(usage);

        // only the picked kind's fields show; the others stay as typed
        void ShowKind()
        {
            var dav = new FrameworkElement[] { url, user, pass };
            var s3f = new FrameworkElement[] { endpoint, bucket, prefix, region, keyID, secret, pathStyle };
            var ghf = new FrameworkElement[] { repo, branch, ghFolder, token };
            foreach (var f in dav) f.Visibility = kind == "webdav" ? Visibility.Visible : Visibility.Collapsed;
            foreach (var f in s3f) f.Visibility = kind == "s3" ? Visibility.Visible : Visibility.Collapsed;
            foreach (var f in ghf) f.Visibility = kind == "github" ? Visibility.Visible : Visibility.Collapsed;
        }
        ShowKind();
        // the kind picked shows its own fields; the others keep what was typed
        if (kindBar is StackPanel bar)
        {
            foreach (var child in bar.Children)
            {
                if (child is RadioButton rb) rb.Checked += (_, _) => ShowKind();
            }
        }

        var scroll = new ScrollViewer { Content = body, MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = active.Length == 0 ? "Set up sync" : "Sync settings",
            Content = scroll,
            PrimaryButtonText = active.Length == 0 ? "Turn on" : kind == active ? "Save" : "Move sync here",
            SecondaryButtonText = active.Length > 0 ? "Turn off" : "",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            await SyncActionAsync("off", new { });
            return;
        }
        if (result != ContentDialogResult.Primary) return;

        // what the kind's fields make of the config (davsync.Config)
        var b = bucket.Text.Trim();
        var p = prefix.Text.Trim().Trim('/');
        var repository = repo.Text.Trim()
            .Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase).TrimEnd('/');
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) repository = repository[..^4];
        var ghFolderText = ghFolder.Text.Trim().Trim('/');

        object config;
        if (kind == "github")
        {
            if (!repository.Contains('/')) { Status.Text = "Name the GitHub repository as owner/repo"; return; }
            config = new
            {
                url = "github://" + repository + (ghFolderText.Length > 0 ? "/" + ghFolderText : ""),
                branch = branch.Text.Trim(),
                password = token.Password.Trim(),
            };
        }
        else if (kind == "s3")
        {
            if (b.Length == 0) { Status.Text = "Name the bucket"; return; }
            config = new
            {
                url = "s3://" + b + (p.Length > 0 ? "/" + p : ""),
                user = keyID.Text.Trim(),
                password = secret.Password,
                endpoint = endpoint.Text.Trim(),
                region = region.Text.Trim(),
                pathStyle = pathStyle.IsChecked == true,
            };
        }
        else
        {
            config = new { url = url.Text.Trim(), user = user.Text.Trim(), password = pass.Password };
        }

        var body_ = new Dictionary<string, object?>
        {
            ["passphrase"] = phrase.Password,
            ["keys"] = keys.IsChecked == true,
            ["agents"] = agents.IsChecked == true,
            ["library"] = library.IsChecked == true,
            ["usage"] = usage.IsChecked == true,
        };
        foreach (var (k, val) in ToDictionary(config)) body_[k] = val;
        await SyncActionAsync("save", body_);
    }

    private static (string Bucket, string Prefix) SplitS3(string? url)
    {
        if (url is not { Length: > 0 }) return ("", "");
        var rest = url.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ? url[5..] : url;
        var i = rest.IndexOf('/');
        return i < 0 ? (rest, "") : (rest[..i], rest[(i + 1)..]);
    }

    private static (string Repo, string Prefix) SplitGitHub(string? url)
    {
        if (url is not { Length: > 0 }) return ("", "");
        var rest = url.StartsWith("github://", StringComparison.OrdinalIgnoreCase) ? url[9..] : url;
        var i = rest.IndexOf('/');
        return i < 0 ? (rest, "") : (rest[..i], rest[(i + 1)..]);
    }

    // ---- export and import ----

    private async Task ExportAsync()
    {
        var p1 = new PasswordBox { Header = "Passphrase", PlaceholderText = "passphrase", Width = 360 };
        var p2 = new PasswordBox { Header = "Again", PlaceholderText = "again", Width = 360 };
        var keys = new CheckBox { Content = "With the providers' API keys", IsChecked = true };
        var library = new CheckBox { Content = "With the library: instructions, MCP servers and skills", IsChecked = true };
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(p1);
        body.Children.Add(p2);
        body.Children.Add(new TextBlock
        {
            Text = "Needed to open the file. Subscriptions aren't in it: sign in to them on the other computer.",
            FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(keys);
        body.Children.Add(library);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Export",
            Content = body,
            PrimaryButtonText = "Export",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (p1.Password.Length == 0) { Status.Text = "Pick a passphrase: the file is sealed with it"; return; }
        if (p1.Password != p2.Password) { Status.Text = "The two passphrases differ"; return; }

        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<ExportAnswerJson>("backup/export",
                new { pass = p1.Password, keys = keys.IsChecked == true, library = library.IsChecked == true });
            if (answer is not null) Status.Text = "Saved to " + answer.Path;
        }, Status, Busy);
    }

    private async Task ImportAsync()
    {
        var picked = await App.CurrentApp.ChooseFileAsync("Import a magpie backup", ".magpie-backup");
        if (picked is not { } file) return;

        var pass = new PasswordBox { Header = "Passphrase", PlaceholderText = "passphrase", Width = 360 };
        var agents = new CheckBox { Content = "Set the agents' models too", IsChecked = true };
        var library = new CheckBox { Content = "Bring in the library too: instructions, MCP servers and skills", IsChecked = true };
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock { Text = file.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        body.Children.Add(pass);
        body.Children.Add(agents);
        body.Children.Add(library);
        body.Children.Add(new TextBlock
        {
            Text = "Providers with the same id are replaced; one that came without a key keeps the key it has here. The library replaces the one here, which is kept with its backups.",
            FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap,
        });

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Import",
            Content = body,
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await PageKit.RunAsync(async () =>
        {
            var answer = await App.CurrentApp.Client.PostAsync<ImportResultJson>("backup/import", new
            {
                data = Convert.ToBase64String(file.Data),
                pass = pass.Password,
                agents = agents.IsChecked == true,
                library = library.IsChecked == true,
            });
            if (answer is not null) await ShowImportResultAsync(answer);
        }, Status, Busy);
    }

    /// <summary>What an import brought in, as the page lists it: one line per
    /// part, and what couldn't be (too many lines for the status row).</summary>
    private async Task ShowImportResultAsync(ImportResultJson r)
    {
        var lines = new List<string>
        {
            $"Providers: {r.Added} added, {r.Replaced} replaced; {r.Profiles} profiles; {r.Agents} agent settings changed",
        };
        if (r.NeedKey is { Count: > 0 } need) lines.Add("Needs a key: " + string.Join(", ", need));
        if (r.SignIn is { Count: > 0 } signIn)
        {
            lines.Add("This backup's settings use " + string.Join(", ", signIn) +
                ", which isn't signed in on this computer. A backup carries settings, not sign-ins: to use it here, sign in in Providers → Add a provider; if you don't, nothing needs doing.");
        }
        if (r.Plugins is { Count: > 0 } plugins) lines.Add("Plugins added: " + string.Join(", ", plugins));
        foreach (var (spec, error) in r.PluginErrors ?? [])
        {
            lines.Add($"Couldn't add the plugin {spec}: {error}");
        }
        if (r.Library) lines.Add("Library brought in and written into the agents");
        foreach (var p in r.LibraryProblems ?? [])
        {
            lines.Add($"{p.Agent} couldn't get {p.What}: {p.Error}");
        }

        var stack = new StackPanel { Spacing = 6 };
        foreach (var line in lines)
        {
            stack.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        }
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Imported",
            Content = new ScrollViewer { Content = stack, MaxHeight = 420 },
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }
}