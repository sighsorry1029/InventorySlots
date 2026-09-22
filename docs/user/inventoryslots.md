# InventorySlots guide

This guide covers InventorySlots' slots, crafting interface, and YAML configuration. For the tools shared with InventoryActions, see [inventory actions](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/inventory-actions.md). Separate guides cover [controllers](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/controller.md) and [installation and compatibility](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/compatibility.md).

## Inventory growth and display

The regular inventory starts at **8 × 4**. InventorySlots can add three rows through item discovery; Haldor's two purchased rows are independent, giving up to **nine regular rows / 72 slots**.

| Default unlock | Added row |
| --- | --- |
| Discover `HardAntler` | InventorySlots row 1 |
| Discover `CryptKey` | InventorySlots row 2 |
| Discover `Wishbone` | InventorySlots row 3 |
| Buy Wider Pockets from Haldor: 1,000 coins after Moder | One purchased row |
| Buy Deeper Pockets from Haldor: 2,000 coins after the Queen | One purchased row |

The purchase conditions use progression in the current world. **Maximum Extra Rows** accepts `0–3`. Turning **Enable Progressive Rows** Off makes those configured extra rows available immediately. Purchased rows are unaffected.

Regular storage coordinates remain reserved, with equipment and quick slots after them. Unlocking or hiding rows does not reshuffle saved items. If a row becomes locked while it contains items, its occupied cells stay accessible for recovery; its empty cells do not become extra storage.

In **4 - Client**, choose **Fixed** to show all unlocked rows or **Expandable** to adjust the visible row count with the mouse wheel. The expandable count is remembered locally. The configured hotbar-switch key rotates the visible hotbar row without moving item coordinates. **Auto Favorite Hotbar Switch Row** defaults to Off; On favorites the second row when the character loads or spawns.

## Equipment and quick slots

The six built-in equipment IDs are `helmet`, `chest`, `legs`, `cape`, `utility`, and `trinket`. Their names can be changed in YAML, but their IDs must stay fixed. With **Enable Equipment Slot Progression** On, a slot unlocks after the character discovers, carries, or has equipped an item that it accepts.

Quick slots appear in a side panel and on the HUD. **Quick Slot Rows** reserves up to three rows of three slots. With progression enabled, row 1 is available initially, row 2 unlocks with `HardAntler`, and row 3 with `CryptKey`.

`QuickSlots` in YAML defines accepted item groups. Defaults include `Melee`, `Ranged`, `Magic`, `healthfood`, `staminafood`, `eitrfood`, `mead`, and `potion`. These rules also affect automatic placement: a matching item's new stack prefers the hotbar before regular rows; a non-matching item prefers regular rows. Compatible partial stacks fill first, and an explicit drag destination is preserved.

### Add a custom equipment slot

Edit the `Slots` list in `InventorySlots/InventorySlots.yml`. Preserve existing entries and use a stable, unique `id`; custom slots follow YAML order. The `items` list can reference exact item names, built-in groups, or your custom groups.

```yaml
Slots:
  - id: circlet
    name: Circlet
    applyArmor: true
    items:
      - HelmetDverger
  - id: wishbone
    name: Wishbone
    applyArmor: false
    items:
      - Wishbone
```

This is a **Slots excerpt**, not a replacement for the full generated file. Keep built-in and compatibility slot IDs unchanged.

`applyArmor` controls the additional armor InventorySlots grants from custom slots and the built-in utility/trinket slots. **Omitted or false adds no armor**; true includes the item's positive armor and quality bonuses. Native helmet/chest/legs/cape armor is unchanged and is not counted twice. Equipped effects, resistances, and set bonuses are unaffected.

New default files enable armor for Circlet. Existing YAML is preserved, so set `applyArmor: true` explicitly for any existing custom slot that should contribute armor. Reloaded or server-synced changes apply without re-equipping the item.

## Configuration files

All paths below are relative to `BepInEx/config`:

| File | Purpose |
| --- | --- |
| `sighsorry.InventorySlots.cfg` | Options shown in Configuration Manager. |
| `InventorySlots/InventorySlots.yml` | Slots, groups, limits, quick-slot rules, and keep-on-death rules. |
| `InventorySlots/ResourceMap.yml` | Material tiers used for sorting. |
| `InventorySlots/ClientState.yml` | Automatically managed local UI state and character-specific favorite memory. |

When InventorySlots is installed on the server, its synchronized settings and the two configuration YAML files are authoritative. Keep `ClientState.yml` local. Invalid configuration YAML or unknown properties leave the last valid configuration active.

