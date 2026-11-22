using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenPEData.Viewer;

/// <summary>Renders a window to PNG (command line option --snapshot, used for documentation and checks).</summary>
public static class Snapshot
{
    public static void Save(Window w, string file)
    {
        var content = (FrameworkElement)w.Content;
        var dpi = VisualTreeHelper.GetDpi(w);
        var bmp = new RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var ctx = dv.RenderOpen())
        {
            var size = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
            ctx.DrawRectangle(Brushes.White, null, size);
            ctx.DrawRectangle(new VisualBrush(content), null, size);
        }
        bmp.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(file);
        enc.Save(fs);
    }
}
