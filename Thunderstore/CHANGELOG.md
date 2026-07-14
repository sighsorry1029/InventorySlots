# Changelog

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
