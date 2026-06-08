namespace InventorySlots;

internal enum HoverTooltipSourceKind
{
    None,
    InventoryContainer,
    InventorySlotsCrafting,
    VneiCrafting
}

internal static class HoverTooltipSourceCore
{
    public static HoverTooltipSourceKind Classify(bool inventoryContainer, bool inventorySlotsCrafting, bool vneiCrafting)
    {
        if (inventoryContainer)
        {
            return HoverTooltipSourceKind.InventoryContainer;
        }

        if (vneiCrafting)
        {
            return HoverTooltipSourceKind.VneiCrafting;
        }

        return inventorySlotsCrafting
            ? HoverTooltipSourceKind.InventorySlotsCrafting
            : HoverTooltipSourceKind.None;
    }

    public static bool UsesInventorySlotsOwnedHoverTooltip(HoverTooltipSourceKind kind) =>
        kind == HoverTooltipSourceKind.VneiCrafting;

    public static bool SuppressesVanillaHoverStart(HoverTooltipSourceKind kind) =>
        kind == HoverTooltipSourceKind.VneiCrafting;

    public static bool SuppressesVanillaLateUpdate(HoverTooltipSourceKind kind) =>
        kind == HoverTooltipSourceKind.VneiCrafting;

    public static bool UsesCraftingHoverTooltipBackgroundAlpha(HoverTooltipSourceKind kind) =>
        kind == HoverTooltipSourceKind.VneiCrafting ||
        kind == HoverTooltipSourceKind.InventorySlotsCrafting;

    public static bool SuppressesEpicLootTooltipLayout(HoverTooltipSourceKind kind) =>
        kind == HoverTooltipSourceKind.VneiCrafting ||
        kind == HoverTooltipSourceKind.InventorySlotsCrafting;
}
