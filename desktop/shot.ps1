# Brings the app's window to the front and photographs it: what a change to
# the app's pages is checked against on this machine.
#
#   pwsh desktop/shot.ps1 -Out desktop/shot.png
#
# The app is the one running from this checkout's build (its exe's folder),
# not any other copy on the machine: several checkouts' builds can run at
# once, and each has its own window.
param([string]$Out = "desktop\shot.png")
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
$exe = (Resolve-Path (Join-Path $repo 'desktop\Magpie.Desktop\bin\x64\Debug\net8.0-windows10.0.19041.0')).Path
$name = (Split-Path $exe -Leaf)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Shot {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
# the window's rect and the screen's are in physical pixels: without this the
# script reads a 125%-scaled rect and cuts off the window's right and foot
[void][Shot]::SetProcessDPIAware()

# this checkout's own build, and the one with a window: another checkout's
# copy may run beside it, and only one of them is the one to photograph
$proc = Get-Process -Name 'Magpie.Desktop' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and (Split-Path $_.Path -Parent) -eq $exe -and $_.MainWindowHandle -ne 0 } |
    Select-Object -First 1
if (-not $proc) { throw "no $name with a window is running from $exe" }

$h = $proc.MainWindowHandle
[void][Shot]::ShowWindow($h, 9)   # SW_RESTORE
[void][Shot]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 900

$r = New-Object Shot+RECT
[void][Shot]::GetWindowRect($h, [ref]$r)
$w = $r.Right - $r.Left
$ht = $r.Bottom - $r.Top

Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$path = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $repo $Out }
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "  $path ($w x $ht)"
