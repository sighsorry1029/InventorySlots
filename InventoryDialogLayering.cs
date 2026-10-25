using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

internal static class InventoryDialogLayering
{
    // The native tooltip can live inside Player's Canvas or a gamepad anchor,
    // or remain under the Canvas used when its shared native host was created.
    // Lift the whole host out of that branch without rebuilding another mod's card.
    internal sealed class TooltipLayer
    {
        private Transform? _tooltip, _root, _originalParent;
        private Transform? _content;
        private int _originalSibling;
        private int _childCount;
        private readonly List<Canvas> _canvases = new();
        private readonly List<Canvas> _overrides = new();

        internal void Update(InventoryGui gui, Transform? quickSlotPanel, Transform? sortLayer, Transform? tooltip,
            bool allowCanvasParent = false, Transform? content = null)
        {
            Transform root = gui.m_inventoryRoot;
            if (root == null || !root.gameObject.activeInHierarchy ||
                tooltip == null || !tooltip.gameObject.activeInHierarchy ||
                tooltip == root || root.IsChildOf(tooltip))
            {
                Restore();
                return;
            }
            if (_tooltip != tooltip || _root != root) Restore();

            Transform? branch = tooltip;
            while (branch != null && branch.parent != root) branch = branch.parent;
            // Only the caller that verified the current native PoV item card may
            // adopt a host directly below an outer Canvas. Unknown popup wrappers
            // keep ownership; never elevate the Canvas or its other children.
            bool outerCanvas = branch == null && allowCanvasParent && tooltip.parent != null &&
                tooltip.parent.gameObject.GetComponent<Canvas>() != null;
            if (!outerCanvas && (branch == null || branch != tooltip && !IsBasePanel(branch, gui, quickSlotPanel)))
            {
                Restore();
                return;
            }

            InventoryDialogLayering.Update(gui, quickSlotPanel, sortLayer);
            bool reparented = _tooltip == null || tooltip.parent != root;
            if (reparented)
            {
                _tooltip = tooltip;
                _root = root;
                _originalParent = tooltip.parent;
                _originalSibling = tooltip.GetSiblingIndex();
                if (tooltip.parent != root) tooltip.SetParent(root, worldPositionStays: true);
            }

            // Inherit the inventory Canvas instead of imposing an arbitrary high
            // sortingOrder which could cover split/confirmation dialogs. PoV's
            // body can be rebuilt in place; rescan only on that identity change,
            // host/reparent changes or direct-child changes, not every UI frame.
            if (reparented || _content != content || _childCount != tooltip.childCount)
            {
                RestoreCanvasOverrides();
                _canvases.Clear();
                tooltip.gameObject.GetComponentsInChildren(true, _canvases);
                _content = content;
                _childCount = tooltip.childCount;
            }
            foreach (Canvas canvas in _canvases)
            {
                if (canvas == null) continue;
                if (!canvas.transform.IsChildOf(tooltip))
                {
                    if (_overrides.Remove(canvas) && !canvas.overrideSorting) canvas.overrideSorting = true;
                    continue;
                }
                if (!canvas.overrideSorting) continue;
                if (!_overrides.Contains(canvas)) _overrides.Add(canvas);
                canvas.overrideSorting = false;
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
            RestoreCanvasOverrides();
            _canvases.Clear();
            // Native HideTooltip may already have destroyed the host. A foreign
            // reparent also ends our ownership; never move that popup back.
            if (_tooltip != null && _root != null && _tooltip.parent == _root && _originalParent != null)
            {
                if (_tooltip.parent != _originalParent) _tooltip.SetParent(_originalParent, worldPositionStays: true);
                int index = Mathf.Clamp(_originalSibling, 0, _originalParent.childCount - 1);
                if (_tooltip.GetSiblingIndex() != index) _tooltip.SetSiblingIndex(index);
            }
            _tooltip = _root = _originalParent = _content = null;
            _childCount = 0;
        }

        private void RestoreCanvasOverrides()
        {
            // Only this flag was changed; preserve sortingOrder/layer and any
            // external reparent. Destroyed/replaced PoV cards are harmless here.
            foreach (Canvas canvas in _overrides)
                if (canvas != null && !canvas.overrideSorting) canvas.overrideSorting = true;
            _overrides.Clear();
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
