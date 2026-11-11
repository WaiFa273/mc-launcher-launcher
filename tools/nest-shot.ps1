# Compose a clean "nesting" screenshot: capture several LauncherLauncher windows
# individually and stack them diagonally on a plain background, so no desktop,
# file explorer or chat window leaks into the docs image.
# ASCII-only on purpose.
param(
    [string]$ProcessName = "LauncherLauncher",
    [int]$Count = 5,
    [string]$Out = "",
    [int]$StepX = 34,
    [int]$StepY = 30,
    [int]$Margin = 26
)

Add-Type -AssemblyName System.Drawing

$code = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class WinCap
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public class Item
    {
        public IntPtr Handle;
        public int Width;
        public int Height;
        public long Area { get { return (long)Width * Height; } }
    }

    public static List<Item> Collect(int pid)
    {
        List<Item> found = new List<Item>();
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if ((int)p != pid) return true;
            if (!IsWindowVisible(h)) return true;
            // skip tool windows (notify icon host etc.)
            IntPtr owner = GetWindow(h, 4 /*GW_OWNER*/);
            RECT r;
            if (!GetWindowRect(h, out r)) return true;
            int w = r.Right - r.Left;
            int hh = r.Bottom - r.Top;
            if (w < 200 || hh < 150) return true;
            Item it = new Item();
            it.Handle = h;
            it.Width = w;
            it.Height = hh;
            found.Add(it);
            return true;
        }, IntPtr.Zero);
        found.Sort(delegate(Item a, Item b) { return b.Area.CompareTo(a.Area); });
        return found;
    }

    public static bool Grab(IntPtr hWnd, int w, int h, string path)
    {
        using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(w, h))
        {
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                bool ok = PrintWindow(hWnd, hdc, 2);
                g.ReleaseHdc(hdc);
                if (!ok) return false;
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        return true;
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing, System.Windows.Forms
[void][WinCap]::SetProcessDPIAware()

$procs = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
if ($procs.Count -eq 0) { Write-Error "no $ProcessName process"; exit 2 }

$items = @()
foreach ($p in $procs) {
    $found = [WinCap]::Collect($p.Id)
    foreach ($f in $found) { $items += $f }
}
if ($items.Count -eq 0) { Write-Error "no windows"; exit 3 }
$items = $items | Sort-Object -Property Area -Descending
if ($items.Count -gt $Count) { $items = $items[0..($Count - 1)] }

$tmp = Join-Path $env:TEMP ("nestshot-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

$shots = @()
$i = 0
foreach ($it in $items) {
    $path = Join-Path $tmp ("w" + $i + ".png")
    if ([WinCap]::Grab($it.Handle, $it.Width, $it.Height, $path)) {
        $shots += [pscustomobject]@{ Path = $path; W = $it.Width; H = $it.Height }
    }
    $i++
}
if ($shots.Count -eq 0) { Write-Error "capture failed"; exit 4 }

$canvasW = 0
$canvasH = 0
for ($k = 0; $k -lt $shots.Count; $k++) {
    $x = $Margin + $k * $StepX + $shots[$k].W
    $y = $Margin + $k * $StepY + $shots[$k].H
    if ($x -gt $canvasW) { $canvasW = $x }
    if ($y -gt $canvasH) { $canvasH = $y }
}
$canvasW += $Margin
$canvasH += $Margin

$canvas = New-Object System.Drawing.Bitmap($canvasW, $canvasH)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(237, 241, 248))

$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(38, 0, 0, 0), 1)

for ($k = 0; $k -lt $shots.Count; $k++) {
    $img = [System.Drawing.Image]::FromFile($shots[$k].Path)
    $x = $Margin + $k * $StepX
    $y = $Margin + $k * $StepY
    $g.DrawImage($img, $x, $y, $img.Width, $img.Height)
    $g.DrawRectangle($pen, $x, $y, $img.Width - 1, $img.Height - 1)
    $img.Dispose()
}
$g.Dispose()
$pen.Dispose()

if (-not $Out) { $Out = Join-Path $PSScriptRoot "..\docs\nesting.png" }
$canvas.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()
Remove-Item $tmp -Recurse -Force

Write-Host ("composed " + $shots.Count + " windows -> " + $Out + " (" + $canvasW + "x" + $canvasH + ")")
