using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal sealed class InventoryPinnedTooltipInputMarker : MonoBehaviour
{
    public bool HoverInitialized { get; set; }
}

internal sealed class InventoryPinnedTooltipGridMarker : MonoBehaviour
{
    public int ElementListId { get; set; }
    public int ElementCount { get; set; }
    public int FirstElementId { get; set; }
    public int LastElementId { get; set; }
}

public sealed partial class InventorySlotsPlugin
{
    private readonly struct InventoryPinnedTooltipTarget
    {
        public InventoryPinnedTooltipTarget(InventoryGrid grid, Vector2i pos, ItemData item)
        {
            Grid = grid;
            Pos = pos;
            Item = item;
        }

        public InventoryGrid Grid { get; }
        public Vector2i Pos { get; }
        public ItemData Item { get; }
    }

    private static void EnsureInventoryPinnedTooltipHandlers(InventoryGrid? grid)
    {
        if (grid == null || grid.m_elements == null)
        {
            return;
        }

        InventoryPinnedTooltipGridMarker? gridMarker = GetInventoryPinnedTooltipGridMarker(grid);
        if (gridMarker != null && IsInventoryPinnedTooltipGridCurrent(grid, gridMarker))
        {
            return;
        }

        foreach (InventoryGrid.Element element in grid.m_elements)
        {
            if (IsUnityNull(element?.m_go))
            {
                continue;
            }

            UIInputHandler? input = element!.m_go.GetComponentInChildren<UIInputHandler>(includeInactive: true);
            if (input == null)
            {
                continue;
            }

            InventoryPinnedTooltipInputMarker marker = input.GetComponent<InventoryPinnedTooltipInputMarker>() ?? input.gameObject.AddComponent<InventoryPinnedTooltipInputMarker>();
            if (marker.HoverInitialized)
            {
                continue;
            }

            input.m_onPointerEnter += HandleInventoryGridTooltipPointerEnter;
            input.m_onPointerExit += HandleInventoryGridTooltipPointerExit;
            marker.HoverInitialized = true;
        }

        if (gridMarker != null)
        {
            StoreInventoryPinnedTooltipGridSignature(grid, gridMarker);
        }
    }

    private static InventoryPinnedTooltipGridMarker? GetInventoryPinnedTooltipGridMarker(InventoryGrid grid)
    {
        if (grid.m_gridRoot == null || IsUnityNull(grid.m_gridRoot))
        {
            return null;
        }

        return grid.m_gridRoot.GetComponent<InventoryPinnedTooltipGridMarker>() ??
               grid.m_gridRoot.gameObject.AddComponent<InventoryPinnedTooltipGridMarker>();
    }

    private static bool IsInventoryPinnedTooltipGridCurrent(InventoryGrid grid, InventoryPinnedTooltipGridMarker marker)
    {
        GetInventoryPinnedTooltipGridSignature(grid, out int elementListId, out int elementCount, out int firstElementId, out int lastElementId);
        return marker.ElementListId == elementListId &&
               marker.ElementCount == elementCount &&
               marker.FirstElementId == firstElementId &&
               marker.LastElementId == lastElementId;
    }

    private static void StoreInventoryPinnedTooltipGridSignature(InventoryGrid grid, InventoryPinnedTooltipGridMarker marker)
    {
        GetInventoryPinnedTooltipGridSignature(grid, out int elementListId, out int elementCount, out int firstElementId, out int lastElementId);
        marker.ElementListId = elementListId;
        marker.ElementCount = elementCount;
        marker.FirstElementId = firstElementId;
        marker.LastElementId = lastElementId;
    }

    private static void GetInventoryPinnedTooltipGridSignature(InventoryGrid grid, out int elementListId, out int elementCount, out int firstElementId, out int lastElementId)
    {
        elementListId = grid.m_elements?.GetHashCode() ?? 0;
        elementCount = grid.m_elements?.Count ?? 0;
        firstElementId = elementCount > 0 && !IsUnityNull(grid.m_elements![0]?.m_go) ? grid.m_elements[0].m_go.GetInstanceID() : 0;
        lastElementId = elementCount > 0 && !IsUnityNull(grid.m_elements![elementCount - 1]?.m_go) ? grid.m_elements[elementCount - 1].m_go.GetInstanceID() : 0;
    }

    private static void HandleInventoryGridTooltipPointerEnter(UIInputHandler handler)
    {
        if (handler == null || InventoryGui.instance == null)
        {
            return;
        }

        InventoryGui gui = InventoryGui.instance;
        if (TryGetInventoryGridItemFromHandler(gui.m_playerGrid, handler, out _, out Vector2i pos))
        {
            TooltipController.SetInventoryHover(gui.m_playerGrid, pos);
            return;
        }

        if (TryGetInventoryGridItemFromHandler(gui.m_containerGrid, handler, out _, out pos))
        {
            TooltipController.SetInventoryHover(gui.m_containerGrid, pos);
        }
    }

