# EpicLoot stacking integration — 2026-09-26

InventorySlots 1.5.15 and InventoryActions 1.1.8 share an optional stacking
adapter in `Shared/Sorting/EpicLootStacking.cs`. No EpicLoot binary is linked or
packaged. Existing client/server access, ownership, favorites and restock-limit
policies remain in place.

## Contracts and behavior

- Bind public `EpicLoot.API` classifiers and
  `EpicLoot.Data.ItemExtensions.Data(ItemData)` /
  `ItemInfo.IsStackableWithOtherInfo(ItemInfo)` once during plugin initialization.
  The latter returns merged metadata or null; no private EpicLoot access is used.
- Accept stackable non-quest materials, tokens, Runestones and ShardStones;
  exclude ordinary enchanted equipment and durability items. Pair identity also
  requires matching prefab/name, quality, world level, variant and cheat marker.
- Preserve data not returned by EpicLoot's merge rule under the existing
  InventorySlots metadata policy or InventoryActions exact-value policy. This
  also protects unresolved EpicLoot component keys.
- Sort and favorite filling use positional `Inventory.MoveItemToThis` so the
  game's EpicLoot-patched `AddItem` applies metadata. Count actual movement,
  clamp to available capacity, and never directly add quantities after a veto.
  Favorite targets never donate or move; callers retain their slot filters.
- InventorySlots replaces `FindFreeStackItem`, bypassing EpicLoot's transpiler.
  For its selected compatible target, apply the public rule's dictionary at the
  same pre-increment boundary as EpicLoot's original lookup postfix. Capacity
  probes remain non-mutating. Skip the old failure cache for EpicLoot materials
  because its key does not encode effect data.
- No item classification/metadata cache is kept. API delegates reset with plugin
  lifetime. A pair API failure disables merges; classification failures protect
  the affected item from direct merging and are logged only once.
- InventoryActions `Start` removes only the known owner/type's patches on three
  private stacking helpers for AdventureTools **0.8.4**. Its Awake installs those
  prefixes before Start. Tracker/input patches and other versions remain intact.

## Original inputs and validation

Supplied DLL SHA-256:

- EpicLoot: `802790E0CBD8A6F951627619A488B84E6A406E8770E2687B2FFF8B63D2ADF9EA`.
- EpicLootAdventureTools 0.8.4:
  `C05B7E529EE3B957C722F7B7CF675A3F5A7C0CC1CC22B8846034DFD5271B1BA4`.

Game transfer behavior was checked against the existing original 1.0.15 client
extraction; the installed compile/contract-check DLL reports Valheim 1.0.16.
Existing publicizer build settings are retained; analysis and contract checks
use original game DLLs, not publicized output. ServerSync/YamlDotNet continue to
be merged through the existing ILRepack targets.

Validation performed:

- Baseline Debug builds: both mods, zero warnings/errors.
- Existing regression suite: 176 checks; its pre-existing StuWard nullable
  warnings are unrelated to this change.
- Shared-source tests: 50 InventoryActions / 53 InventorySlots checks, including
  six original EpicLoot public-contract checks per run.
- Compiled InventoryActions DLL, original game assemblies: 30 ordinary
  favorite/stack checks and six actual-Harmony overlap-cleanup checks on desktop
  CLR. No game objects or tracker UI are run.
- Final Debug and Release builds: both mods, zero warnings/errors. Debug
  deployment to the local game's plugins directory passed SHA-256 comparison.
- Final Release static game-contract checks: Slots 1,103 references / 153
  Harmony targets / 49 reflected contracts; Actions 663 / 47 / 7. No failures.
  Ten existing dynamic/manual entries for Slots and two for Actions remain
  outside this static checker; this does not prove runtime patch composition.
- Both Thunderstore ZIPs match their final DLL, manifest version/dependency,
  README, changelog and English translation sources; the Slots Nexus ZIP also
  matches its DLL. Final assemblies embed ServerSync/YamlDotNet and contain no
  hard EpicLoot assembly reference. The 30 + 6 compiled Actions checks also
  passed against its Release DLL.

Actual Valheim gameplay, EpicLoot's native hooks, Unity startup ordering with the
full profile, multiplayer and crossplay are **not** validated by these tests.
See `build/EpicLootStackTests/README.md` for the reproducible checks.
