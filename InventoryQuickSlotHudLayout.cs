using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Vector3 ResolveQuickSlotHudPosition(RectTransform hudRoot, InventoryGrid? grid,
        int inventoryWidth, int rows, bool followsPanel, out float elementSpace, int columns = QuickSlotPanelColumns)
    {
        bool useDefaultPosition = InventoryClient.ClientState.Inventory.QuickSlotsHudPosition == null;
        Vector3 position = useDefaultPosition
            ? GetDefaultQuickSlotHudPosition(hudRoot.rect)
            : GetQuickSlotHudPosition();
        elementSpace = GetQuickSlotHudElementSpace();
        InventoryGui? gui = InventoryGui.instance;
        if (followsPanel && gui != null && grid != null && grid.m_gridRoot != null &&
            gui.m_player != null && inventoryWidth > 0 && grid.m_elementSpace > 0f &&
            !float.IsInfinity(grid.m_elementSpace) && grid.transform is RectTransform gridRect &&
            gridRect.rect.width > 0f)
        {
            // Awake captured the native inventory's shown pose. Apply its
            // slide correction before the first Tab, without creating a panel
            // or changing any inventory transforms while the inventory is hidden.
            RefreshFeatureGuideLayout(gui);
            Vector3 target = GetSidePanelBasePosition(GetGridOrigin(grid), inventoryWidth, grid.m_elementSpace) +
                             GetQuickSlotPanelDisplayOffset(grid.m_elementSpace);
            Vector3 world = grid.m_gridRoot.TransformPoint(target) - GetFeatureGuideAnimationOffset(grid.m_gridRoot);
            position = hudRoot.InverseTransformPoint(world);
            elementSpace = grid.m_elementSpace;
        }

        position = ClampQuickSlotHudPosition(hudRoot.rect, position, elementSpace, rows, columns);
        // These are HUD-root local coordinates, not RectTransform anchoredPosition.
        // Keep only runtime coordinates here. An unset default stays viewport-
        // anchored as GuiScaler settles or the aspect ratio changes; explicit
        // capture/drag/toggle paths save positions in the existing local space.
        if (hudRoot.rect.width > 0f && hudRoot.rect.height > 0f)
        {
            InventoryPanels.QuickSlotHudAnchoredPosition = position;
            InventoryPanels.QuickSlotHudElementSpace = elementSpace;
            InventoryPanels.QuickSlotHudAnchorValid = true;
        }
        return position;
    }

    private static Vector3 GetDefaultQuickSlotHudPosition(Rect viewport) => new(
        viewport.xMin + ClientStateCore.DefaultQuickSlotsHudLeft,
        viewport.yMax - ClientStateCore.DefaultQuickSlotsHudTop,
        0f);

    private static Vector3 ClampQuickSlotHudPosition(Rect viewport, Vector3 position, float elementSpace, int rows, int columns = QuickSlotPanelColumns)
    {
        if (viewport.width <= 0f || viewport.height <= 0f) return position;

        const float margin = 8f;
        float cellSize = Mathf.Max(24f, elementSpace - 8f);
        columns = Mathf.Max(1, columns);
        float width = Mathf.Max(columns * elementSpace,
            (columns - 1) * elementSpace + cellSize);
        float height = Mathf.Max(rows * elementSpace, (Mathf.Max(1, rows) - 1) * elementSpace + cellSize);
        float left = viewport.xMin + margin;
        float top = viewport.yMax - margin;
        // If an unusually large layout cannot fit, retain its top-left controls.
        float right = Mathf.Max(left, viewport.xMax - margin - width);
        float bottom = Mathf.Min(top, viewport.yMin + margin + height);
        return new Vector3(Mathf.Clamp(position.x, left, right), Mathf.Clamp(position.y, bottom, top), position.z);
    }
}
