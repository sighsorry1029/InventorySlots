# Changelog

## 1.4.13

- Fixed missing slots and items when a 4x4 container is the first chest opened or previewed, including custom container sizes configured by other mods. The original game now creates the missing UI cells without changing container capacity or item data.
- Fixed container previews incorrectly blocking hover restock (Alt+E by default) and pending nearby-container ownership handoffs while the inventory is closed.
- Clarified that setting Enable Progressive Rows to Off immediately unlocks all extra rows set by Maximum Extra Rows (three by default), without item discovery. Haldor's purchased rows remain independent.

## 1.4.12

- Fixed container previews that could leave the player inventory visible alongside the hovered container. Shared UI ancestors now retain only the branch leading to the read-only preview.
- Moved Restock and Auto Pickup Exclude controls into the player-grid render hierarchy used by Trash, so container panels and native dialogs remain above all three controls. External toolbar listeners and objects are also released with the inventory UI lifecycle.
- Changed the Restock limit editor to keep an in-progress numeric buffer and clamp it to `0..max stack` when editing ends. For example, an item with a maximum stack of 30 now saves both `40` and `230` as `30` instead of restoring `4` or `23`.
- Removed the redundant automatic-save status and empty-hand Trash hover tooltip, added a localized title to the delete confirmation, and shortened the quick guide's Restock configuration section.

## 1.4.11

- Restored the configurable built-in multi-user chest feature for standard player-built chests. Multiple players may keep a chest open for viewing, while drag/drop, stack/restock, take-all, sorting, and nearby-container actions wait for an approved ownership handoff and fresh inventory state before changing items. Stale selections, denied or timed-out requests, ownership/token changes, and duplicate responses fail without moving items.
- Added client-side Restock and Auto Pickup Exclude controls below the player inventory. Dropping an item registers its rule without moving it; restock quantities and removals save immediately, and excluded items remain available through manual pickup.
- Kept the rule buttons aligned with the last visible regular inventory row, including native purchased rows. Either button can be hidden independently, and Restock shifts into the adjacent position when Auto Pickup Exclude is hidden.
- Updated rule panels to use Valheim's Craft button and inventory-slot visuals, reduced their width, kept them below native dialogs, and fixed missing LiberationSans font warnings by assigning an existing game font before activating generated text.
- Retained the standalone MultiUserChest compatibility boundary: when that mod is active it controls shared opening, and InventorySlots does not start non-owner area transfers through its own protocol.

## 1.4.10

- Fixed the mouse cursor remaining captured after a built-in multi-user container opened while the Use key was held. The remote-container update path now releases Valheim's stack-wait state together with the hold state, matching the vanilla container lifecycle.
- Preserved the existing multi-user access, ownership, request-ordering, and item-transfer safeguards; this change only restores normal mouse input after opening the container.

## 1.4.9

- Fixed special upgrader Idol resources appearing in ordinary crafting requirements. The compact requirement strip and pinned/hover tooltip rows now follow Valheim's `m_upgraderResource` and current-station policy, while special upgrader stations continue to show their applicable Idol costs.
- Kept crafting availability, resource consumption, search, and tier sorting behavior unchanged; Valheim remains authoritative for validating and consuming the active station's resources.

## 1.4.8

- Fixed built-in multi-user containers failing to open and repeatedly logging an unknown RPC warning. The intercepted request now replies through Valheim's registered `RPC_OpenResponse` method while retaining the existing access, distance, ownership, and duplicate-processing guards.
- Fixed Wide Pockets and Deep Pockets purchases expanding and shifting the entire player inventory panel. Native purchased rows still increase storage independently, while InventorySlots continues to size only its visible grid and background viewport.

## 1.4.7

