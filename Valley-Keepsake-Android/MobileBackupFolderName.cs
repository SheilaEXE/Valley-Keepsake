using System.Text.Json;

namespace ValleyKeepsake.Android;

internal static class MobileBackupFolderName
{
    private const string Key = "Backups before restore";

    public static string ForLanguage(string language)
    {
        var resource = language == "pt-BR"
            ? "ValleyKeepsake.Android.Languages.pt-BR.json"
            : "ValleyKeepsake.Android.Languages.en.json";
        using var stream = typeof(MobileBackupFolderName).Assembly.GetManifestResourceStream(resource);
        if (stream is null) return Key;

        using var document = JsonDocument.Parse(stream);
        var folder = document.RootElement.GetProperty("strings").GetProperty(Key).GetString();
        return IsSafe(folder) ? folder! : Key;
    }

    private static bool IsSafe(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && folder is not "." and not ".." &&
        folder == folder.TrimEnd(' ', '.') &&
        folder.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !folder.Contains('/') && !folder.Contains('\\');
}
