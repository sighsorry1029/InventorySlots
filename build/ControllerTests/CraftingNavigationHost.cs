#if INVENTORY_SLOTS
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool _craftingRedesignApplied, _craftingListViewActive;
    private static Transform? _craftingRecipeGrid;
    private static int _craftingListRevealedSelection;
    public static bool TestForeignCrafting, TestSelectedIndexAllowsUpgrade, TestCenterRecipe;
    public static int TestRecipeSelected, TestRecipeSelections, TestRecipeColumns, TestRecipePage;
    public static int[]? TestPendingRecipeView;
    private readonly record struct TestRecipeEntry(int OriginalIndex);
    private static class CraftingRecipes { internal static readonly List<TestRecipeEntry> View = new(); }
    private static class CraftingController { internal static void ClearHoveredRecipeAndRequestMouseSync() { } }
    private static bool ShouldShowCraftingPanelRedesign(InventoryGui gui) =>
        !IsDedicatedServer && !TestForeignCrafting && gui.m_crafting != null && gui.m_crafting.gameObject.activeInHierarchy;
    private static bool UpdateCraftingRecipeView(InventoryGui gui)
    {
        if (TestPendingRecipeView == null) return false;
        CraftingRecipes.View.Clear();
        foreach (int index in TestPendingRecipeView) CraftingRecipes.View.Add(new TestRecipeEntry(index));
        TestPendingRecipeView = null;
        return true;
    }
    private static int FindCraftingRecipeViewIndex(int original) => CraftingRecipes.View.FindIndex(e => e.OriginalIndex == original);
    private static int GetSelectedCraftingRecipeIndexSafe(InventoryGui gui, bool acceptOneLevelHigher = true)
    {
        TestSelectedIndexAllowsUpgrade = acceptOneLevelHigher;
        return TestRecipeSelected;
    }
    private static int GetCraftingRecipeGridDimension() => _craftingListViewActive ? 1 : TestRecipeColumns;
    private static void SetCraftingRecipeWithStoredVariant(InventoryGui gui, int index, bool center)
    {
        TestRecipeSelected = index;
        TestCenterRecipe = center;
        TestRecipeSelections++;
    }
    private static void SyncCraftingRecipePageToSelected(InventoryGui gui)
    {
        if (_craftingListViewActive && _craftingListRevealedSelection == TestRecipeSelected) return;
        _craftingListRevealedSelection = TestRecipeSelected;
        TestRecipePage = CraftingViewCore.RevealSelection(_craftingListViewActive, TestRecipePage,
            FindCraftingRecipeViewIndex(TestRecipeSelected), CraftingRecipes.View.Count,
            _craftingListViewActive ? CraftingViewCore.ListRows : TestRecipeColumns * TestRecipeColumns);
    }
    public static InventoryGui TestCraftingReset(bool list, params int[] displayedOriginals)
    {
        InventoryGui gui = TestReset();
        gui.m_playerGrid.m_uiGroup.IsActive = false;
        gui.ContainerGrid.m_uiGroup.IsActive = false;
        gui.ActiveGroup = 3;
        gui.m_crafting = new GameObject().transform;
        _craftingRecipeGrid = new GameObject().transform;
        _craftingRecipeGrid.parent = gui.m_crafting;
        _craftingRedesignApplied = true;
        _craftingListViewActive = list;
        TestForeignCrafting = TestCenterRecipe = false;
        TestSelectedIndexAllowsUpgrade = true;
        TestRecipeColumns = 3;
        TestRecipeSelections = TestRecipePage = 0;
        TestRecipeSelected = displayedOriginals.Length == 0 ? -1 : displayedOriginals[0];
        TestPendingRecipeView = displayedOriginals;
        UpdateCraftingRecipeView(gui);
        return gui;
    }
    public static bool TestRecipeOwnsDPad => IsCraftingRecipeGamepadFocused(InventoryGui.instance);
    public static void TestHideRecipeGrid() => _craftingRecipeGrid!.gameObject.activeInHierarchy = false;
    public static void TestReserveRecipeInput() => _controllerReservedFrame = Time.frameCount;
}
#endif
