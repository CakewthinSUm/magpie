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

    /// <summary>The one line a page shows what it is doing or what failed.</summary>
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
        }
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
