using System.Text.Json;

namespace Magpie.Desktop.Backend;

/// <summary>
/// The page's window asks, answered without magpie knowing this app exists.
/// magpie web's host has no windows and no pickers (webHost in
/// internal/gui/web.go): a panel's height is a no-op there, and a folder
/// picker answers "in the browser magpie can't show a folder picker". The app
/// is a window, so it answers them itself — the page is untouched, and so is
/// magpie.
///
///  * a panel's height is read from the page's own scroll height, which is
///    what it asked /api/window/fit for;
///  * a folder picker is answered with Windows' picker, and the page is told
///    the folder as if magpie had answered it.
/// </summary>
public sealed class DesktopChannel : IDisposable
{
    /// <summary>Nothing of the app's is held open between answers: the page's
    /// requests are answered as they come, and the app owns the window.</summary>
    public void Dispose() { }

    /// <summary>The app's window, for the page's panel height and pickers.</summary>
    public Func<Task>? FitPanelAsync;
    /// <summary>A folder is asked for: the folder, "" when cancelled.</summary>
    public Func<string, Task<string>>? ChooseFolderAsync;

    /// <summary>
    /// Answers the page's folder picker when its request reaches the app:
    /// magpie web says it can't, and the app shows the picker instead. The
    /// page's own POST is left unanswered by magpie, and the folder goes back
    /// as its answer.
    /// </summary>
    public static bool IsChoosePath(string url) =>
        url.EndsWith("/api/library/projects/choose", StringComparison.OrdinalIgnoreCase) ||
        url.EndsWith("/api/plugins/choose", StringComparison.OrdinalIgnoreCase);

    public static string ChooseTitle(string url) =>
        url.EndsWith("/api/plugins/choose", StringComparison.OrdinalIgnoreCase)
            ? "Choose a plugin folder"
            : "Choose a project";

    /// <summary>The answer magpie's own handler would have given: {"dir": …}
    /// (library.go, plugins.go).</summary>
    public static string ChooseAnswer(string dir) =>
        JsonSerializer.Serialize(new { dir }, Json.Options);
}
