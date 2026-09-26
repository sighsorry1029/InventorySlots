# Installation and compatibility

**InventorySlots** is the full slot and interface overhaul. **InventoryActions** provides the shared inventory tools separately. Install **one**, together with the BepInEx dependency listed by its package.

See [InventorySlots settings](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/inventoryslots.md), [inventory actions](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/inventory-actions.md), and [controller controls](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/controller.md) for usage.

## Multiplayer installation

| Setup | Required installation and behavior |
| --- | --- |
| Mod on your client; server without it | Supported. Other players need not install it. Your local settings apply; InventorySlots also uses your local YAML. |
| Mod installed on the server | Every connecting client must install the same mod version. Server-synced settings follow the server; InventorySlots' configuration YAML does too. |

Client-only area quick stack/restock uses normal game chest access and ownership requests. Occupied or inaccessible chests are skipped, and container action effects are local. InventorySlots' built-in shared-chest feature falls back to exclusive game access when the server lacks InventorySlots. Extra equipment attachments require InventorySlots on the viewing client to be visible.

Finish inventory actions and exit normally before updating. Update the server and all required clients together.

## Shared chests

InventorySlots' **Enable Multi User Chest** defaults to On. With InventorySlots on the server, it lets several players view a supported standard chest. Item changes wait until access is approved and current chest contents are available; a changed source slot cancels a pending click. Area actions process eligible chests one at a time, including supported shared chests already being viewed.

Off retains exclusive opening and area transfers to eligible unused chests. Changing the setting closes the inventory and cancels pending actions before reopening. Access and ward restrictions remain in force. Tombstones, ships, and unsupported container types keep their existing behavior. This cannot protect against other mods changing chest contents directly, or a crash saving the world and character at different times.

The **standalone MultiUserChest mod** has different interactions with the two mods:

| Mod | When standalone MultiUserChest is present |
| --- | --- |
| InventorySlots | MultiUserChest controls concurrent opening. InventorySlots excludes non-owned containers from area transfers. |
| InventoryActions | Area quick stack/restock is disabled. With MultiUserChest 0.6.1 or newer, non-owner Take All is also left to MultiUserChest. |

## InventorySlots integrations

Both mods have optional **EpicLoot** support for compatible crafting-material, Forest/Iron/Gold token, Runestone, and ShardStone stacks. Sort merges ordinary stacks and fills favorites from ordinary stacks without moving or consuming favorites. Container transfers, Quick Stack, and Restock preserve EpicLoot metadata and existing access rules. Different effects or incompatible item data remain separate; enchanted equipment is excluded from this material-stacking integration.

InventoryActions includes this stacking support directly. If **EpicLootAdventureTools 0.8.4** is installed, InventoryActions retires only its overlapping stacking patches and leaves the adventure tracker active. Other AdventureTools versions are not automatically modified. EpicLoot's public item-data API is required for this optional integration; a missing or failing API does not permit unchecked merging.

InventorySlots cannot be combined with other slot owners declared incompatible: **AzuExtendedPlayerInventory, ExtraSlots, ExtraSlotsCustomSlots, Equipment and Quick Slots, ComfyQuickSlots**, or **Quick Stack Store**. InventoryActions is also an alternative, not an add-on to InventorySlots.

| Optional mod | InventorySlots behavior |
| --- | --- |
| Jewelcrafting | Ring/necklace slots, socket and gem tooltips, and Socket tab integration. |
| EpicLoot | Preserves item tooltip content and supports comparisons; equipment/custom routing can be configured in YAML. |
| AdventureBackpacks / Smoothbrain Backpacks | Backpack slots and equipped-backpack synchronization. |
| RustyBags | Bag/quiver slots and equipped-state synchronization. |
| Magic Supremacy | Belt slot and equipped-belt synchronization. |
| BetterArchery | Conservative handling of its quiver/reserved cells. |
| AzuCraftyBoxes | Shows nearby material counts/colors through its optional API. AzuCraftyBoxes remains responsible for consumption. |
| Recycle N Reclaim | Grid/List support for the Reclaim tab. |
| TooltipExpansion | Keeps InventorySlots' owned tooltip layouts separate from vanilla tooltip changes. |
| VNEI | Exposes crafting/upgrade icon item information for recipe lookup. |
| ContentsWithin | Disables the integrated container preview to avoid duplicate panels. |
| ServerCharacters / ServerManager | In multiplayer, leaves character recovery to that mod and skips InventorySlots' extra-slot backup save/restore. Single-player backup behavior is unchanged. |

Keep generated compatibility slot IDs unchanged when editing YAML. Optional integrations use the relevant installed mod's supported interfaces; this list does not mean every feature of every mod shares the same layout.

## InventoryActions with other slot mods

InventoryActions does not replace the slot layout. It is incompatible with **InventorySlots** and **Quick Stack Store** to avoid duplicate controls and automatic item actions.

| Optional mod | InventoryActions behavior |
| --- | --- |
| ExtraSlots | Uses the public displayed-inventory-height API to align bottom buttons with regular rows, excluding hidden equipment/quick-slot storage rows from that layout calculation. |
| Equipment and Quick Slots 3.x | Uses its visible-row API so automatic actions and favorites stay within ordinary visible player rows. Equipment, quick, reserved, and custom rows are excluded; buttons follow the visible inventory. |
| AzuExtendedPlayerInventory | Uses its slot-index API to keep automatic actions and favorites above equipment/quick/custom rows. With a separate equipment panel, buttons follow the regular inventory; with inline slots, they follow the full displayed grid. |

The EAQS/AzuEPI boundaries apply to sorting, quick stack, restock, favorites, and trash. Explicit item moves continue to use the slot mod's and game's rules. AzuEPI's own favorite data remains separate. Purchased regular inventory rows are supported, while ordinary automatic actions continue to protect the hotbar.
