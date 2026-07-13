using System;
using HarmonyLib;

namespace InventorySlots;

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
internal static class InventoryGuiCraftingQueueStartPatch
{
    private static void Prefix(InventoryGui __instance)
    {
        InventorySlotsPlugin.BeginCraftingInventoryLimitNotice();
        InventorySlotsPlugin.PrepareCraftingQueue(__instance);
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
internal static class InventoryGuiUpgradeFavoriteCraftingPatch
{
    private static void Prefix(InventoryGui __instance)
    {
        InventorySlotsPlugin.BeginCraftingInventoryLimitNotice();
        InventorySlotsPlugin.CaptureUpgradeFavoriteBeforeCrafting(__instance);
    }

    private static void Postfix(InventoryGui __instance, Player player)
    {
        InventorySlotsPlugin.RestoreUpgradeFavoriteAfterCrafting(__instance, player);
        if (InventorySlotsPlugin.EndCraftingInventoryLimitNotice(showMessage: true))
        {
            InventorySlotsPlugin.ClearCraftingQueue();
        }
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        InventorySlotsPlugin.EndCraftingInventoryLimitNotice(showMessage: false);
        return __exception;
    }
}
