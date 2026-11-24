using System.ComponentModel;

namespace TradutorModsStardew;

internal sealed class ThemeTitleLabel : Label
{
    private bool slimeGlow;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SlimeGlow
    {
        get => slimeGlow;
        set
        {
            if (slimeGlow == value) return;
            slimeGlow = value;
            Padding = value ? new Padding(2, 0, 3, 3) : Padding.Empty;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!slimeGlow)
        {
            base.OnPaint(e);
            return;
        }

        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoClipping | TextFormatFlags.NoPrefix;
        var dark = Color.FromArgb(11, 53, 14);
        var edge = Color.FromArgb(39, 144, 31);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Point(2, 3), dark, flags);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Point(1, 1), edge, flags);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Point(0, 0), ForeColor, flags);
    }
}