The current YAML paths above are the ones to edit. Older root-level `InventorySlots.yml`, `InventorySlots.Client.yml`, and inline `resourceMap` settings are not imported; reapply any custom values to the current files.

Configuration Manager groups settings into **1 - General**, **2 - Progressive Slots**, **3 - Inventory Buttons**, **4 - Client**, **5 - Client UI**, **6 - Client Keys**, and **7 - Controller Input**. Shared button and restock settings are explained in the inventory-actions guide.

## Item groups

The fixed top-level sections are `Melee`, `Ranged`, `Magic`, `Equipment`, `Food`, `Consumable`, `Meadbase`, and `Misc`. Their lists set subgroup order for crafting and sorting. Other keys define custom groups containing **exact prefab/internal item names**, rather than nested group references.

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

Use a custom name that does not collide with a built-in subgroup. Adding `custom_swords` to the Melee ordering does not redefine the built-in Melee membership used by other rules. Reference `custom_swords` explicitly wherever you want that custom list, such as a slot's `items`, `QuickSlots`, `InventoryLimits`, or `KeepOnDeath`.

## Inventory limits and death rules

`InventoryLimits` sets a maximum number of matching **item units**, counting regular rows, the hotbar, equipment, and quick slots. Keys may name an exact item, a built-in group, or a custom group.

```yaml
InventoryLimits:
  FishingRod: 1
  tankards: 3
```

A limit of `0` blocks new additions. Items already above a lowered limit remain intact, but new additions are blocked until the count permits them. Moving items within the same inventory does not add to the count. Blocked crafting stops before consuming materials and ends a crafting queue; manual pickups/transfers display a message, while passive auto-pickup stays quiet.

`KeepOnDeath` names the items or groups that stay with the player instead of entering the tombstone. Defaults:

```yaml
KeepOnDeath:
  - Melee
  - Ranged
  - Magic
  - Equipment
```

Turn **Enable Death Keep Rules** Off to disable this feature.

## Crafting and comparisons

Use the **Grid / List** button immediately left of the search field to switch views. New installations default to **List**; an existing saved selection is preserved. Both views share search, filters, favorites, selection, craft quantity, and the queue.

| View | How it works |
| --- | --- |
| List | Recipe names on the left; selected-item details on the right. Each scrolls independently. Scrolling the list does not change the selection, and there is no automatic recipe hover popup. |
| Grid | Recipe icons with hover tooltips, mouse-wheel paging, and zoom using the configured modifier. The initial grid is 6 × 6; zoom is remembered locally. |

Search matches display names, English names, internal/prefab names, and recipe materials. Recipe favorites and upgrade-item favorites are separate; an upgrade favorite belongs to that particular item. Sorting can consider favorites, craftability, resource tier, group, set, and name. Queue up to **999 operations**; each operation still checks materials, available space, inventory limits, and the station.

Craft, Upgrade, Jewelcrafting's Socket, and Recycle N Reclaim's Reclaim tabs support both views. In List, Socket warnings and Reclaim status messages appear above the item description; material costs, returned-item icons, and action buttons stay below. Other external tabs retain their own layouts.

Hover tooltips can scroll, and up to three pinned tooltips can compare gear, meals, potions, or recipes. List recipes can still be pinned with the configured pin key. Upgrade tooltips highlight changed values in yellow. **5 - Client UI** controls tooltip opacity and pinned-panel options; **6 - Client Keys** contains the bindings.

### Sort tiers

Crafting and inventory/container sorting have separate client sort preferences. `TierThenGroup` sorts by resource tier first; `GroupThenTier` sorts by group first. Crafting controls are on its panel, while inventory/container settings are under **4 - Client**.

`InventorySlots/ResourceMap.yml` maps tier names to materials. Order runs **top to bottom**; the first tier listing a duplicate material wins. These tiers affect sorting, not crafting costs or unlocks.

```yaml
Meadows:
  - Wood
  - Stone
BlackForest:
  - HardAntler
  - Bronze
```

Existing files are preserved during updates. Merge needed entries into your existing map while retaining custom entries and tier order; do not alphabetize its sections. YAML changes reload and synchronize, refreshing crafting views and taking effect for inventory/chests on their next sort. Invalid YAML keeps the last valid map.

## Container preview

Looking at an accessible chest can show a read-only contents preview without opening or claiming it. The configurable close delay retains the last valid display briefly after looking away; set the delay to `0` to disable the preview. If standalone ContentsWithin is installed, InventorySlots disables its own preview.
