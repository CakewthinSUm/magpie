using Microsoft.UI.Xaml;

namespace Magpie.Desktop.Pages;

/// <summary>
/// One of a page's own tabs, as the sidebar lists it under the page's row:
/// the id the page and its view name use ("requests"), the English word the
/// page says (a key in i18n.js, translated by L.T), and what the row's badge
/// shows — the number of things the tab lists ("Installed · 3"), and how many
/// of them have an update, which the sidebar marks in the accent colour.
/// </summary>
public sealed record PageTab(string Id, string Label, int? Count = null, int Updates = 0);

/// <summary>
/// A page whose tabs the window's sidebar lists, as magpie's own page lists
/// them under the open page when the window is wide enough (app.js's
/// navSubs). The sidebar shows them; the page's own strip of tabs is hidden
/// while it does, and comes back when the pane is too narrow for the
/// sidebar, exactly as the page does.
/// </summary>
public interface ITabbed
{
    /// <summary>Its tabs, in the page's own order. (Not <c>Tabs</c>: a page's
    /// XAML names its own strip of them that, and a page is one class.)</summary>
    IReadOnlyList<PageTab> TabList { get; }

    /// <summary>The tab shown now, by its id.</summary>
    string Tab { get; }

    /// <summary>Shows a tab, as clicking it in the page's own strip does. An
    /// id the page has no tab for is left alone.</summary>
    void SelectTab(string id);

    /// <summary>The page's own strip of tabs, hidden while the sidebar lists
    /// them in its place.</summary>
    FrameworkElement Strip { get; }

    /// <summary>Raised when what a tab says changed (its count, an update):
    /// the sidebar draws the list again.</summary>
    event EventHandler? TabsChanged;
}

/// <summary>What the window needs of a page to list its tabs, as a plain
/// call: a page without tabs says so.</summary>
public static class TabsOf
{
    /// <summary>The tab a view names ("usage&amp;tab=requests"), or "" when it
    /// names none.</summary>
    public static string Named(string view)
    {
        foreach (var part in view.Split('&'))
        {
            if (part.StartsWith("tab=", StringComparison.Ordinal) && part.Length > 4) return part[4..];
        }
        return "";
    }

    /// <summary>The view without the tab it names: "usage&amp;tab=requests"
    /// is "usage".</summary>
    public static string Page(string view) => view.Split('&')[0];

    /// <summary>The view a tab of a page is: "usage" and "requests" are
    /// "usage&amp;tab=requests".</summary>
    public static string View(string page, string tab) => page + "&tab=" + tab;
}
