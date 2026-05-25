using System;
using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void InitializeMagicSupremacyCompatibility()
    {
        _ = TryGetMagicSupremacyApi(out _);
    }

    private static bool TryAddMagicSupremacyCompatSlot(YamlSlot slot, string id)
    {
        string normalizedId = NormalizeSlotId(id);
        if (!string.Equals(normalizedId, MagicSupremacyBeltSlotId, StringComparison.Ordinal))
        {
            return false;
        }

        if (SlotDefinitions.Any(existing => existing.Id == MagicSupremacyBeltSlotId))
        {
            return true;
        }

        List<string> items = GetSlotItems(slot);
        bool hasApi = TryGetMagicSupremacyApi(out MagicSupremacyApi? api) && api != null;
        if (!hasApi && items.Count == 0)
        {
            return true;
        }

        string name = string.IsNullOrWhiteSpace(slot.Name) ? "MagicBelt" : slot.Name.Trim();
        SlotDefinitions.Add(new SlotDefinition(
            MagicSupremacyBeltSlotId,
            name,
            SlotKind.CustomEquipment,
            item => IsMagicSupremacyBeltItem(item) || items.Count > 0 && ItemMatchesSlotItems(item, items)));
        return true;
    }

    private static bool IsMagicSupremacyBeltItem(ItemData? item) =>
        item != null &&
        TryGetMagicSupremacyApi(out MagicSupremacyApi? api) &&
        api != null &&
        api.IsBeltItem(item);

    private static void SyncMagicSupremacyCompatState(Player player)
    {
        if (player == null)
        {
            return;
        }

        if (!TryGetMagicSupremacyApi(out MagicSupremacyApi? api) || api == null)
        {
            CompatRuntime.LastMagicSupremacyBeltCompatItem = null;
            return;
        }

        ItemData? current = FindCustomEquippedItem(player, IsMagicSupremacyBeltItem);
        if (!ReferenceEquals(current, CompatRuntime.LastMagicSupremacyBeltCompatItem))
        {
            if (CompatRuntime.LastMagicSupremacyBeltCompatItem != null)
            {
                api.ClearBeltIfCurrent(player, CompatRuntime.LastMagicSupremacyBeltCompatItem);
            }

            CompatRuntime.LastMagicSupremacyBeltCompatItem = current;
            if (current != null)
            {
                api.SyncBelt(player, current);
            }

            return;
        }

        if (current != null && !api.IsBeltEquipped(player, current))
        {
            api.SyncBelt(player, current);
        }
    }

    private static void OnMagicSupremacyBeltEquipped(Player player, ItemData item)
    {
        if (player == null || item == null || !IsMagicSupremacyBeltItem(item))
        {
            return;
        }

        if (TryGetMagicSupremacyApi(out MagicSupremacyApi? api) && api != null)
        {
            api.SyncBelt(player, item);
            CompatRuntime.LastMagicSupremacyBeltCompatItem = item;
        }
    }

    private static void OnMagicSupremacyBeltUnequipping(Player player, ItemData item)
    {
        if (player == null || item == null)
        {
            return;
        }

        if ((ReferenceEquals(CompatRuntime.LastMagicSupremacyBeltCompatItem, item) || IsMagicSupremacyBeltItem(item)) &&
            TryGetMagicSupremacyApi(out MagicSupremacyApi? api) &&
            api != null)
        {
            api.ClearBeltIfCurrent(player, item);
            if (ReferenceEquals(CompatRuntime.LastMagicSupremacyBeltCompatItem, item))
            {
                CompatRuntime.LastMagicSupremacyBeltCompatItem = null;
            }
        }
    }

    private static bool TryGetMagicSupremacyApi(out MagicSupremacyApi? api)
    {
        const string capability = "Magic Supremacy";
        return TryGetCompatApi(
            MagicSupremacyGuid,
            capability,
            CompatRuntime.MagicSupremacy,
            MagicSupremacyApi.TryCreate,
            "Magic Supremacy compatibility disabled",
            out api);
    }
}
