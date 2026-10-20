using Microsoft.Win32;

namespace Magpie.Desktop;

/// <summary>
/// <c>magpie://</c> links, as the Wails app's were (internal/gui/scheme_windows.go):
/// a URL under HKCU's Classes pointing at this executable, so a link a page,
/// a chat or the site hands the user opens the app. The link arrives as the
/// process's argument, as it did there.
///
/// The Wails app registered its own magpie.exe; this one registers itself,
/// and the two never both sit in the key — the app's registration replaces
/// it, as turning one on replaces the other.
/// </summary>
internal static class Scheme
{
    private const string Key = @"Software\Classes\magpie";

    /// <summary>Registers this exe as <c>magpie://</c>'s handler. Called at
    /// start, as the Wails app did: a copy that moved runs from its new place
    /// from then on.</summary>
    public static void Register()
    {
        try
        {
            var exe = Environment.ProcessPath ?? "";
            using var key = Registry.CurrentUser.CreateSubKey(Key, writable: true)
                ?? throw new InvalidOperationException("could not open the magpie key");
            key.SetValue("", "URL:magpie", RegistryValueKind.String);
            key.SetValue("URL Protocol", "", RegistryValueKind.String);
            using (var icon = key.CreateSubKey("DefaultIcon"))
            {
                icon?.SetValue("", "\"" + exe + "\",0", RegistryValueKind.String);
            }
            using (var command = key.CreateSubKey(@"shell\open\command"))
            {
                command?.SetValue("", "\"" + exe + "\" \"%1\"", RegistryValueKind.String);
            }
        }
        catch (Exception e)
        {
            App.Log("magpie:// links: " + e.Message);
        }
    }

    /// <summary>The link this process was started with, or "": a
    /// <c>magpie://</c> argument, which Windows hands as the first thing
    /// after the exe (quoted, so a query string survives).</summary>
    public static string FromArgs(string[] args)
    {
        foreach (var arg in args.Skip(1))
        {
            if (arg.StartsWith("magpie://", StringComparison.OrdinalIgnoreCase)) return arg;
        }
        return "";
    }
}