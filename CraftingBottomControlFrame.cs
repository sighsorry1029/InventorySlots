using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void UpdateCraftingActionControls(
        InventoryGui gui,
        RectTransform? countRect,
        RectTransform? upgradeProgressionRect,
        Vector2 craftPosition,
        Vector2 countPosition,
        Vector2 craftButtonSize,
        Vector2 countInputSize,
        bool updateLayout)
    {
        if (_craftingCountInputRect != null && countRect == null)
        {
            _craftingCountInputRect.gameObject.SetActive(false);
        }

        if (_craftingUpgradeProgressionRect != null && upgradeProgressionRect == null)
        {
            _craftingUpgradeProgressionRect.gameObject.SetActive(false);
        }

        if (gui.m_craftButton != null)
        {
            gui.m_craftButton.gameObject.SetActive(gui.m_selectedRecipe.Recipe != null && gui.m_craftTimer < 0f);
            if (updateLayout)
            {
                RectTransform craftRect = (RectTransform)gui.m_craftButton.transform;
                SetTopLeftRectLayout(gui.m_crafting, craftRect, craftPosition, craftButtonSize);
            }
        }

        if (gui.m_craftProgressPanel is RectTransform progressRect)
        {
            if (updateLayout)
            {
                SetTopLeftRectLayout(gui.m_crafting, progressRect, craftPosition, craftButtonSize);
                gui.m_craftProgressBar?.SetWidth(craftButtonSize.x);
            }
        }

        if (countRect != null)
        {
            if (updateLayout)
            {
                SetTopLeftRectLayout(gui.m_crafting, countRect, countPosition, countInputSize);
            }

            countRect.gameObject.SetActive(gui.m_selectedRecipe.Recipe != null && gui.m_selectedRecipe.ItemData == null);
            UpdateCraftingCountInputState(gui);
        }

        if (upgradeProgressionRect != null)
        {
            if (updateLayout)
            {
                SetTopLeftRectLayout(gui.m_crafting, upgradeProgressionRect, countPosition, countInputSize);
            }

            UpdateCraftingUpgradeProgression(gui);
        }

        UpdateCraftingCraftButtonLabel(gui);
        UpdateJewelcraftingSocketCraftButtonState(gui);
        UpdateCraftingProgressLabel(gui);
    }

    private static void UpdateCraftingStationAndWarningControls(InventoryGui gui, RectTransform grid, Vector2 requiredStationPosition, bool updateLayout)
    {
        if (ShouldShowCraftingStatusHud(gui))
        {
            LayoutCraftingStatusHud(gui, grid, updateLayout);
        }
        else
        {
            HideCraftingSocketWarning();
        }

        UpdateCraftingRequiredStationLevel(gui, requiredStationPosition, updateLayout);
    }

    private static void UpdateCraftingRequirementStrip(InventoryGui gui, Vector2 requirementPosition, List<Requirement> visibleRequirements, bool updateLayout)
    {
        int quality = GetSelectedCraftingQuality(gui);
        int craftMultiplier = GetEffectiveCraftingCount(gui);
        HideCraftingVanillaRequirementSlots(gui);

        int slotCount = Mathf.Max(CraftingVisibleRequirementSlots, CraftingRequirements.OwnedSlots.Count);
        for (int i = 0; i < slotCount; i++)
        {
            if (i >= CraftingVisibleRequirementSlots || i >= visibleRequirements.Count)
            {
                if (i < CraftingRequirements.OwnedSlots.Count &&
                    CraftingRequirements.OwnedSlots[i] is { } staleSlot &&
                    !IsUnityNull(staleSlot))
                {
                    staleSlot.gameObject.SetActive(false);
                }

                continue;
            }

            RectTransform? rect = EnsureOwnedCraftingRequirementSlot(gui, i);
            if (rect == null || IsUnityNull(rect))
            {
                continue;
            }

            rect.gameObject.SetActive(true);
            SetTopLeftRectLayout(gui.m_crafting, rect, requirementPosition + new Vector2(i * CraftingRecipeGridCellSpace, 0f), new Vector2(CraftingRecipeGridCellSize, CraftingRecipeGridCellSize));
            ConfigureCompactCraftingRequirement(gui, rect, visibleRequirements[i], quality, craftMultiplier);
        }
    }

}
