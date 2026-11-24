using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TradutorModsStardew;
using DrawingColor = System.Drawing.Color;

namespace TradutorModsDeck;

internal sealed class JunimoDisplay : Control
{
    private readonly WriteableBitmap?[] strips = new WriteableBitmap?[5];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private AppTheme theme = ThemeStore.Current;
    private int frame;

    public JunimoDisplay()
    {
        ClipToBounds = true;
        var colors = new[]
        {
            DrawingColor.FromArgb(93, 185, 255), DrawingColor.FromArgb(255, 143, 195),
            DrawingColor.FromArgb(150, 224, 105), DrawingColor.FromArgb(255, 222, 105),
            DrawingColor.FromArgb(255, 95, 95)
        };
        for (int i = 0; i < strips.Length; i++)
        {
            var path = DeckAssets.PathFor($"Junimo{(i == 4 ? 1 : i + 1)}.png");
            if (File.Exists(path)) strips[i] = Tint(path, colors[i]);
        }
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        timer.Tick += (_, _) => { frame = (frame + 1) % 4; InvalidateVisual(); };
        timer.Start();
    }

    public void ApplyTheme(AppTheme value) { theme = value; InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        const int displaySize = 64, spacing = 8;
        double left = Math.Max(0, (Bounds.Width - 5 * displaySize - 4 * spacing) / 2);
        double top = Math.Max(0, (Bounds.Height - displaySize) / 2 + 2);
        for (int i = 0; i < strips.Length; i++)
            if (strips[i] is { } strip)
                context.DrawImage(strip, new Rect(frame * 16, 0, 16, 16),
                    new Rect(left + i * (displaySize + spacing), top, displaySize, displaySize));
    }

