using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static RectTransform? EnsureCraftingRecipeGrid(InventoryGui gui, bool syncCells = true)
    {
        if (_craftingRecipeGrid != null && !IsUnityNull(_craftingRecipeGrid) && _craftingRecipeGrid!.parent == gui.m_crafting)
        {
            ConfigureCraftingRecipeGrid(_craftingRecipeGrid);
            _craftingRecipeGrid.SetAsLastSibling();
            if (syncCells || _craftingRecipeGridCellCapacity != GetCraftingRecipeGridCapacity())
            {
                EnsureCraftingRecipeCells(gui, _craftingRecipeGrid);
            }

            _craftingRecipeGrid.gameObject.SetActive(true);
            return _craftingRecipeGrid;
        }

        Transform? existing = gui.m_crafting.Find(CraftingRecipeGridName);
        _craftingRecipeGrid = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (_craftingRecipeGrid == null)
        {
            _craftingRecipeGrid = new GameObject(CraftingRecipeGridName, typeof(RectTransform)).GetComponent<RectTransform>();
            _craftingRecipeGrid.SetParent(gui.m_crafting, false);
        }

        _craftingRecipeGridLayoutSignature = "";
        ConfigureCraftingRecipeGrid(_craftingRecipeGrid);
        EnsureCraftingRecipeCells(gui, _craftingRecipeGrid);
        _craftingRecipeGrid.gameObject.SetActive(true);
        return _craftingRecipeGrid;
    }

    private static void ConfigureCraftingRecipeGrid(RectTransform grid)
    {
        Vector2 position = CraftingRecipeGridFixedOffset;
        Vector2 size = new(CraftingRecipeGridColumns * CraftingRecipeGridCellSpace, CraftingRecipeGridRows * CraftingRecipeGridCellSpace);
        string signature = $"{position}|{size}";
        if (string.Equals(_craftingRecipeGridLayoutSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        grid.anchorMin = new Vector2(0f, 1f);
        grid.anchorMax = new Vector2(0f, 1f);
        grid.pivot = new Vector2(0f, 1f);
        grid.localScale = Vector3.one;
        grid.localRotation = Quaternion.identity;
        grid.anchoredPosition = position;
        grid.sizeDelta = size;
        grid.SetAsLastSibling();
        _craftingRecipeGridLayoutSignature = signature;
    }

    private static int GetCraftingRecipeGridDimension() =>
        Mathf.Clamp(_craftingRecipeGridSize?.Value ?? CraftingRecipeGridMaxDimension, CraftingRecipeGridMinDimension, CraftingRecipeGridMaxDimension);

    private static int GetCraftingRecipeGridCapacity()
    {
        int dimension = GetCraftingRecipeGridDimension();
        return dimension * dimension;
    }

    private static float GetCraftingRecipeIconAreaSize() =>
        CraftingRecipeGridColumns * CraftingRecipeGridCellSpace;

    private static float GetCraftingRecipeDynamicCellSpace()
    {
        int dimension = GetCraftingRecipeGridDimension();
        return GetCraftingRecipeIconAreaSize() / dimension;
    }

    private static float GetCraftingRecipeDynamicCellSize(float cellSpace) =>
        Mathf.Max(24f, cellSpace - 8f);

    private static void EnsureCraftingRecipeCells(InventoryGui gui, RectTransform grid)
    {
        HideForeignCraftingGridChildren(grid);
        int capacity = GetCraftingRecipeGridCapacity();
        _craftingRecipeGridCellCapacity = capacity;

        for (int i = CraftingRecipes.GridCells.Count - 1; i >= 0; i--)
        {
            CraftingRecipeGridCell cell = CraftingRecipes.GridCells[i];
            if (cell.Rect == null || IsUnityNull(cell.Rect) || cell.Rect.parent != grid)
            {
                CraftingRecipes.GridCells.RemoveAt(i);
            }
        }

        for (int i = CraftingRecipes.GridCells.Count; i < capacity; i++)
        {
            CraftingRecipes.GridCells.Add(CreateCraftingRecipeCell(gui, grid, i));
        }

        while (CraftingRecipes.GridCells.Count > capacity)
        {
            int lastIndex = CraftingRecipes.GridCells.Count - 1;
            CraftingRecipeGridCell cell = CraftingRecipes.GridCells[lastIndex];
            CraftingRecipes.GridCells.RemoveAt(lastIndex);
            if (cell.Go != null && !IsUnityNull(cell.Go))
            {
                UnityEngine.Object.Destroy(cell.Go);
            }
        }
    }

    private static void HideForeignCraftingGridChildren(RectTransform grid)
    {
        for (int i = 0; i < grid.childCount; i++)
        {
            Transform child = grid.GetChild(i);
            if (!child.name.StartsWith(CraftingRecipeGridCellNamePrefix, StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static CraftingRecipeGridCell CreateCraftingRecipeCell(InventoryGui gui, RectTransform grid, int slotIndex)
    {
        GameObject go = CreateCleanCraftingRecipeCell(grid);
        go.name = CraftingRecipeGridCellNamePrefix + slotIndex;
        go.SetActive(false);
        ApplyDefaultFontAssetToChildren(go);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Button? button = go.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }

        go.SetActive(true);
        return new CraftingRecipeGridCell(go, slotIndex);
    }

    private static GameObject CreateCleanCraftingRecipeCell(RectTransform parent)
    {
        GameObject go = new(CraftingRecipeGridCellNamePrefix + "clean", typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(UITooltip));
        go.transform.SetParent(parent, false);

        Image background = go.GetComponent<Image>();
        background.sprite = GetSolidUiSprite();
        background.color = new Color(0.055f, 0.035f, 0.025f, 0.42f);
        background.raycastTarget = true;

        CreateCleanCraftingOverlay("equiped", go.transform, new Color(0.42f, 0.68f, 0.92f, 0.34f), active: false);
        CreateCleanCraftingOverlay("queued", go.transform, new Color(1f, 0.68f, 0.18f, 0.28f), active: false);
        CreateCleanCraftingOverlay("selected", go.transform, new Color(0.42f, 0.68f, 0.92f, 0f), active: true);
        CreateCleanCraftingOverlay("noteleport", go.transform, new Color(0.9f, 0.2f, 0.2f, 0.45f), active: false);
        CreateCleanCraftingOverlay("foodicon", go.transform, new Color(0.2f, 0.85f, 0.35f, 0.35f), active: false);
        CreateCleanCraftingOverlay("durability", go.transform, new Color(0.08f, 0.02f, 0.02f, 0.55f), active: false);

        RectTransform icon = CreateTopLeftImageChild("icon", go.transform, Color.white, active: true);
        icon.GetComponent<Image>().raycastTarget = false;

        CreateTextRect("amount", go.transform);

        CreateTextRect("quality", go.transform);

        return go;
    }

    private static RectTransform CreateCleanCraftingOverlay(string name, Transform parent, Color color, bool active)
    {
        RectTransform rect = CreateTopLeftImageChild(name, parent, color, active);
        Image image = rect.GetComponent<Image>();
        image.sprite = GetSolidUiSprite();
        image.raycastTarget = false;
        return rect;
    }

    private static void UpdateCraftingGridLayering(InventoryGui gui, RectTransform grid)
    {
        if (_craftingControlsBackground == null || IsUnityNull(_craftingControlsBackground))
        {
            Transform? existing = gui.m_crafting.Find(CraftingControlsBackgroundName);
            _craftingControlsBackground = existing != null ? existing.GetComponent<RectTransform>() : null;
        }

        if (_craftingControlsBackground != null && !IsUnityNull(_craftingControlsBackground))
        {
            _craftingControlsBackground.gameObject.SetActive(false);
        }
    }
}
