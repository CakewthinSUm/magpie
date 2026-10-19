# Brings the app's window to the front and photographs it: what a change to
# the app's pages is checked against on this machine.
param([string]$Out = "desktop\shot.png")
$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Shot {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$proc = Get-Process -Name 'Magpie.Desktop' -ErrorAction Stop
$h = $proc.MainWindowHandle
if ($h -eq 0) { throw "the app has no window" }
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
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "  $Out ($w x $ht)"
