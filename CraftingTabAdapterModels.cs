namespace InventorySlots;

internal enum CraftingTabAdapterKind
{
    None,
    Vanilla,
    JewelcraftingSocket,
    RecycleNReclaim,
    Foreign
}

internal readonly struct CraftingTabAdapterState
{
    public CraftingTabAdapterState(CraftingTabAdapterKind kind)
    {
        Kind = kind;
    }

    public CraftingTabAdapterKind Kind { get; }
    public bool IsRedesign => Kind is CraftingTabAdapterKind.Vanilla or CraftingTabAdapterKind.JewelcraftingSocket or CraftingTabAdapterKind.RecycleNReclaim;
    public bool IsForeign => Kind == CraftingTabAdapterKind.Foreign;
    public bool IsJewelcraftingSocket => Kind == CraftingTabAdapterKind.JewelcraftingSocket;
    public bool IsRecycleNReclaim => Kind == CraftingTabAdapterKind.RecycleNReclaim;
    public bool UsesDefaultGroupRail => Kind != CraftingTabAdapterKind.RecycleNReclaim;
    public bool UsesRecycleNReclaimBottomControls => Kind == CraftingTabAdapterKind.RecycleNReclaim;
}
