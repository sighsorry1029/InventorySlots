#if INVENTORY_SLOTS
using System.Collections.Generic;
using UnityEngine;

internal static partial class Program
{
    private static void CheckInventoryDialogLayering()
    {
        string[] normal = { "Player", "Quick", "Info", "Crafting", "Split", "Achievements", "Trophies", "Variant", "Skills", "Texts", "Popup" };
        var (gui, panels) = CreateLayerFixture(normal);
        panels["Texts"].SetActive(true);
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform);
        Check(Transform.SiblingWrites == 0, "Normal dialog hierarchy needs no writes");

        // Each native dialog must work by itself, including the GameObject field.
        foreach (string dialog in new[] { "Split", "Achievements", "Trophies", "Variant", "Skills", "Texts" })
        {
            (gui, panels) = CreateLayerFixture("Player", "Info", dialog, "Popup", "Crafting", "Quick");
            panels[dialog].SetActive(true);
            InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform);
            CheckLayerOrder(gui, panels, "Player", "Info", "Crafting", "Quick", dialog, "Popup");
            Check(Transform.SiblingWrites == 2, dialog + " moves only the two misplaced base panels");
            InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform);
            Check(Transform.SiblingWrites == 2, dialog + " does not rewrite the corrected hierarchy");
        }

        // Preserve the actual modal/base ordering rather than imposing enumeration order.
        (gui, panels) = CreateLayerFixture("Texts", "Quick", "Popup", "Skills", "Crafting", "Player", "Info");
        panels["Texts"].SetActive(true);
        panels["Skills"].SetActive(true);
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform);
        CheckLayerOrder(gui, panels, "Quick", "Crafting", "Player", "Info", "Texts", "Popup", "Skills");

        // Hidden native dialogs must not affect the normal panels or other popups.
        (gui, panels) = CreateLayerFixture("Texts", "Player", "Info", "Crafting", "Quick", "Popup");
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform);
        Check(Transform.SiblingWrites == 0, "No open native dialog means no layer changes");
        panels["Texts"].SetActive(true);
        gui.m_inventoryRoot.gameObject.SetActive(false);
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform);
        Check(Transform.SiblingWrites == 0, "Hidden inventory root is not reordered");

        // Closing quick slots can temporarily live beside the root for their animation.
        (gui, panels) = CreateLayerFixture("Player", "Info", "Texts", "Crafting", "Quick", "Popup");
        panels["Texts"].SetActive(true);
        Transform outroParent = new GameObject().transform;
        panels["Quick"].transform.parent = outroParent;
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform);
        CheckLayerOrder(gui, panels, "Player", "Info", "Crafting", "Texts", "Popup");
        Check(panels["Quick"].transform.parent == outroParent && outroParent.GetChild(0) == panels["Quick"].transform,
            "Quick-slot outro parent is untouched");

        // External owners of a reparented dialog keep control of their hierarchy.
        (gui, panels) = CreateLayerFixture("Texts", "Crafting", "Popup");
        panels["Texts"].SetActive(true);
        panels["Texts"].transform.parent = new GameObject().transform;
        InventorySlots.InventoryDialogLayering.Update(gui, null);
        Check(Transform.SiblingWrites == 0, "Foreign dialog parent is left alone");
        gui.m_textsDialog = null;
        gui.m_splitDialog = null;
        gui.m_variantDialog = null;
        gui.m_skillsDialog = null;
        gui.m_achievementsPanel = null;
        gui.m_trophiesPanel = null;
        InventorySlots.InventoryDialogLayering.Update(gui, null);
        Check(Transform.SiblingWrites == 0, "Absent dialogs and quick slots are supported");
        gui.m_inventoryRoot = null!;
        InventorySlots.InventoryDialogLayering.Update(gui, null);
        Check(Transform.SiblingWrites == 0, "Missing root is safe during teardown");
    }

    private static (InventoryGui, Dictionary<string, GameObject>) CreateLayerFixture(params string[] order)
    {
        InventoryGui gui = new();
        Dictionary<string, GameObject> panels = new();
        foreach (string name in new[] { "Player", "Quick", "Info", "Crafting", "Split", "Achievements", "Trophies", "Variant", "Skills", "Texts", "Popup" })
            panels[name] = new GameObject();
        gui.m_player = panels["Player"].transform;
        gui.m_info = panels["Info"].transform;
        gui.m_crafting = panels["Crafting"].transform;
        gui.m_splitDialog = new Component { gameObject = panels["Split"] };
        gui.m_achievementsPanel = new Component { gameObject = panels["Achievements"] };
        gui.m_trophiesPanel = panels["Trophies"];
        gui.m_variantDialog = new Component { gameObject = panels["Variant"] };
        gui.m_skillsDialog = new Component { gameObject = panels["Skills"] };
        gui.m_textsDialog = new Component { gameObject = panels["Texts"] };
        foreach (string name in new[] { "Split", "Achievements", "Trophies", "Variant", "Skills", "Texts" })
            panels[name].SetActive(false);
        foreach (string name in order) panels[name].transform.parent = gui.m_inventoryRoot;
        Transform.SiblingWrites = 0;
        return (gui, panels);
    }

    private static void CheckLayerOrder(InventoryGui gui, Dictionary<string, GameObject> panels, params string[] expected)
    {
        Check(gui.m_inventoryRoot.childCount == expected.Length, "Layer correction preserves every child");
        for (int i = 0; i < expected.Length; i++)
            Check(gui.m_inventoryRoot.GetChild(i) == panels[expected[i]].transform,
                "Expected dialog hierarchy: " + string.Join(", ", expected) + " at " + i);
    }
}
#endif
