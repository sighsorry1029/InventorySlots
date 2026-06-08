# InventorySlots Compatibility Review

## Scope

Reviewed compatibility targets provided by the user:

- MultiUserChest / No-Chest-Block
  - Source: `C:\Users\blizz\RiderProjects\InventorySlots\reference\No-Chest-Block`
  - DLL: `C:\Users\blizz\RiderProjects\InventorySlots\Libs\MultiUserChest.dll`
- Jewelcrafting
  - Source: `C:\Users\blizz\RiderProjects\InventorySlots\reference\Jewelcrafting-src`
- BetterArchery
  - DLL: `C:\Users\blizz\RiderProjects\InventorySlots\Libs\BetterArchery.dll`
  - No public GitHub source was provided/found in this pass.

## MultiUserChest

MultiUserChest changes the meaning of container ownership. In vanilla, inventory-mod actions should generally only mutate a container when the local client owns the `ZNetView`. MultiUserChest patches `Inventory.AddItem`, `Inventory.MoveItemToThis`, `Inventory.RemoveItem`, `Inventory.MoveAll`, and `Inventory.Changed` so non-owner clients can request server/owner-side container changes through its RPC layer.

InventorySlots now treats non-owner containers as usable only when MultiUserChest is loaded and the container does not have the `MUC_Ignore` ZDO flag. This applies to:

- current-container buttons
- area quick stack/restock container filtering
- safe take all
- sort requests

Current opened local/virtual containers without a `ZNetView` remain usable, which keeps item-container style UIs from being blocked by the MultiUserChest ownership guard.

For non-owner MultiUserChest source containers, restock/take-all move counts are handled optimistically when the local container stack does not change immediately after the RPC request.

## BetterArchery

BetterArchery quiver support is implemented as a soft compatibility layer, not a hard dependency. InventorySlots reflects these public static BetterArchery fields when the plugin is loaded:

- `ConfigQuiverEnabled`
- `QuiverRowIndex`

Detected quiver cells are treated as external reserved cells. InventorySlots does not classify them as normal progressive rows for sorting, store all, stack, restock, favorite-slot targeting, or inventory projection. This avoids InventorySlots moving or cleaning up BetterArchery quiver items as if they were regular inventory cells.

This is intentionally conservative. It does not recreate BetterArchery's quiver UI or arrow rules; it only prevents InventorySlots from trampling that row.

## Jewelcrafting

Jewelcrafting's synergy UI is created in `Synergy/Synergy.cs` by cloning the vanilla `Armor` panel under `InventoryGui.m_player` and naming the clone `Jewelcrafting Synergy`.

InventorySlots already includes that exact name in the player-stat panel relocation scan, so the compatibility layer is mostly UI positioning:

- move Armor/Weight and compatible sibling stat panels next to the InventorySlots equipment panel
- also move `Jewelcrafting Synergy` when present
- ignore `InventorySlots_` host objects to avoid recursively moving our own wrapper panels

No inventory save/load compatibility is required for Jewelcrafting itself from this review. Its complex item-container/socket behavior is handled through item custom data and its own InventoryGui patches, so InventorySlots should mainly avoid destructive assumptions around custom data. The current native container actions already avoid quick stack/restock for custom-data stacks.

## Decision

Full reimplementation is not needed for these mods right now.

- MultiUserChest needed real compatibility because it affects multiplayer container ownership and RPC routing.
- BetterArchery needed defensive reserved-cell detection because it uses extra inventory rows.
- Jewelcrafting currently needs UI placement support and custom-data caution, not deep integration.
