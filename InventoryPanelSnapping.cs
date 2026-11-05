using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float PanelSnapDistance = 10f;
    private const float PanelSnapDefaultGap = -5f;
    private const float PanelSnapMinimumGap = -10f;
    private const float PanelSnapMaximumGap = 0f;
    private static readonly Vector3[] PanelSnapCorners = new Vector3[4];

    private static Vector2 SnapDraggedInventoryPanelOffset(Vector2 rawOffset)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (_panelSnapping?.Value != Toggle.On ||
            gui?.m_playerGrid == null || gui.m_playerGrid.m_gridRoot == null) return rawOffset;

        InventoryGrid grid = gui.m_playerGrid;
        int key = grid.GetInstanceID();
        InventoryPanels.CustomSlotPanels.TryGetValue(key, out RectTransform? equipment);
        InventoryPanels.QuickSlotPanels.TryGetValue(key, out RectTransform? quick);
        bool movingQuick = InventoryPanels.DraggingQuickSlotsPanelOffset;
        // The mouse-down marker may be on the drag border, not the panel root.
        RectTransform? moving = movingQuick ? quick : equipment;
        RectTransform? other = movingQuick ? equipment : quick;
        if (!TryGetPanelSnapBounds(moving, grid.m_gridRoot, out Rect bounds) ||
            !TryGetPanelSnapBounds(gui.m_player, grid.m_gridRoot, out Rect inventoryBounds)) return rawOffset;

        Vector2 currentOffset = movingQuick ? InventoryPanels.QuickSlotsPanelRuntimeOffset :
            InventoryPanels.EquipmentSlotsPanelRuntimeOffset;
        bounds.position += rawOffset - currentOffset;
        Rect? otherBounds = TryGetPanelSnapBounds(other, grid.m_gridRoot, out Rect candidate) ? candidate : null;
        return rawOffset + GetPanelSnapDelta(bounds, inventoryBounds, otherBounds, _panelSnapGap?.Value ?? PanelSnapDefaultGap);
    }

    private static bool TryGetPanelSnapBounds(RectTransform? panel, Transform space, out Rect bounds)
    {
        bounds = default;
        if (panel == null || !panel.gameObject.activeInHierarchy) return false;
        // Use the background rect, not the inset content rect. The sprite can
        // still have transparent margins; a negative gap compensates for those.
        RectTransform visual = panel.Find("Bkg") as RectTransform ?? panel;
        if (!visual.gameObject.activeInHierarchy) return false;
        visual.GetWorldCorners(PanelSnapCorners);
        Vector3 first = space.InverseTransformPoint(PanelSnapCorners[0]);
        float left = first.x, right = first.x, bottom = first.y, top = first.y;
        for (int i = 1; i < PanelSnapCorners.Length; i++)
        {
            Vector3 point = space.InverseTransformPoint(PanelSnapCorners[i]);
            left = Mathf.Min(left, point.x);
            right = Mathf.Max(right, point.x);
            bottom = Mathf.Min(bottom, point.y);
            top = Mathf.Max(top, point.y);
        }
        bounds = Rect.MinMaxRect(left, bottom, right, top);
        return bounds.width > 0f && bounds.height > 0f &&
            !float.IsInfinity(left) && !float.IsInfinity(right) &&
            !float.IsInfinity(bottom) && !float.IsInfinity(top);
    }

    private static Vector2 GetPanelSnapDelta(Rect moving, Rect inventory, Rect? other, float gap = PanelSnapDefaultGap)
    {
        const float distance = PanelSnapDistance;
        gap = float.IsNaN(gap) || float.IsInfinity(gap) ? PanelSnapDefaultGap : Mathf.Clamp(gap, PanelSnapMinimumGap, PanelSnapMaximumGap);
        float dx = 0f, dy = 0f, bestX = distance + 1f, bestY = distance + 1f;
        if (PanelSnapRangesMeet(moving.yMin, moving.yMax, inventory.yMin, inventory.yMax, distance))
            ConsiderPanelSnap(inventory.xMax + gap - moving.xMin, distance, ref dx, ref bestX);
        if (other is Rect horizontal &&
            PanelSnapRangesMeet(moving.yMin, moving.yMax, horizontal.yMin, horizontal.yMax, distance))
            ConsiderPanelSnap(horizontal.xMin - moving.xMin, distance, ref dx, ref bestX);

        moving.x += dx;
        if (PanelSnapRangesMeet(moving.xMin, moving.xMax, inventory.xMin, inventory.xMax, distance))
            ConsiderPanelSnap(inventory.yMax - moving.yMax, distance, ref dy, ref bestY);
        if (other is Rect vertical &&
            PanelSnapRangesMeet(moving.xMin, moving.xMax, vertical.xMin, vertical.xMax, distance))
        {
            ConsiderPanelSnap(vertical.yMax - moving.yMax, distance, ref dy, ref bestY);
            ConsiderPanelSnap(vertical.yMin - moving.yMin, distance, ref dy, ref bestY);
            ConsiderPanelSnap(vertical.yMin - gap - moving.yMax, distance, ref dy, ref bestY);
            ConsiderPanelSnap(vertical.yMax + gap - moving.yMin, distance, ref dy, ref bestY);
        }
        return new Vector2(dx, dy);
    }

    private static bool PanelSnapRangesMeet(float minA, float maxA, float minB, float maxB, float distance) =>
        maxA + distance >= minB && maxB + distance >= minA;

    private static void ConsiderPanelSnap(float delta, float distance, ref float bestDelta, ref float bestDistance)
    {
        float magnitude = Mathf.Abs(delta);
        if (magnitude <= distance && magnitude < bestDistance)
        {
            bestDelta = delta;
            bestDistance = magnitude;
        }
    }
}
