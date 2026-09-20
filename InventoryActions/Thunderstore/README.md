# InventoryActions

Standalone inventory actions split from InventorySlots mod: hold containers to quick stack/restock, favorite slots, sort inventories/containers, set restock limits, and trash selected items.

![](https://i.ibb.co/b5qGKm8k/Inventory-Button.gif) <br>
Three inventory buttons: Restock targets, Auto Pickup Exclude, and Trash

![](https://i.ibb.co/xtpGM34P/quickstackchest.png) <br>
Hovering a container shows hold actions for area quick stack and area restock. Area ranges are centered on the interacted container.

![](https://i.ibb.co/rJYRL18/quickstack.gif) <br>
Hold `E` to quick stack matching non-favorited player items into the hovered container and nearby eligible containers.

Favoriting marks the inventory slot itself, making that slot immune to quick stack while also registering it as a restock target.

![](https://i.ibb.co/kgqHWzbk/restock.gif) <br>
Hold `Alt+E` by default to restock favorited inventory stacks from the hovered container and nearby eligible containers.

Take stacks pulls only matching stackable items that are not favorited.

![](https://i.ibb.co/yFQWpxjF/restocklimit.png) <br>
Client restock targets set a quantity and mode per prefab, such as `Stone: 10 | Existing` or `Coins: 500 | IncludeEmpty`.

Two item-rule icons sit below the player inventory, to the left of the trash button:

- **Restock targets:** drop an item from your inventory on the icon to register it and enter its target quantity. Valid quantities save immediately. The item stays in your inventory. Targets apply to Alt+E favorite-stack restock, not the opened-container Take stacks button. Quantities range from 1 to the item's maximum stack; Off disables restocking while preserving the quantity.
- **Auto pickup exclusions:** dropping an inventory item immediately registers its prefab and opens the list. Repeating it shows the existing entry. Excluded types stay on the ground when you walk near them; manual E pickup still works. This does not delete items or change quick stack/restock.
- Hover an icon for a list preview, or click to pin it. The wooden dropdown opens below that icon and shows up to six rows, with scrolling when space is limited. At the screen edge the panel is kept on-screen so its controls remain reachable. Valid quantities and removals save immediately. Incomplete or invalid input restores the last saved quantity when editing ends.

The restock parcel/return-arrow and excluded-pickup icons use the trash button's native background, muted gray color and golden highlight when holding an inventory item that can be registered. They remain clickable with empty hands. Popup actions use the Craft button style, while quantity fields use the inventory grid's dark translucent slot style.

Under `3 - Inventory Buttons`, `Restock Button`, `Auto Pickup Exclude Button`, and `Trash Button` use client-only **Off / Auto / On** modes, defaulting to **Auto**. Auto shows the bottom edge and expands only the hovered button; holding an item alone does not expand it. An open editor keeps its button expanded, and gamepad use expands Auto buttons. On always displays the full button; Off hides it and closes its editor without disabling saved rules. Enabled buttons pack from the right in Trash, Exclude, Restock order and follow the last regular row as it grows. The synced `1 - General / Enable Inventory Trash Panel` still controls whether trash is allowed at all. The separate `Sort Button Position` setting remains available. No old-section migration is performed.

ExtraSlots is optional. When installed, its public inventory-height API provides the regular row count, excluding hidden equipment/quick-slot storage rows. This keeps buttons aligned through purchased rows and ExtraSlots row changes without changing stored items or slot permissions.

Equipment and Quick Slots 3.x is optional. InventoryActions uses its public visible-row API so automatic sorting, quick stack, restock, favorites, trash, and bottom-button layout stay inside the visible player inventory instead of treating EAQS equipment, quick, reserved, or custom-slot rows as regular storage. Explicit item moves remain under EAQS and Valheim's normal item-move rules.

AzuExtendedPlayerInventory 2.4.14 is optional. InventoryActions uses AzuEPI's public slot-index API so automatic sorting, quick stack, restock, favorites, and trash stay above its equipment, quick, and custom-slot rows. Bottom buttons follow AzuEPI's live separate-panel setting: they use the regular inventory bottom with a separate equipment panel and the full grid bottom when slots are inline. Explicit item moves and AzuEPI's own favorite data remain under AzuEPI's rules.

Both lists use client config under `3 - Inventory Buttons`: `Restock Target Stack Limits` and `Auto Pickup Excluded Items`. They are not server-synced and apply to all characters using that config. In-game/Configuration Manager changes apply immediately; editing the cfg on disk requires a config reload or game restart. Rules do not add item metadata. Unresolved mod items remain in the list. Removing a restock entry can reveal a remaining internal/localized-name rule; it does not necessarily restore the default maximum.

Each restock item has a compact mode button to the left of its quantity. Clicking cycles **Off** (dash), **Existing** (circular arrows), and **IncludeEmpty** (the same arrows with a box in the center); hovering explains the current mode. New entries default to Existing. Existing tops up only favorite stacks that still contain the item. IncludeEmpty also lets Alt+E restore that item to its remembered empty favorite slots. For example, `Wood: 30 | IncludeEmpty` restores up to 30 wood in each remembered wood slot after consumption. The target applies per slot and remains capped at the current max stack. Occupied slots are never replaced. The mode saves immediately with the quantity and is also editable in F1.

Only the explicit `Item: positive quantity | Off/Existing/IncludeEmpty` format is supported. Numeric-only rules, zero targets, and the old `| refill` suffix are ignored and are not migrated. Re-register old rules through the panel or update their config text. Items without a valid rule refill existing favorite stacks to their normal maximum, so an ignored old rule no longer limits or disables restock.

The last item prefab is remembered per character in `InventoryActions.Favorites.<playerId>.txt`, alongside each favorite coordinate. Emptying a slot retains its memory; replacing its item updates it, and unfavoriting clears it. Remembered slots are reserved for their item during restock, independent of chest order. Occupied, locked, removed or incompatible slots are skipped. Several remembered slots of the same item can be restored, in row/column order. Old coordinate-only entries remain readable; an item consumed before it was observed cannot be identified retroactively.

In IncludeEmpty mode, if an item has neither a remembered slot nor an existing favorite stack, refill may use one unassigned empty favorite slot, top to bottom and left to right. Ordinary empty slots and another item's remembered slots are never used. Existing stackability/metadata restrictions, special-slot exclusions, chest access/ownership checks and `Restock Leave One Item` remain in effect. The target config is shared across characters, while slot memory is character-specific. Sort, Take stacks, manual moves and material consumption are unchanged.

`3 - Inventory Buttons / Restock Leave One Item` defaults to **On**. Favorite restock (Alt+E by default) leaves one item of each kind in each source chest, across all stacks with the same internal item name, to keep that chest eligible for future quick stack. A chest with only one remaining item will not supply it. Turn this client-only setting Off to allow full depletion; in-game changes apply to subsequent transfers immediately. `Take stacks`, `Take All`, and manual moves are unchanged. Other players and other mods can still take the last item.

- favorite player inventory slots with `LeftAlt + left click`
- hold `E` while hovering a container to quick stack matching non-favorited stackable items
- restock existing stacks from the current container
- area quick stack with vanilla Use and area favorite restock with `LeftAlt + E`
- player and container sort buttons
- trash confirmation for held player inventory items

Player inventory Sort first fills existing favorite stacks from compatible non-favorite stacks outside the hotbar, using only the ordinary slots already eligible for sorting. Favorites are filled top to bottom, left to right, up to their current maximum stack size. Favorite stacks keep their positions and never donate to each other; empty favorite slots stay empty. The remaining ordinary stacks are then merged and sorted. This is always enabled and independent of restock limits. Items filled into favorite slots become protected from quick stack. Container sorting and restock/quick-stack rules are unchanged.

InventoryActions is incompatible with InventorySlots and Quick Stack Store to avoid duplicate buttons, hotkeys, and inventory mutations.

Valheim 1.0 support includes purchased inventory rows: favorites can use every loaded player row, while sorting, quick stack, and trash still protect the hotbar. The action buttons follow the current inventory height. Sorting keeps cheat-marked and unmarked stacks separate when consolidating stacks, and custom-data items retain their existing stacking protection.

## Multiplayer

Install the same InventoryActions version on the dedicated server and every client. Area quick stack/restock processes eligible closed containers one at a time; when another peer owns a container, that owner validates access, range, and idle state before handing ownership to the requesting player.

When MultiUserChest is detected, area quick stack/restock is disabled because MultiUserChest does not expose enough state to prove that a locally owned container has no secondary user or pending item request. With MultiUserChest 0.6.1 or newer, InventoryActions also leaves non-owner Take All to MultiUserChest.

## Github

Quick stack, restock, and inventory favorite code from [QuickStackStore](https://github.com/Goldenrevolver/QuickStackStore). <br>
https://github.com/sighsorry1029/InventorySlots
