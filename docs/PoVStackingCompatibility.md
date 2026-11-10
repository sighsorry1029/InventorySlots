# PoV compatibility update — 2026-11-10

This update was checked against the supplied original PathOfValheiman **6.0.7**
(public API **1.7.0**), SHA-256
`D6F7D6AE1D4772650E8657AF030022ED2873FE430B48FADF5A57C076FA4069E1`.
The older release notes below describe their own historical builds.

## InventorySlots character and Rune Carver integration — 2026-11-11

The crafting panel now has a Crafting/Character switch immediately outside its
upper-left corner when the PoV equipment integration is active. InventorySlots
continues to own inventory cells; PoV's full equipment Sheet is not restored.

- Appearance uses the public `Valheiman.Api.Looks` API. Click an owned hotbar
  item or an actually equipped helmet, chest, legs, cape, belt, trinket or quiver
  in the InventorySlots grid, select a look, then apply it. PoV owns unlocks,
  rune consumption, appearance metadata and visual refresh. Original look resets
  through that same API. Hidden appearance is limited to supported equipped
  roles. Extra YAML visual attachments and a live portrait are not added here.
- The target must retain the same player, actual item reference and inventory
  cell at application time. Removed, replaced or moved items invalidate the
  selection. A failed API result is never treated as success. The expensive
  native choice/refusal lookup runs on selection/application, not idle frames.
- Skills and Stats host PoV's existing GemBoardPanel and CharacterSheet under
  the Character page. These are **internal**, individually checked constructor
  and lifecycle contracts, not public UI APIs. Closed delegates are bound once
  per view. A failed tab is isolated for that GUI; other tabs and crafting stay
  available. Native Chisel navigation opens Skills with its original unlock
  state. PoV still owns held-gem settlement and the mutation rules.
- Closing freezes the visible page during the inventory outro, then restores
  the crafting CanvasGroup. GUI/player replacement and teardown dispose only
  owned views. The page stays next to the crafting branch in sibling order,
  below native dialogs and tooltips. Craft execution is blocked while Character
  is displayed.
- Rune Carver Disenchant previews use PoV's typed return overload, including
  corrupted dust and its scaled amount. Native Enchant behavior is retained.
  Disenchant/Salvage, Enchant and VNEI are reconciled together in the final tab
  layout pass; search controls move above this wider tab row when needed.

Debug build/merge/deployment passed with zero warnings/errors. Original game
Mono with the supplied original PoV passed **621 cold / 623 warm** managed checks
and exited zero with Mono cleanup. New checks execute native Looks rune spending,
reset and rejection, the actual InventorySlots reference validator, native
singleton cleanup and typed dust returns. Rendering calls and world services
are explicit test boundaries; null-root cleanup must never call Unity Destroy.
The crafting regression suite also passed, including **400 tab/header** checks.
Final InventorySlots DLL and Steam plugin copy both have SHA-256
`017351CE158CC0402D3B72FFB9C4220F151053E75105AEBC7F394CBCC8027C23`.

Actual Unity rendering, controller navigation, Chisel/gem drag, appearance on
remote players, closing/reopening and reconnect still require in-game checks.
No game session, Release build, version bump, commit or push was performed for
this character UI step. InventoryActions is unchanged by this step.

### Applied-look display follow-up — 2026-11-11

Appearance now identifies the currently applied look below the selected item's
name and marks its choice with a blue Applied badge. The pending choice gets a
gold outline, independently of the applied marker. Existing PoV variants and
Hidden remain distinct through `Looks.SameLook`.

The public `Looks.Current` result is cached for the selected item. A change to
that item reference or the reviewed `valheiman.equipment.v1.look` value invalidates
it; the key is read only as a change signal. Actual look resolution stays in PoV.
Reset, external appearance changes and loss of the target refresh/clear the cache,
and closing destroys the cached selection. Idle UI refresh does not repeatedly
scan ObjectDB through `Looks.Current`.

Debug merge/deployment passed with zero warnings/errors; the original Mono
suite passed **627 cold** checks and exited zero. The six additional checks cover
original/hidden/reset appearance, unchanged repeated refresh, item replacement
and target clearing. Steam plugin and build DLL SHA-256 were verified equal.
Rendered highlights have not been checked in game. World-character preview,
rune-footer redesign and quiver replacement are reviewed proposals, not part of
this follow-up implementation.

### Local preview and native quiver replacement — 2026-11-11

The subsequent patch adds a free, local-world appearance preview on selection.
Locked looks can be selected, but final application still calls PoV's original
`Looks.WhyNot/Apply`; dismantling unlocks and rune consumption are unchanged.
The footer is a Rune of Guise icon and count; its tooltip uses the actual item
name. Locked choices explain the dismantling requirement. Failed application
shows its refusal, including insufficient runes, instead of disabling the button.

