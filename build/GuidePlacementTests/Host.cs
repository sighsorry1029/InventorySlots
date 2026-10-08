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
        public static Vector2 QuickSlotsPanelRuntimeOffset = new(-80, -552);
        public static Vector3 QuickSlotHudAnchoredPosition;
        public static float QuickSlotHudElementSpace;
        public static bool QuickSlotHudAnchorValid;
    }
    private const int QuickSlotPanelColumns = 3;
    private static Vector3 GetGridOrigin(InventoryGrid grid) => grid.Origin;
    private static Vector3 GetSidePanelBasePosition(Vector3 origin, int width, float space) => origin + new Vector3(width * space, 0, 0);
    private static Vector3 GetQuickSlotHudPosition() => InventoryPanels.QuickSlotHudAnchoredPosition;
    private static float GetQuickSlotHudElementSpace() => InventoryPanels.QuickSlotHudElementSpace;
    public static void SavedHud(Vector3 position, float space = 70)
    {
        InventoryPanels.QuickSlotHudAnchoredPosition = position;
        InventoryPanels.QuickSlotHudElementSpace = space;
        InventoryPanels.QuickSlotHudAnchorValid = true;
        InventoryPanels.QuickSlotsPanelRuntimeOffset = new(-80, -552);
    }
    public static Vector3 HudPosition(RectTransform hud, InventoryGrid? grid, int rows, bool follows, out float space) =>
        ResolveQuickSlotHudPosition(hud, grid, 8, rows, follows, out space);
    public static Vector3 ClampHud(Rect viewport, Vector3 position, float space, int rows) =>
        ClampQuickSlotHudPosition(viewport, position, space, rows);
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
