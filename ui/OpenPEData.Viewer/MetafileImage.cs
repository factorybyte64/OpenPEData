using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenPEData.Viewer;

/// <summary>Enhanced (emf) and Windows metafiles (wmf, also with the placeable header) as bitmaps, played through GDI as
/// the WinCC runtime does. The size is the frame of the emf header or the bounding box of the placeable header (96 dpi).
/// Pixels the metafile does not draw stay transparent (sample C05: the runtime shows the background there).</summary>
internal static class MetafileImage
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32 { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MetafilePict { public int Mm, XExt, YExt; public IntPtr Hmf; }

    [DllImport("gdi32.dll")] private static extern IntPtr SetEnhMetaFileBits(uint cb, byte[] data);
    [DllImport("gdi32.dll")] private static extern IntPtr SetWinMetaFileBits(uint cb, byte[] data, IntPtr hdcRef, ref MetafilePict mfp);
    [DllImport("gdi32.dll")] private static extern bool DeleteEnhMetaFile(IntPtr h);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool PlayEnhMetaFile(IntPtr hdc, IntPtr hemf, ref Rect32 rect);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();

    /// <summary>Key color of the untouched pixels (0x010203, not used by the test graphics).</summary>
    private const uint Key = 0x00010203;

    public static BitmapSource? Load(byte[] data)
    {
        if (data.Length < 22) return null;
        IntPtr emf;
        int w, h;
        if (BitConverter.ToUInt32(data, 0) == 1 && data.Length >= 88 && BitConverter.ToUInt32(data, 40) == 0x464D4520)
        {
            // ENHMETAHEADER: rclFrame (0.01 mm) at 24, szlDevice (pixels) at 72, szlMillimeters at 80
            var (fl, ft, fr, fb) = (BitConverter.ToInt32(data, 24), BitConverter.ToInt32(data, 28), BitConverter.ToInt32(data, 32), BitConverter.ToInt32(data, 36));
            var (dx, dy) = (BitConverter.ToInt32(data, 72), BitConverter.ToInt32(data, 76));
            var (mx, my) = (BitConverter.ToInt32(data, 80), BitConverter.ToInt32(data, 84));
            if (mx <= 0 || my <= 0) return null;
            w = (int)Math.Round((fr - fl) / 100.0 * dx / mx);
            h = (int)Math.Round((fb - ft) / 100.0 * dy / my);
            emf = SetEnhMetaFileBits((uint)data.Length, data);
        }
        else
        {
            var bits = data;
            w = h = 0;
            if (BitConverter.ToUInt32(data, 0) == 0x9AC6CDD7)
            {
                // placeable header: bounding box (int16 left, top, right, bottom) at 6, units per inch at 14
                var inch = BitConverter.ToUInt16(data, 14);
                if (inch == 0) return null;
                w = (int)Math.Round((BitConverter.ToInt16(data, 10) - BitConverter.ToInt16(data, 6)) * 96.0 / inch);
                h = (int)Math.Round((BitConverter.ToInt16(data, 12) - BitConverter.ToInt16(data, 8)) * 96.0 / inch);
                bits = data[22..];
            }
            if (w <= 0 || h <= 0) return null;
            var mfp = new MetafilePict { Mm = 8, XExt = (int)Math.Round(w * 2540 / 96.0), YExt = (int)Math.Round(h * 2540 / 96.0) };
            emf = SetWinMetaFileBits((uint)bits.Length, bits, IntPtr.Zero, ref mfp);
        }
        if (emf == IntPtr.Zero) return null;
        try
        {
            if (w <= 0 || h <= 0 || w > 8192 || h > 8192) return null;
            return Play(emf, w, h);
        }
        finally
        {
            DeleteEnhMetaFile(emf);
        }
    }

    private static BitmapSource? Play(IntPtr emf, int w, int h)
    {
        var dc = CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) return null;
        var bmi = new BitmapInfoHeader { Size = 40, Width = w, Height = -h, Planes = 1, BitCount = 32 };
        var dib = CreateDIBSection(dc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero)
        {
            DeleteDC(dc);
            return null;
        }
        var old = SelectObject(dc, dib);
        try
        {
            var px = new int[w * h];
            Array.Fill(px, (int)Key);
            Marshal.Copy(px, 0, bits, px.Length);
            var r = new Rect32 { Right = w, Bottom = h };
            PlayEnhMetaFile(dc, emf, ref r);
            GdiFlush();
            Marshal.Copy(bits, px, 0, px.Length);
            for (var i = 0; i < px.Length; i++)
                px[i] = ((uint)px[i] & 0xFFFFFF) == Key ? 0 : (int)((uint)px[i] | 0xFF000000);
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
            bmp.Freeze();
            return bmp;
        }
        finally
        {
            SelectObject(dc, old);
            DeleteObject(dib);
            DeleteDC(dc);
        }
    }
}