- Updated InventorySlots for Valheim 1.0.7. Valheim's purchased Wide Pockets and Deep Pockets rows now extend the inventory independently from InventorySlots' three progression rows, preserving the native unlock state, capacity changes, overflow handling, death recovery, and UI sizing.
- Limited InventorySlots progression to Hard Antler, Swamp Key, and Wishbone. Dragon Tear and Yagluth Drop no longer add mod-owned rows because the two new vanilla pocket upgrades own the later expansion stages.
- Updated changed game API and UI boundaries for inventory resizing, grid elements and input, split dialogs, container revisions, item serialization, and equipment visuals. Custom equipment now tracks item quality, and multi-user transfers preserve Valheim's cheated-item marker without weakening ownership, revision, rollback, or duplicate-request checks.
- Updated the bundled ServerSync compatibility build for Valheim 1.0.7 while retaining configuration/version contracts and additional login-packet ordering protection.
- Expanded the default `ResourceMap.yml` from 132 to 210 entries. Deep North sorting now covers verified materials, crops, trophies, molds, and conversion-only outputs, while Writhan resources remain Swamp, Hook remains Mistlands, and the lava blob trophy remains Ashlands. Existing generated maps are preserved and require a manual merge to receive the additions.
- Updated the project and compatibility checks to build against the current original game assemblies, corrected the BepInEx package dependency to 5.4.2350, and added original client/server contract and Harmony resize-patch verification.

## 1.4.6

- Increased the crafting queue limit to 999. The quantity field accepts three digits and adjusts its text size to fit, while each queued craft retains the existing material, inventory-space, and crafting-station checks.
- Highlighted the upgraded values in yellow in both pinned and hover upgrade-comparison tooltips.
- Fixed the Quick Slot HUD remaining visible when vanilla HUD visibility is disabled, including Ctrl+F3. Hidden HUD positions no longer overwrite the saved Quick Slot HUD anchor.
- Replaced generated mouse-wheel graphics with Valheim's native wheel icon. Improved the inventory side hint, Alt+wheel hint, quantity-field icon, and shared crafting/upgrade/socket button alignment.
- Simplified crafting wheel handling and tooltip scroll state, and reduced temporary allocations in pending container-operation cleanup and UI projection while preserving transfer validation and compatibility behavior.

## 1.4.5

- Disabled InventorySlots' extra-slot backup save and restore in multiplayer with ServerManager, matching the existing ServerCharacters policy. This prevents stale backup data from restoring items after a newer server inventory snapshot. Single-player recovery is unchanged.

## 1.4.4

- Improved the feature guide with a darker size-to-content background and screen-aware placement for better readability across languages and resolutions.
- Added a collapse control whose state persists in client UI state. Its guarded toggle remains clickable while the inventory is open without intercepting item drags or modal dialogs.
- Reworked the hotbar switch hint into a stable single-line arrow and localized key label positioned after the actual eighth hotbar slot. Duplicate HUD objects and redundant per-frame text layout are removed, preventing overlap, flicker, and missing text after HUD recreation.

## 1.4.3

- Expanded the default `ResourceMap.yml` with 22 additional vanilla resources, keys, crops, mushrooms, and trophies from Black Forest through Ashlands. `SerpentMeat` remains in the Ocean tier, and redundant later entries for `Resin` and `BoneFragments` were removed without changing their effective tiers. Existing generated resource maps are not overwritten and must be merged manually or regenerated to receive the additions.

## 1.4.2

- Added a localized feature guide beside the hotbar with live key bindings, highlighted controls and configuration paths, and a client-only `Show Feature Guide` toggle.
- Changed food classification so any food with positive eitr is Eitr Food; otherwise health greater than stamina is Health Food, while equal or greater stamina is Stamina Food. Items without positive food stats remain unclassified.
- Fixed the Quick Slot panel position so unlocking or showing the Equipment Slot panel no longer moves it. Invalid saved panel coordinates are normalized, and client-state files are replaced atomically.
- Refreshed visible pinned inventory and crafting tooltips and Quick Slot HUD tooltip text on a bounded cadence, keeping time-varying FineDining and other item state current without resetting tooltip scroll positions.
- Fixed full-inventory upgrades of equipped Equipment Slot items. Only the exact slot vacated by the matching upgrade may be reused, the replacement and equipped state are committed transactionally, and failures restore the original before resources can be consumed. The safety boundary is ordered around Jewelcrafting, Recycle_N_Reclaim, and FineDining crafting hooks.
- Hardened full-inventory and special-slot mutations with protected swaps, exact-reference ownership checks, safe equipped-item world drops, bounded Take All amounts, duplicate custom-equipment cleanup, recovery for removed or reordered tail slots, and fail-closed invalid slot coordinates.
- Hardened built-in multi-user chest transfers with shared authoritative mutation validation, support for moving only whole intentional over-stacks, and causally tracked world-drop and shutdown recovery so uncertain delivery cannot create both an inventory fallback and a live drop.
- Extended stack-metadata policy registration with an optional symmetric, fail-closed compatibility predicate. Removed the built-in BeingSpoiled clock fallback and the public `BeingSpoiledExpiryWorldTicksKey` constant; integrations must now register their own authoritative merge and compatibility policy.

