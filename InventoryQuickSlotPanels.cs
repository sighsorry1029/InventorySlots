using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly Vector3[] QuickSlotHudAnchorCorners = new Vector3[4];

    private static void PositionQuickSlotPanel(InventoryGrid playerGrid, RectTransform quickPanel, Vector3 targetGridLocalPosition, float elementSpace)
    {
        // Equipment and quick slots share the grid's coordinates and native
        // inventory animation. The gameplay HUD remains a separate object.
        if (quickPanel.parent != playerGrid.m_gridRoot)
            quickPanel.SetParent(playerGrid.m_gridRoot, false);
        quickPanel.localPosition = targetGridLocalPosition;
        CaptureQuickSlotHudAnchor(quickPanel, targetGridLocalPosition, elementSpace);
    }

    private static void CaptureQuickSlotHudAnchor(RectTransform quickPanel, Vector3 targetPosition, float elementSpace)
    {
        // Persist only the native inventory's settled pose, not its slide.
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || IsInventoryPanelClosing(gui) ||
            !IsInventoryPanelAnimationSettled(gui))
        {
            return;
        }

        if (_quickSlotHudFollowsPanel?.Value != Toggle.On)
        {
            return;
        }

        if (quickPanel == null || quickPanel.parent == null || Hud.instance == null || Hud.instance.m_rootObject == null)
        {
            return;
        }

        // Vanilla hides the HUD by moving its root offscreen. Capturing against
        // that root would cancel the hidden offset and persist the wrong position.
        if (Hud.IsUserHidden() || !Hud.instance.IsVisible())
        {
            return;
        }

        RectTransform? hudRoot = Hud.instance.m_rootObject.GetComponent<RectTransform>();
        if (hudRoot == null)
        {
            return;
        }

        quickPanel.GetWorldCorners(QuickSlotHudAnchorCorners);
        Vector3 targetOffset = quickPanel.parent.TransformVector(targetPosition - quickPanel.localPosition);
        Vector3 anchoredPosition = hudRoot.InverseTransformPoint(QuickSlotHudAnchorCorners[1] + targetOffset);
        float hudElementSpace = Mathf.Max(1f, elementSpace);
        SyncQuickSlotHudVisibility(Player.m_localPlayer);
        anchoredPosition = ClampQuickSlotHudPosition(hudRoot.rect, anchoredPosition, hudElementSpace,
            _quickHudRows, _quickHudColumns);
        InventoryPanels.QuickSlotHudAnchoredPosition = anchoredPosition;
        InventoryPanels.QuickSlotHudElementSpace = hudElementSpace;
        InventoryPanels.QuickSlotHudAnchorValid = true;
        if (!InventoryPanels.DraggingQuickSlotsPanelOffset)
        {
            // The hidden HUD may already have projected this position into the
            // runtime cache. Save compares against persisted state and skips I/O
            // when unchanged, so settled capture must not compare runtime alone.
            SaveQuickSlotHudAnchor();
        }
    }

    private static void OnQuickSlotHudFollowsPanelChanged()
    {
        if (_quickSlotHudFollowsPanel.Value == Toggle.Off)
        {
            SaveQuickSlotHudAnchor();
        }
    }

    internal static void HideQuickSlotInventoryPanels()
    {
        foreach (RectTransform panel in InventoryPanels.QuickSlotPanels.Values)
        {
            if (!IsUnityNull(panel))
            {
                panel.gameObject.SetActive(false);
            }
        }
    }

}
