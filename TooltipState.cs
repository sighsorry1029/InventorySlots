using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class InventoryPinnedTooltipRuntimeState
    {
        public readonly RectTransform?[] Panels = new RectTransform?[PinnedTooltipSlotCount];
        public readonly Image?[] Icons = new Image?[PinnedTooltipSlotCount];
        public readonly TMP_Text?[] Texts = new TMP_Text?[PinnedTooltipSlotCount];
        public readonly RectTransform?[] JewelcraftingTooltipRoots = new RectTransform?[PinnedTooltipSlotCount];
        public readonly ItemData?[] Items = new ItemData?[PinnedTooltipSlotCount];
        public readonly InventoryGrid?[] Grids = new InventoryGrid?[PinnedTooltipSlotCount];
        public readonly Vector2i[] Positions = Enumerable.Repeat(new Vector2i(-1, -1), PinnedTooltipSlotCount).ToArray();
        public InventoryGrid? HoveredGrid;
        public Vector2i HoveredPos = new(-1, -1);
    }

    private sealed class CraftingPinnedTooltipRuntimeState
    {
        public readonly RectTransform?[] Panels = new RectTransform?[PinnedTooltipSlotCount];
        public readonly Image?[] Icons = new Image?[PinnedTooltipSlotCount];
        public readonly TMP_Text?[] Texts = new TMP_Text?[PinnedTooltipSlotCount];
        public readonly RectTransform?[] JewelcraftingTooltipRoots = new RectTransform?[PinnedTooltipSlotCount];
        public readonly RectTransform?[] GemIconRows = new RectTransform?[PinnedTooltipSlotCount];
        public readonly int[] RecipeIndices = Enumerable.Repeat(-1, PinnedTooltipSlotCount).ToArray();
    }

    private sealed class PinnedTooltipRuntimeState
    {
        public readonly InventoryPinnedTooltipRuntimeState Inventory = new();
        public readonly CraftingPinnedTooltipRuntimeState Crafting = new();
        public PinnedTooltipContext ActiveContext = PinnedTooltipContext.None;
    }

    private sealed class TooltipUiRuntimeState
    {
        public RectTransform? HotbarSwitchHudHint;
        public TMP_Text? HotbarSwitchHudHintText;
        public RectTransform? InventoryWheelHint;
        public TMP_Text? InventoryWheelHintText;
        public TMP_FontAsset? DefaultFontAsset;
        public Material? DefaultFontMaterial;
        public Sprite? SolidUiSprite;
        public Sprite? MouseWheelHintSprite;
    }

    private static class TooltipController
    {
        public static void SetPinnedContext(PinnedTooltipContext context)
        {
            if (PinnedTooltips.ActiveContext == context)
            {
                return;
            }

            PinnedTooltipContext previous = PinnedTooltips.ActiveContext;
            PinnedTooltips.ActiveContext = context;
            if (previous == PinnedTooltipContext.InventoryContainer)
            {
                HideInventoryPinnedTooltips();
            }
            else if (previous == PinnedTooltipContext.CraftingCraft || previous == PinnedTooltipContext.CraftingUpgrade)
            {
                HideCraftingPinnedTooltips();
            }
        }

        public static void SyncCraftingPinnedContext(PinnedTooltipContext current)
        {
            if (PinnedTooltips.ActiveContext != PinnedTooltipContext.CraftingCraft &&
                PinnedTooltips.ActiveContext != PinnedTooltipContext.CraftingUpgrade)
            {
                return;
            }

            if (PinnedTooltips.ActiveContext == current)
            {
                return;
            }

            HideCraftingPinnedTooltips();
            PinnedTooltips.ActiveContext = current;
        }

        public static bool IsCraftingPinnedContext() =>
            PinnedTooltips.ActiveContext == PinnedTooltipContext.CraftingCraft ||
            PinnedTooltips.ActiveContext == PinnedTooltipContext.CraftingUpgrade;

        public static bool IsInventoryPinnedContext() =>
            PinnedTooltips.ActiveContext == PinnedTooltipContext.InventoryContainer;

        public static void SetInventoryHover(InventoryGrid grid, Vector2i pos)
        {
            PinnedTooltips.Inventory.HoveredGrid = grid;
            PinnedTooltips.Inventory.HoveredPos = pos;
        }

        public static void ClearInventoryHover()
        {
            PinnedTooltips.Inventory.HoveredGrid = null;
            PinnedTooltips.Inventory.HoveredPos = new Vector2i(-1, -1);
        }

        public static bool IsInventoryHover(InventoryGrid grid, Vector2i pos) =>
            PinnedTooltips.Inventory.HoveredGrid == grid &&
            PinnedTooltips.Inventory.HoveredPos == pos;

        public static bool TryGetInventoryHover(out InventoryGrid? grid, out Vector2i pos)
        {
            grid = PinnedTooltips.Inventory.HoveredGrid;
            pos = PinnedTooltips.Inventory.HoveredPos;
            return grid != null && pos.x >= 0 && pos.y >= 0;
        }
    }

    private static readonly PinnedTooltipRuntimeState PinnedTooltips = new();
    private static readonly TooltipUiRuntimeState TooltipUi = new();
}
