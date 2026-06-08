using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

[HarmonyPatch(typeof(InventoryGui), "OnTakeAll")]
internal static class InventoryGuiSafeTakeAllPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        return !InventoryActionsPlugin.TryHandleSafeTakeAll(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnStackAll")]
internal static class InventoryGuiPlaceStacksPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        return !InventoryActionsPlugin.TryHandleVanillaPlaceStacks(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "Update")]
internal static class InventoryGuiUpdateInventoryActionsPatch
{
    private static void Postfix(InventoryGui __instance)
    {
        InventoryActionsPlugin.UpdateInventoryActionsUi(__instance);
        InventoryActionsPlugin.UpdateInventoryTrashConfirmDialogInput();
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
internal static class InventoryGuiTopFirstMoveSelectedItemPatch
{
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemData item, Vector2i pos, InventoryGrid.Modifier mod)
    {
        return !InventoryActionsPlugin.TryHandleTopFirstMoveSelectedItem(__instance, grid, item, mod);
    }
}

[HarmonyPatch(typeof(UITooltip), "OnHoverStart")]
internal static class UITooltipNullPrefabGuardInventoryActionsPatch
{
    private static bool Prefix(UITooltip __instance)
    {
        return InventoryActionsPlugin.ShouldAllowTooltipHoverStart(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "Hide")]
internal static class InventoryGuiHideInventoryActionsPatch
{
    private static void Postfix()
    {
        InventoryActionsPlugin.HideInventoryActionPanels();
    }
}

[HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
internal static class InventoryGuiCloseContainerInventoryActionsPatch
{
    private static void Postfix()
    {
        InventoryActionsPlugin.HideInventoryActionPanels();
    }
}

[HarmonyPatch(typeof(Container), "StackAll")]
internal static class ContainerStackAllInventoryActionsPatch
{
    private static bool Prefix(Container __instance)
    {
        return !InventoryActionsPlugin.TryHandleContainerStackAll(__instance);
    }
}

[HarmonyPatch(typeof(Container), "GetHoverText")]
internal static class ContainerActionHoverTextPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        InventoryActionsPlugin.AppendContainerActionHoverText(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Container), "Interact")]
internal static class ContainerRestockInteractPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character)
    {
        return !InventoryActionsPlugin.ShouldSuppressContainerInteractForRestock(__instance, character);
    }
}

[HarmonyPatch(typeof(Container), "Awake")]
internal static class ContainerAwakeInventoryActionsPatch
{
    private static void Postfix(Container __instance)
    {
        InventoryActionsPlugin.RegisterContainer(__instance);
    }
}

[HarmonyPatch(typeof(Container), "OnDestroyed")]
internal static class ContainerDestroyedInventoryActionsPatch
{
    private static void Postfix(Container __instance)
    {
        InventoryActionsPlugin.UnregisterContainer(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "OnLeftClick")]
internal static class InventoryGridFavoriteLeftClickPatch
{
    private static bool Prefix(InventoryGrid __instance, UIInputHandler clickHandler)
    {
        return InventoryActionsPlugin.HandleFavoriteClick(__instance, clickHandler);
    }
}
