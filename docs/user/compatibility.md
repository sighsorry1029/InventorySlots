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
| InventoryActions | With a compatible MultiUserChest API, hold E for area Quick Stack or Alt+E to restock favorites through MUC. Restock also restores configured **Include empty** entries using remembered favorite slots. One request runs at a time. Non-owner Take All is left to MUC 0.6.1 or newer. |

When external MultiUserChest is detected, InventorySlots' expanded quick guide explains this limitation and recommends its built-in shared-chest feature. On a server without InventorySlots, the notice explains that the built-in alternative requires InventorySlots on the server and all clients. It does not change the guide's collapsed or hidden state.

InventoryActions checks the required MUC API and installs its transfer hooks at startup instead of restricting area actions to specific version numbers. Missing or incompatible members, or a hook failure, disable the adapter and log the detected version and reason. MUC 0.6.1/0.6.2 remain the reviewed implementations; passing these structural checks does not prove that another version preserves request timing or transfer behavior.

InventoryActions' MUC transfers retain favorite/hotbar protection, targets, stack identity checks, leave-one setting, chest range, and access/ward checks. Quick Stack fills matching partial stacks before empty cells and disables item swapping for its automatic requests. Empty-favorite restoration uses the same remembered-slot rules as ordinary restock. The adapter supports standard player-built chests and skips unsupported containers and chests with pending local MUC transactions. It does not claim chest ownership. Opening the inventory, moving away, dying, or teleporting stops further requests; an already submitted MUC request can still finish. A timeout does not trigger a retry or refund, and another area action waits for that response (restart the session if it never arrives).

This is limited compatibility, not an atomic transfer guarantee. MUC's existing withdrawal protocol addresses the source by slot and does not transmit the expected item identity. A concurrent slot replacement can therefore return a different item; MUC still handles that response and InventoryActions stops the batch. Unexpected empty-slot deliveries do not overwrite the remembered item and are skipped by subsequent MUC restock until moved/replaced or unfavorited. MUC's owner-side deposit merge checks are less strict than InventoryActions' metadata checks, so a concurrent destination change remains a risk. MUC may drop returned items if the destination changes or cannot fit them. Leave-one and target limits are local observations, not server reservations. Two-player contention, disconnects, and game execution still require testing.

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
| Magic Supremacy | MagicBelt slot with native Tome equipment and saved-state synchronization. |
| BetterArchery | Conservative handling of its quiver/reserved cells. |
| AzuCraftyBoxes | Shows nearby material counts/colors through its optional API. AzuCraftyBoxes remains responsible for consumption. |
| Recycle N Reclaim | Grid/List support for the Reclaim tab. |
| TooltipExpansion | Keeps InventorySlots' owned tooltip layouts separate from vanilla tooltip changes. |
| VNEI | Exposes crafting/upgrade icon item information for recipe lookup. |
| ContentsWithin | Disables the integrated container preview to avoid duplicate panels. |
| ServerCharacters / ServerManager | In multiplayer, leaves character recovery to that mod and skips InventorySlots' extra-slot backup save/restore. Single-player backup behavior is unchanged. |

Keep generated compatibility slot IDs unchanged when editing YAML. Optional integrations use the relevant installed mod's supported interfaces; this list does not mean every feature of every mod shares the same layout.

### Path of Valheiman

**InventorySlots includes an API-based adapter checked against PoV 5.0.8 and 5.0.9.** It is activated only when PoV is installed on the client. InventorySlots keeps the physical inventory cells and its panel; PoV keeps equipment rules, node unlocks and item effects. The adapter requires PoV API 1.x with the reviewed storage/equipment contracts. It does not require `Equipment system = Off` and does not rewrite that option. A missing or conflicting storage contract blocks character spawning instead of letting two incompatible storage owners modify a save.