Preview changes only arguments to the original private VisEquipment renderer
setters, using cached field accessors and exact local equipped-item references.
It never changes ItemData, persisted VisEquipment item fields or ZDO. Supported
channels are normal armor, belt, trinket, hands and sheathed weapons; quivers,
custom attachments and unequipped hotbar items keep selection/application but
have no live preview. Closing begins restoration immediately; switching pages,
targets, equipment or players, death, Apply/Reset and cleanup also restore the
latest committed visuals. No extra portrait or ring slots were added.

Native PoV quivers now replace an occupied quiver cell by moving the old quiver
into the incoming quiver's regular cell, including a full backpack. The wrapper
around PoV's native Equip prefix commits only when native equipping completes;
queued actions leave coordinates unchanged until then. Exceptions roll back
native equipment and item metadata/coordinates. Rejected recovery retains both
items, reconciles their worn markers with native ownership and disables only
replacement for the session. Registered external quivers retain the previous
empty-destination policy; their callback replacement semantics are unverified.

Debug merge/deployment passed with zero warnings/errors. Original game/PoV Mono
passed **683 cold** managed checks, exit 0 and successful cleanup, including
preview argument isolation/cancellation, immediate/queued native swaps, full
backpack, retained ammunition, failed equip, rejected recovery, acceptance and
late external registrations. Unity prefab identity, shader IDs, rendering,
publication and queue timing are explicit test boundaries. Reconciliation is
checked by repeating provider Put, not a complete world inventory audit.
Build and installed DLL SHA-256 match:
`373262A055431F0D43948A594A0487021369F62C9BD9CF61B511EA26E4BE6263`.
Actual UI, cloth/LOD, input timing, multiplayer observations and reconnect remain
game checks; no game session or release/commit/push was performed.

## Metadata normalization

Both inventory mods now execute PoV's native `StackRules.SameData`, including
`Implicits.Settle`, before applying an owned pair's registered merge rules.
InventorySlots also normalizes before its early positional rejection and source
metadata snapshot. PoV retains responsibility for obsolete implicit-roll cleanup,
corruption exceptions and prefab-preview exclusions. The adapter does not copy
that cleanup algorithm or indiscriminately delete metadata.

The existing input/call validation accepts the reviewed two argument-specific
`Settle(ItemData)` calls with discarded return values. New native bookkeeping
exclusions are recognized for contract validation, but **do not** make our merge
policy ignore `pt.fish.caught` or `valheiman.equipment.v1.cell`. Unknown metadata,
real stat differences and FineDining's owner guards remain protected. An
unrecognized comparison keeps the native restriction and reports the fallback;
this bounded IL check is not a general proof of semantic equivalence.

## Containers and retired storage

- RuneChest and Runic Anvil own ledger storage. Their live component queries
  exclude them from generic nearby transfers, container sorting and shared
  container access, including execution-time checks and dedicated-server paths.
  No negative classification result is cached because PoV can attach its Store
  component after `Container.Awake`. Missing queries fall back to the reviewed
  component identity; unverifiable storage blocks generic container writes.
- Ordinary chest execution and RPC replies remain owned by the inventory mod.
  Native RuneChest replies alone are forwarded to PoV, after the existing handoff
  fence. PoV retains its ownership wait/cancellation path. Runic Anvil keeps its
  native lease and ledger deposit flow. Bulk deposits exclude favorite/equipped
  items and protected cells; manual deposits are unchanged. These native bulk
  paths protect whole favorite stacks, while ordinary chests retain the separate
  favorite-target surplus policy. Deferred anvil callbacks are invalidated if
  the adapter is disposed.
- InventorySlots recognizes `siege.bolts` / `BoltMagazine` as the retired role
  identified by the reviewed descriptor and `RetireNow(Player)` contract. No
  slot is added. Its coordinate remains available for existing load recovery,
  while the misleading unhosted wearable warning is omitted. Native recovery
  remains PoV's responsibility. Other unknown roles keep their existing handling.
- The previous 5.3.2 source/type inventory contains neither special container
  type, so it does not enter the new RuneChest response-contract checks. No DLL
  version, hash or MVID is used as an execution allowlist.

## Verification for the earlier metadata/container step

Both Debug builds and final DLL merges passed with zero warnings/errors, using
`DeployToGame=true`. Steam plugin copies match the final build SHA-256 values:

| DLL | SHA-256 |
| --- | --- |
| InventorySlots | `21EA781B68208E10ADA9532FA8031C9382E01DA894A2F50088BEFF26F04D1940` |
| InventoryActions | `4B8DA2D759F41E603B202DA84D0356DC0C6DC41E95CFE49CB63E37DE5BBD052D` |

