using System.Runtime.InteropServices;

namespace TradutorModsStardew;

internal static class WindowsTitleBar
{
    private const int BorderColorAttribute = 34;
    private const int CaptionColorAttribute = 35;
    private const int TextColorAttribute = 36;

    public static void Apply(Form form, AppTheme theme)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || !form.IsHandleCreated)
            return;

        var caption = theme.Kind switch
        {
            ThemeKind.Parchment => theme.Panel,
            ThemeKind.Garden or ThemeKind.Wood => theme.Header,
            _ => theme.Background
        };

        int captionColor = ToColorRef(caption);
        int textColor = ToColorRef(theme.Text);
        DwmSetWindowAttribute(form.Handle, CaptionColorAttribute, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(form.Handle, BorderColorAttribute, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(form.Handle, TextColorAttribute, ref textColor, sizeof(int));
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
