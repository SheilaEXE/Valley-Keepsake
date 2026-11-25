# Optional SMAPI update notifier

This companion is optional. Install its ZIP in Stardew Valley's `Mods` folder only if you want SMAPI to show update notices for [Valley Keepsake on Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/56037). It does not install or update the standalone app, and the app works without it.

The same SMAPI mod package is intended for Windows and Linux/Steam Deck. Android support depends on the experimental SMAPI Android port and has not been tested. SMAPI checks for updates when Stardew Valley is started through SMAPI.

Build with `dotnet build ValleyKeepsake.UpdateNotifier.csproj -c Release`. The SMAPI build package creates an installable ZIP under `bin/Release`. Building does not copy the companion into the local game's `Mods` folder. The manifest version is filled from the repository's `Directory.Build.props`; keep it aligned with the version shown on Nexus Mods when publishing a new release.
