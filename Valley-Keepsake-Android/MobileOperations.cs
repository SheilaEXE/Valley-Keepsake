using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Platform.Storage;

namespace ValleyKeepsake.Android;

internal sealed record MobileMod(string Name, string UniqueId, string RelativeFolder, IStorageFolder Folder,
    IStorageItem? InstalledTranslation, IStorageItem? SavedTranslation, IStorageFile? CurrentConfig, IStorageFile? SavedConfig);

internal sealed class MobileOperations
{
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    private readonly List<IStorageItem> lastApplied = [];

    public async Task<List<MobileMod>> ScanAsync(IStorageFolder mods, IStorageFolder? translations,
        IStorageFolder? configurations, string locale)
    {
        var found = new List<(IStorageFolder Folder, string RelativeFolder, string Name, string Id)>();
        await ScanFolderAsync(mods, "", found);
        var result = new List<MobileMod>();
        foreach (var mod in found)
        {
            var installedI18n = await mod.Folder.GetFolderAsync("i18n");
            var installedTranslation = await FindTranslationAsync(installedI18n, locale);
            var savedFolder = mod.RelativeFolder.Length == 0 ? translations
                : await MobileStorage.FolderAtAsync(translations, mod.RelativeFolder.Split('/'));
            var savedI18n = savedFolder is null ? null : await savedFolder.GetFolderAsync("i18n");
            var savedTranslation = await FindTranslationAsync(savedI18n, locale);
            var configFolder = configurations is null ? null : await configurations.GetFolderAsync(SafeName(mod.Id));
            result.Add(new MobileMod(mod.Name, mod.Id, mod.RelativeFolder, mod.Folder,
                installedTranslation, savedTranslation, await mod.Folder.GetFileAsync("config.json"),
                configFolder is null ? null : await configFolder.GetFileAsync("config.json")));
        }
        return result.OrderBy(mod => mod.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static async Task ScanFolderAsync(IStorageFolder folder, string relative,
        List<(IStorageFolder Folder, string RelativeFolder, string Name, string Id)> found)
    {
        var children = new List<IStorageFolder>();
        IStorageFile? manifest = null;
        await foreach (var item in folder.GetItemsAsync())
        {
            if (item is IStorageFile file && file.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) manifest = file;
            else if (item is IStorageFolder child && !new[] { "i18n", "assets" }.Contains(child.Name, StringComparer.OrdinalIgnoreCase)) children.Add(child);
        }
        if (manifest is not null)
        {
            try
            {
                using var json = JsonDocument.Parse(await MobileStorage.ReadTextAsync(manifest), JsonOptions);
                var root = json.RootElement;
                var id = Property(root, "UniqueID");
                if (!string.IsNullOrWhiteSpace(id))
                    found.Add((folder, relative, Property(root, "Name") ?? folder.Name, id));
            }
            catch (JsonException) { }
        }
        foreach (var child in children)
            await ScanFolderAsync(child, relative.Length == 0 ? child.Name : relative + "/" + child.Name, found);
    }

    private static string? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static async Task<IStorageItem?> FindTranslationAsync(IStorageFolder? i18n, string locale)
    {
        if (i18n is null) return null;
        IStorageItem? alias = null;
        await foreach (var item in i18n.GetItemsAsync())
        {
            var name = item is IStorageFile ? Path.GetFileNameWithoutExtension(item.Name) : item.Name;
            if (item is IStorageFile && !item.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Equals(locale, StringComparison.OrdinalIgnoreCase)) return item;
            if (locale.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)
                && name.Equals("pt", StringComparison.OrdinalIgnoreCase)) alias = item;
        }
        return alias;
    }

    public async Task<int> SaveTranslationsAsync(IStorageFolder library, IEnumerable<MobileMod> mods)
    {
        var count = 0;
        foreach (var mod in mods.Where(mod => mod.InstalledTranslation is not null && mod.SavedTranslation is null))
        {
            var folder = mod.RelativeFolder.Length == 0 ? library
                : await MobileStorage.FolderAtAsync(library, mod.RelativeFolder.Split('/'), create: true);
            var i18n = await folder!.CreateFolderAsync("i18n")
                ?? throw new IOException("Não foi possível criar a pasta i18n na biblioteca.");
            await CopyNewAsync(mod.InstalledTranslation!, i18n);
            count++;
        }
        return count;
    }

    public async Task<int> ApplyTranslationsAsync(IEnumerable<MobileMod> mods)
    {
        lastApplied.Clear();
        foreach (var mod in mods.Where(mod => mod.InstalledTranslation is null && mod.SavedTranslation is not null))
        {
            var i18n = await mod.Folder.CreateFolderAsync("i18n")
                ?? throw new IOException("Não foi possível criar a pasta i18n do mod.");
            var created = await CopyNewAsync(mod.SavedTranslation!, i18n);
            lastApplied.Add(created);
        }
        return lastApplied.Count;
    }

    public async Task<int> UndoAsync()
    {
        var count = 0;
        foreach (var item in lastApplied.AsEnumerable().Reverse())
        {
            await item.DeleteAsync();
            count++;
        }
        lastApplied.Clear();
        return count;
    }

    public async Task<int> BackupConfigsAsync(IStorageFolder library, IEnumerable<MobileMod> mods)
    {
        var count = 0;
        foreach (var mod in mods.Where(mod => mod.CurrentConfig is not null))
        {
            var folder = await library.CreateFolderAsync(SafeName(mod.UniqueId))
                ?? throw new IOException("Não foi possível criar a pasta do backup.");
            await MobileStorage.CopyFileAsync(mod.CurrentConfig!, folder, "config.json");
            await MobileStorage.WriteTextAsync(folder, "informacoes.json", JsonSerializer.Serialize(new
            {
                mod.UniqueId, DisplayName = mod.Name, ExportedAt = DateTimeOffset.UtcNow
            }));
            count++;
        }
        return count;
    }

    public async Task<int> RestoreConfigsAsync(IStorageFolder library, IEnumerable<MobileMod> mods, string backupFolderName)
    {
        var count = 0;
        var backupRun = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + "_" + Guid.NewGuid().ToString("N")[..6];
        foreach (var mod in mods.Where(mod => mod.SavedConfig is not null))
        {
            if (mod.CurrentConfig is not null && await SameContentAsync(mod.CurrentConfig, mod.SavedConfig!))
                continue;
            if (mod.CurrentConfig is not null)
            {
                var backup = await MobileStorage.FolderAtAsync(library,
                    [backupFolderName, backupRun, SafeName(mod.UniqueId)], true);
                await MobileStorage.CopyFileAsync(mod.CurrentConfig, backup!, "config.json");
            }
            await MobileStorage.CopyFileAsync(mod.SavedConfig!, mod.Folder, "config.json");
            count++;
        }
        return count;
    }

    private static async Task<bool> SameContentAsync(IStorageFile current, IStorageFile saved)
    {
        await using var currentStream = await current.OpenReadAsync();
        await using var savedStream = await saved.OpenReadAsync();
        var currentHash = await SHA256.HashDataAsync(currentStream);
        var savedHash = await SHA256.HashDataAsync(savedStream);
        return currentHash.AsSpan().SequenceEqual(savedHash);
    }

    private static async Task<IStorageItem> CopyNewAsync(IStorageItem source, IStorageFolder destination)
    {
        if (await destination.GetFileAsync(source.Name) is not null || await destination.GetFolderAsync(source.Name) is not null)
            throw new IOException($"O destino já existe: {source.Name}");
        if (source is IStorageFile file)
        {
            await MobileStorage.CopyFileAsync(file, destination);
            return (await destination.GetFileAsync(file.Name))!;
        }
        var folder = await destination.CreateFolderAsync(source.Name)
            ?? throw new IOException("Não foi possível criar a pasta de tradução.");
        await MobileStorage.CopyFolderAsync((IStorageFolder)source, folder);
        return folder;
    }

    private static string SafeName(string id) => string.Concat(id.Select(character =>
        Path.GetInvalidFileNameChars().Contains(character) || character is '/' or '\\' or ':' ? '_' : character));
}
