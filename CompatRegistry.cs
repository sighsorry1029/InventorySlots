using System;
using BepInEx.Bootstrap;

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

    private static bool HasServerCharactersActive => !ZNet.IsSinglePlayer && HasPlugin(ServerCharactersGuid);
    private static bool HasMultiUserChestActive => HasPlugin(MultiUserChestGuid);
    private static bool HasJewelcraftingActive => HasPlugin(JewelcraftingGuid);

    private static bool HasPlugin(string guid)
    {
        return !string.IsNullOrWhiteSpace(guid) && Chainloader.PluginInfos.ContainsKey(guid);
    }
}
