using StardewModdingAPI;

namespace ValleyKeepsake.UpdateNotifier;

/// <summary>Registers the optional companion with SMAPI so it can check the Nexus update key.</summary>
public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        // SMAPI handles update checks from the manifest; the app runs separately.
    }
}