    private static WriteableBitmap? Tint(string path, DrawingColor tint)
    {
        try
        {
            using var source = new Bitmap(path);
            if (source.PixelSize.Width != 64 || source.PixelSize.Height != 16) return null;
            var result = new WriteableBitmap(source.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using var pixels = result.Lock();
            source.CopyPixels(pixels, AlphaFormat.Unpremul);
            var line = new byte[source.PixelSize.Width * 4];
            for (int y = 0; y < source.PixelSize.Height; y++)
            {
                var address = IntPtr.Add(pixels.Address, y * pixels.RowBytes);
                Marshal.Copy(address, line, 0, line.Length);
                for (int x = 0; x < line.Length; x += 4)
                {
                    line[x] = (byte)(line[x] * tint.B / 255);
                    line[x + 1] = (byte)(line[x + 1] * tint.G / 255);
                    line[x + 2] = (byte)(line[x + 2] * tint.R / 255);
                }
                Marshal.Copy(line, 0, address, line.Length);
            }
            return result;
        }
        catch { return null; }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        foreach (var strip in strips) strip?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}

internal sealed class SlimeDisplay : Control
{
    private readonly Bitmap? sheet;
    private readonly WriteableBitmap?[] tinted = new WriteableBitmap?[5];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private int frame;

    public SlimeDisplay()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        var path = DeckAssets.PathFor("Slime.png");
        try { if (File.Exists(path)) sheet = new Bitmap(path); }
        catch { }
        if (sheet is not null && sheet.PixelSize.Width == 64 && sheet.PixelSize.Height == 280)
        {
            DrawingColor[] colors =
            [
                DrawingColor.FromArgb(255, 75, 76), DrawingColor.FromArgb(178, 113, 247),
                DrawingColor.FromArgb(112, 218, 85), DrawingColor.FromArgb(88, 171, 255),
                DrawingColor.FromArgb(255, 145, 195)
            ];
            for (int i = 0; i < tinted.Length; i++) tinted[i] = TintSheet(sheet, colors[i]);
        }
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        timer.Tick += (_, _) => { frame = (frame + 1) % 4; InvalidateVisual(); };
        timer.Start();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (sheet is null) return;
        const int displayWidth = 64, displayHeight = 96, spacing = 8;
        double left = Math.Max(0, (Bounds.Width - 5 * displayWidth - 4 * spacing) / 2);
        double top = Math.Max(0, Bounds.Height - displayHeight - 3);
        for (int i = 0; i < tinted.Length; i++)
        {
            if (tinted[i] is not { } colored) continue;
            double x = left + i * (displayWidth + spacing);
            context.DrawImage(colored, new Rect(frame * 16, 0, 16, 24),
                new Rect(x, top, displayWidth, displayHeight));
            context.DrawImage(colored, new Rect(frame * 16, 168, 16, 24),
                new Rect(x + 20, top, displayWidth, displayHeight));
            int faceOffset = frame switch { 0 => 7, 1 => 6, 2 => 5, _ => 6 };
            context.DrawImage(sheet, new Rect(i is 1 or 4 ? 48 : 32, 128, 16, 24),
                new Rect(x, top + faceOffset * 4, displayWidth, displayHeight));
        }
    }

    private static WriteableBitmap TintSheet(Bitmap source, DrawingColor tint)
    {
        var result = new WriteableBitmap(source.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var pixels = result.Lock();
        source.CopyPixels(pixels, AlphaFormat.Unpremul);
        var line = new byte[source.PixelSize.Width * 4];
        for (int y = 0; y < source.PixelSize.Height; y++)
        {
            var address = IntPtr.Add(pixels.Address, y * pixels.RowBytes);
            Marshal.Copy(address, line, 0, line.Length);
            for (int x = 0; x < line.Length; x += 4)
            {
                line[x] = (byte)(line[x] * tint.B / 255);
                line[x + 1] = (byte)(line[x + 1] * tint.G / 255);
                line[x + 2] = (byte)(line[x + 2] * tint.R / 255);
            }
            Marshal.Copy(line, 0, address, line.Length);
        }
        return result;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        sheet?.Dispose();
        foreach (var image in tinted) image?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}

internal sealed class LeafLayer : Control
{
    // Sprite indices 5 through 10, then back without repeating the endpoints.
    private static readonly int[] GreenSwayFrames = [5, 6, 7, 8, 9, 10, 9, 8, 7, 6];
    private static readonly int[] PinkSwayFrames = GreenSwayFrames;
    private readonly Bitmap? greenSheet;
    private readonly Bitmap? pinkSheet;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Random random = new(1739);
    private readonly Leaf[] leaves = new Leaf[24];
    private int tick;
    private bool enabled;

    private struct Leaf
    {
        public double X, Y, Speed, Phase;
        public int Frame, SwayStep, SpinSteps;
        public bool Spinning, Pink;
    }

    public LeafLayer()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        SizeChanged += (_, _) =>
        {
            if (enabled && leaves.All(leaf => leaf.X == 0))
                for (int i = 0; i < leaves.Length; i++) leaves[i] = NewLeaf(random.Next(Math.Max(1, (int)Bounds.Height)), i >= 12);
        };
        greenSheet = LoadSheet("green_debris.png", 11);
        pinkSheet = LoadSheet("pink_debris.png", 11);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        timer.Tick += (_, _) =>
        {
            if (!enabled) return;
            tick++;
            for (int i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
                int frameCount = 11;
                int[] swayFrames = leaf.Pink ? PinkSwayFrames : GreenSwayFrames;
                leaf.Phase += .07;
                leaf.X += .12 + Math.Sin(leaf.Phase) * .65;
                leaf.Y += leaf.Speed;
                if (tick % 2 == 0)
                {
                    if (leaf.Spinning)
                    {
                        leaf.Frame = (leaf.Frame + 1) % frameCount;
                        if (++leaf.SpinSteps == frameCount) { leaf.Spinning = false; leaf.SpinSteps = 0; leaf.Frame = swayFrames[leaf.SwayStep]; }
                    }
                    else if (tick % 4 == 0)
                    {
                        leaf.SwayStep = (leaf.SwayStep + 1) % swayFrames.Length;
                        leaf.Frame = swayFrames[leaf.SwayStep];
                        if (leaf.SwayStep == 0 && random.NextDouble() < .0005)
                            leaf.Spinning = true;
                    }
                }
                leaves[i] = leaf.Y > Bounds.Height + 48 ? NewLeaf(-random.Next(48, 138), leaf.Pink) : leaf;
            }
            InvalidateVisual();
        };
    }

    private static Bitmap? LoadSheet(string name, int frameCount)
    {
        var path = DeckAssets.PathFor(name);
        try
        {
            if (!File.Exists(path)) return null;
            var bitmap = new Bitmap(path);
            if (bitmap.PixelSize.Width == 16 * frameCount && bitmap.PixelSize.Height == 16) return bitmap;
            bitmap.Dispose();
        }
        catch { }
        return null;
    }

    public void ApplyTheme(AppTheme theme)
    {
        enabled = theme.Kind == ThemeKind.Garden;
        if (enabled)
        {
            for (int i = 0; i < leaves.Length; i++) leaves[i] = NewLeaf(random.Next(Math.Max(1, (int)Bounds.Height)), i >= 12);
            timer.Start();
        }
        else timer.Stop();
        InvalidateVisual();
    }

    private Leaf NewLeaf(double y, bool pink)
    {
        int[] swayFrames = pink ? PinkSwayFrames : GreenSwayFrames;
        int swayStep = random.Next(swayFrames.Length);
        return new Leaf
        {
            Pink = pink,
            X = random.Next(Math.Max(1, (int)Bounds.Width)), Y = y,
            Speed = .7 + random.NextDouble() * .8, Phase = random.NextDouble() * Math.Tau,
            SwayStep = swayStep, Frame = swayFrames[swayStep]
        };
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!enabled || greenSheet is null && pinkSheet is null) return;
        using (context.PushOpacity(.68))
            foreach (var leaf in leaves)
            {
                var sheet = leaf.Pink ? pinkSheet : greenSheet;
                if (sheet is not null)
                    context.DrawImage(sheet, new Rect(leaf.Frame * 16, 0, 16, 16), new Rect(leaf.X, leaf.Y, 48, 48));
            }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        timer.Stop();
        greenSheet?.Dispose();
        pinkSheet?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}

internal static class DeckAssets
{
    public static string PathFor(string fileName) => Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
}
