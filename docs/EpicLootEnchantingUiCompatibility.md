# EpicLoot enchanting dialog compatibility — 2026-09-14

## Evidence and scope

Reviewed InventorySlots `607b04f` (1.4.15) and the supplied original EpicLoot DLL, assembly version 0.13.0.0, SHA-256 `9A0B06F3E05E8880F985AD68158D847750DA5F73EAEDEFABE5E047CE7ABEF103`. The DLL came from the Gale `vvvzzvvvv` profile, `BepInEx/plugins/RandyKnapp-EpicLoot/EpicLoot.dll`. Targeted decompilation used ILSpy 9.1.0.7988; the original DLL was not publicized or modified. The current game reference is Valheim 1.0.12.

The user reported a blank Enchant result dialog. This review found a code path explaining that observation; it did not reproduce the display in a running game.

## Cause and repair

`CraftingVanillaBridge.SetCraftingVanillaDetailVisible(false)` hides the original recipe name, description and icon GameObjects. EpicLoot's `CraftSuccessDialog.Create` clones those objects, inheriting their inactive state. Its `Show(ItemDrop.ItemData)` fills their content and activates the dialog root, but does not activate these children.

Both Enchant and Rune enhancement use this result dialog. `AugmentHelper.CreateAugmentChoiceDialog` clones the same controls for `AugmentChoiceDialog`, so that dialog has the same exposure. In addition, `CraftSuccessDialog.ConvertToScrollingDescription` clones `m_recipeListScroll`, inheriting the root/Graphic/raycast suppression from InventorySlots' `CraftingScrollbar`.

`EpicLootCraftingUiCompat.cs` patches the two dialog `Show` methods when EpicLoot is present and the four injected field types match. Before EpicLoot fills the dialog, it activates only those cloned children and restores their description scrollbar's Graphics and raycasts. References outside the dialog are left alone. No original recipe objects are activated. EpicLoot retains text, icons, rarity-background enablement, colors, audio, callbacks, scroll values and dialog lifetime. No per-frame work or new persistent state is added.

## Other inspected UI paths

- Main enchanting lists use EpicLoot's own assets and StoreGui tooltip templates, rather than the hidden recipe controls. No equivalent issue was established there.
- EpicLoot reports its open UI through `Minimap.IsOpen` and `Minimap.InTextInput`; InventorySlots' existing input guards already use those signals for preview and relevant hotkeys. No additional global input suppression was added.
- Existing tooltip ownership and EpicLoot scroll protections remain in place. The result dialogs are repaired directly instead of removing those protections.
- Enchanting calculations, material consumption, effect selection, inventory/network transactions and other mods were not changed or exhaustively audited.

## Verification and remaining checks

- Baseline and patched Debug builds with `DeployToGame=true` succeeded, with zero build warnings/errors.
- Original EpicLoot metadata confirms both instance `void Show` methods, MonoBehaviour bases, and public `TMP_Text`/`Image` fields used by the patch.
- Source-linked managed UI checks cover hidden/visible clones, scrollbar recovery, repeated Show, null fields and foreign references, and preservation of content/listeners/source controls. These use test doubles, not Unity or Harmony detours.
- Existing suite: 167 checks passed. Its separate test build reports two nullable warnings in unchanged `StuWardCompat.cs`.
- Still requires actual game verification: Enchant and Rune results, Augment choices and confirmation/cancel, long-description mouse/gamepad scrolling, close/reopen, returning to ordinary crafting, and operation with crafting redesign disabled. No game or multiplayer pass is claimed.

This compatibility patch is included in InventorySlots 1.4.16.
