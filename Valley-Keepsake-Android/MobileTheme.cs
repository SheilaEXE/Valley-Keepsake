using Avalonia.Media;

namespace ValleyKeepsake.Android;

internal enum MobileThemeKind { Classic, Parchment, Mine, Garden, Wood }

internal sealed record MobileTheme(
    Color Background, Color Panel, Color Field, Color Text, Color MutedText,
    Color Title, Color Header, Color Row, Color Selection, Color Button,
    Color ButtonText, Color ButtonBorder, Color BorderDark, Color ScrollTrack,
    Color ScrollThumb);

internal static class MobileThemes
{
    private static Color C(byte red, byte green, byte blue) => Color.FromRgb(red, green, blue);

    public static MobileTheme Get(MobileThemeKind kind) => kind switch
    {
        MobileThemeKind.Parchment => new(
            C(250, 214, 157), C(255, 226, 176), C(247, 203, 145), C(72, 37, 19), C(108, 65, 39),
            C(105, 43, 17), C(220, 163, 99), C(253, 222, 178), C(208, 143, 78), C(119, 60, 27),
            Colors.White, C(86, 41, 20), C(96, 43, 17), C(217, 169, 112), C(122, 78, 35)),
        MobileThemeKind.Mine => new(
            C(20, 17, 39), C(38, 34, 68), C(20, 18, 42), C(236, 234, 248), C(194, 192, 219),
            C(106, 255, 62), C(57, 52, 92), C(39, 36, 69), C(76, 67, 116), C(54, 69, 100),
            Colors.White, C(106, 105, 153), C(25, 23, 51), C(26, 25, 52), C(142, 239, 95)),
        MobileThemeKind.Garden => new(
            C(125, 165, 128), C(181, 202, 166), C(237, 228, 200), C(52, 43, 42), C(69, 60, 56),
            C(87, 40, 60), C(218, 157, 166), C(240, 229, 200), C(166, 75, 112), C(193, 84, 125),
            Colors.White, C(110, 48, 72), C(66, 43, 44), C(154, 186, 146), C(213, 100, 145)),
        MobileThemeKind.Wood => new(
            C(229, 166, 105), C(249, 200, 139), C(250, 214, 164), C(70, 36, 19), C(112, 69, 41),
            C(100, 42, 16), C(198, 122, 64), C(250, 205, 151), C(196, 111, 59), C(150, 74, 31),
            Colors.White, C(105, 48, 22), C(102, 45, 17), C(207, 143, 82), C(129, 72, 32)),
        _ => new(
            C(10, 31, 53), C(19, 58, 83), C(12, 41, 61), C(231, 249, 247), C(174, 223, 231),
            C(161, 232, 74), C(26, 97, 125), C(19, 58, 83), C(31, 112, 132), C(30, 91, 120),
            Colors.White, C(77, 174, 202), C(10, 31, 53), C(18, 55, 76), C(161, 232, 74))
    };
}
