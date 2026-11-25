using System.Text.Json;
using Color = System.Drawing.Color;

namespace TradutorModsStardew;

internal enum ThemeKind { Classic, Parchment, Mine, Garden, Wood }

internal sealed record AppTheme(
    ThemeKind Kind,
    Color Background,
    Color Panel,
    Color Field,
    Color Text,
    Color MutedText,
    Color Title,
    Color Header,
    Color Row,
    Color AlternateRow,
    Color Selection,
    Color Button,
    Color ButtonText,
    Color ButtonBorder,
    Color HoverText,
    Color BorderDark,
    Color BorderLight,
    Color ScrollTrack,
    Color ScrollThumb)
{
    public bool IsLight => Kind is ThemeKind.Parchment or ThemeKind.Garden or ThemeKind.Wood;
    public Color GridSelection => Selection;
    public Color GridSelectionText => Kind == ThemeKind.Garden ? Color.White : Text;

    public Color RowFor(MatchState state) => Kind == ThemeKind.Classic ? state switch
    {
        MatchState.Safe => Color.FromArgb(23, 92, 85),
        MatchState.NeedsConfirmation => Color.FromArgb(94, 78, 38),
        MatchState.AlreadyTranslated => Color.FromArgb(25, 68, 91),
        MatchState.NoMatch => Color.FromArgb(91, 50, 65),
        MatchState.MissingTranslationFile => Color.FromArgb(128, 47, 59),
        MatchState.Applied => Color.FromArgb(25, 104, 82),
        MatchState.Failed => Color.FromArgb(112, 49, 58),
        _ => Row
    } : state switch
    {
        MatchState.Safe or MatchState.Applied => Blend(Row, Color.FromArgb(91, 164, 90), .30),
        MatchState.NeedsConfirmation => Blend(Row, Color.FromArgb(213, 172, 61), .32),
        MatchState.MissingTranslationFile => Kind == ThemeKind.Mine
            ? Blend(Row, Color.FromArgb(205, 63, 75), .52)
            : Blend(Row, Color.FromArgb(224, 88, 91), .48),
        MatchState.NoMatch or MatchState.Failed =>
            Blend(Row, Color.FromArgb(196, 91, 110), .27),
        _ => Row
    };

    public Color ConfigRowFor(ConfigState state) => RowFor(state switch
    {
        ConfigState.ReadyToRestore => MatchState.NeedsConfirmation,
        ConfigState.SameAsSaved or ConfigState.NoSavedCopy => MatchState.AlreadyTranslated,
        _ => MatchState.Failed
    });

    public static Color Blend(Color first, Color second, double amount) => Color.FromArgb(
        (int)Math.Round(first.R * (1 - amount) + second.R * amount),
        (int)Math.Round(first.G * (1 - amount) + second.G * amount),
        (int)Math.Round(first.B * (1 - amount) + second.B * amount));
}

internal static class ThemeStore
{
    private static readonly string PathToSetting = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TradutorModsStardew", "theme.json");

    public static ThemeKind CurrentKind { get; private set; } = Load();
    public static AppTheme Current => Get(CurrentKind);

    public static void Set(ThemeKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Directory.CreateDirectory(Path.GetDirectoryName(PathToSetting)!);
        File.WriteAllText(PathToSetting, JsonSerializer.Serialize(new ThemeSetting(kind.ToString())));
        CurrentKind = kind;
    }

    public static string Name(ThemeKind kind) => kind switch
    {
        ThemeKind.Classic => UiLanguage.Text("Azul clássico", "Classic blue"),
        ThemeKind.Parchment => UiLanguage.Text("Nota dos Junimos", "Junimo Note"),
        ThemeKind.Mine => UiLanguage.Text("Galpão de Slimes", "Slime Hutch"),
        ThemeKind.Garden => UiLanguage.Text("Jardim", "Garden"),
        ThemeKind.Wood => UiLanguage.Text("Caixa de Diálogo", "Dialogue Box"),
        _ => kind.ToString()
    };

    public static AppTheme Get(ThemeKind kind) => kind switch
    {
        ThemeKind.Parchment => new(kind,
            C(250, 214, 157), C(255, 226, 176), C(247, 203, 145), C(72, 37, 19), C(108, 65, 39),
            C(105, 43, 17), C(220, 163, 99), C(253, 222, 178), C(246, 209, 157), C(208, 143, 78),
            C(119, 60, 27), Color.White, C(86, 41, 20), Color.White, C(96, 43, 17), C(247, 181, 106),
            C(217, 169, 112), C(122, 78, 35)),
        ThemeKind.Mine => new(kind,
            C(20, 17, 39), C(38, 34, 68), C(20, 18, 42), C(236, 234, 248), C(194, 192, 219),
            C(106, 255, 62), C(57, 52, 92), C(39, 36, 69), C(31, 29, 58), C(76, 67, 116),
            C(54, 69, 100), Color.White, C(106, 105, 153), Color.White, C(25, 23, 51), C(117, 113, 170),
            C(26, 25, 52), C(142, 239, 95)),
        ThemeKind.Garden => new(kind,
            C(125, 165, 128), C(181, 202, 166), C(237, 228, 200), C(52, 43, 42), C(69, 60, 56),
            C(87, 40, 60), C(218, 157, 166), C(240, 229, 200), C(226, 215, 188), C(166, 75, 112),
            C(193, 84, 125), Color.White, C(110, 48, 72), Color.White, C(66, 43, 44), C(246, 176, 195),
            C(154, 186, 146), C(213, 100, 145)),
        ThemeKind.Wood => new(kind,
            C(229, 166, 105), C(249, 200, 139), C(250, 214, 164), C(70, 36, 19), C(112, 69, 41),
            C(100, 42, 16), C(198, 122, 64), C(250, 205, 151), C(241, 187, 126), C(196, 111, 59),
            C(150, 74, 31), Color.White, C(105, 48, 22), Color.White, C(102, 45, 17), C(251, 188, 103),
            C(207, 143, 82), C(129, 72, 32)),
        _ => new(ThemeKind.Classic,
            C(10, 31, 53), C(19, 58, 83), C(12, 41, 61), C(231, 249, 247), C(174, 223, 231),
            C(161, 232, 74), C(26, 97, 125), C(19, 58, 83), C(16, 49, 72), C(31, 112, 132),
            C(30, 91, 120), Color.White, C(77, 174, 202), C(12, 41, 61), C(10, 31, 53), C(77, 174, 202),
            C(18, 55, 76), C(161, 232, 74))
    };

    private static Color C(int red, int green, int blue) => Color.FromArgb(red, green, blue);

    private static ThemeKind Load()
    {
        try
        {
            if (File.Exists(PathToSetting))
            {
                var saved = JsonSerializer.Deserialize<ThemeSetting>(File.ReadAllText(PathToSetting));
                if (Enum.TryParse<ThemeKind>(saved?.Theme, true, out var kind) && Enum.IsDefined(kind)) return kind;
            }
        }
        catch { }
        return ThemeKind.Classic;
    }

    private sealed record ThemeSetting(string Theme);
}
