using System.Runtime.InteropServices;

namespace Magpie.Desktop.Tray;

/// <summary>
/// magpie's icon in the notification area, as the Wails app's tray was
/// (internal/gui/app.go): a click opens the quick panel, a right-click its
/// menu. WinUI 3 has no tray API, so the icon is Windows' own
/// Shell_NotifyIcon, on a message-only window that outlives every frame.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const string ClassName = "MagpieTrayIcon";

    private readonly IntPtr _hwnd;
    private IntPtr _hicon;
    private readonly NativeMethods.WndProc _proc; // held: the class keeps the pointer
    private bool _added;

    /// <summary>The icon was clicked (left, or picked from the keyboard):
    /// the panel opens or closes.</summary>
    public event Action? Activated;
    /// <summary>The icon was right-clicked: its menu opens.</summary>
    public event Action? MenuRequested;

    public TrayIcon(string iconPath, string tooltip)
    {
        _proc = WndProc;
        var instance = NativeMethods.GetModuleHandle(null);
        var wc = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = _proc,
            hInstance = instance,
            lpszClassName = ClassName,
        };
        NativeMethods.RegisterClassEx(ref wc); // already registered on a second icon: fine
        _hwnd = NativeMethods.CreateWindowEx(0, ClassName, "magpie", NativeMethods.WS_OVERLAPPED,
            0, 0, 0, 0, new IntPtr(NativeMethods.HWND_MESSAGE), IntPtr.Zero, instance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("could not make the tray icon's window");
        }

        _hicon = NativeMethods.LoadImage(IntPtr.Zero, iconPath, NativeMethods.IMAGE_ICON,
            0, 0, NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);
        Add(tooltip);
    }

    private void Add(string tooltip)
    {
        var data = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = IconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAY,
            hIcon = _hicon,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
            szInfo = "",
            szInfoTitle = "",
        };
        _added = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
        // said in the log: an icon that doesn't show is the first thing to
        // look at when the tray isn't there
        App.Log($"tray icon added: {_added}, icon handle {_hicon}");
    }

    /// <summary>Changes the tooltip: the version, or what an update waits for.</summary>
    public void SetTooltip(string tooltip)
    {
        if (!_added) return;
        var data = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = IconId,
            uFlags = NativeMethods.NIF_TIP,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
            szInfo = "",
            szInfoTitle = "",
        };
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_TRAY)
        {
            switch ((int)lParam & 0xFFFF)
            {
                case NativeMethods.WM_LBUTTONUP:
                case NativeMethods.NIN_SELECT:
                case NativeMethods.NIN_KEYSELECT:
                    Activated?.Invoke();
                    return IntPtr.Zero;
                case NativeMethods.WM_RBUTTONUP:
                case NativeMethods.WM_CONTEXTMENU:
                    MenuRequested?.Invoke();
                    return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = IconId,
                szTip = "",
                szInfo = "",
                szInfoTitle = "",
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
            _added = false;
        }
        if (_hwnd != IntPtr.Zero) NativeMethods.DestroyWindow(_hwnd);
        if (_hicon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_hicon);
            _hicon = IntPtr.Zero;
        }
    }
}
