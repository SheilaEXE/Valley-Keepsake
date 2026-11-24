using System.Text.Json;
using System.Text.RegularExpressions;

namespace TradutorModsStardew;

internal static class UiLanguage
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TradutorModsStardew", "language.json");
    public static string UserLanguagesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TradutorModsStardew", "Languages");

    private static readonly Dictionary<string, LanguagePack> packs = LoadPacks();
    private static LanguageSetting settings = Load();
    public static bool Portuguese => CurrentCode == "pt-BR";
    public static string CurrentCode => packs.ContainsKey(settings.Language) || settings.Language is "en" or "pt-BR"
        ? settings.Language : "en";
    public static string TranslationLocale => settings.TranslationLocale ?? "pt-BR";

    public static IReadOnlyList<LanguageOption> AvailableLanguages() =>
        new[] { new LanguageOption("en", "English"), new LanguageOption("pt-BR", "Português (Brasil)") }
            .Concat(packs.Values.Where(pack => pack.Code is not ("en" or "pt-BR"))
                .Select(pack => new LanguageOption(pack.Code, pack.Name)).OrderBy(option => option.Name))
            .ToArray();

    public static string Text(string portuguese, string english)
    {
        if (packs.TryGetValue(CurrentCode, out var pack) && pack.Translate(english) is { } translated)
            return translated;
        return Portuguese ? portuguese : english;
    }

    public static void Set(bool portuguese)
        => Set(portuguese ? "pt-BR" : "en");

    public static void Set(string code)
    {
        if (!AvailableLanguages().Any(option => option.Code == code))
            throw new ArgumentException("Unknown interface language", nameof(code));
        var updated = settings with { Language = code };
        Save(updated);
        settings = updated;
    }

    public static void SetTranslationLocale(string locale)
    {
        var updated = settings with { TranslationLocale = locale };
        Save(updated);
        settings = updated;
    }

    private static void Save(LanguageSetting value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(value));
    }

    private static LanguageSetting Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<LanguageSetting>(File.ReadAllText(SettingsPath)) ?? new("en");
        }
        catch { }
        return new("en");
    }

    private sealed record LanguageSetting(string Language, string? TranslationLocale = null);

    internal sealed record LanguageOption(string Code, string Name)
    {
        public override string ToString() => Name;
    }

    private static Dictionary<string, LanguagePack> LoadPacks()
    {
        var result = new Dictionary<string, LanguagePack>(StringComparer.OrdinalIgnoreCase);
        try { Directory.CreateDirectory(UserLanguagesPath); } catch (Exception) { }
        foreach (var folder in new[] { Path.Combine(AppContext.BaseDirectory, "Languages"), UserLanguagesPath })
        {
            if (!Directory.Exists(folder)) continue;
            string[] files;
            try { files = Directory.GetFiles(folder, "*.json"); }
            catch (Exception) { continue; }
            foreach (var file in files)
            {
                try
                {
                    if (new FileInfo(file).Length > 2_000_000) continue;
                    using var document = JsonDocument.Parse(File.ReadAllText(file));
                    var root = document.RootElement;
                    var code = root.GetProperty("language").GetString();
                    var name = root.GetProperty("name").GetString();
                    if (string.IsNullOrWhiteSpace(code) || code != Path.GetFileNameWithoutExtension(file) ||
                        !Regex.IsMatch(code, "^[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*$") ||
                        string.IsNullOrWhiteSpace(name) || name.Length > 80) continue;
                    var strings = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var item in root.GetProperty("strings").EnumerateObject())
                        if (item.Value.ValueKind == JsonValueKind.String && item.Value.GetString() is { } value &&
                            !string.IsNullOrWhiteSpace(value) && ValidTranslation(item.Name, value))
                            strings[item.Name] = value;
                    result[code] = new LanguagePack(code, name, strings);
                }
                catch (Exception) { /* Ignore one invalid file and keep the other languages available. */ }
            }
        }
        return result;
    }

    private static bool ValidTranslation(string key, string value)
    {
        var expected = Regex.Matches(key, @"\{(\d+)\}").Select(match => int.Parse(match.Groups[1].Value)).Distinct().Order().ToArray();
        var actual = Regex.Matches(value, @"\{(\d+)\}").Select(match => int.Parse(match.Groups[1].Value)).Distinct().Order().ToArray();
        if (!expected.SequenceEqual(actual)) return false;
        if (expected.Length == 0) return true;
        try { _ = string.Format(value, new object[expected[^1] + 1]); return true; }
        catch (FormatException) { return false; }
    }

    private sealed class LanguagePack(string code, string name, Dictionary<string, string> strings)
    {
        public string Code => code;
        public string Name => name;
        private readonly (Regex Pattern, string Value)[] templates = strings
            .Where(item => Regex.IsMatch(item.Key, @"\{\d+\}"))
            .OrderByDescending(item => item.Key.Length)
            .Select(item => (BuildPattern(item.Key), item.Value))
            .ToArray();

        public string? Translate(string english)
        {
            if (strings.TryGetValue(english, out var exact)) return exact;
            foreach (var (pattern, value) in templates)
            {
                var match = pattern.Match(english);
                if (!match.Success) continue;
                return Regex.Replace(value, @"\{(\d+)\}", placeholder =>
                    match.Groups[int.Parse(placeholder.Groups[1].Value) + 1].Value);
            }
            return null;
        }

        private static Regex BuildPattern(string template)
        {
            var pieces = Regex.Matches(template, @"\{\d+\}");
            var pattern = "\\A";
            var cursor = 0;
            foreach (Match piece in pieces)
            {
                pattern += Regex.Escape(template[cursor..piece.Index]) + "(.*?)";
                cursor = piece.Index + piece.Length;
            }
            pattern += Regex.Escape(template[cursor..]) + "\\z";
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
        }
    }
}
