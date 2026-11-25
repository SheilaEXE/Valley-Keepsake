using System.Drawing.Drawing2D;

namespace TradutorModsStardew;

internal sealed class ThemePaletteButton : Control
{
    private AppTheme theme = ThemeStore.Current;
    private Bitmap? icon;

    public ThemePaletteButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(31, 29);
        MinimumSize = Size;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        ApplyTheme(theme);
    }

    public void ApplyTheme(AppTheme value)
    {
        if (icon is null || theme.Kind != value.Kind)
        {
            icon?.Dispose();
            icon = null;
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", $"PaintPalette-{value.Kind}.png");
            try { if (File.Exists(path)) icon = new Bitmap(path); }
            catch (ArgumentException) { }
        }
        theme = value;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        base.OnPaintBackground(e);
        if (icon is null) return;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(icon, new Rectangle(0, 2, Width, Height - 4));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) icon?.Dispose();
        base.Dispose(disposing);
    }
}