## 1.4.1

- Updated Expandable inventory rows to reveal newly unlocked progressive rows once when the local player first discovers a configured unlock item. The added space becomes immediately visible, while character loading and later rediscovery leave the locally remembered row count unchanged.

## 1.4.0

- Added EpicLoot 0.13 public-API compatibility. Items in InventorySlots quick slots are excluded from EpicLoot's Sacrifice tab, custom quick-slot HUD cells show and clear EpicLoot rarity backgrounds, and custom-equipment changes refresh EpicLoot effects and worn visuals through its supported cache endpoint. EpicLoot remains optional, with the existing compatibility paths retained for older versions.
- Updated Quick Stack, Restock, Take All, and related container actions to recognize EpicLoot ShardStones and magic crafting materials through the public API. Effect-bearing Runestones remain excluded from automatic stacking.
- Hardened Sort so it combines only stacks whose metadata InventorySlots governs and has confirmed compatible. Items with unregistered third-party custom data are still repositioned but remain separate, preventing per-item effects or other metadata from being collapsed.
- Fixed the Restock Target Limits editor so negative amounts retain their runtime meaning and normalize to `0` instead of being rewritten as positive values; runtime parsing and editor normalization now share the same entry rules.

## 1.3.9

- Fixed Jewelcrafting socket limits becoming stale while the Socket tab remained open. Socket recipes now adopt the rebuilt state after each attempt and revalidate the exact recipe and item both when crafting starts and immediately before completion, preventing table-level and maximum-socket limits from being bypassed.
- Hardened keep-on-death and tombstone restoration so preparation failures roll back removed items, escrow is released only after exact inventory ownership is confirmed, and a final non-overwriting path preserves items that normal restoration could not return.
- Hardened direct Quick Stack and Store All transfers with ownership-safe positional moves, mutation-time equipped-item checks, and trusted custom stack-metadata handling, removing the temporary shared-item-reference path that could risk duplication or loss.
- Hardened built-in multi-user chest requests against transient RPC failures by publishing pending state before uncertain sends, restoring pre-publication escrow exactly once, and retaining the same request and escrow for safe receipt polling and retries.
- Prevented quest items from being trashed, including a final policy check when the confirmation is accepted.
- Removed unused crafting, inventory UI, tooltip-cache, classification, and layout paths while preserving existing behavior and compatibility boundaries.

## 1.3.8

- Shared hover-hold Area Quick Stack and Area Take Stacks success effects with currently loaded nearby players: VFX appears at up to 10 changed containers and the SFX plays once at the interacted container.
- Kept the shared effects transient and local on each receiving client, with distance/configuration checks, bounded rendering, and five-second cleanup. The RPC is not persisted or retried, so players entering the area later do not replay completed actions.

## 1.3.7

- Fixed long crafting, upgrade, and socket-tab hover tooltips so their scrollbar stays inside the panel and the first wheel input scrolls immediately.
- Prevented an overflowing hover tooltip and its underlying crafting tab from scrolling together, while preserving existing pinned-tooltip input behavior.

## 1.3.6

