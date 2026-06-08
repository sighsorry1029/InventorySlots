# InventorySlots

InventorySlots is an all-in-one Valheim inventory overhaul focused on stable slots, predictable item movement, and compact UI tools.

It adds progressive inventory rows, equipment slots, quick slots, favorites, keep-on-death rules, scrollable/pinned tooltips, a crafting browser, container tools, sorting, restock, multicraft, and controller support.

## Visual Overview

### Stable Inventory Growth

![](https://i.ibb.co/QFFMsQgx/progressiveslotfinal.gif) <br>
Progressive rows and quick slots unlock over time without reshuffling saved item positions. Clients can expand or collapse the currently visible inventory rows, while equipment slots are defined by `InventorySlots.yml`.

![](https://i.ibb.co/B2RrkCH1/hotbarswitch.gif) <br>
The hotbar switch key rotates the visible hotbar row, keeping a tall inventory practical without moving the underlying item coordinates.

![](https://i.ibb.co/5XMjS4Cw/keyhint.png) <br>
Key hints show the current favorite and tooltip hotkeys, including mouse/controller-aware bindings from the client config.

### Crafting Browser

![](https://i.ibb.co/pBJJ84sJ/craftingpanellook.png) <br>
Crafting stations are redesigned into an icon-grid browser with search, group filters, favorites, sorting buttons, page scrolling, and hover/pinned tooltip support.

![](https://i.ibb.co/0yP8R3vf/gridsizefavorite.gif) <br>
Mark favorite recipes and resize the crafting grid with mouse-wheel zoom.

![](https://i.ibb.co/Y7586d2w/sortingandmulticraft.gif) <br>
Sort recipes by group and resource tier, then craft multiple items from the same station flow.

![](https://i.ibb.co/bMjbRm73/recipeandgrid.gif) <br>
Recipe hover and pinned tooltips stay usable while changing grid size and browsing the crafting station.

![](https://i.ibb.co/tPDH0MPc/upgradebenefit.png) <br>
Upgrade views show the stat changes gained from upgrading gear, with upgrade favorites kept separate from crafting favorites.

### Scrollable Tooltips And Comparisons

![](https://i.ibb.co/NdVdTFdk/jeweltooltiptest.gif) <br>
InventorySlots tooltips can expand into scrollable panels and support multiple pinned comparison slots.

![](https://i.ibb.co/JWzSGMcd/favoritecomparepotions.gif)

![](https://i.ibb.co/j9bNG7Ft/comparemeal.gif) <br>
Pin up to three tooltips to compare recipes, meals, potions, and gear without losing your current hover target.

![](https://i.ibb.co/3Dv1Ct1/comparegears.png)

![](https://i.ibb.co/LXcfNxtG/comparemeals.png) <br>
Gear and food comparisons use the same pinned tooltip system.

![](https://i.ibb.co/kVJ3fjJ2/Tooltipalpha.gif) <br>
Inventory/container and crafting hover tooltip background opacity are configurable on the client.

### Container Tools

![](https://i.ibb.co/xtpGM34P/quickstackchest.png) <br>
Hovering a container shows hold actions for area quick stack and area restock. Area ranges are centered on the interacted container.

![](https://i.ibb.co/rJYRL18/quickstack.gif) <br>
Hold `E` to quick stack matching non-favorited player items into the hovered container and nearby eligible containers.

Favoriting marks the inventory slot itself, making that slot immune to quick stack while also registering it as a restock target.

![](https://i.ibb.co/kgqHWzbk/restock.gif) <br>
Hold `Alt+E` by default to restock favorited inventory stacks from the hovered container and nearby eligible containers.

Take stacks pulls only matching stackable items that are not favorited.

![](https://i.ibb.co/yFQWpxjF/restocklimit.png) <br>
Client restock limits can cap favorite restock targets per prefab, such as `Stone: 10` or `Coins: 500`.

### Mod Compatibility Examples

![](https://i.ibb.co/JWPfnMWn/epiclootcompatible.png) <br>
EpicLoot items keep their tooltip data and can use InventorySlots equipment/custom slot routing when configured in `InventorySlots.yml`.

![](https://i.ibb.co/4ZD7PW47/upgradeepicloot.png) <br>
EpicLoot gear can be compared from upgrade views with InventorySlots pinned tooltips.

![](https://i.ibb.co/ymQvwRzd/comparejewel.png) <br>
Jewelcrafting sockets and gem tooltip content are supported in InventorySlots tooltip and comparison flows.

## Highlights

- Stable inventory growth: locked rows and special slots stay reserved internally, so item coordinates do not reshuffle as progression changes.
- Dedicated equipment and quick slots: built-in equipment slots, YAML-defined custom slots, and a fixed quick slot panel with hotkeys.
- Clean favorite rules: regular rows, hotbar cells, and quick slots can be favorited.
- Container tools: quick stack, take stacks, favorite restock, player/container sort, and safer tombstone take-all behavior.
- Crafting browser: icon grid, search, group filters, recipe favorites, recipe sorting, grid zoom, and multicraft.
- Tooltips: scrollable hover tooltips and pinned comparison panels for inventory, containers, crafting, quick slots, and supported modded tabs.
- Compatibility support for EpicLoot, Jewelcrafting, backpacks, RustyBags, Magic Supremacy, BetterArchery, MultiUserChest, ServerCharacters, TooltipExpansion, and VNEI.

## Slot Model

InventorySlots reserves the maximum supported layout internally, then exposes only the cells that are currently usable.

- Regular rows are the normal inventory area.
- The hotbar is the first regular row.
- The hotbar switch row is the second regular row.
- Equipment slots and quick slots are anchored after the reserved regular inventory area.
- Locked rows are hidden and blocked, but their coordinates remain stable.

This makes the inventory safer for progression, multiplayer, tombstones, and compatibility code that may touch item positions.

## Progressive Rows

The base inventory starts as an 8x4 grid. Up to five extra regular rows can unlock through item discovery.

Default extra row unlocks:

- Extra Row 1: `HardAntler`
- Extra Row 2: `CryptKey`
- Extra Row 3: `Wishbone`
- Extra Row 4: `DragonTear`
- Extra Row 5: `YagluthDrop`

Clients can display unlocked rows in two ways:

- `Fixed`: show all unlocked regular rows.
- `Expandable`: remember the visible row count locally and adjust it with the mouse wheel while the inventory is open.

`Auto Favorite Hotbar Switch Row` defaults to `On` and marks the second inventory row as favorite when the local player is loaded or spawned.

## Equipment Slots

Built-in equipment slots:

- `helmet`
- `chest`
- `legs`
- `cape`
- `utility`
- `trinket`

Custom equipment slots can be added with YAML. Each custom slot has a stable `id`, a display `name`, and an optional `items` list.

```yaml
Slots:
  - id: wishbone
    name: Wishbone
    items:
      - Wishbone

  - id: pickaxe
    name: Pickaxe
    items:
      - pickaxe
      - custom_pickaxes
```

The built-in slot ids are fixed. Their display names can be changed. Custom slot order follows YAML order.

## Quick Slots

Quick slots are fixed special slots shown in a side panel and mirrored in a lightweight HUD.

Default quick slot progression:

- Row 1: available at start
- Row 2: `HardAntler`
- Row 3: `CryptKey`

Default quick slot item rules:

```yaml
QuickSlots:
  - Melee
  - Ranged
  - Magic
  - healthfood
  - staminafood
  - eitrfood
  - mead
  - potion
```

Default quick slot hotkeys are `Z`, `X`, `C`, `Alt+Z`, `Alt+X`, `Alt+C`, `Alt+1`, `Alt+2`, and `Alt+3`.

## Favorites

InventorySlots has separate favorite systems for inventory slots, crafting recipes, and upgrade items.

Inventory favorites are position-based. Hold the favorite modifier key, `LeftAlt` by default, and left-click a regular inventory, hotbar, or quick slot cell to toggle the blue favorite border.

Favorite inventory slots are protected from:

- player inventory sorting
- quick stack
- area quick stack
- take-all safety movement
- inventory trash

Favorite restock uses favorites as intentional targets:

- regular favorite stack: restock target
- hotbar favorite stack: restock target
- quick slot favorite stack: restock target
- empty favorite slot: not used as generic storage
- non-stackable favorite item: not restocked

Current quick stack policy:

- regular non-favorite stackable items can quick stack
- regular favorite items are protected
- hotbar items do not quick stack
- quick slot items do not quick stack

Crafting favorites apply to recipes. Upgrade favorites apply to the specific upgrade item instance, not every item with the same prefab.

## Container Tools

When a container is open:

- `Place stacks` stacks matching non-favorited player items into that container only.
- `Take stacks` fills matching non-favorited partial player stacks from that container only.
- Sort buttons sort the current player inventory or current container.

When hovering a container:

- Hold `E` to quick stack into the hovered container and nearby eligible player-built containers.
- Hold `Alt+E` by default to refill favorite stacks from the hovered container and nearby eligible containers.

Area ranges use the interacted container as the center. Setting a range to `0` disables nearby-container behavior for that action.

Container actions respect container access, wards, tombstones, ships, in-use containers, and ownership constraints.

Restock target limits can cap favorite restock targets per item:

```text
Stone: 10, Coins: 500
```

Items not listed refill to their normal max stack. A target of `0` prevents restocking that item.

## Crafting Browser

The crafting panel is redesigned into a fast icon-grid browser.

Core features:

- recipe icon grid
- mouse wheel paging
- grid zoom with the configured zoom modifier
- recipe favorites
- group filter rail
- search input
- craftable recipe highlighting
- pinned recipe comparison tooltips
- recipe requirement rows in hover tooltips
- recipe sorting by favorite, craftable state, resource tier, group, equipment set, and name

Crafting search indexes user-facing names, English localization text, prefab/internal names, and recipe materials.

Crafting and inventory/container sorting use separate client sort modes:

- `TierThenGroup`: resource tier first, then group.
- `GroupThenTier`: group first, then resource tier.

The resource tier map comes from YAML and defaults to a biome-style progression from Meadows through Ashlands.

## Tooltips

InventorySlots uses scrollable custom hover tooltips for:

- inventory items
- container items
- quick slot panels
- crafting recipes
- upgrade recipes
- compatible external crafting tabs

Pinned comparison tooltips use the same scrollable body behavior. Client UI options control pinned tooltip slots and hover tooltip opacity.

## Item Groups

Item groups power crafting filters, sorting, quick slot rules, keep-on-death rules, and custom slot item lists.

Fixed top-level groups:

- `Melee`
- `Ranged`
- `Magic`
- `Equipment`
- `Food`
- `Consumable`
- `Meadbase`
- `Misc`

YAML can reorder built-in subgroups and add custom prefab groups:

```yaml
Groups:
  Melee:
    - sword
    - axe
    - custom_swords

  custom_swords:
    - SwordNiedhogg
    - THSwordSlayer
```

Custom groups can then be reused by sorting, quick slots, keep-on-death, and custom equipment slots.

## Keep On Death

`KeepOnDeath` decides which items stay with the player instead of moving to the tombstone.

Entries can reference top-level groups, subgroups, custom groups, or exact prefab/internal item names.

Default:

```yaml
KeepOnDeath:
  - Melee
  - Ranged
  - Magic
  - Equipment
```

The feature can be disabled with `Enable Death Keep Rules`.

## YAML Model

The server-authoritative YAML controls:

- `Slots`
- `Groups`
- `QuickSlots`
- `KeepOnDeath`
- `resourceMap`

Invalid YAML and unknown YAML properties are rejected so the last stable configuration can remain active.

Minimal shape:

```yaml
Slots:
  - id: helmet
    name: Helmet
  - id: chest
    name: Chest
  - id: legs
    name: Legs
  - id: cape
    name: Cape
  - id: utility
    name: Utility
  - id: trinket
    name: Trinket

Groups:
  Melee:
    - sword
    - axe
  Food:
    - healthfood
    - staminafood

QuickSlots:
  - Melee
  - healthfood

KeepOnDeath:
  - Melee
  - Equipment
```

## Config Sections

- `1 - General`: server lock, death keep rules, trash panel, area quick stack, area take stacks.
- `2 - Progressive Slots`: extra rows, quick slot count, quick slot progression.
- `3 - Client`: inventory display, sort modes, crafting grid, container hover behavior, container FX, mouse UI scroll.
- `4 - Client UI`: hints and tooltip display options.
- `5 - Client Keys`: keyboard and mouse shortcuts.
- `6 - Restock`: favorite restock target limits.
- `7 - Controller Input`: controller scrolling and controller hotkeys.

## Controller Input

Controller support is client-side. InventorySlots stores controller hotkeys as a fixed action enum and exposes them through a custom Configuration Manager editor with capture, clear, and preset controls.

`Controller DPad Hotkey Mode` defaults to `InventoryNavigation`, leaving DPad input available for vanilla inventory movement. It can also allow DPad hotkeys directly or only while a configured modifier is held.

## Compatibility

InventorySlots declares incompatibility with mods that also take ownership of equipment slots, quick slots, or inventory slot movement.

Soft compatibility and adaptive behavior include:

- AzuCraftyBoxes: nearby material counts and colors.
- Jewelcrafting: ring/necklace slots, socket tooltips, gem rows, crafting socket UI placement, and visual/stat panel support.
- AdventureBackpacks and Smoothbrain Backpacks: backpack slot support and equipped-backpack synchronization.
- RustyBags: bag/quiver slot support and equipped state synchronization.
- Magic Supremacy: belt slot support and equipped-belt synchronization.
- BetterArchery: quiver/reserved cells are treated conservatively.
- MultiUserChest: remote-owner container access is respected.
- ServerCharacters: local slot backup/restore avoids stale server-character data.
- EpicLoot: item tooltip content is preserved while InventorySlots-owned tooltip layouts stay isolated.
- TooltipExpansion: InventorySlots-owned tooltips avoid vanilla tooltip scrollbar/layout interference.
- VNEI: crafting and upgrade recipe icons expose item tooltip data for VNEI recipe lookup.

## Github

Quick stack, restock, and inventory favorite behavior were originally informed by [QuickStackStore](https://github.com/Goldenrevolver/QuickStackStore). <br>
https://github.com/sighsorry1029/InventorySlots