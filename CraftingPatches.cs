using System;
using HarmonyLib;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InventorySlots;

[HarmonyPatch(typeof(ScrollRect), nameof(ScrollRect.OnScroll))]
[HarmonyPriority(Priority.First)]
internal static class CraftingTooltipUnderlyingScrollRectGuardPatch
{
    private static bool Prefix(ScrollRect __instance, PointerEventData __0)
    {
        if (!InventorySlotsPlugin.TryHandleCraftingPointerScroll(__instance, __0))
        {
            return true;
        }

        __0.Use();
        return false;
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
[HarmonyPriority(Priority.First)]
[HarmonyBefore(new[] { "org.bepinex.plugins.jewelcrafting" })]
internal static class InventoryGuiJewelcraftingPrimaryTabPreflightPatch
{
    private static void Prefix(InventoryGui __instance)
    {
        InventorySlotsPlugin.NormalizeJewelcraftingSocketTabForPrimaryCraftingTab(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
[HarmonyPriority(Priority.Last)]
[HarmonyAfter(new[] { "com.maxsch.valheim.vnei", "org.bepinex.plugins.jewelcrafting", "Azumatt.Recycle_N_Reclaim" })]
internal static class InventoryGuiCraftingPanelRedesignPatch
{
    private static void Postfix(InventoryGui __instance)
    {
        InventorySlotsPlugin.UpdateCraftingPanelRedesign(__instance, CraftingPanelUpdateReason.FrameTick);
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
[HarmonyPriority(Priority.Last)]
[HarmonyAfter(new[] { "org.bepinex.plugins.jewelcrafting", "Azumatt.Recycle_N_Reclaim" })]
internal static class InventoryGuiCraftingRecipeRedesignPatch
{
    private static void Postfix(InventoryGui __instance)
    {
        InventorySlotsPlugin.UpdateCraftingPanelRedesign(__instance, CraftingPanelUpdateReason.RecipeChanged);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnVariantSelected")]
internal static class InventoryGuiCraftingRecipeVariantSelectedPatch
{
    private static void Postfix(InventoryGui __instance, int index)
    {
        InventorySlotsPlugin.OnCraftingRecipeVariantSelected(__instance, index);
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList")]
[HarmonyPriority(Priority.First)]
[HarmonyBefore(new[] { "org.bepinex.plugins.jewelcrafting" })]
internal static class InventoryGuiJewelcraftingRecipeListPrimaryTabPreflightPatch
{
    private static void Prefix(InventoryGui __instance)
    {
        InventorySlotsPlugin.NormalizeJewelcraftingSocketTabForPrimaryCraftingTab(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList")]
[HarmonyPriority(Priority.Last)]
[HarmonyAfter(new[] { "com.maxsch.valheim.vnei", "org.bepinex.plugins.jewelcrafting" })]
internal static class InventoryGuiCraftingRecipeListRedesignPatch
{
    private static void Postfix(InventoryGui __instance)
    {
        InventorySlotsPlugin.OnCraftingRecipeListUpdated(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
[HarmonyPriority(Priority.First)]
[HarmonyBefore(new[] { "org.bepinex.plugins.jewelcrafting" })]
internal static class InventoryGuiCraftingQueueStartPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        if (!InventorySlotsPlugin.CanStartCraftingAction(__instance))
        {
            return false;
        }

        InventorySlotsPlugin.BeginCraftingInventoryLimitNotice();
        InventorySlotsPlugin.PrepareCraftingQueue(__instance);
        return true;
    }

    private static void Postfix(InventoryGui __instance)
    {
        if (InventorySlotsPlugin.EndCraftingInventoryLimitNotice(showMessage: true))
        {
            InventorySlotsPlugin.ClearCraftingQueue();
        }

        InventorySlotsPlugin.ValidateCraftingQueueStarted(__instance);
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        InventorySlotsPlugin.EndCraftingInventoryLimitNotice(showMessage: false);
        return __exception;
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnCraftCancelPressed")]
internal static class InventoryGuiCraftingQueueCancelPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.ClearCraftingQueue();
    }
}

[HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
[HarmonyPriority(Priority.First)]
[HarmonyBefore(new[] { "org.bepinex.plugins.jewelcrafting" })]
internal static class InventoryGuiUpgradeFavoriteCraftingPatch
{
    private static bool Prefix(InventoryGui __instance, out bool __state)
    {
        __state = false;
        if (!InventorySlotsPlugin.CanCompleteCraftingAction(__instance))
        {
            return false;
        }

        __state = true;
        InventorySlotsPlugin.BeginCraftingInventoryLimitNotice();
        InventorySlotsPlugin.CaptureUpgradeFavoriteBeforeCrafting(__instance);
        return true;
    }

    private static void Postfix(InventoryGui __instance, Player player, bool __state)
    {
        if (!__state)
        {
            return;
        }

        InventorySlotsPlugin.RestoreUpgradeFavoriteAfterCrafting(__instance, player);
        if (InventorySlotsPlugin.EndCraftingInventoryLimitNotice(showMessage: true))
        {
            InventorySlotsPlugin.ClearCraftingQueue();
        }
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            InventorySlotsPlugin.EndCraftingInventoryLimitNotice(showMessage: false);
        }

        return __exception;
    }
}