- Fixed inventory, container, and crafting-result fork icons so they appear only for actual `Consumable` items with direct health, stamina, or eitr food stats. Raw ingredients can still use `m_appendToolTip` for food grouping without being shown as edible.
- Removed the obsolete standalone fork-icon subproject; direct-consumable fork coloring remains built into InventorySlots.
- Updated BeingSpoiled stack merging for its signed clock format: positive values are running world deadlines and negative values are frozen remaining durations. Mixed states now compare effective remaining time on the shared ZNet clock and preserve the destination state.
- Ensured an already-expired running BeingSpoiled clock cannot be rescued by merging into a Mountain-frozen stack; the result remains immediately due for spoilage.
- Added a load-order-safe BeingSpoiled policy handoff. Its authoritative merge callback can replace InventorySlots' fallback once, while malformed clocks and mixed states without an available world clock remain conservatively unmerged.

## 1.3.5

- Added mergeable stack-metadata support for BeingSpoiled. Food stacks with different `sighsorry.BeingSpoiled.ExpiryWorldTicks` values can merge while the combined stack keeps the earlier expiry.
- Applied the same metadata rule to automatic and positional adds, sorting, container actions, and owner-authoritative multi-user container transfers without weakening exact checks for unrelated custom item data.

## 1.3.4

- Added formal CircletExtended compatibility that synchronizes its native circlet state across equip, restore, unequip, death, and configuration changes while avoiding recursive custom visual attachment and duplicate weight or durability handling.
- Added a conditional HipLantern equipment slot. It is available when HipLantern is installed with `Use utility slot = false`; HipLantern remains the single owner of its visual, weight, fuel drain, heat multiplier, and recharge behavior. Utility-slot mode continues to use HipLantern's native vanilla utility path.
- Added the HipLantern slot to the default `BepInEx/config/InventorySlots/InventorySlots.yml`. Existing generated files are not overwritten. To add it manually, insert the following entry at the desired position under `Slots:`. The `name` value may be changed; keep the `id` and `HipLantern` prefab value unchanged.

```yaml
  - id: hiplantern.lantern
    name: Hip Lantern
    items:
      - HipLantern
```

## 1.3.3

- Fixed Area Quick Stack and Area Take Stacks success VFX being created as networked objects, which could replay a completed action's sound to players entering the area later. The effects are now local-only and cleaned up after playback.

## 1.3.2

- Extended the built-in multi-user chest support to Area Quick Stack and Area Take Stacks when another player owns the loaded zone or target container. Remote targets run as sequential owner-authoritative item transactions while retaining access, eligibility, and distance checks.
- Hardened pending remote transfers so durable owner receipts can still resolve an operation after its container object closes, unloads, or changes owner, reducing ambiguous retries during area batches.
- Fixed CurrencyPocket compatibility so the weight panel uses its overlap-aware layout with InventorySlots even when Jewelcrafting is not installed, without adding a second offset when Jewelcrafting is present.

## 1.3.1

- Fixed cold-start character loads moving items out of occupied progressive quick-slot and inventory rows before prefab-to-item-name lookup data is ready.
- Normal loads now preserve occupied progression rows without authorizing reset reconciliation; explicit progression resets and genuinely locked-row recovery still run after lookup data becomes available.

## 1.3.0

- Added a Jötunn-free built-in multi-user chest implementation, enabled by default for standard player-built chests. It supports Ctrl-click, drag and split transfers, stack placement, whole-stack exchanges, remote consume and world drop, Take All, and Place Stacks through owner-authoritative transactions.
- Preserved exact item state, including Jewelcrafting socket custom data, while validating pre-mutation stack state and projecting pending remote changes in the container UI.
- Hardened remote transfers with immutable requests, bounded payloads, retries, duplicate prevention, acknowledgement receipts, escrow recovery, and conservative handling of owner changes or ambiguous results. Take All and Place Stacks run as sequential item transactions and stop on the first unresolved conflict.
- Made the external MultiUserChest mod take precedence automatically, and limited the built-in implementation to eligible player-built chests while excluding tombstones, carts, ships, and unsupported container types.
- Changed automatic new-stack placement so items matching `InventorySlots/InventorySlots.yml` `QuickSlots` rules prefer the hotbar, while other items continue to prefer regular rows. Existing partial-stack filling and explicit drag destinations are unchanged.
- Moved InventorySlots YAML files into `BepInEx/config/InventorySlots/`: server-authoritative slot/group rules live in `InventorySlots.yml`, the server-authoritative resource tiers live in `ResourceMap.yml`, and local auto-managed UI state lives in `ClientState.yml`.
- Simplified `ResourceMap.yml` to an ordered biome-to-material mapping. Biomes are assigned tiers from top to bottom, and when a material appears more than once its first occurrence wins.
- **Breaking config change:** root-level `InventorySlots.yml`, root-level `InventorySlots.Client.yml`, and inline `resourceMap` data are not read or migrated. Existing legacy files are left untouched; reapply custom settings manually to the new files. Update the server and all clients together before reconnecting.

