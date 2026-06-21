namespace InventoryActions;

internal static class InventoryActionCellPolicyCore
{
    internal static bool CanFavoriteSlot(InventoryCellKind kind) =>
        kind is InventoryCellKind.Hotbar or InventoryCellKind.RegularUnlocked or InventoryCellKind.Quick;

    internal static bool CanUseContainerActionSource(InventoryCellKind kind, bool includeHotbar) =>
        kind == InventoryCellKind.RegularUnlocked || includeHotbar && kind == InventoryCellKind.Hotbar;

    internal static bool CanUseFavoriteRestockTarget(InventoryCellKind kind) =>
        kind is InventoryCellKind.RegularUnlocked or InventoryCellKind.Hotbar or InventoryCellKind.Quick;

    internal static bool CanTrashSlot(InventoryCellKind kind) =>
        kind == InventoryCellKind.RegularUnlocked;
}
