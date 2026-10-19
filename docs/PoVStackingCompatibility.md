# PoV crafted-stack compatibility — 2026-10-19

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
