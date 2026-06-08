using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void PrepareCraftingTabAdapterPreflight(InventoryGui gui)
    {
        EnsureJewelcraftingCraftingPanelVisible(gui);
    }

    private static CraftingTabAdapterState GetCraftingTabAdapterState(InventoryGui? gui)
    {
        if (gui == null ||
            IsUnityNull(gui) ||
            gui.m_crafting == null ||
            IsUnityNull(gui.m_crafting) ||
            !gui.m_crafting.gameObject.activeInHierarchy)
        {
            return new CraftingTabAdapterState(CraftingTabAdapterKind.None);
        }

        if (IsJewelcraftingCraftingRedesignTab(gui))
        {
            return new CraftingTabAdapterState(CraftingTabAdapterKind.JewelcraftingSocket);
        }

        if (IsRecycleNReclaimReclaimTabActive(gui))
        {
            return new CraftingTabAdapterState(CraftingTabAdapterKind.RecycleNReclaim);
        }

        if (HasActiveForeignCraftingTab(gui) || !gui.InCraftTab() && !gui.InUpradeTab())
        {
            return new CraftingTabAdapterState(CraftingTabAdapterKind.Foreign);
        }

        return new CraftingTabAdapterState(CraftingTabAdapterKind.Vanilla);
    }

    private static void UpdateCraftingTabAdapterSuppression(InventoryGui gui, bool shouldSuppress, CraftingTabAdapterState adapter)
    {
        UpdateMyLittleUICraftingObjectSuppression(gui, shouldSuppress, adapter);
    }

    private static void ApplyCraftingTabAdapterVisibility(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        if (adapter.IsJewelcraftingSocket)
        {
            ApplyJewelcraftingCraftingRedesignVisibility(gui);
            return;
        }

        if (adapter.IsRecycleNReclaim)
        {
            ApplyRecycleNReclaimCraftingRedesignVisibility(gui);
            return;
        }

        SetCraftingVanillaDetailVisible(gui, visible: false);
        _craftingVanillaDetailHidden = true;
        SuppressJewelcraftingCraftingSocketUiForRedesign(gui, includeSocketTab: adapter.IsJewelcraftingSocket);
        EnsureCraftingVanillaPanelBackgroundsHidden(gui);
    }

    private static void SuppressCraftingTabAdapterFrameResidue(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        if (!adapter.IsRedesign)
        {
            return;
        }

        SetCraftingVanillaDetailVisible(gui, visible: false);
        _craftingVanillaDetailHidden = true;
    }

    private static void LayoutCraftingTabAdapterBottomControls(InventoryGui gui, RectTransform grid, CraftingTabAdapterState adapter)
    {
        if (adapter.UsesRecycleNReclaimBottomControls)
        {
            LayoutRecycleNReclaimBottomControls(gui, grid);
            return;
        }

        LayoutCraftingBottomControls(gui, grid);
    }

    private static void FinalizeCraftingTabAdapterFrame(
        InventoryGui gui,
        CraftingTabAdapterState adapter,
        bool firstApply,
        CraftingPanelUpdateReason reason,
        bool viewChanged)
    {
        if (ShouldFinalizeJewelcraftingCraftingSocketSuppression(adapter.IsJewelcraftingSocket, firstApply, reason, viewChanged))
        {
            SuppressJewelcraftingCraftingSocketUiForRedesign(gui);
        }

        if (adapter.IsJewelcraftingSocket)
        {
            SuppressJewelcraftingCraftingSocketUiForRedesign(gui, includeSocketTab: true);
        }
    }

    private static bool ShouldIncludeRecipeInCraftingTabView(CraftingTabAdapterState adapter, InventoryGui.RecipeDataPair pair)
    {
        return adapter.Kind switch
        {
            CraftingTabAdapterKind.JewelcraftingSocket => pair.ItemData != null,
            CraftingTabAdapterKind.RecycleNReclaim => ShouldIncludeRecycleNReclaimRecipeInView(pair),
            CraftingTabAdapterKind.Vanilla => RecipeMatchesSelectedCraftingGroup(pair) && RecipeMatchesCraftingSearch(pair),
            _ => false
        };
    }

    private static int CompareCraftingRecipeViewEntriesForAdapter(CraftingTabAdapterState adapter, CraftingRecipeViewEntry a, CraftingRecipeViewEntry b)
    {
        if (adapter.IsRecycleNReclaim)
        {
            return CompareRecycleNReclaimRecipeViewEntries(a, b);
        }

        int veiledRecipeGrouping = CompareVeiledRecipeMaskGrouping(a.IsVeiledRecipePreview, b.IsVeiledRecipePreview);
        if (veiledRecipeGrouping != 0)
        {
            return veiledRecipeGrouping;
        }

        CraftingRecipeSortMode mode = _craftingRecipeSortMode?.Value ?? CraftingRecipeSortMode.TierThenGroup;
        return CraftingRecipeViewCore.CompareWithSortKey(
            a.IsFavorite,
            b.IsFavorite,
            a.Pair.CanCraft,
            b.Pair.CanCraft,
            a.SortKey,
            b.SortKey,
            a.OriginalIndex,
            b.OriginalIndex,
            mode);
    }

    private static bool IsCraftingRecipeActionAvailable(InventoryGui? gui, InventoryGui.RecipeDataPair pair)
    {
        return IsCraftingRecipeActionAvailable(gui, pair, originalIndex: -1);
    }

    private static bool IsCraftingRecipeActionAvailable(InventoryGui? gui, InventoryGui.RecipeDataPair pair, int originalIndex)
    {
        if (IsVeiledRecipeMasked(pair))
        {
            return false;
        }

        CraftingTabAdapterState adapter = GetCraftingTabAdapterState(gui);
        return adapter.Kind switch
        {
            CraftingTabAdapterKind.JewelcraftingSocket => CanAttemptJewelcraftingSocket(pair),
            CraftingTabAdapterKind.RecycleNReclaim => TryGetRecycleNReclaimRecipeActionAvailable(originalIndex, pair, out bool available)
                ? available
                : pair.CanCraft,
            _ => pair.CanCraft
        };
    }

    private static string GetCraftingRecipeDisplayName(InventoryGui.RecipeDataPair pair)
    {
        if (pair.Recipe == null || pair.Recipe.m_item == null)
        {
            return "";
        }

        if (IsVeiledRecipeMasked(pair))
        {
            return GetVeiledRecipeUnknownNameText();
        }

        CraftingTabAdapterState adapter = GetCraftingTabAdapterState(InventoryGui.instance);
        if (adapter.IsRecycleNReclaim && pair.ItemData != null)
        {
            return GetRecycleNReclaimRecipeDisplayName(pair);
        }

        string text = Localization.instance.Localize(pair.Recipe.m_item.m_itemData.m_shared.m_name);
        if (pair.Recipe.m_amount > 1)
        {
            text += $" x{pair.Recipe.m_amount}";
        }

        return text;
    }

    private static string GetCraftingRecipeTooltip(InventoryGui.RecipeDataPair pair)
    {
        if (pair.Recipe == null || pair.Recipe.m_item == null)
        {
            return "";
        }

        if (IsVeiledRecipeMasked(pair))
        {
            return GetVeiledRecipeUnknownDescriptionText();
        }

        if (pair.ItemData != null)
        {
            CraftingTabAdapterState adapter = GetCraftingTabAdapterState(InventoryGui.instance);
            if (adapter.IsRecycleNReclaim)
            {
                return GetRecycleNReclaimRecipeTooltip(pair);
            }

            if (adapter.IsJewelcraftingSocket)
            {
                return GetSocketRecipeItemTooltip(pair);
            }

            return GetUpgradeRecipeComparisonTooltip(pair);
        }

        int quality = 1;
        int amount = pair.Recipe.m_amount;
        string tooltip = GetLocalizedStaticItemTooltip(pair.Recipe.m_item.m_itemData, quality, crafting: true, amount);
        return AppendItemRequiresSkillLevelCraftingTooltip(pair, tooltip);
    }
}
