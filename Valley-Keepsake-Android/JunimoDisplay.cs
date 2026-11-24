using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace ValleyKeepsake.Android;

internal sealed class JunimoDisplay : Control
{
    public double MaxSpriteSize { get; set; } = 64;
    private readonly WriteableBitmap?[] sprites = new WriteableBitmap?[5];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private int frame;

    public JunimoDisplay()
    {
        Height = 76;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        var colors = new (byte R, byte G, byte B)[]
        {
            (93, 185, 255), (255, 143, 195), (150, 224, 105),
            (255, 222, 105), (255, 95, 95)
        };
        for (var i = 0; i < sprites.Length; i++)
            sprites[i] = Tint(i == 4 ? 1 : i + 1, colors[i]);
        timer.Tick += (_, _) => { frame = (frame + 1) % 4; InvalidateVisual(); };
        AttachedToVisualTree += (_, _) => timer.Start();
        DetachedFromVisualTree += (_, _) => timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var size = Math.Min(MaxSpriteSize, Math.Max(32, (Bounds.Width - 32) / 5));
        var spacing = Math.Min(8, Math.Max(2, (Bounds.Width - size * 5) / 4));
        var left = (Bounds.Width - (size * 5 + spacing * 4)) / 2;
        var top = (Bounds.Height - size) / 2;
        for (var i = 0; i < sprites.Length; i++)
            if (sprites[i] is { } sprite)
                context.DrawImage(sprite, new Rect(frame * 16, 0, 16, 16),
                    new Rect(left + i * (size + spacing), top, size, size));
    }

    private static WriteableBitmap? Tint(int number, (byte R, byte G, byte B) color)
    {
        try
        {
            var uri = new Uri($"avares://ValleyKeepsake.Android/Assets/Junimo{number}.png");
            using var input = AssetLoader.Open(uri);
            using var source = new Bitmap(input);
            if (source.PixelSize.Width != 64 || source.PixelSize.Height != 16) return null;
            var result = new WriteableBitmap(source.PixelSize, new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using var pixels = result.Lock();
            source.CopyPixels(new PixelRect(0, 0, source.PixelSize.Width, source.PixelSize.Height),
                pixels.Address, pixels.RowBytes * source.PixelSize.Height, pixels.RowBytes);
            var line = new byte[source.PixelSize.Width * 4];
            for (var y = 0; y < source.PixelSize.Height; y++)
            {
                var address = IntPtr.Add(pixels.Address, y * pixels.RowBytes);
                Marshal.Copy(address, line, 0, line.Length);
                for (var x = 0; x < line.Length; x += 4)
                {
                    line[x] = (byte)(line[x] * color.B / 255);
                    line[x + 1] = (byte)(line[x + 1] * color.G / 255);
                    line[x + 2] = (byte)(line[x + 2] * color.R / 255);
                }
                Marshal.Copy(line, 0, address, line.Length);
            }
            return result;
        }
        catch { return null; }
    }
}
