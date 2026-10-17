namespace InventorySlots;

internal enum CraftingTabAdapterKind
{
    None,
    Vanilla,
    JewelcraftingSocket,
    RecycleNReclaim,
    Foreign,
    PathOfValheimanSalvage
}

internal readonly struct CraftingTabAdapterState
{
    public CraftingTabAdapterState(CraftingTabAdapterKind kind)
    {
        Kind = kind;
    }

    public CraftingTabAdapterKind Kind { get; }
    public bool IsRedesign => Kind is CraftingTabAdapterKind.Vanilla or CraftingTabAdapterKind.JewelcraftingSocket or CraftingTabAdapterKind.RecycleNReclaim or CraftingTabAdapterKind.PathOfValheimanSalvage;
    public bool IsPathOfValheimanSalvage => Kind == CraftingTabAdapterKind.PathOfValheimanSalvage;
    public bool IsForeign => Kind == CraftingTabAdapterKind.Foreign;
    public bool IsJewelcraftingSocket => Kind == CraftingTabAdapterKind.JewelcraftingSocket;
    public bool IsRecycleNReclaim => Kind == CraftingTabAdapterKind.RecycleNReclaim;
    public bool UsesDefaultGroupRail => Kind != CraftingTabAdapterKind.RecycleNReclaim && !IsPathOfValheimanSalvage;
    public bool UsesRecycleNReclaimBottomControls => Kind == CraftingTabAdapterKind.RecycleNReclaim;
}