## 1.2.7

- Fixed character progression resets, including vanilla reset commands and AdminQoL Unlearn All, so progressive quick-slot rows are recalculated from the remaining known items.
- Safely moves items out of newly locked quick-slot rows from the highest row downward, preserves occupied rows when regular inventory space is insufficient, and retries automatically after space becomes available.
- Prevented suppressed equipment restoration from auto-adopting built-in slot grid candidates, and improved recovery of equipment left in hidden or locked rows after death or migration from another inventory mod.
- Reduced Jewelcrafting equipment-chest tooltip overhead by reusing item signatures and bounding retries for tooltips whose socket rows are not produced.

## 1.2.6

- Added acknowledged, owner-validated remote container sort requests with stale-response and timeout handling; older peers are rejected before incompatible RPC contracts can diverge.
- Prevented sorting, Take All, and restocking from treating different world-level item stacks as interchangeable, and made tombstone fit checks honor compatible partial stacks and configured inventory limits before pickup.
- Hardened YAML reloads and station conversion-token refreshes so invalid slot entries keep the last stable configuration and transient collection failures can retry.
- Reduced crafting-panel work by reusing prepared tab state, removing duplicate Recycle N Reclaim API calls, and including adapter state in tooltip caches.
- Removed dead UI/cache state and proxy layers, simplified valid YAML matching, co-located feature state with its owners, and reused the shared ServerSync assembly for InventoryActions builds.
- Made Release builds synchronize manifest versions and create versioned packages automatically, with `BuildPackage=false` available for compile-only builds and `DeployToGame=true` still required for game-folder deployment.

## 1.2.4

- Changed cooking-recipe ingredient detection into a fallback classification so fermenter outputs, potions, mead bases, tools, equipment, and explicitly grouped items keep their primary category. This allows items such as ValheimCuisine's `VC_VineberryAle` to use the default mead quick-slot rule even when they are also ingredients in food recipes.

## 1.2.3

- Replaced `Quick Slot Count` with `Quick Slot Rows`, using 0-3 rows of three slots each, and removed the nine redundant quick-slot hotkey display-text options.
- Reorganized Configuration Manager sections and option ordering, moved Restock directly after Progressive Slots, hid crafting grid size and crafting sort mode while retaining their in-game controls and saved values, and limited Container Preview Close Delay to 1 second.
- Simplified container action success effects to one On/Off option. Area quick stack/restock now shows VFX at up to 10 changed containers and plays its SFX once at the interacted container, with the hold duration fixed at 0.5 seconds.

## 1.2.2

- Added server-authoritative `InventoryLimits` rules for exact prefab/internal names, built-in groups, and custom YAML groups. Limits count total item units across regular rows, the hotbar, quick slots, and equipment slots.
- Blocked pickups, inventory transfers, and crafting before they exceed a configured limit, with a localized center message showing the item and maximum. Crafting stops before consuming materials, and active crafting queues stop when they reach a limit.
- Preserved existing excess items during character loading or after lowering a limit, while allowing movement within the same player inventory and blocking further additions until the count is below the limit.
- Changed automatic item placement to fill unlocked regular inventory rows from row 2 onward before using the hotbar row.

```yaml
# Maximum total item units the player can carry across regular rows, hotbar,
# quick slots, and equipment slots. Keys can be exact prefab/internal names,
# built-in groups, or custom groups above. A limit of 0 blocks new additions.
# Existing excess items are preserved when loading or lowering a limit.
InventoryLimits:
  FishingRod: 1
  tankards: 3
  FLG_TamingOrb: 3
```

