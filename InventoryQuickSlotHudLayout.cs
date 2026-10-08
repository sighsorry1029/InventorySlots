using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Vector3 ResolveQuickSlotHudPosition(RectTransform hudRoot, InventoryGrid? grid,
        int inventoryWidth, int rows, bool followsPanel, out float elementSpace)
    {
        Vector3 position = GetQuickSlotHudPosition();
        elementSpace = GetQuickSlotHudElementSpace();
        InventoryGui? gui = InventoryGui.instance;
        if (followsPanel && gui != null && grid != null && grid.m_gridRoot != null &&
            gui.m_player != null && inventoryWidth > 0 && grid.m_elementSpace > 0f &&
            !float.IsInfinity(grid.m_elementSpace) && grid.transform is RectTransform gridRect &&
            gridRect.rect.width > 0f)
        {
            // Awake captured the shown pose for the guide. Use the same native
            // slide correction before the first Tab, without creating a panel
            // or changing any inventory transforms while the inventory is hidden.
            RefreshFeatureGuideLayout(gui);
            Vector3 target = GetSidePanelBasePosition(GetGridOrigin(grid), inventoryWidth, grid.m_elementSpace) +
                             (Vector3)InventoryPanels.QuickSlotsPanelRuntimeOffset;
            Vector3 world = grid.m_gridRoot.TransformPoint(target) - GetFeatureGuideAnimationOffset(grid.m_gridRoot);
            position = hudRoot.InverseTransformPoint(world);
            elementSpace = grid.m_elementSpace;
        }

        position = ClampQuickSlotHudPosition(hudRoot.rect, position, elementSpace, rows);
        // These are HUD-root local coordinates, not RectTransform anchoredPosition.
        // Keep only the runtime result here: screen/scale changes must not cause
        // per-frame YAML writes. Existing settled capture/drag/toggle paths save it.
        InventoryPanels.QuickSlotHudAnchoredPosition = position;
        InventoryPanels.QuickSlotHudElementSpace = elementSpace;
        InventoryPanels.QuickSlotHudAnchorValid = true;
        return position;
    }

    private static Vector3 ClampQuickSlotHudPosition(Rect viewport, Vector3 position, float elementSpace, int rows)
    {
        if (viewport.width <= 0f || viewport.height <= 0f) return position;

        const float margin = 8f;
        float cellSize = Mathf.Max(24f, elementSpace - 8f);
        float width = Mathf.Max(QuickSlotPanelColumns * elementSpace,
            (QuickSlotPanelColumns - 1) * elementSpace + cellSize);
        float height = Mathf.Max(rows * elementSpace, (Mathf.Max(1, rows) - 1) * elementSpace + cellSize);
        float left = viewport.xMin + margin;
        float top = viewport.yMax - margin;
        // If an unusually large layout cannot fit, retain its top-left controls.
        float right = Mathf.Max(left, viewport.xMax - margin - width);
        float bottom = Mathf.Min(top, viewport.yMin + margin + height);
        return new Vector3(Mathf.Clamp(position.x, left, right), Mathf.Clamp(position.y, bottom, top), position.z);
    }
}
