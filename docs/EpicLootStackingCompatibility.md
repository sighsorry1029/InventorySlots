# EpicLoot stacking integration

## Modern MagicItemData adapter — 2026-11-05

InventorySlots 1.7.0 and InventoryActions 1.2.0 bind either the complete
legacy public `ItemInfo` contract below or, when both legacy types are absent,
`EpicLoot.MagicItemData.StacksWith(ItemData existing, ItemData item)`. The supplied
EpicLoot **0.15.6** has removed `EpicLoot.Data.ItemInfo` and `ItemExtensions`.
Its assembly version is still 0.13.0.0; the BepInPlugin version is 0.15.6.

- The modern method is **internal static**, not a public integration API. Resolve
  its exact declared signature once and bind an explicit delegate. Call the
  destination first, source second. No hard EpicLoot reference, publicization,
  copied rule implementation, version allowlist or full-DLL hash gate is added.
- Modern approval returns no merged keys. Slots' registered metadata guards and
  composers remain in charge of foreign fields; Actions retains exact equality
  for unmerged EpicLoot-item metadata. Actions deliberately does not broaden that
  policy: its EpicLoot transfers bypass the general Actions metadata composer.
- The inspected predicate compares raw magic-component data and does not write
  metadata. This is **not** a promise that EpicLoot classification is read-only:
  `IsMagicItem -> Get/Resolve -> MagicItemComponent.Reload` can normalize its own
  serialized data, fix effect values and synchronize Indestructible state.
- Partial legacy APIs or missing/malformed modern methods are rejected. Existing
  exception handling disables only this adapter, not inventory use, saving or
  joining. No per-pair reflection or new item cache is introduced.
- Native positional transfer, actual movement accounting, identity checks,
  favorites and slot restrictions remain unchanged. HarmonyX runs all prefixes;
  modern EpicLoot's lookup respects `__runOriginal`, so no extra patch-order
  override or removal of another mod's patches is required for this pair.
- The reviewed FourPlayer bridge is not needed for 0.15.6 and skips it anyway.
  That bridge still intercepts InventorySlots 1.6.25 with EpicLoot 0.15.4/0.15.5;
  running that older combination may replace this adapter with its stricter
  all-metadata-equal rule. This patch does not disable the third-party bundle.

Original EpicLoot SHA-256:
`3BD349EEA71565B43EF7B70D656926039EFDACEC1FD0AEEE97D30787571BDD58`.
Input: Gale `qwerqwer/BepInEx/plugins/RandyKnapp-EpicLoot/EpicLoot.dll`.
Original game input: global reference snapshot
`client-b26249040-windows-x64-20261029T094619Z-depot-restored`, Valheim 1.0.21.

Validation:

- Baseline shared-source tests: Actions 44 / Slots 47; baseline and final Debug
  mod builds: zero warnings/errors. Existing production publicizer configuration
  is retained; original DLLs are used for metadata and isolated runtime checks.
- Modern fixtures + supplied original DLL contracts: Actions **57**, Slots **60**
  checks. Legacy fixtures: Actions **50**, Slots **53**. Covers native veto and
  partial transfer, owner guards, unknown data, binding failure and unrelated
  assembly version/identity changes. Native movement in these fixtures is fake.
- Both final merged DLLs: **18 checks each** on original Valheim x64 Mono with
  the supplied original EpicLoot. Real internal delegate invocation, raw magic
  data approval/rejection and metadata preservation passed. The real EpicLoot
  lookup prefix preserves a rejecting boundary stand-in in either registration
  order. This does not execute the complete Slots placement policy or plugin
  lifecycle. Both processes exited 0 and completed Mono cleanup.
- Static original-game checks: Slots 1,157 references / 164 Harmony targets /
  52 reflected contracts, Actions 665 / 51 / 7; zero failures. Existing manual
  entries remain 10 / 2. The modern internal endpoint is additionally checked by
  the dedicated tests above.
- Final Debug DLLs were deployed to the Steam game's plugins directory and
  matched by SHA-256. Manifest BepInEx dependencies were corrected to 5.4.2360.
  The initial adapter validation kept mod versions unchanged and did not create
  Release ZIPs. The adapter is included in the 1.7.0 / 1.2.0 releases.

Actual game pickup, sorting, restock, Unity UI, optional-mod combinations and
multiplayer/server execution remain untested. No gameplay performance claim is
made. The older public contract remains fixture-tested; this run did not execute
an older EpicLoot DLL.

## Original ItemInfo integration — 2026-09-26

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
