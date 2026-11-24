using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TradutorModsStardew;

internal sealed class JunimoAnimation : Control
{
    private const int FrameSize = 16;
    private const int DisplaySize = 64;
    private const int Spacing = 8;
    private const int FrameCount = 4;

    private readonly Bitmap?[] strips = new Bitmap?[5];
    private readonly ImageAttributes[] tints = new ImageAttributes[5];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 180 };
    private AppTheme theme = ThemeStore.Current;
    private int frame;

    public JunimoAnimation()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.FromArgb(10, 31, 53);

        Color[] colors =
        [
            Color.FromArgb(93, 185, 255),  // Blue
            Color.FromArgb(255, 143, 195), // Pink
            Color.FromArgb(150, 224, 105), // Green
            Color.FromArgb(255, 222, 105), // Yellow
            Color.FromArgb(255, 95, 95)    // Red
        ];

        for (int i = 0; i < strips.Length; i++)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", $"Junimo{(i == 4 ? 1 : i + 1)}.png");
            if (File.Exists(path))
            {
                using var source = Image.FromFile(path);
                if (source.Width == FrameCount * FrameSize && source.Height == FrameSize)
                    strips[i] = new Bitmap(source);
            }

            var matrix = new ColorMatrix
            {
                Matrix00 = colors[i].R / 255f,
                Matrix11 = colors[i].G / 255f,
                Matrix22 = colors[i].B / 255f
            };
            tints[i] = new ImageAttributes();
            tints[i].SetColorMatrix(matrix);
        }

        timer.Tick += (_, _) =>
        {
            frame = (frame + 1) % FrameCount;
            Invalidate();
        };
        timer.Start();
    }

    public void ApplyTheme(AppTheme newTheme)
    {
        theme = newTheme;
        BackColor = newTheme.Kind is ThemeKind.Parchment or ThemeKind.Wood or ThemeKind.Garden ? Color.Transparent : newTheme.Background;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        int totalWidth = strips.Length * DisplaySize + (strips.Length - 1) * Spacing;
        int left = Math.Max(0, (Width - totalWidth) / 2);
        int top = theme.Kind is ThemeKind.Parchment or ThemeKind.Wood or ThemeKind.Garden
            ? Math.Max(0, Height - DisplaySize - 2)
            : Math.Max(0, (Height - DisplaySize) / 2 + 2);
        for (int i = 0; i < strips.Length; i++)
        {
            if (strips[i] is not { } strip)
                continue;

            var destination = new Rectangle(left + i * (DisplaySize + Spacing), top, DisplaySize, DisplaySize);
            e.Graphics.DrawImage(strip, destination, frame * FrameSize, 0, FrameSize, FrameSize,
                GraphicsUnit.Pixel, tints[i]);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            foreach (var strip in strips)
                strip?.Dispose();
            foreach (var tint in tints)
                tint.Dispose();
        }
        base.Dispose(disposing);
    }
}
