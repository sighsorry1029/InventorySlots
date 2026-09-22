using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static readonly Vector3[] FeatureGuidePanelCorners = new Vector3[4];

    // HUD and inventory use different scaled roots. Measure every obstacle in
    // the guide parent's coordinates instead of assuming a screen resolution.
    private static bool TryGetInventoryFeatureGuideArea(RectTransform parent, out Rect area)
    {
        area = default;
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || !InventoryGui.IsVisible()) return false;

        Rect screen = Rect.MinMaxRect(parent.rect.xMin + FeatureGuideGap, parent.rect.yMin + FeatureGuideGap,
            parent.rect.xMax - FeatureGuideGap, parent.rect.yMax - FeatureGuideGap);
        float left = screen.xMin;
        float right = screen.xMax;
        float top = screen.yMax;
        bool haveTop = false;
        if (TryGetFeatureGuidePanelBounds(gui.m_player, parent, screen, out Rect player))
        {
            left = Mathf.Max(left, player.xMax + FeatureGuideGap);
            top = Mathf.Min(screen.yMax, player.yMax);
            haveTop = true;
        }
        if (TryGetFeatureGuidePanelBounds(gui.m_info, parent, screen, out Rect info))
        {
            right = Mathf.Min(right, info.xMin - FeatureGuideGap);
            top = Mathf.Min(screen.yMax, haveTop ? Mathf.Max(top, info.yMax) : info.yMax);
        }

        ReserveFeatureGuidePanel(gui.m_container, parent, screen, true, ref left, ref right);
        ReserveFeatureGuidePanel(gui.m_armor != null ? gui.m_armor.transform.parent as RectTransform : null, parent, screen, true, ref left, ref right);
        ReserveFeatureGuidePanel(gui.m_weight != null ? gui.m_weight.transform.parent as RectTransform : null, parent, screen, true, ref left, ref right);
        ReserveFeatureGuidePanel(gui.m_crafting, parent, screen, false, ref left, ref right);
        ReserveFeatureGuidePanel(gui.m_repairPanel as RectTransform, parent, screen, false, ref left, ref right);
#if INVENTORY_SLOTS
        foreach (RectTransform panel in InventoryPanels.CustomSlotPanels.Values)
            ReserveFeatureGuidePanel(panel, parent, screen, true, ref left, ref right);
        foreach (RectTransform panel in InventoryPanels.QuickSlotPanels.Values)
            ReserveFeatureGuidePanel(panel, parent, screen, true, ref left, ref right);
        foreach (MovedPlayerStatPanel panel in InventoryPanels.MovedPlayerStatPanels)
            ReserveFeatureGuidePanel(panel.Rect, parent, screen, true, ref left, ref right);
        ReserveFeatureGuidePanel(_craftingGroupRail, parent, screen, false, ref left, ref right);
#endif
        area = new Rect(left, screen.yMin, Mathf.Max(0f, right - left), Mathf.Max(0f, top - screen.yMin));
        return true;
    }

    private static void ReserveFeatureGuidePanel(RectTransform? panel, RectTransform parent, Rect screen,
        bool onLeft, ref float left, ref float right)
    {
        if (!TryGetFeatureGuidePanelBounds(panel, parent, screen, out Rect bounds)) return;
        if (onLeft) left = Mathf.Max(left, bounds.xMax + FeatureGuideGap);
        else right = Mathf.Min(right, bounds.xMin - FeatureGuideGap);
    }

    private static bool TryGetFeatureGuidePanelBounds(RectTransform? panel, RectTransform parent, Rect screen, out Rect bounds)
    {
        bounds = default;
        if (panel == null || !panel.gameObject.activeInHierarchy || panel.rect.width <= 0f || panel.rect.height <= 0f) return false;
        panel.GetWorldCorners(FeatureGuidePanelCorners);
        Vector2 min = parent.InverseTransformPoint(FeatureGuidePanelCorners[0]);
        Vector2 max = min;
        for (int i = 1; i < FeatureGuidePanelCorners.Length; i++)
        {
            Vector2 corner = parent.InverseTransformPoint(FeatureGuidePanelCorners[i]);
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return screen.Overlaps(bounds);
    }
}