    private static void HandleInventoryGridTooltipPointerExit(UIInputHandler handler)
    {
        if (handler == null || InventoryGui.instance == null)
        {
            return;
        }

        InventoryGui gui = InventoryGui.instance;
        if (TryGetInventoryGridItemFromHandler(gui.m_playerGrid, handler, out _, out Vector2i pos) &&
            TooltipController.IsInventoryHover(gui.m_playerGrid, pos))
        {
            ClearInventoryPinnedTooltipHover();
            return;
        }

        if (TryGetInventoryGridItemFromHandler(gui.m_containerGrid, handler, out _, out pos) &&
            TooltipController.IsInventoryHover(gui.m_containerGrid, pos))
        {
            ClearInventoryPinnedTooltipHover();
        }
    }

    private static void ClearInventoryPinnedTooltipHover()
    {
        TooltipController.ClearInventoryHover();
    }

    private static void HandlePinnedTooltipHotkey()
    {
        if (!InventoryGui.IsVisible() ||
            InventoryGui.instance == null ||
            ShouldBlockGlobalHotkeys(Player.m_localPlayer) ||
            !IsPinnedTooltipKeyDown())
        {
            return;
        }

        InventoryGui gui = InventoryGui.instance;
        if (TryGetCraftingPinnedTooltipTargetIndex(gui, out int craftingRecipeIndex))
        {
            ToggleCraftingRecipeTooltip(gui, craftingRecipeIndex);
            ResetHoverTooltipAfterPinnedTooltipToggle();
            return;
        }

        if (TryGetInventoryPinnedTooltipHoverTarget(out InventoryGrid? grid, out Vector2i pos, out ItemData? item))
        {
            ToggleInventoryItemTooltip(gui, grid!, pos, item!);
            ResetHoverTooltipAfterPinnedTooltipToggle();
        }
    }

    private static bool TryGetCraftingPinnedTooltipTargetIndex(InventoryGui gui, out int index)
    {
        index = CraftingController.HoveredRecipeIndex;
        if (index >= 0 && TryGetCraftingRecipePair(gui, index, out _))
        {
            return true;
        }

        index = GetSelectedCraftingRecipeIndexSafe(gui);
        if (index < 0 || !TryGetCraftingRecipePair(gui, index, out _))
        {
            index = -1;
            return false;
        }

        int viewIndex = FindCraftingRecipeViewIndex(index);
        int capacity = GetCraftingRecipeGridCapacity();
        int slotIndex = viewIndex - _craftingRecipePage * capacity;
        if (viewIndex < 0 ||
            slotIndex < 0 ||
            slotIndex >= capacity ||
            slotIndex >= CraftingRecipes.GridCells.Count)
        {
            index = -1;
            return false;
        }

        CraftingRecipeGridCell cell = CraftingRecipes.GridCells[slotIndex];
        if (cell.Go == null ||
            IsUnityNull(cell.Go) ||
            !cell.Go.activeInHierarchy ||
            !RectContainsScreenPoint(cell.Rect, GetUiMousePosition()))
        {
            index = -1;
            return false;
        }

        return true;
    }

    private static void ResetHoverTooltipAfterPinnedTooltipToggle()
    {
        UITooltip? currentTooltip = UITooltip.m_current;
        GameObject? hoveredObject = UITooltip.m_hovered;
        HideInventoryOwnedHoverTooltips();
        UITooltip.HideTooltip();
        RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
        RestartInventoryContainerHoverTooltip(currentTooltip, hoveredObject);
    }

    private static bool IsPinnedTooltipKeyDown() =>
        _pinnedTooltipKey != null && IsShortcutDownAllowingAltPair(_pinnedTooltipKey.Value) ||
        IsControllerHotkeyDown(_controllerPinnedTooltipButton);

    private static bool TryGetInventoryPinnedTooltipHoverTarget(out InventoryGrid? grid, out Vector2i pos, out ItemData? item)
    {
        TooltipController.TryGetInventoryHover(out grid, out pos);
        item = null;
        if (grid == null || grid.m_inventory == null || pos.x < 0 || pos.y < 0 || IsOutOfBounds(grid.m_inventory, pos))
        {
            return false;
        }

        item = grid.m_inventory.GetItemAt(pos.x, pos.y);
        return item?.m_shared != null;
    }

    private static bool TryGetInventoryGridItemFromHandler(InventoryGrid? grid, UIInputHandler handler, out ItemData? item, out Vector2i pos)
    {
        item = null;
        pos = new Vector2i(-1, -1);
        if (grid == null || grid.m_inventory == null || handler == null)
        {
            return false;
        }

        pos = grid.GetButtonPos(handler.gameObject);
        if (pos.x < 0 || pos.y < 0 || IsOutOfBounds(grid.m_inventory, pos))
        {
            return false;
        }

        item = grid.m_inventory.GetItemAt(pos.x, pos.y);
        return item?.m_shared != null;
    }

    private static bool IsAnyAltHeld() =>
        Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
}
