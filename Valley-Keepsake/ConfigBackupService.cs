using System.Text.Json;

namespace TradutorModsStardew;

/// <summary>Guarda config.json por UniqueID, para que mudanças no nome das pastas não apaguem os atalhos da jogadora.</summary>
internal sealed class ConfigBackupService
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private const string ConfigFolderName = "Configurações dos Mods";

    public static string DefaultStorageRoot(string translationLibraryRoot) => Path.Combine(translationLibraryRoot, ConfigFolderName);

    public string GetStorageRoot(string configLibraryRoot) => configLibraryRoot;

    public List<ConfigResult> Compare(string libraryRoot, IEnumerable<InstalledMod> mods)
    {
        return mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.UniqueId))
            .GroupBy(mod => mod.UniqueId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(mod => new { Mod = mod, SavedPath = Path.Combine(GetStorageRoot(libraryRoot), FolderFor(mod.UniqueId), "config.json") })
            .Where(item => File.Exists(Path.Combine(item.Mod.Directory, "config.json")) || File.Exists(item.SavedPath))
            .OrderBy(item => item.Mod.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => CreateResult(item.Mod, File.Exists(item.SavedPath) ? new ConfigSnapshot(item.SavedPath) : null))
            .ToList();
    }

    public int ExportAll(string libraryRoot, IEnumerable<InstalledMod> mods)
    {
        var count = 0;
        foreach (var mod in mods.Where(mod => !string.IsNullOrWhiteSpace(mod.UniqueId)))
        {
            var source = Path.Combine(mod.Directory, "config.json");
            if (!File.Exists(source))
                continue;

            var folder = Path.Combine(GetStorageRoot(libraryRoot), FolderFor(mod.UniqueId));
            Directory.CreateDirectory(folder);
            var destination = Path.Combine(folder, "config.json");
            File.Copy(source, destination, true);
            count++;
        }
        return count;
    }

    public string Restore(string libraryRoot, ConfigResult result)
    {
        if (result.Snapshot is null || !File.Exists(result.Snapshot.ConfigPath))
            throw new InvalidOperationException(T("Não foi encontrada uma cópia salva deste config.json.", "No saved copy of this config.json was found."));

        var destination = Path.Combine(result.Mod.Directory, "config.json");
        var folderName = T("Backups antes de restaurar", "Backups before restore");
        if (string.IsNullOrWhiteSpace(folderName) || folderName is "." or ".." ||
            folderName != folderName.TrimEnd(' ', '.') ||
            folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            folderName.Contains('/') || folderName.Contains('\\'))
            folderName = "Backups before restore";
        var backupRoot = Path.Combine(GetStorageRoot(libraryRoot), folderName, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"), FolderFor(result.Mod.UniqueId));
        if (File.Exists(destination))
        {
            Directory.CreateDirectory(backupRoot);
            File.Copy(destination, Path.Combine(backupRoot, "config.json"), true);
        }

        Directory.CreateDirectory(result.Mod.Directory);
        File.Copy(result.Snapshot.ConfigPath, destination, true);
        return backupRoot;
    }

    private ConfigResult CreateResult(InstalledMod mod, ConfigSnapshot? snapshot)
    {
        if (snapshot is null)
            return new ConfigResult { Mod = mod, State = ConfigState.NoSavedCopy, Detail = T("Ainda não há uma cópia salva.", "There is no saved copy yet.") };

        var current = Path.Combine(mod.Directory, "config.json");
        if (File.Exists(current) && FilesEqual(current, snapshot.ConfigPath))
            return new ConfigResult { Mod = mod, Snapshot = snapshot, State = ConfigState.SameAsSaved, Detail = T("A configuração atual já é igual à cópia salva.", "The current configuration matches the saved copy.") };

        var detail = File.Exists(current)
            ? T("Há diferenças. Ao restaurar, a configuração atual será guardada em um backup datado.", "There are differences. Restoring will first save the current configuration in a dated backup.")
            : T("O mod ainda não possui config.json. A cópia salva pode ser restaurada.", "This mod has no config.json yet. The saved copy can be restored.");
        return new ConfigResult { Mod = mod, Snapshot = snapshot, State = ConfigState.ReadyToRestore, Detail = detail, Selected = true };
    }

    private static bool FilesEqual(string first, string second) => File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second));

    private static string FolderFor(string uniqueId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(uniqueId.Select(character => invalid.Contains(character) ? '_' : character));
    }
}

internal static class LibrarySelectionStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValleyKeepsake", "libraries-" + typeof(LibrarySelectionStore).Module.ModuleVersionId.ToString("N") + ".json");

    private sealed record Selection(string? Translation, string? Configuration);

    public static (string Translation, string Configuration) Load()
    {
        try
        {
            var selected = JsonSerializer.Deserialize<Selection>(File.ReadAllText(SettingsPath));
            return (ExistingFolder(selected?.Translation), ExistingFolder(selected?.Configuration));
        }
        catch { return ("", ""); }
    }

    public static void Save(string? translation, string? configuration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var selected = new Selection(ExistingFolder(translation), ExistingFolder(configuration));
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(selected));
    }

    private static string ExistingFolder(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? Path.GetFullPath(path) : "";
}
