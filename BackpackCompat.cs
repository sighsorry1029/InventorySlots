using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void InitializeBackpackCompatibility()
    {
        if (!CompatRuntime.AdventureBackpacksPatchesApplied &&
            TryGetAdventureBackpacksApi(out AdventureBackpacksApi? adventureApi) &&
            adventureApi != null)
        {
            adventureApi.ApplyPatches(_instance._harmony);
            CompatRuntime.AdventureBackpacksPatchesApplied = true;
        }

        _ = TryGetSmoothbrainBackpacksApi(out _);
        _ = TryGetRustyBagsApi(out _);
    }

    private static bool TryAddBackpackCompatSlot(YamlSlot slot, string id)
    {
        string normalizedId = NormalizeSlotId(id);
        return normalizedId switch
        {
            AdventureBackpackSlotId => TryAddAdventureBackpackSlot(slot),
            SmoothbrainBackpackSlotId => TryAddSmoothbrainBackpackSlot(slot),
            RustyBagSlotId => TryAddRustyBagSlot(slot),
            RustyQuiverSlotId => TryAddRustyQuiverSlot(slot),
            _ => false
        };
    }

    private static bool TryAddAdventureBackpackSlot(YamlSlot slot)
    {
        bool hasApi = TryGetAdventureBackpacksApi(out AdventureBackpacksApi? api) && api != null;
        return TryAddBackpackCompatSlot(slot, AdventureBackpackSlotId, "Backpack", hasApi, item => IsAdventureBackpackItem(item));
    }

    private static bool TryAddSmoothbrainBackpackSlot(YamlSlot slot)
    {
        bool hasApi = TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? api) && api != null;
        return TryAddBackpackCompatSlot(slot, SmoothbrainBackpackSlotId, "Backpack", hasApi, item => IsSmoothbrainBackpackItem(item));
    }

    private static bool TryAddRustyBagSlot(YamlSlot slot)
    {
        bool hasApi = TryGetRustyBagsApi(out RustyBagsApi? api) && api != null;
        return TryAddBackpackCompatSlot(slot, RustyBagSlotId, "Bag", hasApi, item => IsRustyBagItem(item));
    }

    private static bool TryAddRustyQuiverSlot(YamlSlot slot)
    {
        bool hasApi = TryGetRustyBagsApi(out RustyBagsApi? api) && api != null;
        return TryAddBackpackCompatSlot(slot, RustyQuiverSlotId, "Quiver", hasApi, item => IsRustyQuiverItem(item));
    }

    private static bool TryAddBackpackCompatSlot(YamlSlot slot, string id, string fallbackName, bool hasApi, Func<ItemData?, bool> accepts)
    {
        if (SlotDefinitions.Any(existing => existing.Id == id))
        {
            return true;
        }

        List<string> items = GetSlotItems(slot);
        if (!hasApi && items.Count == 0)
        {
            return true;
        }

        string name = string.IsNullOrWhiteSpace(slot.Name) ? fallbackName : slot.Name.Trim();
        SlotDefinitions.Add(new SlotDefinition(
            id,
            name,
            SlotKind.CustomEquipment,
            item => accepts(item) || items.Count > 0 && ItemMatchesSlotItems(item, items)));
        return true;
    }

    private static bool IsAdventureBackpackItem(ItemData? item) =>
        item != null &&
        TryGetAdventureBackpacksApi(out AdventureBackpacksApi? api) &&
        api != null &&
        api.IsBackpack(item);

    private static bool IsSmoothbrainBackpackItem(ItemData? item) =>
        item != null &&
        TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? api) &&
        api != null &&
        api.IsBackpack(item);

    private static bool IsRustyBagItem(ItemData? item) =>
        item != null &&
        TryGetRustyBagsApi(out RustyBagsApi? api) &&
        api != null &&
        api.IsBag(item) &&
        !api.IsQuiver(item);

    private static bool IsRustyQuiverItem(ItemData? item) =>
        item != null &&
        TryGetRustyBagsApi(out RustyBagsApi? api) &&
        api != null &&
        api.IsQuiver(item);

    private static void SyncBackpackCompatState(Player player)
    {
        if (player == null)
        {
            return;
        }

        SyncAdventureBackpackCompatState(player);
        SyncSmoothbrainBackpackCompatState(player);
        SyncRustyBagsCompatState(player);
    }

    private static void OnCustomEquipmentCompatEquipped(Player player, ItemData item)
    {
        if (player == null || item == null)
        {
            return;
        }

        if (IsAdventureBackpackItem(item) &&
            TryGetAdventureBackpacksApi(out AdventureBackpacksApi? adventureApi) &&
            adventureApi != null)
        {
            if (!ReferenceEquals(CompatRuntime.LastAdventureBackpackCompatItem, item) || !adventureApi.IsBackpackEquippedFlagSet())
            {
                adventureApi.OnCustomBackpackEquipped(player, item);
            }

            CompatRuntime.LastAdventureBackpackCompatItem = item;
        }

        if (IsSmoothbrainBackpackItem(item) &&
            TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? smoothbrainApi) &&
            smoothbrainApi != null)
        {
            smoothbrainApi.SyncEquippedBackpack(player, item);
            CompatRuntime.LastSmoothbrainBackpackCompatItem = item;
        }

        if (TryGetRustyBagsApi(out RustyBagsApi? rustyApi) && rustyApi != null)
        {
            if (IsRustyBagItem(item))
            {
                rustyApi.SyncBag(player, item);
                CompatRuntime.LastRustyBagCompatItem = item;
            }
            else if (IsRustyQuiverItem(item))
            {
                rustyApi.SyncQuiver(player, item);
                CompatRuntime.LastRustyQuiverCompatItem = item;
            }
        }

        OnMagicSupremacyBeltEquipped(player, item);
    }

    private static void OnCustomEquipmentCompatUnequipping(Player? player, ItemData item)
    {
        player ??= Player.m_localPlayer;
        if (player == null || item == null)
        {
            return;
        }

        if ((ReferenceEquals(CompatRuntime.LastAdventureBackpackCompatItem, item) || IsAdventureBackpackItem(item)) &&
            TryGetAdventureBackpacksApi(out AdventureBackpacksApi? adventureApi) &&
            adventureApi != null)
        {
            adventureApi.OnCustomBackpackUnequipping(player, item);
            if (ReferenceEquals(CompatRuntime.LastAdventureBackpackCompatItem, item))
            {
                CompatRuntime.LastAdventureBackpackCompatItem = null;
            }
        }

        if ((ReferenceEquals(CompatRuntime.LastSmoothbrainBackpackCompatItem, item) || IsSmoothbrainBackpackItem(item)) &&
            TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? smoothbrainApi) &&
            smoothbrainApi != null)
        {
            smoothbrainApi.SyncEquippedBackpack(player, null);
            if (ReferenceEquals(CompatRuntime.LastSmoothbrainBackpackCompatItem, item))
            {
                CompatRuntime.LastSmoothbrainBackpackCompatItem = null;
            }
        }

        if (TryGetRustyBagsApi(out RustyBagsApi? rustyApi) && rustyApi != null)
        {
            if (ReferenceEquals(CompatRuntime.LastRustyBagCompatItem, item) || IsRustyBagItem(item))
            {
                rustyApi.ClearBagIfCurrent(player, item);
                if (ReferenceEquals(CompatRuntime.LastRustyBagCompatItem, item))
                {
                    CompatRuntime.LastRustyBagCompatItem = null;
                }
            }

            if (ReferenceEquals(CompatRuntime.LastRustyQuiverCompatItem, item) || IsRustyQuiverItem(item))
            {
                rustyApi.ClearQuiverIfCurrent(player, item);
                if (ReferenceEquals(CompatRuntime.LastRustyQuiverCompatItem, item))
                {
                    CompatRuntime.LastRustyQuiverCompatItem = null;
                }
            }
        }

        OnMagicSupremacyBeltUnequipping(player, item);
    }

    private static void SyncAdventureBackpackCompatState(Player player)
    {
        if (!TryGetAdventureBackpacksApi(out AdventureBackpacksApi? api) || api == null)
        {
            CompatRuntime.LastAdventureBackpackCompatItem = null;
            return;
        }

        ItemData? current = FindCustomEquippedItem(player, IsAdventureBackpackItem);
        if (!ReferenceEquals(current, CompatRuntime.LastAdventureBackpackCompatItem))
        {
            if (CompatRuntime.LastAdventureBackpackCompatItem != null)
            {
                api.OnCustomBackpackUnequipping(player, CompatRuntime.LastAdventureBackpackCompatItem);
            }

            CompatRuntime.LastAdventureBackpackCompatItem = current;
            if (current != null)
            {
                api.OnCustomBackpackEquipped(player, current);
            }

            return;
        }

        if (current != null && !api.IsBackpackEquippedFlagSet())
        {
            api.OnCustomBackpackEquipped(player, current);
        }
    }

    private static void SyncSmoothbrainBackpackCompatState(Player player)
    {
        if (!TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? api) || api == null)
        {
            CompatRuntime.LastSmoothbrainBackpackCompatItem = null;
            return;
        }

        ItemData? current = FindCustomEquippedItem(player, IsSmoothbrainBackpackItem);
        api.SyncEquippedBackpack(player, current);
        CompatRuntime.LastSmoothbrainBackpackCompatItem = current;
    }

    private static void SyncRustyBagsCompatState(Player player)
    {
        if (!TryGetRustyBagsApi(out RustyBagsApi? api) || api == null)
        {
            CompatRuntime.LastRustyBagCompatItem = null;
            CompatRuntime.LastRustyQuiverCompatItem = null;
            return;
        }

        ItemData? currentBag = FindCustomEquippedItem(player, IsRustyBagItem);
        ItemData? currentQuiver = FindCustomEquippedItem(player, IsRustyQuiverItem);
        api.SyncBag(player, currentBag);
        api.SyncQuiver(player, currentQuiver);
        CompatRuntime.LastRustyBagCompatItem = currentBag;
        CompatRuntime.LastRustyQuiverCompatItem = currentQuiver;
    }

    private static ItemData? FindCustomEquippedItem(Player player, Func<ItemData?, bool> predicate)
    {
        if (player == null)
        {
            return null;
        }

        return GetCustomEquippedItems(player).FirstOrDefault(item => item != null && predicate(item));
    }

    private static bool TryGetAdventureBackpacksApi(out AdventureBackpacksApi? api)
    {
        const string capability = "AdventureBackpacks";
        return TryGetCompatApi(
            AdventureBackpacksGuid,
            capability,
            CompatRuntime.AdventureBackpacks,
            AdventureBackpacksApi.TryCreate,
            "AdventureBackpacks compatibility disabled",
            out api);
    }

    private static bool TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? api)
    {
        const string capability = "Smoothbrain Backpacks";
        return TryGetCompatApi(
            SmoothbrainBackpacksGuid,
            capability,
            CompatRuntime.SmoothbrainBackpacks,
            SmoothbrainBackpacksApi.TryCreate,
            "Smoothbrain Backpacks compatibility disabled",
            out api);
    }

    private static bool TryGetRustyBagsApi(out RustyBagsApi? api)
    {
        const string capability = "RustyBags";
        return TryGetCompatApi(
            RustyBagsGuid,
            capability,
            CompatRuntime.RustyBags,
            RustyBagsApi.TryCreate,
            "RustyBags compatibility disabled",
            out api);
    }

    private static void AdventureBackpackIsBackpackEquippedPostfix(Player player, ref bool __result)
    {
        if (!__result && player != null && FindCustomEquippedItem(player, IsAdventureBackpackItem) != null)
        {
            __result = true;
        }
    }

    private static void AdventureBackpackIsThisBackpackEquippedPostfix(Player player, ItemData itemData, ref bool __result)
    {
        if (__result || player == null || itemData == null || !IsAdventureBackpackItem(itemData))
        {
            return;
        }

        __result = GetCustomEquippedItems(player).Any(item => ReferenceEquals(item, itemData));
    }

    private static void AdventureBackpackGetEquippedBackpackPrefix(Player player, ref ItemData? __state)
    {
        if (player == null)
        {
            return;
        }

        Humanoid humanoid = player;
        __state = humanoid.m_shoulderItem;
        if (!IsAdventureBackpackItem(humanoid.m_shoulderItem))
        {
            ItemData? customBackpack = FindCustomEquippedItem(player, IsAdventureBackpackItem);
            if (customBackpack != null)
            {
                humanoid.m_shoulderItem = customBackpack;
            }
        }
    }

    private static void AdventureBackpackGetEquippedBackpackPostfix(Player player, ItemData? __state)
    {
        if (player != null)
        {
            ((Humanoid)player).m_shoulderItem = __state;
        }
    }
}
