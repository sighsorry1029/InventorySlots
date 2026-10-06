using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private const float FeatureGuideGap = 12;
    public static bool Settled(Animator? animator) => IsInventoryPanelAnimationSettled(animator);
#if INVENTORY_SLOTS
    private static class InventoryPanels
    {
        public static readonly Dictionary<int, RectTransform> CustomSlotPanels = new();
        public static readonly Dictionary<int, RectTransform> QuickSlotPanels = new();
        public static readonly List<MovedPlayerStatPanel> MovedPlayerStatPanels = new();
    }
    private readonly record struct MovedPlayerStatPanel(RectTransform Rect);
    private static RectTransform? _craftingGroupRail;
    public static void ExtraPanels(RectTransform equipment, RectTransform quick, RectTransform rail)
    {
        InventoryPanels.CustomSlotPanels.Clear();
        InventoryPanels.QuickSlotPanels.Clear();
        InventoryPanels.CustomSlotPanels.Add(1, equipment);
        InventoryPanels.QuickSlotPanels.Add(1, quick);
        _craftingGroupRail = rail;
    }
#endif
    public static Rect Area(RectTransform hud)
    {
        if (!TryGetFeatureGuideArea(hud, out Rect area)) throw new System.Exception("Guide area missing");
        return area;
    }
    public static void Awake(InventoryGui gui) => typeof(FeatureGuideLayoutAwakePatch)
        .GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { gui });
    public static void Destroy(InventoryGui gui) => typeof(FeatureGuideLayoutDestroyPatch)
        .GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { gui });
}
