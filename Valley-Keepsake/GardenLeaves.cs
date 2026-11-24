using System.Drawing.Imaging;

namespace TradutorModsStardew;

internal sealed class GardenLeaves : IDisposable
{
    private const int GreenCount = 24;
    private const int Count = GreenCount * 2;
    private const int FrameSize = 16;
    private const int GreenFrameCount = 11;
    private const int PinkFrameCount = 11;
    private const int DisplaySize = 48;
    private const double SpinChance = .0005;
    // Sprite indices 5 through 10, then back without repeating the endpoints.
    private static readonly int[] GreenSwayFrames = [5, 6, 7, 8, 9, 10, 9, 8, 7, 6];
    private static readonly int[] PinkSwayFrames = GreenSwayFrames;
    private readonly Random random = new(1739);
    private readonly Leaf[] leaves = new Leaf[Count];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 50 };
    private readonly Func<Size> areaSize;
    private readonly Action invalidate;
    private readonly Bitmap? greenSheet;
    private readonly Bitmap? pinkSheet;
    private readonly ImageAttributes transparency = new();
    private int frameTick;
    private bool enabled;

    public GardenLeaves(Func<Size> areaSize, Action invalidate)
    {
        this.areaSize = areaSize;
        this.invalidate = invalidate;
        greenSheet = LoadSheet("green_debris.png", GreenFrameCount);
        pinkSheet = LoadSheet("pink_debris.png", PinkFrameCount);
        transparency.SetColorMatrix(new ColorMatrix
        {
            Matrix00 = 1, Matrix11 = 1, Matrix22 = 1, Matrix33 = .68f, Matrix44 = 1
        });
        timer.Tick += (_, _) =>
        {
            if (!enabled) return;
            frameTick++;
            Advance();
            invalidate();
        };
    }

    private static Bitmap? LoadSheet(string name, int frameCount)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", name);
        try
        {
            if (!File.Exists(path)) return null;
            using var source = new Bitmap(path);
            return source.Width == FrameSize * frameCount && source.Height == FrameSize
                ? new Bitmap(source) : null;
        }
        catch (ArgumentException) { return null; }
    }

    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value) return;
            enabled = value;
            if (value)
            {
                Reset();
                timer.Start();
            }
            else timer.Stop();
            invalidate();
        }
    }

    public void Reset()
    {
        Size size = areaSize();
        if (size.Width <= 0 || size.Height <= 0) return;
        for (int i = 0; i < leaves.Length; i++)
            leaves[i] = NewLeaf(size, random.Next(size.Height), i >= GreenCount);
        invalidate();
    }

    public void Draw(Graphics graphics, Control surface, Control root)
    {
        if (!enabled || greenSheet is null && pinkSheet is null) return;
        int offsetX = 0, offsetY = 0;
        Control? parent = surface;
        while (parent is not null && parent != root)
        {
            offsetX += parent.Left;
            offsetY += parent.Top;
            parent = parent.Parent;
        }
        if (parent is null) return;

        Rectangle[] blockedFields = surface.Controls.OfType<Panel>()
            .Where(panel => panel.Controls.OfType<TextBox>().Any())
            .Select(panel => Rectangle.Inflate(panel.Bounds, 5, 5)).ToArray();
        if (root.Width <= 144 || root.Height <= 144) return;
        var state = graphics.Save();
        graphics.SetClip(surface == root
            ? root.ClientRectangle
            : new Rectangle(72 - offsetX, 72 - offsetY, root.Width - 144, root.Height - 144),
            System.Drawing.Drawing2D.CombineMode.Intersect);
        var oldInterpolation = graphics.InterpolationMode;
        var oldOffset = graphics.PixelOffsetMode;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        foreach (var leaf in leaves)
        {
            var sheet = leaf.Pink ? pinkSheet : greenSheet;
            if (sheet is null) continue;
            int x = (int)leaf.X - offsetX;
            int y = (int)leaf.Y - offsetY;
            if (x < -DisplaySize || y < -DisplaySize || x >= surface.Width || y >= surface.Height) continue;
            var spriteBounds = new Rectangle(x, y, DisplaySize, DisplaySize);
            if (blockedFields.Any(field => field.IntersectsWith(spriteBounds))) continue;
            int frame = leaf.Frame;
            graphics.DrawImage(sheet, spriteBounds,
                frame * FrameSize, 0, FrameSize, FrameSize, GraphicsUnit.Pixel, transparency);
        }
        graphics.InterpolationMode = oldInterpolation;
        graphics.PixelOffsetMode = oldOffset;
        graphics.Restore(state);
    }

    private void Advance()
    {
        Size size = areaSize();
        if (size.Width <= 0 || size.Height <= 0) return;
        for (int i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            int frameCount = leaf.Pink ? PinkFrameCount : GreenFrameCount;
            int[] swayFrames = leaf.Pink ? PinkSwayFrames : GreenSwayFrames;
            leaf.Phase += .07f;
            leaf.X += .12f + MathF.Sin(leaf.Phase) * .65f;
            leaf.Y += leaf.Speed;
            if (frameTick % 2 == 0)
            {
                if (leaf.Spinning)
                {
                    leaf.Frame = (leaf.Frame + 1) % frameCount;
                    if (++leaf.SpinSteps == frameCount)
                    {
                        leaf.Spinning = false;
                        leaf.SpinSteps = 0;
                        leaf.Frame = swayFrames[leaf.SwayStep];
                    }
                }
                else if (frameTick % 4 == 0)
                {
                    leaf.SwayStep = (leaf.SwayStep + 1) % swayFrames.Length;
                    leaf.Frame = swayFrames[leaf.SwayStep];
                    if (leaf.SwayStep == 0 && random.NextDouble() < SpinChance)
                        leaf.Spinning = true;
                }
            }
            leaves[i] = leaf.Y > size.Height + DisplaySize || leaf.X > size.Width + DisplaySize
                ? NewLeaf(size, -random.Next(DisplaySize, DisplaySize + 90), leaf.Pink) : leaf;
        }
    }

    private Leaf NewLeaf(Size size, int y, bool pink)
    {
        int[] swayFrames = pink ? PinkSwayFrames : GreenSwayFrames;
        int swayStep = random.Next(swayFrames.Length);
        return new Leaf
        {
            Pink = pink,
            X = random.Next(Math.Max(1, size.Width)),
            Y = y,
            Speed = .7f + (float)random.NextDouble() * .8f,
            Phase = (float)random.NextDouble() * MathF.Tau,
            SwayStep = swayStep,
            Frame = swayFrames[swayStep]
        };
    }

    public void Dispose()
    {
        timer.Dispose();
        greenSheet?.Dispose();
        pinkSheet?.Dispose();
        transparency.Dispose();
    }

    private struct Leaf
    {
        public float X, Y, Speed, Phase;
        public int Frame, SwayStep, SpinSteps;
        public bool Spinning, Pink;
    }
}

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
{
    public BufferedFlowLayoutPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}