## 1.2.1

- Moved crafting recipe food fork indicators closer to the right edge so their visible top and right margins are better aligned across recipe grid sizes.

## 1.2.0

- Corrected food fork colors in player and container inventories so modded foods follow their highest health, stamina, or eitr value using the same classification as InventorySlots food groups.
- Added matching colored food fork indicators to crafting recipe result icons, while keeping zero-stat consumables and veiled recipes hidden.

## 1.1.9

- Added a client-side read-only container preview that shows accessible container contents while looking at them without opening or claiming the container.
- Added a configurable Container Preview Close Delay from 0 to 2 seconds; setting it to 0 disables the preview, while enabled values keep the last valid grid visible briefly after looking away.
- Blocked take-all, stack, right-click, and drag interactions while the preview owns the container UI, and safely restores the regular inventory when opening or closing it.
- Added grid validation and automatically disables the integrated preview when standalone ContentsWithin is installed to prevent duplicate GUI ownership.

## 1.1.8

- Reduced runtime and maintenance complexity by consolidating inventory definition/layout, item classification/sorting, crafting UI refresh/tooltip, and compatibility state into their owning modules, removing redundant controller and proxy layers without intended feature changes.
- Reduced InventorySlots/InventoryActions drift risk with synchronized action-cell, container transfer, and restock-policy copies plus parity tests.
- Fixed container action success sound volume scaling to use Valheim's ZSFX modifier directly, so the configured volume is applied once instead of being multiplied across reflected audio fields.
- Removed obsolete fallback, debug, and duplicated UI update paths and simplified project source discovery.

## 1.1.7

- Fixed EpicLoot and Jewelcrafting equipment effects not reliably refreshing after keep-on-death restores items kept in InventorySlots equipment slots.
- Improved keep-on-death slot restoration so built-in and custom equipment slots restore their equipped state through the same path.
- Moved Tankard, Tankard_dvergr, and TankardAnniversary out of the hard-coded tool override and into the default YAML tankards group so players can customize the grouping more flexibly.

## 1.1.6

- Improved AdventureBackpacks custom equipment slot visuals by applying AdventureBackpacks' own bone reordering after backpack visuals are attached, fixing Iron/Silver backpack visuals that could appear offset like they were held in the left hand.
- Removed the temporary shoulder-slot visual swap used for AdventureBackpacks and now keeps backpack visuals on the InventorySlots custom visual path.
- Fixed custom equipment death-drop preparation so equipment-slot items are handled before tombstone contents are created.

## 1.1.5

- Kept the center-screen result counts for container quick stack and container restock actions.
- Removed the other action success/result messages, including take all, place all, sort, and trash, while keeping failure and warning messages.
- Restored only the quick stack/restock result localization tokens and removed unused result tokens for the quieter actions.

## 1.1.4

- Reduced inventory state audit, slot projection, and custom equipment visual refresh overhead, especially around equipment changes and auto-equip flows.
- Improved dedicated slot routing and inventory placement cache paths to reduce repeated scans during item movement and pickup handling.
- Removed debug/performance logging from hot InventorySlots and compatibility paths so disabled diagnostics no longer spend time building log messages.

## 1.1.3

- Added a client option to logarithmically scale player health, food, stamina, eitr, and adrenaline bar lengths so high stats take less screen space.
- Fixed quick slot HUD anchoring after game start and improved equipment/quick slot panel dragging so panels track the cursor more accurately with less layout churn.
- Improved Jewelcrafting compatibility for socketed ring and necklace tooltips, socket container interaction from InventorySlots equipment slots, utility gem equip blocking, and first-hover socket row layout stability.
- Limited trash deletion to regular inventory cells and clarified the related message.
- Reduced several low-risk duplicate refresh paths and added policy tests to reduce InventoryActions drift.

## 1.1.2

