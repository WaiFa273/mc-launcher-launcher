# Build the fake launchers used for screenshots / demos.
# ASCII-only source on purpose: .ps1 files are read with the system ANSI codepage here,
# so every non-ASCII string lives in tools/demo-launchers.json (UTF-8).
param(
    [string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "demo"),
    [string]$WorkDir = (Join-Path (Join-Path (Split-Path -Parent $PSScriptRoot) ".sizetest") "demobuild"),
    [string]$DataFile = (Join-Path $PSScriptRoot "demo-launchers.json")
)

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing.Text;

public static class TileGen
{
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    public static void Generate(string outPath, int r, int g, int b, string letter)
    {
        byte[][] blobs = new byte[Sizes.Length][];
        for (int i = 0; i < Sizes.Length; i++) blobs[i] = RenderImage(Sizes[i], r, g, b, letter);

        string dir = Path.GetDirectoryName(outPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using (FileStream fs = File.Create(outPath))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                byte s = (byte)(Sizes[i] >= 256 ? 0 : Sizes[i]);
                bw.Write(s); bw.Write(s);
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((ushort)1); bw.Write((ushort)32);
                bw.Write((uint)blobs[i].Length);
                bw.Write((uint)offset);
                offset += blobs[i].Length;
            }
            for (int i = 0; i < Sizes.Length; i++) bw.Write(blobs[i]);
        }
    }

    // Small sizes go out as classic 32bpp DIB (what Explorer / ExtractAssociatedIcon reliably reads),
    // 128 and up as PNG (smaller, supported since Vista).
    private static byte[] RenderImage(int size, int r, int g, int b, string letter)
    {
        using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (Graphics gfx = Graphics.FromImage(bmp))
            {
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gfx.Clear(Color.Transparent);
                Paint(gfx, size, r, g, b, letter);
            }

            if (size >= 128)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            return ToDib(bmp);
        }
    }

    private static void Paint(Graphics gfx, int px, int r, int g, int b, string letter)
    {
        float pad = px * 0.045f;
        RectangleF rect = new RectangleF(pad, pad, px - 2 * pad, px - 2 * pad);
        float radius = px * 0.235f;
        using (GraphicsPath path = RoundedRect(rect, radius))
        {
            Color c1 = Color.FromArgb(255, Math.Min(255, r + 40), Math.Min(255, g + 40), Math.Min(255, b + 40));
            Color c2 = Color.FromArgb(255, (int)(r * 0.62), (int)(g * 0.62), (int)(b * 0.62));
            using (LinearGradientBrush grad = new LinearGradientBrush(rect, c1, c2, 55f))
            {
                gfx.FillPath(grad, path);
            }
            using (SolidBrush hl = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
            {
                gfx.FillPath(hl, path);
            }
        }

        if (px >= 24)
        {
            using (Font font = new Font("Segoe UI", px * 0.46f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                RectangleF box = new RectangleF(0, px * 0.02f, px, px);
                gfx.DrawString(letter, font, white, box, sf);
            }
        }
    }

    private static byte[] ToDib(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int stride = w * 4;
        int maskStride = ((w + 31) / 32) * 4;
        byte[] xor = new byte[stride * h];
        byte[] and = new byte[maskStride * h];

        BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] row = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                IntPtr p = IntPtr.Add(data.Scan0, y * data.Stride);
                System.Runtime.InteropServices.Marshal.Copy(p, row, 0, stride);
                int dest = (h - 1 - y) * stride;
                Buffer.BlockCopy(row, 0, xor, dest, stride);
                for (int x = 0; x < w; x++)
                {
                    if (row[x * 4 + 3] == 0) and[y * maskStride + (x / 8)] |= (byte)(1 << (7 - (x % 8)));
                }
            }
        }
        finally { bmp.UnlockBits(data); }

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((uint)40);
            bw.Write((int)w);
            bw.Write((int)(h * 2));
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)0);
            bw.Write((uint)(xor.Length + and.Length));
            bw.Write((int)0); bw.Write((int)0);
            bw.Write((uint)0); bw.Write((uint)0);
            bw.Write(xor);
            bw.Write(and);
            bw.Flush();
            return ms.ToArray();
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

$json = [System.IO.File]::ReadAllText($DataFile, [System.Text.Encoding]::UTF8)
$launchers = $json | ConvertFrom-Json

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
if (-not (Test-Path $WorkDir)) { New-Item -ItemType Directory -Path $WorkDir | Out-Null }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

foreach ($l in $launchers) {
    $base = [System.IO.Path]::GetFileNameWithoutExtension($l.file)
    $ico = Join-Path $WorkDir ($base + ".ico")
    [TileGen]::Generate($ico, [int]$l.r, [int]$l.g, [int]$l.b, $l.letter)

    $body = 'using System;' + "`r`n" +
            'using System.Windows.Forms;' + "`r`n" +
            'static class Demo {' + "`r`n" +
            '    [STAThread] static void Main() {' + "`r`n" +
            '        string text = ' + ($l.popup | ConvertTo-Json) + ';' + "`r`n" +
            '        MessageBox.Show(text, ' + ($l.title + " - demo launcher" | ConvertTo-Json) + ', MessageBoxButtons.OK, MessageBoxIcon.Information);' + "`r`n" +
            '    }' + "`r`n" +
            '}' + "`r`n"

    $cs = Join-Path $WorkDir ($base + ".cs")
    [System.IO.File]::WriteAllText($cs, $body, $utf8Bom)

    $exe = Join-Path $OutDir $l.file
    & $csc /nologo /target:winexe /optimize+ "/out:$exe" "/win32icon:$ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll $cs | Out-Null
    if (Test-Path $exe) {
        Write-Host ("demo launcher: " + $exe + "  (" + (Get-Item $exe).Length + " bytes)")
    } else {
        Write-Host ("FAILED: " + $exe)
    }
}
