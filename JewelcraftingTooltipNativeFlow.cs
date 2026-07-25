using System;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool TryReuseJewelcraftingNativeTooltipCache(
        RectTransform root,
        JewelcraftingTooltipLayoutCache cache,
        ItemData item,
        string signature,
        out bool visible)
    {
        visible = false;
        if (JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                cache.Signature,
                signature,
                cache.Visible,
                cache.HasResolvedSocketGems,
                cache.RowlessRefreshAttempts))
        {
            return false;
        }

        UpdateJewelcraftingTooltipCacheItemIdentity(cache, item);
        RestoreCachedJewelcraftingNativeTooltip(root, cache);
        visible = cache.Visible;
        root.gameObject.SetActive(visible);
        return true;
    }

    private static bool RebuildJewelcraftingNativeTooltip(
        RectTransform root,
        ItemData tooltipItem,
        ItemData identityItem,
        bool showInteract,
        string signature,
        JewelcraftingTooltipLayoutCache cache)
    {
        try
        {
            if (!TryGetJewelcraftingTooltipApi(out JewelcraftingTooltipApi? api) || api == null)
            {
                SetJewelcraftingNativeTooltipCacheState(root, cache, signature, identityItem, visible: false, hasSocketRows: false);
                return false;
            }

            RestoreJewelcraftingSourceTooltipForNativeUpdate(root);
            api.FillItemContainerTooltip(tooltipItem, root, showInteract);
            DisableJewelcraftingNativeLayoutDrivers(root);
            ConfigureJewelcraftingTooltipRoot(root);
            ConfigureJewelcraftingNativeGemIconTooltips(root, tooltipItem);

            bool visible = HasVisibleNativeJewelcraftingTooltipContent(root);
            bool hasSocketRows = HasVisibleNativeJewelcraftingSocketRows(root);
            SetJewelcraftingNativeTooltipCacheState(root, cache, signature, identityItem, visible, hasSocketRows);
            return visible;
        }
        catch (Exception)
        {
            SetJewelcraftingNativeTooltipCacheState(root, cache, signature, identityItem, visible: false, hasSocketRows: false);
            return false;
        }
    }

    private static void SetJewelcraftingNativeTooltipCacheState(
        RectTransform root,
        JewelcraftingTooltipLayoutCache cache,
        string signature,
        ItemData item,
        bool visible,
        bool hasSocketRows)
    {
        bool sameSignature = string.Equals(cache.Signature, signature, StringComparison.Ordinal);
        cache.Signature = signature;
        cache.Visible = visible;
        cache.HasResolvedSocketGems = hasSocketRows;
        cache.RowlessRefreshAttempts = visible && !hasSocketRows
            ? sameSignature
                ? Math.Min(cache.RowlessRefreshAttempts + 1, JewelcraftingTooltipCore.MaxRowlessRefreshAttempts)
                : 1
            : 0;
        UpdateJewelcraftingTooltipCacheItemIdentity(cache, item);
        root.gameObject.SetActive(visible);
    }
}
