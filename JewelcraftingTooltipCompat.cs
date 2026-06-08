namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool TryGetJewelcraftingTooltipApi(out JewelcraftingTooltipApi? api)
    {
        const string capability = "Jewelcrafting tooltip";
        return TryGetCompatApi(
            JewelcraftingGuid,
            capability,
            CompatRuntime.JewelcraftingTooltip,
            JewelcraftingTooltipApi.TryCreate,
            "Jewelcrafting tooltip compatibility disabled",
            out api);
    }

    private static bool TryGetJewelcraftingGemApi(out JewelcraftingGemApi? api)
    {
        const string capability = "Jewelcrafting gem row";
        return TryGetCompatApi(
            JewelcraftingGuid,
            capability,
            CompatRuntime.JewelcraftingGem,
            JewelcraftingGemApi.TryCreate,
            "Jewelcrafting gem row compatibility disabled",
            out api);
    }
}
