# Path of Valheiman compatibility checks

Build the plugin with `dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true` and run `dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug` from the repository root. The suite includes production row-state logic and production adapter methods compiled with boundary doubles for sorting, tooltip ownership, salvage-tab transitions, item-specific return previews, cache invalidation and recipe-specific chest counts. Those doubles do not verify Unity rendering or Harmony patch installation. The expanded suite passed **194 tests**.

Build this executable with `dotnet build build/PathOfValheimanSmoke/PathOfValheimanSmoke.csproj -c Debug`. Run it through the repository's x64 Mono host (source: `InventoryActions/build/CompatibilitySmoke/MonoHost.cpp`; build procedure: adjacent `Run-MonoSmoke.ps1`). Example for the existing local host:

```powershell
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
$pov = 'C:\Users\blizz\AppData\Roaming\com.kesomannen.gale\cache\treextr-PathOfValheiman\5.0.4\BepInEx\plugins\treextr-PathOfValheiman\PathOfValheiman\PathOfValheiman.dll'
& .\InventoryActions\build\CompatibilitySmoke\obj\MonoHost-x64\MonoHost.exe `
    "$game\MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll" `
    "$game\valheim_Data\Managed" "$game\MonoBleedingEdge\etc" `
    "$PWD\build\PathOfValheimanSmoke\bin\Debug\net48\PathOfValheimanSmoke.exe" `
    "$PWD\bin\Debug\InventorySlots.dll" "$game\valheim_Data\Managed" "$game\BepInEx\core" $pov
```

The probe isolates BepInEx configuration paths in a new temporary directory. It loads original game/PoV assemblies, patches and invokes the actual private PoV metadata-registration method with the final plugin's prefix, checks preserved/different/absent metadata and first-run/repeated registration, validates other external method signatures, and executes explicit readers for the game's original private tooltip fields. It does not publicize or modify input DLLs.

The crafting extension binds the production delegates to original private/public PoV members and invokes the safe static selection/field readers. Return calculation and chest enumeration need Unity and are not invoked. `CraftingOrderProbe` uses real HarmonyX with attributes merged from the final Slots and original PoV patches, but instrumented bodies on a dummy target. It checks both registration orders, PoV's absence, false-return continuation, failures before/after simulated removal, normal upgrades and vetoes. This verifies patch ordering and state/exception propagation, not actual inventory transactions. Only the safety/favorite prefixes acquire the PoV ordering constraint; favorite restoration keeps its existing postfix annotations.

2026-10-17: **60 Mono checks passed**, exit 0, against Valheim client **1.0.20** (25909357) and supplied PoV **5.0.4**, SHA-256 `36E45B65B44667AD596446CAC2AE989041669948C11710B05185E4FE16697DB4`. Of these, 15 checks cover the instrumented Harmony ordering probe. The final plugin's static compatibility check passed **1,150 references, 163 Harmony targets and 52 reflected contracts**, with **0 failures** and **10 existing manual-review items**.

The optional `--engine-hooks` probe attempts the additional row/sort/panel hooks. It was attempted and exited 1 because patch compilation initializes `Player` → `ZSyncAnimation` → Unity's native `Animator.StringToHash_Injected`, unavailable in standalone Mono. This is **not** a passed runtime hook check or evidence of mismatched installed game libraries. Do not replace/publicize game DLLs or stub native game methods to hide that limitation. The default run explicitly reports these hooks as not executed.

Still requiring actual game verification: Big Bag gains/respec, occupied-row removal, save/reconnect, Tidy Pack with equipped/favorite/quick-slot items, multiplayer chest contention, hover delay, Alt-pinned keyword/socket interaction, live tooltip layout On/Off, Mouse3 pins/chat text, tree opening/closing, and dedicated-server startup. For crafting, exercise Salvage/Disenchant in List and Grid, items sharing a recipe but having different quality/socket data, actual returned materials, full-inventory behavior, ordinary equipment-slot upgrades, VNEI/tab transitions, node respec, Box radius and chest access/counts. No game/client/server session was launched for these checks.
