# Controller controls

These controls apply to **InventorySlots** and **InventoryActions**. Button names below use the A/B/X/Y positions; in-game hints show the current bindings. Keyboard and mouse remain available.

## Expand or collapse the quick guide

Open the inventory, hold **LT**, then click the **right stick (R3)** once to expand or collapse the quick guide. Release and click R3 again to toggle it back. The guide shows this combination beside its title while using a controller, even when collapsed. With a mouse, click the triangle instead.

This works even when optional controller hotkeys are Off and remembers the guide's folded state. It does not open the slot actions menu on release. Finish item dragging, text entry, or another dialog before using it; the shortcut does not run with the inventory closed or the guide hidden.

While the inventory is open, the quick guide sits at the top of the space to the left of the crafting panels, clear of the player inventory, equipment and weight panels. Its width adapts to the available space and UI scale; folding keeps the right edge in place. When the inventory closes it returns beside the HUD hotbar. If the panels leave no readable space, the guide is temporarily hidden instead of overlapping them.

## Favorite and sort without a combination

1. Select a player or chest slot with the D-pad or left stick. Leave the item in its slot.
2. Briefly click and release the **right stick**.
3. Choose an action with Up/Down, then press **A** to apply or **B** to return.

Eligible player slots offer **Favorite / Remove favorite** and **Sort inventory**. A chest offers sorting. A long hold, another action, movement, or carrying a picked-up item cancels the tap gesture. Using an existing right-stick combination does not open the menu on release.

The menu and direct button navigation below work even when optional controller hotkeys are Off. Other input dialogs temporarily own the controls.

## Select the S button

| Start at | Input |
| --- | --- |
| Player inventory's bottom-right visible slot | Press Right once more to select the player's **S** button. |
| Open chest's top-right slot | Press Right once more to select the chest's **S** button. |

Press **A** to sort. **B**, Left, Up, or Down returns to the original grid slot. Sorting requires an available button and no picked-up item. Shoulder/tab navigation can leave the button for another inventory group.

## Restock, Exclude, and Trash buttons

With the modifier released, press **Down from the last visible player row** to enter the bottom button row. You can also enter it with Up from the first chest row. Left/Right selects the enabled buttons; hidden buttons are skipped.

| Button | A with empty hands | A while holding a player-inventory item |
| --- | --- | --- |
| Restock | Open targets | Register the item and open targets |
| Exclude | Open pickup exclusions | Register the item and open exclusions |
| Trash | Requires a held item | Open deletion confirmation |

Only the selected Auto button expands; the previous one retracts when focus moves. On buttons remain expanded and Off buttons remain hidden. **B or Up** returns to the player grid. **Down** moves to an open chest after you release the direction used to enter the button row.

Action hints follow the selected button just below it, including while Auto buttons slide open. If the screen edge leaves no room below, the hint moves above the button. The S buttons use the same placement.

Trash confirmation starts with **Cancel** selected. Use Left/Right to choose a confirmation button and A to activate it, or B to cancel. The usual favorite, hotbar, and slot protections still apply.

## Edit restock and exclusion lists

Release any modifier before editing:

| Input | Effect |
| --- | --- |
| Up/Down | Select an item row. |
| Left/Right | Select Mode, Quantity, or Remove; exclusions use Checkbox or Remove. |
| A | Activate the selected control. |
| X | Remove the selected entry. |
| B | Close the editor. |

While editing a quantity, **Up increases** it and **Down decreases** it. **A or B finishes editing**. Valid changes save immediately, so B keeps the saved value. Restock modes and exclusion checkboxes also save immediately.

See [inventory actions](https://github.com/sighsorry1029/InventorySlots/blob/main/docs/user/inventory-actions.md) for target quantities, remembered empty slots, and pickup rules.

## Optional held combinations

With **Enable Controller Hotkeys** On, hold **Inventory Action Modifier** — right-stick click / `JoyRStick` by default — while a player or chest grid is selected:

| Press while holding the modifier | Action |
| --- | --- |
| A | Toggle the selected eligible player slot's favorite. |
| X | Sort the selected inventory or chest. |
| Y | Open Restock targets; register a held player-inventory item if present. |
| B | Open Auto pickup exclusions; register a held player-inventory item if present. |

Sorting and favoriting do not act on a picked-up item. The Y/B rule shortcuts also remain available while navigating the inventory buttons.

With the inventory closed, hold **Use** while looking at a chest to quick stack. Hold **Favorite Restock Modifier + Use** to refill favorites. The default modifier is `JoyAltKeys`, which follows the game's alternate-action binding; Use is `JoyUse`. This restock uses the same quantities, modes, remembered slots, range, and leave-one setting as the keyboard shortcut.

Both modifier settings are client-only and can be changed or set to Off independently. Check the in-game hint after changing a binding.

InventorySlots additionally provides configurable quick-slot hotkeys and **Controller DPad Hotkey Mode**. Its default, **InventoryNavigation**, leaves D-pad movement available in the inventory; other modes allow D-pad hotkeys directly or with a configured modifier.
