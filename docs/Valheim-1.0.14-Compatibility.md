# InventorySlots / InventoryActions: Valheim 1.0.14

Reviewed and patched 2026-09-17 on main, starting at clean commit
`39f8e257935c07eebb5aad8f3e391526cfb8eff0` (InventorySlots 1.5.2,
InventoryActions 1.0.17). Both projects belong to this checkout. The subsequent
authorized release publishes these changes as InventorySlots 1.5.3 and
InventoryActions 1.0.18. No configuration, saved-data format, RPC, or dependency
was changed.

## Inputs and affected paths

Global reference: `C:/Users/blizz/.codex/references/valheim/INDEX.md` and
`comparisons/1.0.12--1.0.14-windows-x64/mod-patching-guide.md` under that root.
Original snapshot inputs, both using `derived/ilspy-9.1.0.7988-r1`:

- Client: `client-b25364265-windows-x64-20260917T122143Z`, Steam 25364265.
- Dedicated server: `dedicated-server-b25364309-windows-x64-20260917T122143Z-depot-restored`, Steam 25364309.
  The archive's single steam_appid.txt restoration is documented globally;
  the DLLs are unchanged originals.

The baseline InventorySlots build fails with CS1501 at
`UiScrollInput.GetGamepadRightStickY`: the new original ZInput removes the
optional bool argument. The replacement calls the parameterless Y helper,
preserving the native Y sign and the mod's existing scroll scaling.
InventoryActions already builds against 1.0.14 without an API compile error.

InventorySlots replaces native `Inventory.FindFreeStackItem`, so merely
recompiling would still bypass the new native cheat-origin predicate. Its
Harmony target now explicitly selects `(string,int,float,bool)`, binds
`cheated`, and forwards it through the placement policy. Candidate stacks must
match even when no source ItemData lookup scope exists. Existing placement
order, locked cells, metadata rules, optional-mod trusted stacking selection,
and prefix return behavior remain intact.

Both mods' shared identity predicates now require equal `m_cheated`:

- InventorySlots `CanShareInventoryStack`: capacity/automatic placement,
  QuickStack, Restock, TakeAll, tombstone recovery, Sort and favorite Fill.
- InventoryActions `HasSameStackIdentity`: Restock and top-first QuickStack
  transfer targets. Its ordinary Sort and favorite Fill already separated
  markers; that behavior is retained.

Ordinary stacks can still merge with ordinary stacks, and cheated stacks with
cheated stacks. Favorites keep their positions and never donate to other
favorites. The old InventorySlots Sort/Fill metadata helper's cheat propagation
and `PlayerProfile.s_bypassCheatChecks` read were removed: both callers now
require matching markers before merging, so setting an already equal flag is
unnecessary. Custom metadata merging remains. This is not a missing-API
fallback or a policy to erase cheat markers.

## Boundaries retained

The native Player.Update hotbar loop is not replaced by these mods; the existing
UseHotbarItem prefix and keyboard-modifier policy remain. No new gamepad/input
configuration or duplicate hotbar handler was introduced. Custom primary/alternate
binding collisions still need actual UI testing. Terrain, graphics settings,
intro coroutine and native manual-save changes require no identified direct
patch in these projects. Server owner/lease, permission checks, busy state,
timeouts, external MultiUserChest guards and disconnect cleanup are unchanged.

Both are BepInEx plugins deployed to plugins, not preloader patchers. Existing
compiler-only BepInEx.AssemblyPublicizer.MSBuild 0.4.2 use remains; original game
DLLs are not publicized in place. Compatibility checks resolve against the
preserved original client and server assemblies. Existing runtime access
attributes are verified, but do not prove all private accesses work in game.

The vendored ServerSync `valheim-1.0.7-r1` remains unchanged, SHA-256
`b4dd786997f4e90d770f09ef3e9d64154754fe7e8edfb4841795751895b35846`.
It and YamlDotNet are still internalized by the existing ILRepack target.
Optional mod DLLs were not changed or separately certified for 1.0.14.

## Validation