- InventorySlots' configured item-discovery rules continue to unlock backpack rows. If PoV grants additional rows, the larger of the two extra-row counts is used, rather than their sum; Haldor's purchased rows remain independent and the fixed nine-row storage boundary is preserved. The supplied PoV 5.0.8/5.0.9 default trees contain no Big Bag or other `InventorySlotsFlat` node, although the localization and row API remain. InventorySlots' own configurable quick slots and YAML custom equipment remain available.
- With Enable Equipment Slot Progression On, equipment-panel cells also require discovery, possession or equipment of an accepted item. This includes Cape, Belt, Trinket and the utility roles. PoV's node/Equipment system requirements still apply: discovering a trinket does not unlock extra trinket seats without their nodes. Turning discovery progression Off removes only the discovery requirement. Food, potion and arrow cells keep their PoV capacity rules. Occupied closed cells remain visible for withdrawal.
- PoV utility roles use the equipment panel, reusing the configured Wishbone and Demister cells. Unlocked food, potion and arrow cells form an adjacent bank in the quick-slot panel and InventorySlots HUD, with PoV's own bindings and rules. Each bank wraps after three columns. When a node is removed or Equipment system is turned off, occupied closed cells stay reachable for withdrawal; they do not accept new items. Tool and Key storage cells and PoV's tool bar are not provided by this adapter.
- `Quick Slot HUD Mode` controls both banks. `AllOnUse` reveals the whole HUD after ordinary quick-slot use, PoV food/potion use or arrow selection; `UsedSlotOnUse` reveals the corresponding cell. Both hold for three seconds and then fade. `On` always shows the HUD and `Off` hides it. Native PoV hotkeys retain their consumption and permanent-potion rules. Right-clicking a HUD food/potion cell invokes PoV's use action; right-clicking any arrow cell cycles ammunition using PoV's normal cycle rule. The selected ammo is highlighted. PoV's native cell HUD is hidden only after the combined HUD is ready; if the action integration cannot initialize, the existing native display remains available.
- Magic Purse grants its coin benefits without adding slots or a backpack row. `Coins` stays in ordinary inventory space. PoV's public weightless and keep-on-death queries determine the benefits; currently both require Magic Purse and Equipment system On. Only carried coin weight is excluded, and node/option changes recalculate it without requiring an item move. Death protection uses InventorySlots' existing reference-preserving recovery together with YAML rules, without counting an item twice; it is independent of the YAML death-rule toggle. Other PoV crafting currencies are not coins. No global item weight or incoming auto-pickup weight rule is changed.
- PoV 5.0.9's extra trinket roles (`trinket.1`, `trinket.2`, `trinket.hand`) appear as equipment cells when their nodes allow them. PoV controls worn order, effects and shield-hand restrictions; InventorySlots reflects the actual result, including promotion after removing the base trinket. Queued equipment actions do not publish a destination early. Node removal never auto-equips an item left in an old cell; with a full backpack it remains in a withdrawable reserved cell until space is available. A trinket equipped in a YAML custom-equipment slot must be unequipped before transferring it to PoV.
- Trinket upgrade restoration uses an actually empty role and verifies the resulting worn identity instead of replacing a peer promoted into the old role. Native saved trinket coordinates are converted without removing PoV's reconnect marks; projection waits for PoV's spawn restoration.
- Quiver equipment uses PoV's registered callbacks. The empty cell is hidden when no supported quiver is available; a carried/equipped quiver remains reachable. A checked native fallback allows withdrawal while Equipment system is Off, when the public equipment queries deliberately hide the equipped quiver. Currently, remove an equipped quiver before placing a replacement in its occupied cell. An empty destination and enough backpack space for removal are required; the adapter does not swap through an unknown external quiver callback.
- Saved native PoV coordinates and the provider's saved slot IDs are reprojected before InventorySlots normalizes the inventory. Moves are planned before changing any coordinates. Removed slots use free regular rows (including visible recovery rows); if recovery space is insufficient, conversion and subsequent saving are blocked. An old positional backup is not replayed after this conversion. Keep a character backup before first testing the combination. Before uninstalling either storage owner, empty special cells into normal backpack space and save; automatic reverse conversion is not provided.
- Native hover cards retain PoV's renderer. InventorySlots pinned item tooltips use the public affix and rarity-color API. Native slot frames, sockets and unidentified visuals remain PoV-owned.
- Salvage/Disenchant tabs, their return previews and nearby crafting stock are adapted to InventorySlots' crafting panel. Actual consumption, random recovery and node requirements remain PoV's. The preview shows guaranteed returns and the additional recovery chance. Slots/favorite-protected items are excluded from the automatic salvage list; PoV's explicit direct salvage shortcut retains its own policy.
- Tidy Pack sorting preserves special and favorite cells. Native PoV nearby quick stack is disabled with external MultiUserChest because their transfer protocols differ.

Storage registration and coin policies use the public API; disabled cell roles, deferred utility unequip, Off-state equipment reads, trinket spawn completion, native HUD and crafting UI still need individually checked native hooks. PoV's cell-classification caches are not modified. The utility patch targets only the utility hand-off and preserves the new trinket replacement's unequip. PoV 5.0.9 reports `Version = 1.1.0` but `AtLeast(1, 1)` is false; the adapter detects the extra cell contracts directly. This is not a promise that every future PoV build or third-party equipment callback is compatible. Original-DLL isolated checks cover contracts and item movement, not rendered Unity UI, real death/reconnect, dedicated-server sessions or multiplayer timing. The [historical compatibility archive](../../archives/compatibility/path-of-valheiman/README.md) is reference-only and is not compiled.

Both InventorySlots and InventoryActions hide PoV's Delete button and block entry into its delete-selection mode. Existing selection/popup state is cleared through PoV's own exit routine. This UI guard does not change either mod's own trash controls or PoV's salvage/disenchant features. InventoryActions otherwise retains PoV's native equipment and storage layout, including its Tool, Key and Purse cells.

