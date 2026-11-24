using System.Text;
using System.Text.Json;
using Avalonia.Platform.Storage;

namespace ValleyKeepsake.Android;

internal sealed record FolderBookmarks(string? Mods, string? Translations, string? Configurations,
    string? InterfaceLanguage = null, string? Theme = null);

internal static class MobileStorage
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValleyKeepsakeAndroid", "folders.json");

    public static FolderBookmarks Load()
    {
        try { return JsonSerializer.Deserialize<FolderBookmarks>(File.ReadAllText(SettingsPath)) ?? new(null, null, null); }
        catch { return new(null, null, null); }
    }

    public static void Save(FolderBookmarks bookmarks)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(bookmarks));
    }

    public static async Task<IStorageFolder?> FolderAtAsync(IStorageFolder? root, IEnumerable<string> segments, bool create = false)
    {
        var current = root;
        foreach (var segment in segments)
        {
            if (current is null || segment is "" or "." or ".." || segment.Contains('/') || segment.Contains('\\'))
                return null;
            var next = await current.GetFolderAsync(segment);
            current = next ?? (create ? await current.CreateFolderAsync(segment) : null);
        }
        return current;
    }

    public static async Task<IStorageFile?> FileAsync(IStorageFolder? folder, string name)
    {
        if (folder is null) return null;
        return await folder.GetFileAsync(name);
    }

    public static async Task<string> ReadTextAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public static async Task CopyFileAsync(IStorageFile source, IStorageFolder destination, string? name = null)
    {
        var target = await destination.CreateFileAsync(name ?? source.Name)
            ?? throw new IOException("Não foi possível criar o arquivo no destino.");
        await using var input = await source.OpenReadAsync();
        await using var output = await target.OpenWriteAsync();
        await input.CopyToAsync(output);
        await output.FlushAsync();
    }

    public static async Task CopyFolderAsync(IStorageFolder source, IStorageFolder destination)
    {
        await foreach (var item in source.GetItemsAsync())
        {
            if (item is IStorageFile file) await CopyFileAsync(file, destination);
            else if (item is IStorageFolder folder)
            {
                var child = await destination.CreateFolderAsync(folder.Name)
                    ?? throw new IOException("Não foi possível criar a pasta no destino.");
                await CopyFolderAsync(folder, child);
            }
        }
    }

    public static async Task WriteTextAsync(IStorageFolder folder, string name, string text)
    {
        var target = await folder.CreateFileAsync(name)
            ?? throw new IOException("Não foi possível criar o arquivo no destino.");
        await using var stream = await target.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(text);
    }
}
