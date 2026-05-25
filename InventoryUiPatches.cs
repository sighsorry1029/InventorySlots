using System;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

[HarmonyPatch(typeof(KeyHints), "Awake")]
internal static class KeyHintsFavoriteHintAwakePatch
{
    private static void Postfix(KeyHints __instance)
    {
        InventorySlotsPlugin.UpdateFavoriteKeyHint(__instance);
    }
}

[HarmonyPatch(typeof(KeyHints), "UpdateHints")]
internal static class KeyHintsFavoriteHintUpdatePatch
{
    private static void Postfix(KeyHints __instance)
    {
        InventorySlotsPlugin.UpdateFavoriteKeyHint(__instance);
    }
}

[HarmonyPatch(typeof(UITooltip), "OnHoverStart")]
[HarmonyAfter(new[] { "randyknapp.mods.epicloot", "org.bepinex.plugins.jewelcrafting", "Azumatt.TooltipExpansion" })]
internal static class UITooltipNullPrefabGuardPatch
{
    private static bool Prefix(UITooltip __instance)
    {
        return InventorySlotsPlugin.ShouldAllowTooltipHoverStart(__instance);
    }

    private static void Postfix(UITooltip __instance)
    {
        InventorySlotsPlugin.EnsureInventoryContainerHoverTooltipScroll(__instance);
        InventorySlotsPlugin.UpdateInventorySlotsOwnedHoverTooltip(__instance, resetScroll: true, handleWheel: false);
    }
}

[HarmonyPatch(typeof(UITooltip), "LateUpdate")]
[HarmonyAfter(new[] { "randyknapp.mods.epicloot", "org.bepinex.plugins.jewelcrafting", "Azumatt.TooltipExpansion" })]
internal static class UITooltipInventoryContainerBackgroundAlphaPatch
{
    private static bool Prefix(UITooltip __instance)
    {
        return InventorySlotsPlugin.ShouldAllowTooltipLateUpdate(__instance);
    }

    private static void Postfix(UITooltip __instance)
    {
        if (UITooltip.m_current == __instance && !InventorySlotsPlugin.ShouldUpdateInventorySlotsOwnedHoverTooltip(__instance))
        {
            InventorySlotsPlugin.UpdateInventoryContainerHoverTooltipScroll(__instance);
        }

        if (InventorySlotsPlugin.ShouldUpdateInventorySlotsOwnedHoverTooltip(__instance))
        {
            InventorySlotsPlugin.UpdateInventorySlotsOwnedHoverTooltip(__instance, resetScroll: false, handleWheel: true);
        }
    }

    private static Exception? Finalizer(UITooltip __instance, Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        return InventorySlotsPlugin.TryRecoverInventorySlotsTooltipLateUpdateException(__instance, __exception)
            ? null
            : __exception;
    }
}

[HarmonyPatch(typeof(UITooltip), "OnPointerExit")]
[HarmonyAfter(new[] { "randyknapp.mods.epicloot", "org.bepinex.plugins.jewelcrafting", "Azumatt.TooltipExpansion" })]
internal static class UITooltipInventorySlotsOwnedHoverTooltipExitPatch
{
    private static void Postfix(UITooltip __instance)
    {
        InventorySlotsPlugin.EndInventoryContainerHoverTooltipOwnership(__instance);
        InventorySlotsPlugin.EndInventorySlotsOwnedHoverTooltip(__instance);
    }
}

[HarmonyPatch(typeof(UITooltip), "HideTooltip")]
internal static class UITooltipInventoryContainerCustomTooltipHidePatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.OnVanillaTooltipHidden();
    }
}

[HarmonyPatch(typeof(InventoryGrid), "CreateItemTooltip")]
[HarmonyBefore(new[] { "randyknapp.mods.epicloot" })]
internal static class InventoryGridCreateItemTooltipEpicLootUiScopeStartPatch
{
    private static void Prefix(InventoryGrid __instance)
    {
        InventorySlotsPlugin.BeginEpicLootInventoryGridTooltipUiPatchScope(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "CreateItemTooltip")]
[HarmonyAfter(new[] { "randyknapp.mods.epicloot" })]
internal static class InventoryGridCreateItemTooltipEpicLootUiScopeEndPatch
{
    private static void Postfix(InventoryGrid __instance)
    {
        InventorySlotsPlugin.EndEpicLootInventoryGridTooltipUiPatchScope(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "CreateItemTooltip")]
[HarmonyAfter(new[] { "randyknapp.mods.epicloot", "org.bepinex.plugins.jewelcrafting", "Azumatt.TooltipExpansion", "kg.ValheimEnchantmentSystem" })]
internal static class InventoryGridCreateItemTooltipEmptyTextFallbackPatch
{
    private static void Postfix(InventoryGrid __instance, ItemData item, UITooltip tooltip)
    {
        InventorySlotsPlugin.RegisterInventoryGridItemTooltip(__instance, item, tooltip);
        InventorySlotsPlugin.EnsureInventoryGridItemTooltipText(item, tooltip);
    }
}