- Original game Mono/PoV: InventorySlots **592 cold** checks, then **603 warm**
  checks with actual FineDining callbacks; InventoryActions **159** with those
  callbacks and **33** without PoV. All processes exited zero and completed Mono
  cleanup. FineDining test DLL SHA-256:
  `3CD19BD20A7F973F7F3FA0BD9EB086E2848DEDF16C703B822B19C7805D690A62`.
- Existing InventorySlots unit suite: **195** checks; shared quick-stack suite:
  **204** checks. Diff whitespace validation passed.
- Regression coverage includes native normalization/corruption exclusions,
  metadata-owner composition, stale/partial/exceptional transfers, patch order,
  consumed/denied RuneChest replies, late special-container detection and the
  actual `Container.StackAll` route with PoV installed first. Ledger/engine
  services are controlled boundaries; this is not a network or Unity-world test.

Game DLLs used by the isolated runtime are originals, without analysis-time
publicizing. Existing compile-time publicizer configuration was retained. No
actual game, host/dedicated server or multiplayer execution was performed.
In-game checks still needed: rune chest/anvil bulk versus manual deposits,
favorite/equipment protection, remote ownership replies and reconnect, and
food stacking with FineDining. No version bump, Release ZIP or publication was
performed for this patch.

# Historical crafted-stack compatibility — 2026-10-19

Reviewed against original PathOfValheiman **5.0.10**, FineDining **1.1.7**,
and Valheim **1.0.20** client assemblies and x64 Mono. No game DLL was publicized
for analysis or runtime tests; the projects retain their existing compile-time
publicizer configuration. The implementation preserves configuration, save
formats and network protocols. The subsequent release is InventorySlots 1.6.14
and InventoryActions 1.1.19.

## Cause and scope

PoV's `CraftWorkHooks.Crafted` can assign a new `pt.work.made` timestamp and
`pt.work.made.id` GUID to each new crafted stack. The self-crafted armor quest
uses these to track one equipped item. Separate food batches can therefore
differ in these fields even when made almost simultaneously; split clones retain
the same values. InventorySlots previously registered every PoV field with exact
equality before PoV's own first-wins registration callback, rejecting such merges.

`Shared/Sorting/PathOfValheimanStackMetadata.cs` relaxes only those two fields.
Their destination values, including absence, remain unchanged together. The
source and any partial remainder are untouched. Actual food/brew bonuses,
rarity, all other PoV fields and unknown metadata retain their existing checks.
The production key list still comes from PoV's checked `InventorySlotsCompat.OurKeys`.
No blanket `pt.*` or `pt.work.*` exemption, ID deletion, clock rounding, or
non-stackable equipment conversion is performed.

InventorySlots uses its existing manual/automatic metadata integration and
FineDining's existing guarded public registrations. InventoryActions links the
same internal metadata policy implementation, without adding a new public API.
When PoV compatibility is ready, its own sort, favorite fill, local quick-stack,
and restock paths can handle recognized metadata. Unknown fields still exclude
items from automatic merging; a differing actual PoV stat still rejects a pair.
Normal manual crafting/dragging in InventoryActions is not replaced by this adapter.

InventoryActions binds FineDining's three merger/validator pairs once in `Start`,
after optional plugin `Awake` calls. Exact internal method signatures and constant
keys are checked before any registration. The owner is tested for Unity destruction
by the guards. Missing/unsupported FineDining leaves its metadata protected.
No reflection lookup or item-result cache is added to per-frame code.

Direct sort/favorite merges compose metadata before changing quantities. Occupied
target transfers revalidate the actual target, use original `MoveItemToThis`, and
compose only after confirmed target growth, including partial moves and mutations
followed by an exception. A second inventory notification persists changed metadata.
This covers local chests that FineDining's native ownership tracker does not cover;
composition is idempotent when that tracker already ran. FineDining's own
`IsSpoiled`/`CompleteKeepOriginalExpiry` callbacks preserve its completed-spoilage
invariant instead of reintroducing clocks/lifetime on an already spoiled stack.

## Deliberately unchanged boundaries

- PoV's **native Sort** still uses its original metadata comparison. It adds
  quantities directly without FineDining's native AddItem tracker; relaxing that
  comparison alone would lose freshness. Use the InventorySlots/InventoryActions
  sort path for the supported composition above.
- InventoryActions **MultiUserChest remote transfers** retain the old eligibility
  (metadata-free or supported EpicLoot items), including response validation.
  Local callbacks do not establish a remote owner's metadata merge contract.
