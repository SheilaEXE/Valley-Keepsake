using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace TradutorModsStardew;

internal sealed class GlowButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.FromArgb(161, 232, 74);
    private AppTheme theme = ThemeStore.Current;
    private Color buttonFill = Color.FromArgb(30, 91, 120);
    private bool hovering;

    public GlowButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.FromArgb(30, 91, 120);
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 9.5f);
        Cursor = Cursors.Hand;
        Padding = new Padding(15, 6, 15, 6);
        AutoSize = true;
        MinimumSize = new Size(0, 38);
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    public void ApplyTheme(AppTheme newTheme, Color accent)
    {
        theme = newTheme;
        AccentColor = accent;
        buttonFill = newTheme.Button;
        BackColor = Color.Transparent;
        ForeColor = newTheme.ButtonText;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        base.OnPaintBackground(e);
        var bounds = ClientRectangle;
        bounds.Inflate(-3, -3);
        if (hovering && Enabled)
        {
            using var glow = new Pen(Color.FromArgb(120, AccentColor), 5);
            using var glowPath = Rounded(bounds, 10);
            e.Graphics.DrawPath(glow, glowPath);
        }
        using var path = Rounded(bounds, 8);
        var fill = !Enabled
            ? theme.Kind == ThemeKind.Classic ? Color.FromArgb(47, 69, 86) : AppTheme.Blend(theme.Button, theme.Panel, .6)
            : hovering ? AccentColor : buttonFill;
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        using var border = new Pen(theme.Kind == ThemeKind.Classic
            ? hovering && Enabled ? Color.White : Color.FromArgb(77, 174, 202)
            : hovering && Enabled ? theme.BorderLight : theme.ButtonBorder, 1);
        e.Graphics.DrawPath(border, path);
        bool brightGreen = fill.G > fill.R * 1.2 && fill.G > fill.B * 1.1;
        bool brightYellow = fill.R > 180 && fill.G > 150 && fill.B < 150;
        var textColor = hovering && Enabled
            ? theme.Kind != ThemeKind.Classic && (brightGreen || brightYellow) ? theme.BorderDark : theme.HoverText
            : Enabled ? ForeColor : theme.MutedText;
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
