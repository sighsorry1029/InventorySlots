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

        (gui, panels) = CreateLayerFixture("Player", "Sort", "Quick", "Container", "Info", "Crafting", "Texts", "Popup");
        panels["Texts"].SetActive(true);
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform, panels["Sort"].transform);
        CheckLayerOrder(gui, panels, "Player", "Quick", "Container", "Info", "Crafting", "Sort", "Texts", "Popup");
        int writes = Transform.SiblingWrites;
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform, panels["Sort"].transform);
        Check(Transform.SiblingWrites == writes, "Stable sort hierarchy is not rewritten every frame");

        foreach (string dialog in new[] { "Split", "Achievements", "Trophies", "Variant", "Skills", "Texts" })
        {
            (gui, panels) = CreateLayerFixture("Player", dialog, "Popup", "Sort", "Container", "Quick");
            Transform pinHost = new GameObject().transform;
            pinHost.parent = panels["Sort"].transform;
            Transform pinnedCard = new GameObject().transform;
            pinnedCard.parent = pinHost;
            panels[dialog].SetActive(true);
            InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform, panels["Sort"].transform);
            CheckLayerOrder(gui, panels, "Player", "Container", "Quick", "Sort", dialog, "Popup");
            Check(pinHost.parent == panels["Sort"].transform && pinnedCard.parent == pinHost,
                "Raised crafting pins remain below " + dialog + " without moving their coordinate host");
        }

        (gui, panels) = CreateLayerFixture("Sort", "Player", "Quick", "Container", "Popup");
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform, panels["Sort"].transform);
        CheckLayerOrder(gui, panels, "Player", "Quick", "Container", "Sort", "Popup");
        panels["Quick"].transform.parent = new GameObject().transform;
        InventorySlots.InventoryDialogLayering.Update(gui, panels["Quick"].transform, panels["Sort"].transform);
        CheckLayerOrder(gui, panels, "Player", "Container", "Sort", "Popup");
        CheckInventoryTooltipLayering();
    }

    private static void CheckInventoryTooltipLayering()
    {
        var layer = new InventorySlots.InventoryDialogLayering.TooltipLayer();
        var (gui, panels) = CreateLayerFixture("Player", "Quick", "Crafting", "Sort", "Rules", "Split", "Confirm", "Popup");
        Transform tooltip = panels["Tooltip"].transform;
        Transform player = panels["Player"].transform;
        Transform sibling = new GameObject().transform;
        tooltip.parent = player;
        sibling.parent = player;
        Transform card = new GameObject().transform;
        card.parent = tooltip;
        panels["Split"].SetActive(true);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        CheckLayerOrder(gui, panels, "Player", "Quick", "Crafting", "Sort", "Tooltip", "Rules", "Split", "Confirm", "Popup");
        Check(tooltip.LastWorldPositionStays == true && card.parent == tooltip,
            "Tooltip promotion requests world-pose preservation and retains card children");
        int writes = Transform.SiblingWrites, parents = Transform.ParentWrites;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(Transform.SiblingWrites == writes && Transform.ParentWrites == parents,
            "Stable/pinned tooltip layer does not rewrite the hierarchy");
        layer.Restore();
        Check(tooltip.parent == player && player.GetChild(0) == tooltip && player.GetChild(1) == sibling,
            "Releasing tooltip restores original parent and sibling order");

        // Native controller/touch AnchorTooltip can reparent again before our postfix.
        Transform anchor = new GameObject().transform;
        anchor.parent = panels["Quick"].transform;
        tooltip.parent = anchor;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(tooltip.parent == gui.m_inventoryRoot, "Quick-slot/controller anchor is promoted too");
        tooltip.parent = anchor;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(tooltip.parent == gui.m_inventoryRoot, "Later native anchoring is corrected after placement");
        gui.m_inventoryRoot.gameObject.SetActive(false);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(tooltip.parent == anchor, "Closing inventory restores the most recent native anchor");
        gui.m_inventoryRoot.gameObject.SetActive(true);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        tooltip.gameObject.SetActive(false);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(tooltip.parent == anchor, "Inactive tooltip releases its layer ownership");
        tooltip.gameObject.SetActive(true);

        // Replacing the host restores the old card, even if native hide was vetoed.
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Transform next = new GameObject().transform;
        next.parent = player;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, next);
        Check(tooltip.parent == anchor && next.parent == gui.m_inventoryRoot, "Host replacement restores previous host only");
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, null);
        Check(next.parent == player, "Absent native host safely clears the current ownership");

        // Foreign popups and already-elevated Canvas children are never adopted.
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        tooltip.parent = panels["Popup"].transform;
        layer.Restore();
        Check(tooltip.parent == panels["Popup"].transform, "External reparent wins over restoration");
        writes = Transform.SiblingWrites; parents = Transform.ParentWrites;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(Transform.SiblingWrites == writes && Transform.ParentWrites == parents, "Foreign popup child is untouched");
        Transform canvas = new GameObject().transform;
        gui.m_inventoryRoot.parent = canvas;
        tooltip.parent = canvas;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(tooltip.parent == canvas && Transform.ParentWrites == parents, "Outer Canvas tooltip keeps its owner's layer");
        CheckVerifiedOuterCanvasTooltip();

        foreach (string dialog in new[] { "Split", "Achievements", "Trophies", "Variant", "Skills", "Texts" })
        {
            (gui, panels) = CreateLayerFixture("Player", dialog, "Popup", "Quick", "Sort");
            tooltip = panels["Tooltip"].transform;
            tooltip.parent = panels["Player"].transform;
            panels[dialog].SetActive(true);
            layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
            CheckLayerOrder(gui, panels, "Player", "Quick", "Sort", "Tooltip", dialog, "Popup");
            layer.Restore();
        }

        (gui, panels) = CreateLayerFixture("Tooltip", "Player", "Quick", "Sort", "Popup");
        tooltip = panels["Tooltip"].transform;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        CheckLayerOrder(gui, panels, "Player", "Quick", "Sort", "Tooltip", "Popup");
        layer.Restore();
        CheckLayerOrder(gui, panels, "Tooltip", "Player", "Quick", "Sort", "Popup");
    }

    private static Canvas AddLayerCanvas(Transform node, int order, bool separate = true)
    {
        var canvas = new Canvas { gameObject = node.gameObject, overrideSorting = separate, sortingOrder = order, sortingLayerID = 7 };
        node.gameObject.Components[typeof(Canvas)] = canvas;
        return canvas;
    }

    private static void CheckVerifiedOuterCanvasTooltip()
    {
        var layer = new InventorySlots.InventoryDialogLayering.TooltipLayer();
        var (gui, panels) = CreateLayerFixture("Player", "Quick", "Container", "Crafting", "Sort", "Split", "Popup");
        panels["Split"].SetActive(true);
        Transform outer = new GameObject().transform;
        Canvas outerCanvas = AddLayerCanvas(outer, -100);
        Transform tooltip = panels["Tooltip"].transform;
        tooltip.parent = outer;
        gui.m_inventoryRoot.parent = outer; // Native host is behind the entire inventory.
        Transform body = new GameObject().transform;
        body.parent = tooltip;
        Canvas hostCanvas = AddLayerCanvas(tooltip, -200);
        Canvas bodyCanvas = AddLayerCanvas(body, 32000);
        Canvas panelCanvas = AddLayerCanvas(panels["Player"].transform, 0, separate: false);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip);
        Check(tooltip.parent == outer && tooltip.GetSiblingIndex() < gui.m_inventoryRoot.GetSiblingIndex(),
            "Unverified outer Canvas host is not adopted");
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip, true, body);
        CheckLayerOrder(gui, panels, "Player", "Quick", "Container", "Crafting", "Sort", "Tooltip", "Split", "Popup");
        Check(tooltip.LastWorldPositionStays == true && body.parent == tooltip,
            "Verified external PoV card moves intact with world pose preserved");
        Check(!hostCanvas.overrideSorting && !bodyCanvas.overrideSorting && outerCanvas.overrideSorting && !panelCanvas.overrideSorting,
            "Only tooltip-owned Canvas overrides yield to inventory/modal hierarchy");
        Check(hostCanvas.sortingOrder == -200 && bodyCanvas.sortingOrder == 32000 && hostCanvas.sortingLayerID == 7,
            "Canvas order/layer values are never overwritten");
        int parents = Transform.ParentWrites, siblings = Transform.SiblingWrites, overrides = Canvas.OverrideWrites;
        for (int n = 0; n < 10; n++) layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip, true, body);
        Check(Transform.ParentWrites == parents && Transform.SiblingWrites == siblings && Canvas.OverrideWrites == overrides,
            "Stable external/pinned tooltip needs no repeat parent/order/Canvas writes");

        // PoV rebuilds its inner card without replacing the shared native host.
        body.parent = outer;
        Transform replacement = new GameObject().transform;
        replacement.parent = tooltip;
        Canvas replacementCanvas = AddLayerCanvas(replacement, -400);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip, true, replacement);
        Check(bodyCanvas.overrideSorting && !replacementCanvas.overrideSorting && !hostCanvas.overrideSorting,
            "Body recreation restores outgoing Canvas and normalizes the replacement");
        layer.Restore();
        Check(tooltip.parent == outer && tooltip.GetSiblingIndex() == 0 &&
            hostCanvas.overrideSorting && replacementCanvas.overrideSorting,
            "Release restores external parent/sibling and all original Canvas flags");

        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip, true, replacement);
        tooltip.parent = panels["Popup"].transform;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip, true, replacement);
        Check(tooltip.parent == panels["Popup"].transform && hostCanvas.overrideSorting && replacementCanvas.overrideSorting,
            "Foreign popup takeover is retained while our Canvas changes are released");
        tooltip.parent = outer;
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip, true, replacement);
        gui.m_inventoryRoot.gameObject.SetActive(false);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, tooltip, true, replacement);
        Check(tooltip.parent == outer && hostCanvas.overrideSorting, "Inventory close releases an externally created tooltip");

        // An unexpected owner/root value must never create a Transform cycle.
        gui.m_inventoryRoot.gameObject.SetActive(true);
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, outer, true);
        Check(gui.m_inventoryRoot.parent == outer && outer.parent == null, "Tooltip ancestor is rejected without a parent cycle");
        layer.Update(gui, panels["Quick"].transform, panels["Sort"].transform, gui.m_inventoryRoot, true);
        Check(gui.m_inventoryRoot.parent == outer, "Inventory root cannot be adopted as its own tooltip");
    }

    private static (InventoryGui, Dictionary<string, GameObject>) CreateLayerFixture(params string[] order)
    {
        InventoryGui gui = new();
        Dictionary<string, GameObject> panels = new();
        foreach (string name in new[] { "Player", "Container", "Quick", "Sort", "Info", "Crafting", "Split", "Achievements", "Trophies", "Variant", "Skills", "Texts", "Popup", "Tooltip", "Rules", "Confirm" })
            panels[name] = new GameObject();
        gui.m_player = panels["Player"].transform;
        gui.m_container = panels["Container"].transform;
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
