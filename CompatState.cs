using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum CompatCapabilityState
    {
        Unknown,
        Unavailable,
        Available,
        MissingApi,
        Failed
    }

    private sealed class CompatApiRuntimeState<TApi>
        where TApi : class
    {
        public TApi? Api;
        public bool ReflectionFailed;
        public CompatCapabilityState CapabilityState = CompatCapabilityState.Unknown;
    }

    private sealed class CompatRuntimeState
    {
        public bool MyLittleUICraftingCompatibilityApplied;
        public bool JewelcraftingSlotStateInitialized;
        public bool AdventureBackpacksPatchesApplied;
        public bool LastJewelcraftingRingSlotEnabled;
        public bool LastJewelcraftingNecklaceSlotEnabled;
        public bool LastJewelcraftingWisplightGemEnabled;
        public bool LastJewelcraftingWishboneGemEnabled;
        public ItemData? LastAdventureBackpackCompatItem;
        public ItemData? LastSmoothbrainBackpackCompatItem;
        public ItemData? LastRustyBagCompatItem;
        public ItemData? LastRustyQuiverCompatItem;
        public ItemData? LastMagicSupremacyBeltCompatItem;
        public readonly CompatApiRuntimeState<AzuCraftyBoxesApi> AzuCraftyBoxes = new();
        public readonly CompatApiRuntimeState<JewelcraftingTooltipApi> JewelcraftingTooltip = new();
        public readonly CompatApiRuntimeState<JewelcraftingGemApi> JewelcraftingGem = new();
        public readonly CompatApiRuntimeState<JewelcraftingSlotApi> JewelcraftingSlot = new();
        public readonly CompatApiRuntimeState<JewelcraftingCraftingSocketUiApi> JewelcraftingCraftingSocketUi = new();
        public readonly CompatApiRuntimeState<JewelcraftingGemCuttingApi> JewelcraftingGemCutting = new();
        public readonly CompatApiRuntimeState<JewelcraftingVisualApi> JewelcraftingVisual = new();
        public readonly CompatApiRuntimeState<ItemRequiresSkillLevelApi> ItemRequiresSkillLevel = new();
        public readonly CompatApiRuntimeState<RecycleNReclaimApi> RecycleNReclaim = new();
        public readonly CompatApiRuntimeState<AdventureBackpacksApi> AdventureBackpacks = new();
        public readonly CompatApiRuntimeState<SmoothbrainBackpacksApi> SmoothbrainBackpacks = new();
        public readonly CompatApiRuntimeState<RustyBagsApi> RustyBags = new();
        public readonly CompatApiRuntimeState<MagicSupremacyApi> MagicSupremacy = new();
        public readonly CompatApiRuntimeState<BetterArcheryQuiverApi> BetterArcheryQuiver = new();
        public readonly CompatApiRuntimeState<VeiledRecipesApi> VeiledRecipes = new();
    }

    private static readonly CompatRuntimeState CompatRuntime = new();
}
