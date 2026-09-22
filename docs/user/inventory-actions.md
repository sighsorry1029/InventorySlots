# Inventory actions

This guide covers the tools shared by **InventorySlots** and **InventoryActions**. Install one of these mods. InventorySlots also includes its own slots, crafting browser, and tooltips; InventoryActions provides the inventory tools separately.

For gamepad controls, see the [controller guide](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/controller.md). See [installation and compatibility](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/compatibility.md) for server and other-mod behavior.

## Favorites and sorting

Hold **Left Alt** and left-click a player inventory cell to toggle its blue favorite border. The favorite belongs to the **slot**, so it remains when that slot becomes empty. The modifier is configurable.

Favorite slots are protected from automatic quick stack, player sorting movement, and trash. They are also the targets for favorite restock. InventorySlots permits favorites in regular rows, the hotbar, and quick slots. InventoryActions permits them in supported player rows, including the hotbar; the external-slot integrations described in the compatibility guide apply.

Press the player's **S** button to sort. Sorting first fills existing favorite stacks from compatible non-favorite stacks outside the hotbar, then merges and sorts the remaining ordinary stacks. It fills favorites from top to bottom, left to right, up to their current maximum stack size. Favorite stacks stay in place, never donate to one another, and empty favorite slots remain empty. This filling step is always enabled and does not use restock target quantities.

The chest's **S** button sorts that chest. Automatic actions retain item metadata and stacking restrictions; InventoryActions also keeps cheat-marked and unmarked stacks separate. Hotbar and supported special slots are excluded from ordinary player sorting and quick stack.

## Container actions

| Control | Effect |
| --- | --- |
| Hold **Use** while looking at a chest; **E** by default | Quick stack matching, eligible non-favorite items into that chest and nearby eligible chests. |
| Hold **Alt + E** by default while looking at a chest | Refill favorite stacks from that chest and nearby eligible chests. |
| **Place stacks** in an open chest | Quick stack matching, eligible non-favorite player items into that chest only. |
| **Take stacks** in an open chest | Fill matching non-favorite partial player stacks from that chest only. |
| **Place all** in an open chest | Move eligible non-favorite regular inventory items into that chest. |

Nearby-container ranges are centered on the chest you interact with. A range of `0` removes the nearby portion of that action. Access restrictions and wards still apply. Occupied, unsupported, or inaccessible targets may be skipped; see the compatibility guide for shared chests.

**Take stacks and favorite restock have different targets.** Restock rules below apply to the favorite-restock shortcut, not Take stacks, Take All, manual moves, or crafting material use. Non-stackable favorite items are not restocked.

## Restock targets

Pick up an item from your player inventory and drop it on **Restock targets** to register its type. The item stays in your inventory. Enter a quantity; valid changes save immediately. Incomplete or invalid input returns to the last saved quantity when editing ends.

The small mode button beside the quantity cycles through:

| Mode | Favorite-restock behavior |
| --- | --- |
| **Off** | Do not restock this item; retain its saved quantity. |
| **Existing** | Top up favorite stacks that still contain this item. New entries use this mode. |
| **IncludeEmpty** | Also restore the item to its remembered empty favorite slots. |

Quantities apply **per favorite slot**, from `1` to the item's current maximum stack. For example, `Wood: 30 | IncludeEmpty` can restore up to 30 wood to each remembered wood slot after you consume it. Occupied slots are never replaced. An item with no valid rule uses Existing behavior up to its normal maximum stack.

In Configuration Manager, these rules are under **3 - Inventory Buttons → Restock Target Stack Limits**:

```text
Stone: 10 | Existing, Coins: 500 | IncludeEmpty, Wood: 30 | Off
```

Use prefab, internal, or localized item names. Separate entries with commas, semicolons, or new lines. Use the explicit positive-quantity-and-mode format above: numeric-only rules, zero quantities, and `| refill` are not accepted or converted. An ignored rule behaves like an unlisted item. Removing one entry can expose another matching internal/localized-name entry, so check the remaining list if a limit persists.

### Remembered empty slots

Each eligible favorite slot remembers the last item type observed there. Emptying it keeps that memory; replacing its item updates it; removing its favorite clears it. An item consumed before the mod observed it cannot be recovered from memory.

Remembered slots are reserved for their own item during restock, regardless of chest order. Locked, removed, incompatible, or occupied slots are skipped. Several slots for one item refill in row/column order. If IncludeEmpty has neither a remembered slot nor an existing favorite stack for an item, it may use **one unassigned empty favorite slot**. It never uses ordinary empty cells or another item's remembered slot.

Target rules are client settings shared across characters. Slot memory is character-specific:

| Mod | Local memory file under `BepInEx/config` |
| --- | --- |
| InventorySlots | `InventorySlots/ClientState.yml` |
| InventoryActions | `InventoryActions.Favorites.<playerId>.txt` |

### Leave one item

**Restock Leave One Item** defaults to **On**. Favorite restock leaves one item of each kind in each source chest, counting all stacks with the same internal name together. This keeps the chest eligible for future matching quick stack. A chest holding only one cannot supply it.

Turn this client setting Off to allow full depletion. It does not affect Take stacks, Take All, or manual moves, and other players or mods can still remove the last item.

## Auto pickup exclusions

Drop a player-inventory item on **Auto Pickup Exclude** to register its type and open the list. A checked box blocks automatic pickup; unchecked allows pickup while retaining the entry. Manual **E** pickup remains available. Removing an entry deletes its rule. Registration does not consume the item or change quick stack/restock.

The **Auto Pickup Excluded Items** setting accepts `Wood` or `Wood | On` to exclude wood, and `Wood | Off` to retain a disabled entry. Unknown or currently unavailable mod items remain listed.

## Trash

Pick up an item from an eligible regular player inventory slot and drop it on **Trash**, then confirm the amount to delete. Items held from a chest cannot be trashed directly. Favorite slots, the hotbar, and protected special slots are excluded. InventorySlots additionally blocks quest items.

The server-synced **1 - General → Enable Inventory Trash Panel** setting controls whether trash is allowed. A hidden Trash button does not grant permission to bypass these checks.

## Button display and editing

The **Restock Button**, **Auto Pickup Exclude Button**, and **Trash Button** settings are under **3 - Inventory Buttons**:

| Mode | Display |
| --- | --- |
| **Auto** — default | Shows the bottom edge; expands on hover or gamepad selection. An open editor keeps its button expanded. |
| **On** | Always shows the full button. |
| **Off** | Hides the button and closes its editor. Saved restock and exclusion rules remain active. |

Holding an item alone does not expand Auto buttons. Enabled buttons pack along the bottom in Restock, Exclude, Trash order from left to right. Hover Restock/Exclude for a preview or click to pin the editor. Long lists scroll, and valid edits save immediately.

**Show Rule Tooltips** controls help in the rule panels and F1 restock controls. It does not disable item-information tooltips or Configuration Manager descriptions. Rules and button settings are client-only; changing them in game applies immediately. Editing a `.cfg` file on disk requires a configuration reload or game restart.
