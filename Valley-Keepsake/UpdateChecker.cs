using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TradutorModsStardew;

internal sealed record AvailableUpdate(string Version, Uri ReleasePage);

internal static class UpdateChecker
{
    private const string LatestReleaseApi = "https://api.github.com/repos/SheilaEXE/Valley-Keepsake/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    public static async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = Assembly.GetEntryAssembly()?.GetName().Version;
        if (current is null) return null;

        try
        {
            using var response = await Client.GetAsync(LatestReleaseApi, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseRelease(document.RootElement, current, PackageForThisSystem());
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (OperationCanceledException) { return null; }
    }

    internal static AvailableUpdate? ParseRelease(JsonElement release, Version current, string? packageName)
    {
        if (!release.TryGetProperty("tag_name", out var tagElement)) return null;
        var tag = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(tag) || !Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || latest <= current)
            return null;
        if (!HasPackage(release, packageName)) return null;

        var releasePage = new Uri($"https://github.com/SheilaEXE/Valley-Keepsake/releases/tag/{Uri.EscapeDataString(tag)}");
        return new AvailableUpdate(tag, releasePage);
    }

    private static bool HasPackage(JsonElement release, string? packageName)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return false;
        if (packageName is null) return false;

        foreach (var asset in assets.EnumerateArray())
            if (asset.TryGetProperty("name", out var name) &&
                name.GetString()?.EndsWith(packageName, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        return false;
    }

    private static string? PackageForThisSystem()
    {
        if (OperatingSystem.IsWindows()) return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "Windows-x64.zip",
            Architecture.X86 => "Windows-x86-EXPERIMENTAL.zip",
            _ => null
        };
        if (OperatingSystem.IsLinux())
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "Linux-x64.zip" : null;
        return null;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ValleyKeepsake", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
