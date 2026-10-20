using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magpie.Desktop.Pages;

/// <summary>
/// What every page drawn here has in common. A XAML page's generated part
/// inherits <see cref="Page"/> (x:Class), so a page can't also inherit this:
/// it holds a PageKit instead, one line of its own.
///
/// A page reads its part of the API when the window opens it, says what went
/// wrong when a read or a write fails, and is read again by the title bar's
/// refresh. A write redraws from the answer magpie gives, as the web page
/// does — magpie's writes answer with the new state, so what is drawn is what
/// it wrote.
/// </summary>
public sealed class PageKit
{
    /// <summary>Reads the page's data again and draws it. Not required: the
    /// XAML type info makes one of every public type to describe it, and a
    /// required member would stop that build (CS9035).</summary>
    public Func<Task> Refresh { get; init; } = () => Task.CompletedTask;

    /// <summary>
    /// The one line a page shows what it is doing or what failed. The page
    /// under the status line is walked for translation once the work is done
    /// (L.Walk): a page sets its words as it is drawn, so this is the one
    /// place every page's text passes through after a draw.
    /// </summary>
    public static async Task RunAsync(Func<Task> work, TextBlock status, ProgressRing? busy = null)
    {
        if (busy is not null) busy.IsActive = true;
        status.Text = "";
        try
        {
            await work();
        }
        catch (Exception e)
        {
            // magpie's own reason for a refused change, as the page shows it
            status.Text = e.Message;
        }
        finally
        {
            if (busy is not null) busy.IsActive = false;
            // the page this status line is on: its words are translated now,
            // once the draw that set them is done
            if (Root(status) is { } page) L.Walk(page);
        }
    }

    /// <summary>The Page an element is on, found up the visual tree: what
    /// L.Walk is given, so a page's own code needn't translate each word.</summary>
    private static Microsoft.UI.Xaml.Controls.Page? Root(DependencyObject? from)
    {
        for (var at = from; at is not null; at = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(at))
        {
            if (at is Microsoft.UI.Xaml.Controls.Page page) return page;
        }
        return null;
    }
}

/// <summary>A page the title bar's refresh can read again.</summary>
public interface IRefreshable
{
    PageKit Kit { get; }
}

/// <summary>
/// A row of a list drawn here: a title, a line under it, and what the row
/// holds. The pages' item templates bind to these.
/// </summary>
public sealed class Row
{
    public string Title { get; set; } = "";
    public string Line { get; set; } = "";
    public string? Tag { get; set; }
    public bool On { get; set; }
    public Microsoft.UI.Xaml.Media.ImageSource? Icon { get; set; }
    /// <summary>What the row holds, for a template that shows a count.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// A setting's row as Windows draws one: its name and what it does on the
/// left, the control that changes it on the right, in a card. The pages
/// that have settings build these rather than a page of their own, so one
/// setting looks the same wherever it is.
/// </summary>
public sealed class SettingRow
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>The control that changes it: a ToggleSwitch, a row of radio
    /// buttons, or a button.</summary>
    public Microsoft.UI.Xaml.FrameworkElement? Control { get; set; }
}

/// <summary>
/// The words the pages say, in the language Settings picked: the page's own
/// translations (Strings, generated from i18n.js), looked up by the English
/// text as the page writes it. A word the page doesn't translate is left
/// English, as the page leaves it.
/// </summary>
public static class L
{
    /// <summary>The table for the language in force, or null for English.</summary>
    private static Dictionary<string, string>? _table;
    private static string _lang = "";

    /// <summary>Sets the language the pages are drawn in (the settings'
    /// lang, or "" for the system's, which the app resolves before this).</summary>
    public static void Use(string? lang)
    {
        _lang = lang ?? "";
        _table = Strings.For(_lang);
    }

    public static string T(string text) =>
        _table is not null && _table.TryGetValue(text, out var translated) ? translated : text;

    /// <summary>The language in force, as Settings names it.</summary>
    public static string Lang => _lang;

    /// <summary>
    /// Translates every word already drawn under root, in place: the pages
    /// set their text when they are built, some in XAML and some in code, and
    /// a word the page translates is found by the English it was written as.
    /// The pages call this after a draw, so a page's own code needn't wrap
    /// every word in T.
    /// </summary>
    public static void Walk(DependencyObject root)
    {
        if (_table is null) return;
        switch (root)
        {
            case TextBlock { Text: { Length: > 0 } text } block:
                block.Text = T(text);
                break;
            case Button { Content: string content } button:
                button.Content = T(content);
                break;
            // the choice rows are RadioButtons, which are not Buttons
            case RadioButton { Content: string radio } option:
                option.Content = T(radio);
                break;
            case MenuFlyoutItem { Text: { Length: > 0 } item } menuItem:
                menuItem.Text = T(item);
                break;
            case NavigationViewItem { Content: string nav } item:
                item.Content = T(nav);
                break;
            case NavigationViewItemHeader { Content: string header } head:
                head.Content = T(header);
                break;
            case CheckBox { Content: string check } box:
                box.Content = T(check);
                break;
            case InfoBar { Title: { Length: > 0 } title } bar:
                bar.Title = T(title);
                break;
        }
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            Walk(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i));
        }
    }
}

