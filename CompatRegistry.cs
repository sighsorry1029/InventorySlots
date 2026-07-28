using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string ServerCharactersGuid = "org.bepinex.plugins.servercharacters";
    private const string MultiUserChestGuid = "com.maxsch.valheim.MultiUserChest";
    private const string BetterArcheryGuid = "ishid4.mods.betterarchery";
    private const string AzuCraftyBoxesGuid = "Azumatt.AzuCraftyBoxes";
    private const string EpicLootGuid = "randyknapp.mods.epicloot";
    private const string TooltipExpansionGuid = "Azumatt.TooltipExpansion";
    private const string JewelcraftingGuid = "org.bepinex.plugins.jewelcrafting";
    private const string ItemRequiresSkillLevelGuid = "WackyMole.ItemRequiresSkillLevel";
    private const string AdventureBackpacksGuid = "vapok.mods.adventurebackpacks";
    private const string SmoothbrainBackpacksGuid = "org.bepinex.plugins.backpacks";
    private const string RustyBagsGuid = "RustyMods.RustyBags";
    private const string MagicSupremacyGuid = "Dreanegade.Magic_Supremacy";
    private const string CurrencyPocketGuid = "Azumatt.CurrencyPocket";
    private const string MyLittleUIGuid = "shudnal.MyLittleUI";
    private const string RecycleNReclaimGuid = "Azumatt.Recycle_N_Reclaim";
    private const string VeiledRecipesGuid = "sighsorry.VeiledRecipes";
    private const string ContentsWithinGuid = "com.maxsch.valheim.contentswithin";

    private static bool HasServerCharactersActive => !ZNet.IsSinglePlayer && HasPlugin(ServerCharactersGuid);
    private static bool HasExternalMultiUserChestActive => HasPlugin(MultiUserChestGuid);
    private static bool IsBuiltInMultiUserChestEnabled =>
        !HasExternalMultiUserChestActive &&
        _enableBuiltInMultiUserChest?.Value == Toggle.On;
    private static bool HasJewelcraftingActive => HasPlugin(JewelcraftingGuid);

    private static bool HasPlugin(string guid)
    {
        return !string.IsNullOrWhiteSpace(guid) && Chainloader.PluginInfos.ContainsKey(guid);
    }

    internal static bool TryGetCurrencyPocketOverlapDetectionMethod(
        out MethodBase? method)
    {
        method = null;
        if (!Chainloader.PluginInfos.TryGetValue(
                CurrencyPocketGuid,
                out BepInEx.PluginInfo pluginInfo) ||
            pluginInfo.Instance == null)
        {
            return false;
        }

        Type? miscFunctionsType =
            pluginInfo.Instance.GetType().Assembly.GetType(
                "CurrencyPocket.MiscFunctions");
        MethodInfo? candidate = miscFunctionsType?.GetMethod(
            "IsOverlappingUIModInstalled",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (candidate == null || candidate.ReturnType != typeof(bool))
        {
            return false;
        }

        method = candidate;
        return true;
    }
}

[HarmonyPatch]
internal static class CurrencyPocketOverlapDetectionInventorySlotsPatch
{
    private static bool Prepare() =>
        InventorySlotsPlugin.TryGetCurrencyPocketOverlapDetectionMethod(out _);

    private static MethodBase TargetMethod()
    {
        InventorySlotsPlugin.TryGetCurrencyPocketOverlapDetectionMethod(
            out MethodBase? method);
        return method!;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref bool __result)
    {
        __result = true;
    }
}
