using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TradutorModsStardew;

internal sealed class SlimeAnimation : Control
{
    private const int CellWidth = 16;
    private const int CellHeight = 24;
    private const int PixelScale = 4;
    private const int Spacing = 8;
    private readonly Bitmap? sheet;
    private readonly ImageAttributes[] tints = new ImageAttributes[5];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 180 };
    private AppTheme theme = ThemeStore.Current;
    private int frame;

    public SlimeAnimation()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Slime.png");
        if (File.Exists(path))
        {
            try { sheet = new Bitmap(path); }
            catch (ArgumentException) { }
        }
        Color[] colors =
        [
            Color.FromArgb(255, 75, 76),   // Red, eyes open
            Color.FromArgb(178, 113, 247), // Purple, eyes closed
            Color.FromArgb(112, 218, 85),  // Green, eyes open
            Color.FromArgb(88, 171, 255),  // Blue, eyes open
            Color.FromArgb(255, 145, 195)  // Pink, eyes closed
        ];
        for (int i = 0; i < tints.Length; i++)
        {
            var matrix = new ColorMatrix
            {
                Matrix00 = colors[i].R / 255f,
                Matrix11 = colors[i].G / 255f,
                Matrix22 = colors[i].B / 255f
            };
            tints[i] = new ImageAttributes();
            tints[i].SetColorMatrix(matrix);
        }
        timer.Tick += (_, _) => { frame = (frame + 1) % 4; Invalidate(); };
        timer.Start();
    }

    public void ApplyTheme(AppTheme value)
    {
        theme = value;
        BackColor = value.Kind == ThemeKind.Mine ? Color.Transparent : value.Background;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (theme.Kind != ThemeKind.Mine || sheet is null) return;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        int spriteWidth = CellWidth * PixelScale;
        int spriteHeight = CellHeight * PixelScale;
        int totalWidth = 5 * spriteWidth + 4 * Spacing;
        int left = Math.Max(0, (Width - totalWidth) / 2);
        int top = Math.Max(0, Height - spriteHeight - 3);
        for (int i = 0; i < 5; i++)
        {
            int x = left + i * (spriteWidth + Spacing);
            // The sheet stores grayscale body poses, plain antennas and faces separately.
            DrawPart(e.Graphics, new Rectangle(x, top, spriteWidth, spriteHeight),
                new Rectangle(frame * CellWidth, 0, CellWidth, CellHeight), tints[i]);
            DrawPart(e.Graphics, new Rectangle(x + 5 * PixelScale, top, spriteWidth, spriteHeight),
                new Rectangle(frame * CellWidth, 168, CellWidth, CellHeight), tints[i]);
            int faceX = i is 1 or 4 ? 48 : 32;
            int faceOffset = frame switch { 0 => 7, 1 => 6, 2 => 5, _ => 6 };
            DrawPart(e.Graphics, new Rectangle(x, top + faceOffset * PixelScale, spriteWidth, spriteHeight),
                new Rectangle(faceX, 128, CellWidth, CellHeight), null);
        }
    }

    private void DrawPart(Graphics graphics, Rectangle destination, Rectangle source, ImageAttributes? tint) =>
        graphics.DrawImage(sheet!, destination, source.X, source.Y, source.Width, source.Height,
            GraphicsUnit.Pixel, tint);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            sheet?.Dispose();
            foreach (var tint in tints) tint.Dispose();
        }
        base.Dispose(disposing);
    }
}
