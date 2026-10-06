using HarmonyLib;
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
    private static InventoryGui? _featureGuideLayoutGui;
    private static Animator? _featureGuideLayoutAnimator;
    private static float _featureGuidePlayerY;
    private static float _featureGuideInfoY;
    private static float _featureGuideCraftingX;

    internal static void CaptureFeatureGuideLayout(InventoryGui gui)
    {
        // Awake sees the prefab's shown pose, even before the first inventory
        // opening. The initial hidden clip only disables the inventory root.
        _featureGuideLayoutGui = gui;
        _featureGuideLayoutAnimator = gui.GetComponent<Animator>();
        CaptureFeatureGuidePanelPositions(gui);
    }

    internal static void ClearFeatureGuideLayout()
    {
        _featureGuideLayoutGui = null;
        _featureGuideLayoutAnimator = null;
    }

    private static void CaptureFeatureGuidePanelPositions(InventoryGui gui)
    {
        _featureGuidePlayerY = gui.m_player != null ? gui.m_player.anchoredPosition.y : 0f;
        _featureGuideInfoY = gui.m_info != null ? gui.m_info.anchoredPosition.y : 0f;
        _featureGuideCraftingX = gui.m_crafting != null ? gui.m_crafting.anchoredPosition.x : 0f;
    }

    private static void RefreshFeatureGuideLayout(InventoryGui gui)
    {
        if (!IsInventoryPanelAnimationSettled(gui)) return;
        CaptureFeatureGuidePanelPositions(gui);
    }

    private static bool IsInventoryPanelAnimationSettled(InventoryGui gui)
    {
        if (_featureGuideLayoutGui != gui) CaptureFeatureGuideLayout(gui);
        return IsInventoryPanelAnimationSettled(_featureGuideLayoutAnimator);
    }

    private static bool IsInventoryPanelAnimationSettled(Animator? animator)
    {
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
        {
            // IsVisible/IsInTransition alone also accept frames within the
            // show/hide clips. Keep the last settled pose across those frames.
            if (!animator.GetBool("visible") || animator.IsInTransition(0)) return false;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return state.IsName("inventory_show") && state.normalizedTime >= 1f;
        }
        return true;
    }

    private static Vector3 GetFeatureGuideAnimationOffset(RectTransform panel)
    {
        InventoryGui? gui = _featureGuideLayoutGui;
        if (gui == null) return Vector3.zero;
        return GetFeatureGuidePanelOffset(panel, gui.m_player, _featureGuidePlayerY, false) +
               GetFeatureGuidePanelOffset(panel, gui.m_info, _featureGuideInfoY, false) +
               GetFeatureGuidePanelOffset(panel, gui.m_crafting, _featureGuideCraftingX, true);
    }

    private static Vector3 GetFeatureGuidePanelOffset(RectTransform panel, RectTransform? animatedPanel,
        float settledPosition, bool horizontal)
    {
        if (animatedPanel == null || animatedPanel.parent == null || !panel.IsChildOf(animatedPanel))
            return Vector3.zero;
        Vector2 current = animatedPanel.anchoredPosition;
        Vector3 offset = horizontal ? new Vector3(current.x - settledPosition, 0f, 0f) :
            new Vector3(0f, current.y - settledPosition, 0f);
        return animatedPanel.parent.TransformVector(offset);
    }

    // Reserve the inventory layout even while it is hidden, so Tab never moves
    // the guide. Ignore only the native slide axes; live sizes, anchors and
    // scale still determine the space available in HUD coordinates.
    private static bool TryGetFeatureGuideArea(RectTransform parent, out Rect area)
    {
        area = default;
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null) return false;
        RefreshFeatureGuideLayout(gui);

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
        // Include inactive panels: inventory visibility and the crafting rail's
        // SetActive calls must not change the reserved right edge on open/close.
        if (panel == null || panel.rect.width <= 0f || panel.rect.height <= 0f) return false;
        panel.GetWorldCorners(FeatureGuidePanelCorners);
        Vector3 animationOffset = GetFeatureGuideAnimationOffset(panel);
        Vector2 min = parent.InverseTransformPoint(FeatureGuidePanelCorners[0] - animationOffset);
        Vector2 max = min;
        for (int i = 1; i < FeatureGuidePanelCorners.Length; i++)
        {
            Vector2 corner = parent.InverseTransformPoint(FeatureGuidePanelCorners[i] - animationOffset);
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        // A quick-slot panel has its own vertical slide. Its horizontal space
        // remains reserved while it is temporarily below the screen.
        return bounds.xMin < screen.xMax && bounds.xMax > screen.xMin;
    }

    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    private static class FeatureGuideLayoutAwakePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(InventoryGui __instance) => CaptureFeatureGuideLayout(__instance);
    }

    [HarmonyPatch(typeof(InventoryGui), "OnDestroy")]
    private static class FeatureGuideLayoutDestroyPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (_featureGuideLayoutGui == __instance) ClearFeatureGuideLayout();
        }
    }
}