- Baseline: InventorySlots compile failure above; InventoryActions Debug
  succeeded; existing suite passed 171 checks.
- Final Debug builds with `DeployToGame=true`: both zero warnings/errors.
  Only the final merged plugin DLLs were deployed; their SHA-256 matches the
  Steam BepInEx/plugins copies.
- Final Release builds: both zero warnings/errors. The generated package manifest,
  assembly version, BepInEx 5.4.2350 dependency and packaged DLL hash match the
  intended 1.5.3 / 1.0.18 releases.
- Existing suite: 171 checks passed after the patch.
- Original client and server static checks, per role: InventorySlots 1,025
  direct game references, 135 Harmony targets, 42 reflected contracts;
  InventoryActions 561 references, 29 Harmony targets. Zero failures.
  The scanner retains 9 / 1 manual-review entries respectively; dynamic optional
  targets and method-level declarations are not claimed as fully covered.
- Original IL smoke checks: InventorySlots native row transpiler 6 checks per
  role; both mods' auto-pickup exclusion transpiler 10 checks per mod/role.
- Extended compiled-DLL `CompatibilitySmoke --favorite-fill`: mixed marker
  rejection in both directions, matching normal/cheated stacks, Restock and
  QuickStack predicates, source-less lookup, Sort consolidation/count
  preservation, favorite fill/order/location, metadata exclusions and no-op
  cases. InventoryActions passes 30 checks per original role on desktop CLR,
  clean exit 0. InventorySlots reaches all 32 assertions per role on Unity
  Editor Mono 6.13, **then exits with native 0xC0000005**, as previously recorded
  for this standalone runner. This is not a clean whole-process pass.
- The first intermediate Slots run hit native Player initialization through
  the now-unnecessary bypass getter when testing cheated-stack Fill. After
  removing that obsolete propagation, all assertions complete; the independent
  runner shutdown fault remains. No game DLL or test assertion was replaced.

Local logs/reports are in git-ignored `artifacts/valheim-1.0.14`, including
`stack-results.json`, each mod/role API report, and IL/stack logs. Reproduction:

```powershell
dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true
dotnet build InventoryActions/InventoryActions.csproj -c Debug -p:DeployToGame=true
dotnet run --project InventorySlots.Tests -c Debug
dotnet build InventoryActions/build/CompatibilitySmoke -c Debug
# Supply each preserved original Managed directory and the installed BepInEx/core:
dotnet run --project build/CompatibilityCheck -c Debug -- <mod.dll> <Managed> <core> <report.json>
dotnet run --project build/HarmonySmoke -c Debug -- bin/Debug/InventorySlots.dll <Managed> <core>
dotnet run --project InventoryActions/build/RuleIlSmoke -c Debug -- <mod.dll> <Managed> <core>
# Desktop CLR for Actions; Unity Editor Mono for Slots (inspect exit code too):
InventoryActions/build/CompatibilitySmoke/bin/Debug/net48/CompatibilitySmoke.exe <mod.dll> <Managed> <core> --favorite-fill
```

Final Release SHA-256:

| DLL | SHA-256 |
| --- | --- |
| InventorySlots 1.5.3 | `96e37fbab62819a5013393fbecc2605709281db1d49b9d22a83db1be2610e7c9` |
| InventoryActions 1.0.18 | `fdb2556e89faf652c19a823cd1b9034568ee6b8680d3c07e59a63df6a586e915` |

## Remaining execution checks

No actual game, host or dedicated-server session was run. Verify controller
scroll direction/sensitivity, primary and alternate hotbar bindings, pickup and
drag/split/merge, all favorite/normal and cheat-marker combinations, full chest
capacity with a mismatched marker, busy/unauthorized/owner-changing containers,
two-player QuickStack/Restock, and item counts/metadata after save/reconnect.
UI, exhausted-donor removal callbacks, native movement, live Harmony composition
with EpicLoot/Jewelcrafting/etc., and ServerSync multiplayer remain outside the
isolated checks. Old 1.0.12 execution compatibility is not provided by this patch.