/// <summary>
/// The pieces the pages drawn here share beyond their own data: the icon
/// names magpie's page serves, and the Settings page's rows.
/// </summary>
public static class PageKitUI
{
    /// <summary>A word in the language in force (L.T), for the pages' own
    /// text: the same key the page's i18n.js has.</summary>
    public static string T(string text) => L.T(text);

    /// <summary>The icons magpie's page keeps as PNG rather than SVG (app.js's
    /// pngIcons), so a vendor logo is asked for as the file it is.</summary>
    private static readonly HashSet<string> PngIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        "crush", "zcode", "alma", "hanako", "cindy", "lody", "typesafe",
        "atomcode", "snow", "mlx-serve", "mothx",
    };

    /// <summary>
    /// A picture from the page's icon folder (/api/icons serves it): a
    /// picture the user gave is kept as file:&lt;name&gt;, and a vendor's is
    /// its name with .svg, or .png for the few kept as one. None when the
    /// name says nothing magpie can show.
    /// </summary>
    public static Microsoft.UI.Xaml.Media.ImageSource? IconOf(string? icon)
    {
        if (string.IsNullOrEmpty(icon)) return null;
        var app = App.CurrentApp;
        if (app.Client.BaseUrl.Length == 0) return null;
        string path;
        string file;
        if (icon.StartsWith("file:", StringComparison.Ordinal))
        {
            file = icon[5..];
            path = "icons/" + Uri.EscapeDataString(file);
        }
        else if (icon.StartsWith("https://", StringComparison.Ordinal) || icon.StartsWith("data:", StringComparison.Ordinal))
        {
            // a plugin's own picture, not one of magpie's files
            return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(icon));
        }
        else
        {
            file = icon + (PngIcons.Contains(icon) ? ".png" : ".svg");
            path = "icons/" + Uri.EscapeDataString(file);
        }
        var uri = new Uri(app.Client.BaseUrl + path);
        // WinUI reads an SVG as one, a BitmapImage only as a bitmap
        return file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(uri)
            : new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri);
    }

    /// <summary>One setting's row, in a card: its name and what it does, the
    /// control on the right. A description is a line under the name, and
    /// wraps rather than being cut off.</summary>
    public static Border Setting(SettingRow row)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var who = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        who.Children.Add(new TextBlock { Text = T(row.Title) });
        if (row.Description.Length > 0)
        {
            who.Children.Add(new TextBlock
            {
                Text = T(row.Description),
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        grid.Children.Add(who);

        if (row.Control is not null)
        {
            row.Control.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(row.Control, 1);
            grid.Children.Add(row.Control);
        }

        return new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(8),
            Background = ThemeBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Child = grid,
        };
    }

    /// <summary>A theme's brush by name, from the app's resources; none when
    /// the system has no such brush (a page still draws, plainly).</summary>
    private static Microsoft.UI.Xaml.Media.Brush? ThemeBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) ? v as Microsoft.UI.Xaml.Media.Brush : null;

    /// <summary>A row of choices: the product rule is no native select, so a
    /// setting with a few values is a row of radio buttons, the picked one
    /// marked. Changed fires once, with the value picked. A value not among
    /// the choices leaves none marked.</summary>
    public static FrameworkElement Choices(IEnumerable<(string Value, string Label)> values, string picked, Action<string> changed)
    {
        var list = values.ToList();
        // a row of radio buttons of their own, each as wide as its word: the
        // RadioButtons control shares its width between its items, which
        // left an inch of space before "100%" and its neighbours
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var group = "choice-" + Guid.NewGuid().ToString("N");
        var drawing = true;
        foreach (var (value, label) in list)
        {
            var button = new RadioButton
            {
                Content = T(label),
                GroupName = group,
                IsChecked = value == picked,
                MinWidth = 0,
            };
            button.Checked += (_, _) =>
            {
                // the pick marked in code is not the user choosing it
                if (!drawing) changed(value);
            };
            row.Children.Add(button);
        }
        drawing = false;
        return row;
    }

    /// <summary>
    /// A setting with too many values for a row of buttons: a button showing
    /// the one picked, which opens the app's own menu (never a native
    /// select, docs/code-standards.md). Changed fires once, with the value
    /// picked.
    /// </summary>
    public static Button Menu(IEnumerable<(string Value, string Label)> values, string picked, Action<string> changed,
        string? buttonLabel = null)
    {
        var list = values.ToList();
        var button = new Button
        {
            // a picker that names an action ("Disable…") rather than the value
            // in force says its own label until one is picked
            Content = T(buttonLabel ?? list.FirstOrDefault(v => v.Value == picked).Label ?? picked),
            Padding = new Thickness(12, 4, 12, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var menu = new MenuFlyout();
        foreach (var (value, label) in list)
        {
            // the menu's own items are not in the visual tree, so they are
            // translated here rather than by L.Walk
            var item = new MenuFlyoutItem { Text = T(label), Tag = value };
            if (value == picked) item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            item.Click += (_, _) =>
            {
                if (value == picked) return;
                button.Content = label;
                changed(value);
            };
            menu.Items.Add(item);
        }
        button.Flyout = menu;
        return button;
    }

    /// <summary>A heading over a group of rows, as Settings groups its parts.</summary>
    public static TextBlock Heading(string text) => new()
    {
        Text = T(text),
        FontSize = 12,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Opacity = 0.7,
        Margin = new Thickness(2, 12, 0, 6),
    };

    /// <summary>A problem said where it is seen: magpie's reason, in an InfoBar
    /// that stays until it is closed. A null or empty text hides it.</summary>
    public static void Say(InfoBar bar, string? text, bool bad = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            bar.IsOpen = false;
            return;
        }
        bar.Severity = bad ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        bar.Message = text;
        bar.IsOpen = true;
    }

    /// <summary>A ToggleSwitch for a setting's row. The page owns its
    /// _drawing guard: toggled fires with the new state, and must no-op
    /// while the page draws (the switch is set in code then).</summary>
    public static ToggleSwitch Switch(bool on, Action<bool> toggled)
    {
        var toggle = new ToggleSwitch
        {
            IsOn = on,
            OnContent = "",
            OffContent = "",
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toggle.Toggled += (_, _) => toggled(toggle.IsOn);
        return toggle;
    }

    /// <summary>A text (or password) box with an explicit Apply button, for a
    /// value the page saves on the button, never on blur (a field moved out
    /// of must not save a half-typed value). apply receives the trimmed text.</summary>
    public static FrameworkElement TextField(string value, string placeholder, Action<string> apply,
        bool password = false, string applyLabel = "Save")
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (password)
        {
            var box = new PasswordBox
            {
                Password = value,
                PlaceholderText = placeholder,
                Width = 260,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var save = new Button { Content = applyLabel, VerticalAlignment = VerticalAlignment.Center };
            save.Click += (_, _) => apply(box.Password.Trim());
            row.Children.Add(box);
            row.Children.Add(save);
            return row;
        }
        else
        {
            var box = new TextBox
            {
                Text = value,
                PlaceholderText = placeholder,
                Width = 260,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var save = new Button { Content = applyLabel, VerticalAlignment = VerticalAlignment.Center };
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter) apply(box.Text.Trim());
            };
            save.Click += (_, _) => apply(box.Text.Trim());
            row.Children.Add(box);
            row.Children.Add(save);
            return row;
        }
    }

    /// <summary>A number box with an Apply button; apply receives the parsed
    /// int, or 0 when the box is empty or not a number.</summary>
    public static FrameworkElement NumberField(int value, string placeholder, Action<int> apply, string applyLabel = "Apply")
    {
        var box = new TextBox
        {
            Text = value > 0 ? value.ToString() : "",
            PlaceholderText = placeholder,
            Width = 120,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var save = new Button { Content = applyLabel, VerticalAlignment = VerticalAlignment.Center };
        void Send()
        {
            if (int.TryParse(box.Text.Trim(), out var n)) apply(n);
        }
        box.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Send(); };
        save.Click += (_, _) => Send();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(box);
        row.Children.Add(save);
        return row;
    }

    /// <summary>A confirm dialog; the caller must pass the page's XamlRoot
    /// (a ContentDialog without one throws on ShowAsync). True on the
    /// primary button.</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message,
        string primary = "OK", string cancel = "Cancel", bool danger = false)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = cancel,
            DefaultButton = danger ? ContentDialogButton.Close : ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>An add-row: a text box and an Add button, for a list of
    /// values (CORS origins, allow IPs, disabled models, masking rules).
    /// add receives the trimmed text.</summary>
    public static FrameworkElement AddRow(string placeholder, Action<string> add, string addLabel = "Add")
    {
        var box = new TextBox { PlaceholderText = placeholder, Width = 260, VerticalAlignment = VerticalAlignment.Center };
        var button = new Button { Content = addLabel, VerticalAlignment = VerticalAlignment.Center };
        void Send()
        {
            var v = box.Text.Trim();
            if (v.Length == 0) return;
            add(v);
            box.Text = "";
        }
        box.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Send(); };
        button.Click += (_, _) => Send();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(box);
        row.Children.Add(button);
        return row;
    }

    /// <summary>A link-style button that opens a URL through the backend's
    /// POST /api/open, as the web page's "Get a key ↗" does.</summary>
    public static Button LinkButton(string label, string url)
    {
        var button = new Button
        {
            Content = label + " ↗",
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Click += async (_, _) =>
        {
            try { await App.CurrentApp.Client.PostAsync("open", new { url }); }
            catch (Exception) { /* opening failed; the row still works */ }
        };
        return button;
    }
}
