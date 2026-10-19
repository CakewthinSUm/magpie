using System.Runtime.InteropServices;

namespace Magpie.Desktop;

/// <summary>
/// A second launch of the app hands over to the one running: its window comes
/// up, as the Wails app's SingleInstance did. The running app listens on a
/// named event; the second one sets it and goes.
///
/// The name carries the folder the app runs from, so two copies — two
/// checkouts' builds, a portable one beside an installed one — are two apps
/// and neither brings the other's window up. The Wails app's own single
/// instance was per program, not per computer, for the same reason.
/// </summary>
internal static class SingleInstance
{
    /// <summary>The app's own name: the folder it runs from, made safe for a
    /// kernel object's name (a backslash is the only one that matters, and
    /// case does not: Windows names are case-insensitive).</summary>
    public static string Key { get; } =
        "magpie-desktop-" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    (Path.GetDirectoryName(Environment.ProcessPath) ?? "").ToLowerInvariant())))[..16];

    private static string EventName => @"Local\" + Key + "-show";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEvent(IntPtr attrs, bool manualReset, bool initialState, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetEvent(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint millis);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private const uint INFINITE = 0xFFFFFFFF;
    private static IntPtr _handle;

    /// <summary>Listens for a second launch, on a thread of its own: the app
    /// running now brings its window up when one comes.</summary>
    public static void Listen(Action show)
    {
        _handle = CreateEvent(IntPtr.Zero, false, false, EventName);
        if (_handle == IntPtr.Zero) return;
        var t = new Thread(() =>
        {
            while (true)
            {
                WaitForSingleObject(_handle, INFINITE);
                show();
            }
        })
        { IsBackground = true, Name = "magpie single instance" };
        t.Start();
    }

    /// <summary>A second launch: wake the running app, which brings its window
    /// up, and go.</summary>
    public static void ShowRunning()
    {
        var h = CreateEvent(IntPtr.Zero, false, false, EventName);
        if (h == IntPtr.Zero) return;
        SetEvent(h);
        CloseHandle(h);
    }
}
