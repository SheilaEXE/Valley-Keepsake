using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace TradutorModsStardew;

internal sealed class ThemedScrollBar : Control
{
    private readonly Orientation orientation;
    private AppTheme theme = ThemeStore.Current;
    private int contentSize = 1;
    private int viewportSize = 1;
    private int value;
    private bool dragging;
    private int dragOffset;

    public event EventHandler? ValueChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, 0, Maximum);
            if (this.value == next) return;
            this.value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int Maximum => Math.Max(0, contentSize - viewportSize);

    public ThemedScrollBar(Orientation orientation)
    {
        this.orientation = orientation;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.Selectable, true);
        BackColor = Color.FromArgb(12, 41, 61);
        Cursor = Cursors.Default;
        TabStop = true;
    }

    public void ApplyTheme(AppTheme newTheme)
    {
        theme = newTheme;
        BackColor = newTheme.ScrollTrack;
        Invalidate();
    }

    public void SetMetrics(int contentSize, int viewportSize, int value)
    {
        this.contentSize = Math.Max(1, contentSize);
        this.viewportSize = Math.Max(1, viewportSize);
        this.value = Math.Clamp(value, 0, Maximum);
        Invalidate();
    }

    private Rectangle Track => new(3, 3, Math.Max(1, Width - 6), Math.Max(1, Height - 6));

    private Rectangle Thumb
    {
        get
        {
            var track = Track;
            int length = orientation == Orientation.Vertical ? track.Height : track.Width;
            int thumbLength = Maximum == 0 ? length : Math.Clamp(
                (int)Math.Round(length * Math.Min(1.0, (double)viewportSize / contentSize)),
                Math.Min(28, length), length);
            int travel = length - thumbLength;
            int offset = Maximum == 0 ? 0 : (int)Math.Round((double)value / Maximum * travel);
            return orientation == Orientation.Vertical
                ? new Rectangle(track.X, track.Y + offset, track.Width, thumbLength)
                : new Rectangle(track.X + offset, track.Y, thumbLength, track.Height);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        if (Maximum == 0) return;
        using var trackBrush = new SolidBrush(theme.ScrollTrack);
        using var trackPath = Rounded(Track, 6);
        e.Graphics.FillPath(trackBrush, trackPath);
        using var thumbBrush = new SolidBrush(theme.ScrollThumb);
        using var thumbPath = Rounded(Thumb, 6);
        e.Graphics.FillPath(thumbBrush, thumbPath);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || Maximum == 0) return;
        Focus();
        int position = orientation == Orientation.Vertical ? e.Y : e.X;
        var thumb = Thumb;
        int start = orientation == Orientation.Vertical ? thumb.Top : thumb.Left;
        int end = orientation == Orientation.Vertical ? thumb.Bottom : thumb.Right;
        if (position >= start && position < end)
        {
            dragging = true;
            dragOffset = position - start;
            Capture = true;
        }
        else Value += position < start ? -viewportSize : viewportSize;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging || Maximum == 0) return;
        var track = Track;
        var thumb = Thumb;
        int trackStart = orientation == Orientation.Vertical ? track.Top : track.Left;
        int trackLength = orientation == Orientation.Vertical ? track.Height : track.Width;
        int thumbLength = orientation == Orientation.Vertical ? thumb.Height : thumb.Width;
        int position = orientation == Orientation.Vertical ? e.Y : e.X;
        int travel = trackLength - thumbLength;
        if (travel > 0)
            Value = (int)Math.Round((double)(position - trackStart - dragOffset) / travel * Maximum);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragging = false;
        Capture = false;
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value -= Math.Sign(e.Delta) * Math.Max(1, viewportSize / 3);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int step = orientation == Orientation.Vertical ? 1 : 24;
        if (e.KeyCode is Keys.Up or Keys.Left) Value -= step;
        else if (e.KeyCode is Keys.Down or Keys.Right) Value += step;
        else if (e.KeyCode == Keys.PageUp) Value -= viewportSize;
        else if (e.KeyCode == Keys.PageDown) Value += viewportSize;
        else if (e.KeyCode == Keys.Home) Value = 0;
        else if (e.KeyCode == Keys.End) Value = Maximum;
        else { base.OnKeyDown(e); return; }
        e.Handled = true;
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        if (diameter <= 1) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
