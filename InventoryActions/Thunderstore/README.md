# InventoryActions

Quick stack, restock, favorite, sort, and manage items while keeping your existing inventory layout. InventoryActions brings the inventory tools from InventorySlots into a standalone mod.

[User guide](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/inventory-actions.md) · [Controller controls](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/controller.md)

## See it in action

Click any image or animation to view the original.

<a href="https://i.ibb.co/b5qGKm8k/Inventory-Button.gif"><img src="https://i.ibb.co/b5qGKm8k/Inventory-Button.gif" alt="Restock, auto pickup exclusion, and trash buttons sliding into view" width="320"></a>

Hover a button or select it with a controller to set restock targets, manage automatic pickup, or delete an unwanted item with confirmation.

| Quick stack | Restock favorites |
| --- | --- |
| <a href="https://i.ibb.co/rJYRL18/quickstack.gif"><img src="https://i.ibb.co/rJYRL18/quickstack.gif" alt="Holding Use at a chest to quick stack nearby" width="400"></a> | <a href="https://i.ibb.co/kgqHWzbk/restock.gif"><img src="https://i.ibb.co/kgqHWzbk/restock.gif" alt="Restocking favorite stacks from nearby chests" width="400"></a> |

Look at a chest and **hold E** to store matching items, or **hold Alt + E** to refill favorites. Both actions include that chest and nearby eligible chests.

| Chest actions | Restock targets |
| --- | --- |
| <a href="https://i.ibb.co/xtpGM34P/quickstackchest.png"><img src="https://i.ibb.co/xtpGM34P/quickstackchest.png" alt="Quick stack and restock hints at a chest" width="300"></a> | <a href="https://i.ibb.co/yFQWpxjF/restocklimit.png"><img src="https://i.ibb.co/yFQWpxjF/restocklimit.png" alt="Per-item restock target controls" width="480"></a> |

Choose a target quantity for each item and whether to restore its remembered empty favorite slots.

## What you get

| Feature | What it does |
| --- | --- |
| Favorite slots | Protect selected slots from quick stack and mark their items for restock. |
| Sorting | Top up existing favorite stacks, then merge and sort eligible ordinary stacks. Favorite slots stay in place and never donate to one another. |
| Nearby chest actions | Hold the normal Use key at a chest to quick stack; add the restock modifier to refill favorites. |
| Restock targets | Set a quantity per favorite stack and choose Off, Existing, or Include empty for each item. |
| Pickup exclusions | Turn automatic pickup off for an item without blocking manual pickup. |
| Trash | Delete a held player-inventory item after confirmation. |

## Start here

These are the default keyboard and mouse controls. In-game hints follow your configured bindings.

| To… | Do this |
| --- | --- |
| Favorite a player inventory slot | **Left Alt + left click** the slot. |
| Sort an inventory | Click its **S** button. |
| Quick stack nearby | Look at a chest and **hold E**. |
| Restock favorites nearby | Look at a chest and **hold Left Alt + E**. |
| Set a restock target | Pick up an item and drop it on **Restock**. The item stays in your inventory. |
| Exclude an item from auto pickup | Drop it on **Exclude**. Uncheck its entry to allow pickup again without removing it. |
| Delete an item | Drop it on **Trash**, then confirm. |

**On a controller:** open the inventory, select a slot without picking up its item, and briefly click and release the **right stick** to open the Favorite/Sort menu. Move down from the last inventory row to reach Restock, Exclude, and Trash. See the [controller guide](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/controller.md) for panel navigation and optional shortcuts.

## Restock, your way

Each target has a quantity from **1 to the item's maximum stack size** and one of three modes:

| Mode | Behavior |
| --- | --- |
| **Off** | Disable restocking for this item while keeping its target quantity. |
| **Existing** | Refill favorite stacks that still contain the item. The default for new targets. |
| **Include empty** | Also restore the item to its remembered empty favorite slots. Occupied slots are never replaced. |

Favorites do **not** stop crafting, building, or other normal item consumption. Include empty helps restore a favorite slot after its stack runs out. **Restock Leave One Item**, enabled by default, keeps one item of each kind in each source chest so future quick stack can still recognize it.

The open-chest **Take stacks** button fills matching non-favorite stacks. It is separate from favorite restock. [Full rules and examples](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/inventory-actions.md).

## Installation

Install with a Valheim mod manager, or place `InventoryActions.dll` in `BepInEx/plugins`. Requires **BepInExPack Valheim 5.4.2350**. Launch once to generate the config.

- **Client only:** you can join a vanilla server. Other players do not need InventoryActions; your local settings apply.
- **Installed on the server:** all clients need the same InventoryActions version, and server-synced settings follow the server.
- **Do not install alongside InventorySlots or Quick Stack Store.** InventorySlots already includes these tools.

ExtraSlots, Equipment and Quick Slots, and AzuExtendedPlayerInventory have optional integrations. Their supported behavior and the restrictions with MultiUserChest are covered in the [compatibility guide](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/compatibility.md).

## Settings and help

The three buttons default to **Auto**: each appears on hover or controller focus. Choose **On** to keep a button visible or **Off** to hide it. Saved restock and pickup rules remain active when their button is hidden.

Look under **3 - Inventory Buttons** for button display, restock targets, pickup exclusions, and rule tooltip settings. These preferences are client-side.

- [Favorites, sorting, and item rules](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/inventory-actions.md)
- [Controller controls](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/controller.md)
- [Multiplayer and compatibility](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/compatibility.md)

[GitHub](https://github.com/sighsorry1029/InventorySlots) · [Report an issue](https://github.com/sighsorry1029/InventorySlots/issues) · [Discord](https://discord.gg/jkcJCq2sK5) · [Changelog](https://github.com/sighsorry1029/InventorySlots/blob/main/InventoryActions/Thunderstore/CHANGELOG.md)

## Credits

Quick stack, restock, and inventory favorite code is based on [QuickStackStore](https://github.com/Goldenrevolver/QuickStackStore).