- Favorites, PoV node gates/equipment boundaries, restock limits, cheat-marker
  identity, existing EpicLoot policy, RPC ownership and access checks remain in place.
- FineDining sources and PoV DLLs were not changed. Implementation validation
  preceded the separately requested version bumps and Release packaging.

## Implementation verification (before the version bumps)

Baseline Debug solution build and existing tests passed before changes. Final
`dotnet build InventorySlots.sln -c Debug -p:DeployToGame=true` succeeded with no
warnings/errors; merged DLLs were copied to Steam's `BepInEx/plugins` and SHA-256
matched their build outputs. The initial full unit-project rebuild had the two
pre-existing nullable warnings in `StuWardCompat.cs`.

- InventorySlots unit checks: **190**; InventoryActions rule checks: **144**;
  UI fixtures: **164**; EpicLoot simulated-host regression checks: **44**.
- Original Mono PoV checks: InventorySlots **360 cold / 362 warm**;
  InventoryActions **112**. MUC session checks: **10**; favorite-fill checks: **30**.
- Original-assembly static contracts: **0 failures** for both DLLs. InventorySlots
  checks 1,153 references / 163 Harmony targets / 52 reflected contracts;
  InventoryActions checks 665 / 51 / 7. Existing dynamic targets still require
  manual review (10 and 2 respectively).

The shared `build/SharedSmoke/PoVStackMetadataChecks.cs` uses the supplied original
PoV keys and actual FineDining callbacks, not copies of their algorithms. Set
`INVENTORY_TEST_FINEDINING_DLL` to an original FineDining DLL before running the
existing PoV Mono runners. Without it, owner-dependent checks explicitly report
NOT RUN. Coverage includes separate craft IDs, unchanged clones, unknown/stat
differences, malformed and mixed clock values, earliest expiry, source preservation,
partial/full/rejected transfers, destroyed owner, failed binding, completed spoilage,
stale target and an exception after actual native quantity movement.

These are isolated managed tests with original game methods and controlled Unity
service substitutes (including disabled native logging), not a running Unity world,
live BepInEx startup, dedicated server or multiplayer test. Actual same-food crafting,
dragging, open-chest transfers and reconnect should still be checked in game. Real
stat differences can legitimately keep apparently identical food stacks separate.

Original PoV SHA-256:
`252B60914EFC9F402A977A8C235E28BCB363D7BB7CEEBDF5F22A228518282E74`.
Original FineDining SHA-256:
`4B5DC2DDFBB9B2BEB38F6DDCD34F6B8F374819D3525A233C1BC2AFB9CBC5C818`.
Final Debug InventorySlots SHA-256:
`FB06C8DAB3458B9541B3CB87CE598B95F87473DF0FED032145371EB907D48E66`.
Final Debug InventoryActions SHA-256:
`FF4A46861CA7BF81EA3FF9EE0BE7D61286887584496E8D2321DDFEC47F1695B9`.

## Release verification — InventorySlots 1.6.14 / InventoryActions 1.1.19

Both requested version bumps, English changelogs and manifests are synchronized.
The Debug solution build succeeded without warnings/errors and both deployed DLL
hashes matched. Release builds succeeded: InventorySlots' solution emitted only
the same two existing nullable warnings in its test project; InventoryActions
reported zero warnings/errors. The 190 unit checks passed after the version bumps.

The final merged Release DLLs passed the original Mono PoV checks (**360 cold /
362 warm** for InventorySlots, **112** for InventoryActions) and the same static
contract checks with zero failures. Assembly and BepInPlugin versions, merged
ServerSync/YamlDotNet, absence of mandatory PoV/FineDining assembly references,
and all six entries in each Thunderstore ZIP were verified against final outputs
and package sources. The BepInEx manifest dependency remains 5.4.2351. Actual game
and multiplayer execution were not performed; no store publishing status was queried.

| Final artifact | SHA-256 |
| --- | --- |
| `bin/Release/InventorySlots.dll` | `A42F054D2818E01B919F91675C5187289376AB3DD3AADA8C14D9428CC23E6E55` |
| `Thunderstore/InventorySlots_v1.6.14.zip` | `C41745E2C36EB3779531A6E2BB8360BD8A4B1C57A4BEBAFB68210A6679C891A2` |
| `InventoryActions/bin/Release/InventoryActions.dll` | `99AFF5B5C8526E761FC92ADF5D983687AA37AFFBB109C4D7CC9558BBE2883CD0` |
| `InventoryActions/Thunderstore/InventoryActions_v1.1.19.zip` | `49F3DFC5740C926581D18BF522ADF4972764FC62930BA8FE51726EE6B49AE7F5` |
