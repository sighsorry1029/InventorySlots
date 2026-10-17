# Path of Valheiman compatibility checks

Build the plugin with `dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true` and run `dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug` from the repository root. The suite includes production row-state logic and production adapter methods compiled with boundary doubles for sorting and tooltip ownership; those doubles do not verify Unity rendering or Harmony patch installation.

Build this executable with `dotnet build build/PathOfValheimanSmoke/PathOfValheimanSmoke.csproj -c Debug`. Run it through the repository's x64 Mono host (source: `InventoryActions/build/CompatibilitySmoke/MonoHost.cpp`; build procedure: adjacent `Run-MonoSmoke.ps1`). Example for the existing local host:

```powershell
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
$pov = 'C:\Users\blizz\AppData\Roaming\com.kesomannen.gale\valheim\profiles\asdfzcxv\BepInEx\plugins\treextr-PathOfValheiman\PathOfValheiman\PathOfValheiman.dll'
& .\InventoryActions\build\CompatibilitySmoke\obj\MonoHost-x64\MonoHost.exe `
    "$game\MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll" `
    "$game\valheim_Data\Managed" "$game\MonoBleedingEdge\etc" `
    "$PWD\build\PathOfValheimanSmoke\bin\Debug\net48\PathOfValheimanSmoke.exe" `
    "$PWD\bin\Debug\InventorySlots.dll" "$game\valheim_Data\Managed" "$game\BepInEx\core" $pov
```

The probe isolates BepInEx configuration paths in a new temporary directory. It loads original game/PoV assemblies, patches and invokes the actual private PoV metadata-registration method with the final plugin's prefix, checks preserved/different/absent metadata and first-run/repeated registration, validates other external method signatures, and executes explicit readers for the game's original private tooltip fields. It does not publicize or modify input DLLs.

2026-10-17: **28 checks passed**, exit 0, against Valheim client **1.0.20** (25909357) and supplied PoV **5.0.4**, SHA-256 `36E45B65B44667AD596446CAC2AE989041669948C11710B05185E4FE16697DB4`.

The optional `--engine-hooks` probe attempts the additional row/sort/panel hooks. It was attempted and exited 1 because patch compilation initializes `Player` → `ZSyncAnimation` → Unity's native `Animator.StringToHash_Injected`, unavailable in standalone Mono. This is **not** a passed runtime hook check or evidence of mismatched installed game libraries. Do not replace/publicize game DLLs or stub native game methods to hide that limitation. The default run explicitly reports these hooks as not executed.

Still requiring actual game verification: Big Bag gains/respec, occupied-row removal, save/reconnect, Tidy Pack with equipped/favorite/quick-slot items, multiplayer chest contention, hover delay, Alt-pinned keyword/socket interaction, live tooltip layout On/Off, Mouse3 pins/chat text, tree opening/closing, and dedicated-server startup. No game/client/server session was launched for these checks.
