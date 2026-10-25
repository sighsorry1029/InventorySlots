using UnityEngine;

namespace InventorySlots;

internal static class InventoryDialogLayering
{
    // The native tooltip can live inside Player's Canvas or a gamepad anchor.
    // Lift the whole host out of that branch without rebuilding another mod's card.
    internal sealed class TooltipLayer
    {
        private Transform? _tooltip, _root, _originalParent;
        private int _originalSibling;

        internal void Update(InventoryGui gui, Transform? quickSlotPanel, Transform? sortLayer, Transform? tooltip)
        {
            Transform root = gui.m_inventoryRoot;
            if (root == null || !root.gameObject.activeInHierarchy ||
                tooltip == null || !tooltip.gameObject.activeInHierarchy)
            {
                Restore();
                return;
            }
            if (_tooltip != tooltip || _root != root) Restore();

            Transform? branch = tooltip;
            while (branch != null && branch.parent != root) branch = branch.parent;
            // Leave outer-Canvas overlays and foreign popups under their owner's control.
            if (branch == null || branch != tooltip && !IsBasePanel(branch, gui, quickSlotPanel))
            {
                Restore();
                return;
            }

            InventoryDialogLayering.Update(gui, quickSlotPanel, sortLayer);
            if (_tooltip == null || tooltip.parent != root)
            {
                _tooltip = tooltip;
                _root = root;
                _originalParent = tooltip.parent;
                _originalSibling = tooltip.GetSiblingIndex();
                if (tooltip.parent != root) tooltip.SetParent(root, worldPositionStays: true);
            }

            Transform? lastPanel = null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (IsBasePanel(child, gui, quickSlotPanel) || child == sortLayer) lastPanel = child;
            }
            if (lastPanel == null) { Restore(); return; }
            int current = tooltip.GetSiblingIndex(), last = lastPanel.GetSiblingIndex();
            int target = last + (current > last ? 1 : 0);
            if (current != target) tooltip.SetSiblingIndex(target);
        }

        internal void Restore()
        {
            // Native HideTooltip may already have destroyed the host. A foreign
            // reparent also ends our ownership; never move that popup back.
            if (_tooltip != null && _root != null && _tooltip.parent == _root && _originalParent != null)
            {
                if (_tooltip.parent != _originalParent) _tooltip.SetParent(_originalParent, worldPositionStays: true);
                int index = Mathf.Clamp(_originalSibling, 0, _originalParent.childCount - 1);
                if (_tooltip.GetSiblingIndex() != index) _tooltip.SetSiblingIndex(index);
            }
            _tooltip = _root = _originalParent = null;
        }
    }

    internal static void Update(InventoryGui gui, Transform? quickSlotPanel, Transform? sortLayer = null)
    {
        Transform root = gui.m_inventoryRoot;
        if (root == null || !root.gameObject.activeInHierarchy) return;

        Transform? firstDialog = null;
        ConsiderDialog(root, gui.m_splitDialog, ref firstDialog);
        ConsiderDialog(root, gui.m_achievementsPanel, ref firstDialog);
        ConsiderDialog(root, gui.m_trophiesPanel, ref firstDialog);
        ConsiderDialog(root, gui.m_variantDialog, ref firstDialog);
        ConsiderDialog(root, gui.m_skillsDialog, ref firstDialog);
        ConsiderDialog(root, gui.m_textsDialog, ref firstDialog);
        // Move only known base panels behind the first open native dialog.
        // Walking in sibling order preserves the order of both groups, without
        // promoting dialogs above unrelated mod popups or changing any Canvas.
        for (int i = firstDialog != null ? firstDialog.GetSiblingIndex() + 1 : root.childCount; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (IsBasePanel(child, gui, quickSlotPanel) || child == sortLayer)
            {
                child.SetSiblingIndex(firstDialog!.GetSiblingIndex());
            }
        }

        if (sortLayer == null || sortLayer.parent != root) return;
        Transform? lastBase = null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (IsBasePanel(child, gui, quickSlotPanel)) lastBase = child;
        }
        if (lastBase == null) return;
        int current = sortLayer.GetSiblingIndex(), last = lastBase.GetSiblingIndex();
        int target = last + (current > last ? 1 : 0);
        if (current != target) sortLayer.SetSiblingIndex(target);
    }

    private static bool IsBasePanel(Transform child, InventoryGui gui, Transform? quickSlotPanel) =>
        child == gui.m_player || child == gui.m_container || child == gui.m_info ||
        child == gui.m_crafting || child == quickSlotPanel;

    private static void ConsiderDialog(Transform root, Component? dialog, ref Transform? first)
    {
        if (dialog != null) ConsiderDialog(root, dialog.gameObject, ref first);
    }

    private static void ConsiderDialog(Transform root, GameObject? dialog, ref Transform? first)
    {
        // Reparented/foreign hierarchies and quick-slot outro canvases keep their
        // owner's ordering; only direct siblings in the native root are handled.
        if (dialog == null || !dialog.activeInHierarchy || dialog.transform.parent != root) return;
        Transform candidate = dialog.transform;
        if (first == null || candidate.GetSiblingIndex() < first.GetSiblingIndex()) first = candidate;
    }
}
