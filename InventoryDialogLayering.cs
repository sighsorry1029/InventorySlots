using UnityEngine;

namespace InventorySlots;

internal static class InventoryDialogLayering
{
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
