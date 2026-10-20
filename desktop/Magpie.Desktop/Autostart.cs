using Microsoft.Win32;

namespace Magpie.Desktop;

/// <summary>
/// Open at login, as this app: a Run value under HKCU pointing at this
/// executable, in the tray with no window. The backend's own
/// internal/autostart writes a value for magpie.exe — the Go program beside
/// this one, which has no window of its own to open — so the app keeps a
/// record of its own rather than writing over that one.
///
/// Its value name is "magpie-desktop", not the backend's "magpie": an
/// installed magpie the user already opens at login keeps its own value,
/// and turning this one on never takes that away. Both can be on at once,
/// and each starts what it names.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "magpie-desktop";
    /// <summary>Task Manager's Startup apps keeps its switched-off marker
    /// here; an odd first byte is off.</summary>
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>The command line the Run value holds: this exe, and the flag
    /// that keeps the window away at login (the tray alone, as magpie's own
    /// autostart runs it).</summary>
    private static string Command()
    {
        var exe = Environment.ProcessPath ?? "";
        return "\"" + exe + "\" tray";
    }

    /// <summary>Whether the app opens at login: the Run value is this exe's,
    /// and Task Manager hasn't switched it off.</summary>
    public static bool Enabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var value = key?.GetValue(Name) as string;
            if (string.IsNullOrEmpty(value)) return false;
            // another copy's magpie.exe (or the backend's own) is not this app
            if (!value.Contains(Environment.ProcessPath ?? "\0", StringComparison.OrdinalIgnoreCase)) return false;
            return !SwitchedOff();
        }
        catch (Exception)
        {
            // a Run key that can't be read is not a no: Windows may still be
            // starting it, and Set is what says so when it can't be turned off
            return true;
        }
    }

    private static bool SwitchedOff()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            if (key?.GetValue(Name) is not byte[] { Length: > 0 } bytes) return false;
            return bytes[0] % 2 == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Has the app open at login, or not. Turning it on replaces
    /// another copy's value (the backend's own), so only one starts.</summary>
    public static void Set(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("could not open the Run key");
        if (on)
        {
            key.SetValue(Name, Command(), RegistryValueKind.String);
            // Task Manager's marker, if one was left off, would keep it from
            // starting: turning it on is the user saying so
            ClearSwitchedOff();
        }
        else
        {
            key.DeleteValue(Name, throwOnMissingValue: false);
        }
    }

    private static void ClearSwitchedOff()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            if (key?.GetValue(Name) is byte[] { Length: > 0 } bytes)
            {
                bytes[0] = (byte)(bytes[0] & ~1);
                key.SetValue(Name, bytes, RegistryValueKind.Binary);
            }
        }
        catch (Exception) { /* the marker stays; the value is still written */ }
    }
}