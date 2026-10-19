using Magpie.Desktop.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace Magpie.Desktop.Pages;

/// <summary>
/// The Providers page: the model sources on this computer, each switched on
/// or off in place, the turned-off ones folded under the list, and the
/// search that narrows them (GET /api/providers, POST /api/provider/on).
/// </summary>
public sealed partial class ProvidersPage : Page, IRefreshable
{
    /// <summary>What the title bar's refresh asks of this page.</summary>
    public PageKit Kit => new() { Refresh = LoadAsync };

    private ProvidersJson _data = new();
    private bool _drawing;

    public ProvidersPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await PageKit.RunAsync(async () =>
        {
            _data = await App.CurrentApp.Client.GetAsync<ProvidersJson>("providers") ?? new ProvidersJson();
            Draw();
        }, Status, Busy);
    }

    private void Draw()
    {
        _drawing = true;
        try
        {
            var find = Find.Text.Trim();
            bool Match(ProviderJson p) =>
                find.Length == 0 ||
                (p.Name + " " + p.Id + " " + p.Preset + " " + p.Host).Contains(find, StringComparison.OrdinalIgnoreCase);

            List.ItemsSource = _data.Providers.Where(p => !p.Off && Match(p)).Select(RowOf).ToList();
            OffList.ItemsSource = _data.Providers.Where(p => p.Off && Match(p)).Select(RowOf).ToList();
            OffFold.Visibility = ((System.Collections.IList)OffList.ItemsSource).Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
            Status.Text = _data.FileError ?? "";
        }
        finally { _drawing = false; }
    }

    private Row RowOf(ProviderJson p)
    {
        var parts = new List<string>();
        if (p.Host.Length > 0) parts.Add(p.Host);
        if (p.Account is { } a && (a.User.Length > 0 || a.Name.Length > 0))
            parts.Add("signed in as " + (a.Name.Length > 0 ? a.Name : a.User));
        else if (p.Key.Set) parts.Add("key " + p.Key.Masked);
        else if (p.Key.Optional) parts.Add("no key needed");
        else parts.Add("no key yet");
        if (p.Models.Count > 0) parts.Add($"{p.Models.Count(m => m.On)} of {p.Models.Count} models");
        else if (p.ListError is { Length: > 0 } why) parts.Add(why);
        if (p.Move is { State: "plugin" }) parts.Add("run by its plugin");
        return new Row
        {
            Title = p.Name,
            Line = string.Join(" · ", parts),
            Tag = p.Id,
            On = !p.Off,
            Icon = IconOf(p.Icon),
        };
    }

    internal static Microsoft.UI.Xaml.Media.ImageSource? IconOf(string icon)
    {
        if (string.IsNullOrEmpty(icon)) return null;
        var app = App.CurrentApp;
        if (app.Client.BaseUrl.Length == 0) return null;
        // the page's own icons, served from the same address
        var name = icon.EndsWith(".svg") || icon.EndsWith(".png") ? icon : icon + ".svg";
        return new BitmapImage(new Uri(app.Client.BaseUrl + "icons/" + Uri.EscapeDataString(name)));
    }

    private void Find_TextChanged(object sender, TextChangedEventArgs e) => Draw();

    private async void Switch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_drawing) return;
        if (sender is not ToggleSwitch { Tag: string id } toggle) return;
        var on = toggle.IsOn;
        await PageKit.RunAsync(async () =>
        {
            // the write answers with the whole list, as every write does
            _data = await App.CurrentApp.Client.PostAsync<ProvidersJson>(
                on ? "provider/on" : "provider/off", new { id }) ?? new ProvidersJson();
            Draw();
        }, Status, Busy);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        // a provider is added from the page's own add sheet, which is drawn
        // there: the sheet, its presets and its sign-ins are the next page
        // to be drawn here (desktop/README.md)
        Status.Text = "Adding a provider is still the page's own sheet: open Providers in magpie web for now.";
    }
}
