# Click at an image coordinate inside a window (coordinates as seen in shot.ps1 output).
# ASCII-only on purpose.
param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [Parameter(Mandatory=$true)][int]$X,
    [Parameter(Mandatory=$true)][int]$Y,
    [switch]$Double,
    [switch]$Right,
    [switch]$NoTopMost
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[void][M]::SetProcessDPIAware()

$p = Get-Process -Id $ProcessId -ErrorAction Stop
for ($i = 0; $i -lt 20 -and $p.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 200; $p.Refresh() }
$h = $p.MainWindowHandle
if ($h -eq 0) { Write-Error "no main window"; exit 2 }

# Foreground stealing is blocked for background processes, so pin the window on top while clicking.
$HWND_TOPMOST = [IntPtr](-1)
$HWND_NOTOPMOST = [IntPtr](-2)
$SWP_NOMOVE = 0x0002
$SWP_NOSIZE = 0x0001
$SWP_SHOWWINDOW = 0x0040

[void][M]::ShowWindow($h, 9)  # SW_RESTORE
[void][M]::SetWindowPos($h, $HWND_TOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_SHOWWINDOW)
[void][M]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 300

$r = New-Object M+RECT
[void][M]::GetWindowRect($h, [ref]$r)
$sx = $r.Left + $X
$sy = $r.Top + $Y
[void][M]::SetCursorPos($sx, $sy)
Start-Sleep -Milliseconds 150

$down = 0x0002; $up = 0x0004
if ($Right) { $down = 0x0008; $up = 0x0010 }

[void][M]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 50
[void][M]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)

if ($Double) {
    Start-Sleep -Milliseconds 90
    [void][M]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [void][M]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)
}

Start-Sleep -Milliseconds 300
if (-not $NoTopMost) {
    [void][M]::SetWindowPos($h, $HWND_NOTOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE)
}

Write-Host ("clicked window (" + $X + "," + $Y + ") -> screen (" + $sx + "," + $sy + ")")