**InventoryActions:** includes a client-side PoV adapter, checked against the supplied PoV **5.0.8 and 5.0.9** DLLs. It uses the public `Valheiman.Api.Equipment` queries for backpack rows, equipment state and cell positions. It protects PoV's equipment, food, potion, arrow, tool, key and wallet rows in InventoryActions-managed sorting, transfers, restock and trash; buttons use the visible backpack height. Equipped items awaiting PoV's initial placement are also protected. Turning Equipment system Off does not expose its rows until PoV finishes releasing them. Take All and Ctrl-move paths delegated to native or external container handlers remain subject to those providers' placement rules; this adapter does not replace every native or MultiUserChest UI transfer.

Player inventory actions pause while the character is loading or native storage is awaiting attachment. An external provider registered through PoV's **Storage API**, or a suspended provider, also blocks those actions even with Equipment system Off: the API does not identify its empty reserved cells or establish storage readiness. This is a protective limit, not support for arbitrary external storage providers. The state is queried live and actions can resume when the block clears; no storage ownership or UI hosting is claimed. This does not disable the existing standalone ExtraSlots/AzuEPI/Equipment and Quick Slots integrations when PoV is absent.

PoV's own Tidy Pack sort and nearby quick stack respect InventoryActions favorites, including empty favorite slots during sorting. PoV retains its node requirements, sort order, range and access rules. With external **MultiUserChest**, use InventoryActions' chest quick-stack shortcut: PoV's native nearby quick stack is disabled because it uses a different transfer protocol.

The adapter checks method contracts rather than an exact mod version number. It requires public API **1.x**, the `equipment.cells` and `storage` capabilities, the queries above and the reviewed native safety hooks; versions without those contracts fail closed instead of using a legacy adapter. Native `EquipRows.Owns` is retained to distinguish actual ownership from loading/release states, and native action/RPC hooks retain favorite and response protection that API 1.0.0 does not expose. If contracts no longer match or a query fails, InventoryActions blocks player inventory actions and logs a warning instead of treating unknown storage rows as backpack space. PoV keeps responsibility for its equipment, crafting UI, weight, save/death handling and multiplayer behavior. No PoV DLL is bundled. The isolated checks do not replace in-game UI, delayed network response, death/reconnect or multiplayer verification.

### Magic Supremacy

Magic Supremacy's native `tome` definition supplies the accepted items automatically, including its Frostcaller and Lightcaller tomes. Keep InventorySlots' existing `magicsupremacy.belt` YAML ID; the default `MagicBelt` display name can be changed. An empty `items` list is sufficient for native tomes. In Magic Supremacy 3.1.3, `ArmorWindcallerUtility_DO` is not a native tome. You can still explicitly add it to the YAML `items` list as an InventorySlots custom item, without registering it in Magic Supremacy's native Tome state. `applyArmor` remains independent of item acceptance.

- Keep the `magicsupremacy.belt` entry to equip native Tomes in MagicBelt. Tomes cannot be placed in Utility while this entry exists, even if listed in `utility.items`.
- Remove that entire entry to use Utility instead. Keep the ordinary `utility` entry; no Tome item list is needed for this fallback.

The dedicated configuration needs no explicit Tome names:

```yaml
  - id: magicsupremacy.belt
    name: MagicBelt
    applyArmor: false
    items: []
```

Only one native Tome can be equipped. Equipping another moves the previous Tome into ordinary inventory; if displaced items cannot fit, the change is rolled back. Windcaller remains an ordinary Utility item and can coexist with a native Tome in MagicBelt. This option does not turn Utility into a general-purpose slot for arbitrary armor or weapons.

AdventureBackpacks queries use its official ABAPI client. Custom-slot unequipping also needs its close-state and container-proxy cleanup, which ABAPI does not currently expose. InventorySlots checks those specific cleanup signatures before enabling the adapter; it does not invoke AdventureBackpacks' internal equipment patches or replace the player's cape reference. The supplied AdventureBackpacks 2.2.9 DLL was checked against these contracts. This is not an exact-version restriction or a claim of in-game multiplayer verification.

## InventoryActions with other slot mods

InventoryActions does not replace the slot layout. It is incompatible with **InventorySlots** and **Quick Stack Store** to avoid duplicate controls and automatic item actions.

| Optional mod | InventoryActions behavior |
| --- | --- |
| ExtraSlots | Uses the public displayed-inventory-height API to align bottom buttons with regular rows, excluding hidden equipment/quick-slot storage rows from that layout calculation. |
| Equipment and Quick Slots 3.x | Uses its visible-row API so automatic actions and favorites stay within ordinary visible player rows. Equipment, quick, reserved, and custom rows are excluded; buttons follow the visible inventory. |
| AzuExtendedPlayerInventory | Uses its slot-index API to keep automatic actions and favorites above equipment/quick/custom rows. With a separate equipment panel, buttons follow the regular inventory; with inline slots, they follow the full displayed grid. |

The EAQS/AzuEPI boundaries apply to sorting, quick stack, restock, favorites, and trash. Explicit item moves continue to use the slot mod's and game's rules. AzuEPI's own favorite data remains separate. Purchased regular inventory rows are supported, while ordinary automatic actions continue to protect the hotbar.
