using System.Collections.Generic;
using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(InventoryGrid), "UpdateGui", new[] { typeof(Player), typeof(ItemDrop.ItemData) })]
internal static class ContainerGridInitialElementsPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(InventoryGrid __instance, Inventory? ___m_inventory,
        List<InventoryElement> ___m_elements, ref int ___m_width, int ___m_height, out int __state)
    {
        __state = 0;
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || gui.ContainerGrid != __instance || ___m_inventory == null ||
            ___m_elements == null || ___m_elements.Count != 0 ||
            __instance.m_elementPrefab == null || __instance.m_gridRoot == null)
        {
            return;
        }

        int width = ___m_inventory.GetWidth();
        int height = ___m_inventory.GetHeight();
        if (width <= 0 || height <= 0 || ___m_width != width || ___m_height != height)
        {
            return;
        }

        // Valheim starts with cached dimensions 4x4 but no elements. A first
        // 4x4 chest otherwise skips creation forever. Invalidate only the UI
        // cache here, after UpdateGamepad; vanilla creates cells and handlers.
        __state = ___m_width;
        ___m_width = -1;
    }

    private static void Finalizer(int __state, ref int ___m_width)
    {
        // A foreign prefix can cancel or throw before vanilla restores the
        // width. Never leave the temporary value for the next gamepad update.
        if (__state > 0 && ___m_width == -1)
        {
            ___m_width = __state;
        }
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnTakeAll")]
internal static class InventoryGuiSafeTakeAllPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        return !InventorySlotsPlugin.ShouldBlockContainerPreviewInteraction(__instance) &&
               !InventorySlotsPlugin.TryHandleSafeTakeAll(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnStackAll")]
internal static class InventoryGuiPlaceStacksPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        return !InventorySlotsPlugin.ShouldBlockContainerPreviewInteraction(__instance) &&
               !InventorySlotsPlugin.TryHandleVanillaPlaceStacks(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "Show")]
internal static class InventoryGuiShowValidateInventoryPatch
{
    private static void Prefix()
    {
        InventorySlotsPlugin.BeforeRealInventoryGuiShown();
    }

    private static void Postfix()
    {
        InventorySlotsPlugin.OnInventoryGuiShow();
    }
}

[HarmonyPatch(typeof(InventoryGui), "Hide")]
internal static class InventoryGuiHideRestoreContainerPanelPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.OnInventoryGuiHide();
    }
}

[HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
internal static class InventoryGuiCloseContainerRestorePanelPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.OnInventoryGuiCloseContainer();
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
internal static class InventoryGuiUpdateContainerInventorySlotsPatch
{
    private static void Postfix(InventoryGui __instance, Player player)
    {
        InventorySlotsPlugin.OnInventoryGuiUpdateContainer(__instance, player);
    }
}
