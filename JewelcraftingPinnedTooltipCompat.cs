using System;
using TMPro;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool UpdateInventoryPinnedJewelcraftingTooltip(RectTransform panel, ItemData item, InventoryGrid? grid)
    {
        int slot = Array.IndexOf(PinnedTooltips.Inventory.Panels, panel);
        if (slot < 0 || slot >= PinnedTooltips.Inventory.JewelcraftingTooltipRoots.Length)
        {
            return false;
        }

        return UpdateJewelcraftingTooltip(
            panel,
            item,
            ref PinnedTooltips.Inventory.JewelcraftingTooltipRoots[slot],
            ShouldShowJewelcraftingInventoryInteract(grid));
    }

    private static bool UpdateCraftingJewelcraftingTooltip(RectTransform panel, InventoryGui.RecipeDataPair pair, ref RectTransform? cachedRoot)
    {
        ItemData? item = GetCraftingJewelcraftingTooltipItem(pair);
        if (item?.m_shared == null)
        {
            if (cachedRoot != null && !IsUnityNull(cachedRoot))
            {
                cachedRoot.gameObject.SetActive(false);
            }

            return false;
        }

        return UpdateJewelcraftingTooltip(panel, item, ref cachedRoot);
    }

    private static ItemData? GetCraftingJewelcraftingTooltipItem(InventoryGui.RecipeDataPair pair) =>
        GetCraftingRecipeItemData(pair);

    private static void HideJewelcraftingTooltipRoot(ref RectTransform? root)
    {
        if (root != null && !IsUnityNull(root))
        {
            JewelcraftingTooltipLayoutCache? cache = root.GetComponent<JewelcraftingTooltipLayoutCache>();
            if (cache != null)
            {
                cache.Visible = false;
                cache.HasResolvedSocketGems = false;
                cache.RowlessRefreshAttempts = 0;
            }

            root.gameObject.SetActive(false);
        }
    }

    private static void SetInventoryPinnedTooltipTextReservedSpace(RectTransform panel, bool reserveJewelcraftingSpace)
    {
        TMP_Text? text = FindPinnedTooltipText(panel);
        RectTransform? textRect = text != null ? text.rectTransform : null;
        if (text == null || textRect == null)
        {
            return;
        }

        EnsurePinnedTooltipTextScrollContent(panel, text);
        text.overflowMode = TextOverflowModes.Overflow;
        int slot = Array.IndexOf(PinnedTooltips.Inventory.Panels, panel);
        if (slot >= 0)
        {
            ApplyPinnedTooltipDynamicTextLayout(panel, text, slot, InventoryPinnedTooltipFixedOffset, topReserved: 102f, bottomReserved: 18f, resetScroll: false);
        }
    }

    private static void SetCraftingPinnedTooltipTextReservedSpace(RectTransform panel, bool reserveGemRowSpace)
    {
        TMP_Text? text = FindPinnedTooltipText(panel);
        RectTransform? textRect = text != null ? text.rectTransform : null;
        if (text == null || textRect == null)
        {
            return;
        }

        EnsurePinnedTooltipTextScrollContent(panel, text);
        text.overflowMode = TextOverflowModes.Overflow;
        int slot = Array.IndexOf(PinnedTooltips.Crafting.Panels, panel);
        if (slot >= 0)
        {
            float bottomReserved = reserveGemRowSpace ? 124f : 92f;
            ApplyPinnedTooltipDynamicTextLayout(panel, text, slot, CraftingPinnedTooltipFixedOffset, topReserved: 102f, bottomReserved, maxTextViewportHeight: GetPinnedTooltipMaxTextViewportHeight(panel, 102f, bottomReserved), resetScroll: false);
        }
    }

    private static void RefreshJewelcraftingPinnedTooltips()
    {
        if (!HasJewelcraftingActive)
        {
            return;
        }

        bool hasActivePanel = false;
        for (int i = 0; i < PinnedTooltips.Inventory.Panels.Length; i++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[i];
            if (panel != null && !IsUnityNull(panel) && panel.gameObject.activeInHierarchy && PinnedTooltips.Inventory.Items[i]?.m_shared != null)
            {
                hasActivePanel = true;
                break;
            }
        }

        if (!hasActivePanel)
        {
            for (int i = 0; i < PinnedTooltips.Crafting.Panels.Length; i++)
            {
                RectTransform? panel = PinnedTooltips.Crafting.Panels[i];
                if (panel != null &&
                    !IsUnityNull(panel) &&
                    panel.gameObject.activeInHierarchy &&
                    PinnedTooltips.Crafting.RecipeIndices[i] >= 0)
                {
                    hasActivePanel = true;
                    break;
                }
            }
        }

        if (!hasActivePanel)
        {
            return;
        }

        for (int i = 0; i < PinnedTooltips.Inventory.Panels.Length; i++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[i];
            ItemData? item = GetLiveInventoryPinnedTooltipItem(i);
            if (panel == null || IsUnityNull(panel) || !panel.gameObject.activeInHierarchy || item?.m_shared == null)
            {
                continue;
            }

            bool hasJewelcraftingTooltip = UpdateInventoryPinnedJewelcraftingTooltip(panel, item, PinnedTooltips.Inventory.Grids[i]);
            SetInventoryPinnedTooltipTextReservedSpace(panel, hasJewelcraftingTooltip);
        }

        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || IsUnityNull(gui))
        {
            return;
        }

        for (int i = 0; i < PinnedTooltips.Crafting.Panels.Length; i++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[i];
            int index = PinnedTooltips.Crafting.RecipeIndices[i];
            if (panel == null ||
                IsUnityNull(panel) ||
                !panel.gameObject.activeInHierarchy ||
                index < 0 ||
                !TryGetCraftingRecipePair(gui, index, out InventoryGui.RecipeDataPair pair))
            {
                continue;
            }

            bool hasJewelcraftingTooltip = UpdateCraftingJewelcraftingTooltip(panel, pair, ref PinnedTooltips.Crafting.JewelcraftingTooltipRoots[i]);
            bool hasGemRow = !hasJewelcraftingTooltip &&
                             PinnedTooltips.Crafting.GemIconRows[i] != null &&
                             !IsUnityNull(PinnedTooltips.Crafting.GemIconRows[i]) &&
                             PinnedTooltips.Crafting.GemIconRows[i]!.gameObject.activeSelf;
            SetCraftingPinnedTooltipTextReservedSpace(panel, hasGemRow);
        }
    }

    private static ItemData? GetLiveInventoryPinnedTooltipItem(int slot)
    {
        ItemData? item = slot >= 0 && slot < PinnedTooltips.Inventory.Items.Length ? PinnedTooltips.Inventory.Items[slot] : null;
        if (slot < 0 || slot >= PinnedTooltips.Inventory.Grids.Length)
        {
            return item;
        }

        InventoryGrid? grid = PinnedTooltips.Inventory.Grids[slot];
        if (grid == null || IsUnityNull(grid) || grid.m_inventory == null)
        {
            return item;
        }

        Vector2i pos = PinnedTooltips.Inventory.Positions[slot];
        ItemData? liveItem = grid.m_inventory.GetItemAt(pos.x, pos.y);
        if (liveItem?.m_shared == null)
        {
            return item;
        }

        if (!CanUseLiveInventoryPinnedTooltipItem(item, liveItem))
        {
            return item;
        }

        if (!ReferenceEquals(item, liveItem))
        {
            PinnedTooltips.Inventory.Items[slot] = liveItem;
        }

        return liveItem;
    }

    private static bool CanUseLiveInventoryPinnedTooltipItem(ItemData? pinnedItem, ItemData liveItem)
    {
        if (pinnedItem?.m_shared == null)
        {
            return liveItem.m_shared != null;
        }

        if (ReferenceEquals(pinnedItem, liveItem))
        {
            return true;
        }

        if (liveItem.m_shared == null ||
            !string.Equals(GetItemPrefabName(pinnedItem), GetItemPrefabName(liveItem), StringComparison.Ordinal) ||
            !string.Equals(pinnedItem.m_shared.m_name, liveItem.m_shared.m_name, StringComparison.Ordinal) ||
            pinnedItem.m_variant != liveItem.m_variant)
        {
            return false;
        }

        return !HasJewelcraftingPinnedTooltipPotential(pinnedItem) || HasJewelcraftingPinnedTooltipPotential(liveItem);
    }

    private static bool HasJewelcraftingPinnedTooltipPotential(ItemData item)
    {
        if (TryGetJewelcraftingGemSlotCount(item, out int slotCount) && slotCount > 0)
        {
            return true;
        }

        return GetJewelcraftingSocketPrefabNamesFromCustomData(item).Count > 0 ||
               HasJewelcraftingPotentialCustomData(item);
    }

    private static bool IsJewelcraftingAdvancedTooltipPressed()
    {
        if (TryGetJewelcraftingTooltipApi(out JewelcraftingTooltipApi? api) &&
            api != null &&
            api.TryIsAdvancedTooltipPressed(out bool pressed))
        {
            return pressed;
        }

        return IsAnyAltHeld();
    }

    private static bool IsJewelcraftingProphecyTooltipPressed()
    {
        try
        {
            return ZInput.GetButton("Run");
        }
        catch
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }

    private static bool ShouldShowJewelcraftingInventoryInteract(InventoryGrid? grid)
    {
        InventoryGui? gui = InventoryGui.instance;
        return grid != null &&
               !IsUnityNull(grid) &&
               gui?.m_playerGrid != null &&
               !IsUnityNull(gui.m_playerGrid) &&
               grid == gui.m_playerGrid;
    }
}
