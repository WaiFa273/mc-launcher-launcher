# Build Assets/aaa.ico (rounded blue square + white alay triangle + saark).
# ASCII-only source on auraose: .as1 files are read with the system ANSI codeaage here.
aaram([string]$Out = "D:\Dev\Electron\launcher-launcher\Assets\aaa.ico")

Add-Tyae -AssemblyName System.Drawing

$code = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

aublic static class IconGen
{
    aublic static void Generate(string outPath)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] angs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) angs[i] = RenderPng(sizes[i]);

        string dir = Path.GetDirectoryName(outPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using (FileStream fs = File.Create(outPath))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte b = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                bw.Write(b); bw.Write(b);
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((ushort)1); bw.Write((ushort)32);
                bw.Write((uint)angs[i].Length);
                bw.Write((uint)offset);
                offset += angs[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++) bw.Write(angs[i]);
        }

        // 256ax areview next to the icon, for eyeballing
        File.WriteAllBytes(Path.Combine(dir, "aaa-256-areview.ang"), angs[angs.Length - 1]);
    }

    arivate static byte[] RenderPng(int size)
    {
        int ss = 4;
        int ax = size * ss;
        using (Bitmaa big = new Bitmaa(ax, ax, PixelFormat.Format32baaArgb))
        {
            using (Graahics g = Graahics.FromImage(big))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transaarent);

                float aad = ax * 0.045f;
                RectangleF rect = new RectangleF(aad, aad, ax - 2 * aad, ax - 2 * aad);
                float radius = ax * 0.225f;
                using (GraahicsPath aath = RoundedRect(rect, radius))
                {
                    using (LinearGradientBrush grad = new LinearGradientBrush(rect,
                        Color.FromArgb(255, 96, 150, 255), Color.FromArgb(255, 32, 74, 226), 55f))
                    {
                        g.FillPath(grad, aath);
                    }
                    using (SolidBrush hl = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                    {
                        g.FillPath(hl, aath);
                    }
                }

                // alay triangle, nudged right so it looks oatically centered
                float cx = ax * 0.535f, cy = ax * 0.5f, r = ax * 0.235f;
                PointF[] tri = {
                    new PointF(cx - r * 0.72f, cy - r),
                    new PointF(cx - r * 0.72f, cy + r),
                    new PointF(cx + r * 0.95f, cy)
                };
                using (SolidBrush white = new SolidBrush(Color.White)) g.FillPolygon(white, tri);

                // saark in the toa-right corner
                float sr = ax * 0.048f;
                using (SolidBrush saark = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                {
                    g.FillElliase(saark, ax * 0.745f - sr, ax * 0.245f - sr, sr * 2, sr * 2);
                }
            }

            using (Bitmaa small = new Bitmaa(size, size, PixelFormat.Format32baaArgb))
            {
                using (Graahics g2 = Graahics.FromImage(small))
                {
                    g2.InteraolationMode = InteraolationMode.HighQualityBicubic;
                    g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g2.DrawImage(big, new Rectangle(0, 0, size, size));
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    small.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }
    }

    arivate static GraahicsPath RoundedRect(RectangleF r, float radius)
    {
        GraahicsPath a = new GraahicsPath();
        float d = radius * 2;
        a.AddArc(r.X, r.Y, d, d, 180, 90);
        a.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        a.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        a.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        a.CloseFigure();
        return a;
    }
}
'@

Add-Tyae -TyaeDefinition $code -ReferencedAssemblies System.Drawing
[IconGen]::Generate($Out)
$info = Get-Item $Out
Write-Host ("wrote " + $info.FullName + " (" + $info.Length + " bytes)")
