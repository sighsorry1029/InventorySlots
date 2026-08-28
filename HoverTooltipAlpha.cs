using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool IsInventoryContainerTooltipSource(UITooltip tooltip)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || tooltip == null)
        {
            return false;
        }

        Transform transform = tooltip.transform;
        return IsTooltipSourceInGrid(transform, gui.m_playerGrid) ||
               IsTooltipSourceInGrid(transform, gui.m_containerGrid) ||
               IsTooltipSourceInInventorySlotsPanel(transform);
    }

    private static bool IsTooltipSourceInGrid(Transform source, InventoryGrid? grid)
    {
        return source != null &&
               grid != null &&
               !IsUnityNull(grid) &&
               grid.transform != null &&
               (source == grid.transform || source.IsChildOf(grid.transform));
    }

    private static bool IsTooltipSourceInInventorySlotsPanel(Transform? source)
    {
        if (source == null)
        {
            return false;
        }

        foreach (RectTransform? panel in InventoryPanels.QuickSlotPanels.Values.Concat(InventoryPanels.CustomSlotPanels.Values))
        {
            if (panel != null &&
                !IsUnityNull(panel) &&
                (source == panel || source.IsChildOf(panel)))
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshCraftingHoverTooltipBackground()
    {
        if (CraftingUi.HoverTooltip != null && !IsUnityNull(CraftingUi.HoverTooltip))
        {
            ApplyCraftingHoverTooltipBackground(CraftingUi.HoverTooltip.GetComponent<Image>());
        }

        RefreshInventoryContainerHoverTooltipBackground();
    }

    private static void RefreshInventoryContainerHoverTooltipBackground()
    {
        if (_inventoryHoverTooltip != null && !IsUnityNull(_inventoryHoverTooltip))
        {
            ApplyInventoryContainerCustomTooltipBackground(
                _inventoryHoverTooltip.GetComponent<Image>(),
                GetInventoryHoverCustomTooltipBackgroundAlpha(GetInventoryHoverTooltipActiveSource()));
        }
    }

    private static void ApplyCraftingHoverTooltipBackground(Image? background)
    {
        if (background == null || IsUnityNull(background))
        {
            return;
        }

        background.sprite = GetSolidUiSprite();
        background.color = new Color(0f, 0f, 0f, GetCraftingHoverTooltipBackgroundAlpha());
        background.raycastTarget = false;
    }

    private static float GetCraftingHoverTooltipBackgroundAlpha() =>
        Mathf.Clamp01(_craftingHoverTooltipBackgroundAlpha != null ? _craftingHoverTooltipBackgroundAlpha.Value : 0.9f);

    private static float GetInventoryContainerHoverTooltipBackgroundAlpha() =>
        Mathf.Clamp01(_inventoryContainerHoverTooltipBackgroundAlpha != null ? _inventoryContainerHoverTooltipBackgroundAlpha.Value : 0.9f);

    private static float GetInventoryHoverCustomTooltipBackgroundAlpha(UITooltip? tooltip)
    {
        HoverTooltipSourceKind kind = ResolveHoverTooltipSourceKind(tooltip);
        return HoverTooltipSourceCore.UsesCraftingHoverTooltipBackgroundAlpha(kind)
            ? GetCraftingHoverTooltipBackgroundAlpha()
            : GetInventoryContainerHoverTooltipBackgroundAlpha();
    }

    private static UITooltip? GetInventoryHoverTooltipActiveSource()
    {
        if (_inventorySlotsOwnedHoverTooltipSource != null && !IsUnityNull(_inventorySlotsOwnedHoverTooltipSource))
        {
            return _inventorySlotsOwnedHoverTooltipSource;
        }

        if (_inventoryContainerHoverTooltipSource != null && !IsUnityNull(_inventoryContainerHoverTooltipSource))
        {
            return _inventoryContainerHoverTooltipSource;
        }

        return UITooltip.m_current != null && !IsUnityNull(UITooltip.m_current) ? UITooltip.m_current : null;
    }

    private static HoverTooltipSourceKind ResolveHoverTooltipSourceKind(UITooltip? tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip))
        {
            return HoverTooltipSourceKind.None;
        }

        Transform source = tooltip.transform;
        return HoverTooltipSourceCore.Classify(
            IsInventoryContainerTooltipSource(tooltip),
            IsInventorySlotsCraftingTooltipSource(source),
            IsVneiCraftingTransform(source));
    }

    private static bool IsInventorySlotsCraftingTooltipSource(Transform? source) =>
        source != null &&
        !IsUnityNull(source) &&
        (IsOwnedCraftingUiTransform(source) || HasInventorySlotsCraftingTooltipRoot(source));

    private static bool ShouldUseInventorySlotsOwnedHoverTooltip(UITooltip? tooltip) =>
        HoverTooltipSourceCore.UsesInventorySlotsOwnedHoverTooltip(ResolveHoverTooltipSourceKind(tooltip));

    private static bool ShouldSuppressVanillaHoverStart(UITooltip? tooltip) =>
        HoverTooltipSourceCore.SuppressesVanillaHoverStart(ResolveHoverTooltipSourceKind(tooltip));

    private static bool ShouldSuppressVanillaLateUpdate(UITooltip? tooltip) =>
        HoverTooltipSourceCore.SuppressesVanillaLateUpdate(ResolveHoverTooltipSourceKind(tooltip));
}