- Fixed a frame drop issue when auto-pickup items were nearby while the inventory was full.
- Added Jewelcrafting compatibility for the Wisplight Gem and Wishbone Gem server options so the default Demister and Wishbone custom equipment slots are disabled while those gem options are On, and restored when they are Off.

## 1.1.1

- Added Circlet, Wishbone, and Demister as enabled default custom equipment slots in InventorySlots.yml.
- Equipment/custom equipment slots now stay hidden until the player has discovered, carries, or has equipped a compatible item.
- Allowed mouse-wheel recipe page changes while hovering the crafting recipe scrollbar area.
- Minor refactoring and optimizations.

## 1.1.0

- Added a Crafting Hover Tooltip mode option with Full, TitleOnly, and Off modes.
- Fixed crafting recipe tooltip pinning so the selected recipe cell can still be pinned while hovered, without changing recipe selection when using the pinned tooltip hotkey.
- Fixed crafting recipe hover tooltips after mouse-wheel page or grid zoom changes so the item under the cursor updates without requiring another hover.
- Allowed mouse-wheel recipe page changes while hovering the crafting recipe scrollbar.
- Stabilized the crafting recipe grid zoom hint layout and tightened the Alt+ mouse-wheel icon spacing.

## 1.0.9

- Improved crafting and inventory classification stability for recipe items whose prefab identity is not populated yet, including Tankard-style tool grouping and YAML prefab aliases.
- Reduced crafting UI refresh/stamp complexity and consolidated more runtime state behind focused controllers.
- Fixed Configuration Manager ordering for client UI options.

## 1.0.8

- Added a client-only Quick Slot HUD Follows Panel option so players can lock the quick slot HUD to its last saved position and move the QuickSlot panel separately.
- Saved quick slot HUD position and cell size in the client state.
- Updated keyboard hint and HUD key labels to display mouse buttons using Valheim's one-based mouse button numbering.
- Set the pinned, crafting hover, and inventory/container tooltip background alpha defaults to 0.9.

## 1.0.7

- Cleaned up more config options.
- Refactoring and optimizations.

## 1.0.6

- Updated crafting group UI: Equipment now uses the leather helmet icon, and crafting hover tooltips can be toggled on the client.
- Reworked food grouping: feast materials are grouped as feast, balancedfood was removed, and tied food stats now resolve by health, then stamina, then eitr.
- Classified Tankard, Tankard_dvergr, and TankardAnniversary directly as tool items.
- Improved quick slot key labels with compact auto text and optional per-slot display overrides.
- Improved container hover actions with movement-friendly quick stack/restock handling and configurable hover hold duration.
- Added protections for hidden inventory rows, full-inventory pickup checks, and hotbar-switch rows during inventory sorting.

## 1.0.5

- Minor cleanup and optimizations.
- Added compatibility for VeiledRecipes.

## 1.0.4

- Restock limit config now accepts localized in-game item names.
- Fixed checking fermenter and cooking station input/output too often, which caused frame drops while the inventory was open and ValheimCuisine was installed.
- Reinforced compatibility for other mods.
- Minor optimization and refactoring.

## 1.0.3

- Added a server-synced Trash panel to the inventory.
- Fixed quick slot rows 2 and 3 moving their items to the inventory when the player logged in.
- Allowed stackable Jewelcrafting and EpicLoot items to use quick stack and restock container actions.

## 1.0.2

- Fixed keep-on-death items in quick slot rows 2 and 3 moving to the inventory on player death.
- Added more compatibility patches for Jewelcrafting and EpicLoot.
- Added compatibility for MyLittleUI, CurrencyPocket, and RecycleNReclaim.
- Fixed errors related to `fx_hildrichest` volume.
- Fixed quick slot hotkeys being recognized while chat was open.

## 1.0.1

- Added WackyItemRequiresSkill requirements to crafting station tooltips.
- Fixed items in rows 5 through 9 being removed on login.
- Fixed the inventory while scrolling over a container.
- Fixed grouping not working correctly for some modded items.
- Added compatibility for the Jewelcrafting table and Homestead circlet configuration.
- Added potion and mead groups to the Consumable group in InventorySlots.yml and removed balancedfood.

## 1.0.0

- Initial release.
