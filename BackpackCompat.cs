using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static ItemData? _lastAdventureBackpackCompatItem;
    private static ItemData? _lastSmoothbrainBackpackCompatItem;
    private static ItemData? _lastRustyBagCompatItem;
    private static ItemData? _lastRustyQuiverCompatItem;

    private static void InitializeBackpackCompatibility()
    {
        _ = TryGetAdventureBackpacksApi(out _);
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

    private static bool OnCustomEquipmentCompatEquipped(Player player, ItemData item)
    {
        if (player == null || item == null)
        {
            return false;
        }

        bool externalStateChanged = OnHipLanternCustomEquipmentEquipped(player, item);

        if (IsAdventureBackpackItem(item) &&
            TryGetAdventureBackpacksApi(out AdventureBackpacksApi? adventureApi) &&
            adventureApi != null)
        {
            if (!ReferenceEquals(_lastAdventureBackpackCompatItem, item) || !adventureApi.IsBackpackEquipped(player))
            {
                adventureApi.OnCustomBackpackEquipped(player, item);
            }

            _lastAdventureBackpackCompatItem = item;
        }

        if (IsSmoothbrainBackpackItem(item) &&
            TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? smoothbrainApi) &&
            smoothbrainApi != null)
        {
            smoothbrainApi.SyncEquippedBackpack(player, item);
            _lastSmoothbrainBackpackCompatItem = item;
        }

        if (TryGetRustyBagsApi(out RustyBagsApi? rustyApi) && rustyApi != null)
        {
            if (IsRustyBagItem(item))
            {
                rustyApi.SyncBag(player, item);
                _lastRustyBagCompatItem = item;
            }
            else if (IsRustyQuiverItem(item))
            {
                rustyApi.SyncQuiver(player, item);
                _lastRustyQuiverCompatItem = item;
            }
        }

        OnMagicSupremacyBeltEquipped(player, item);
        return externalStateChanged;
    }

    private static bool OnCustomEquipmentCompatUnequipping(Player? player, ItemData item)
    {
        player ??= Player.m_localPlayer;
        if (player == null || item == null)
        {
            return false;
        }

        bool externalStateChanged =
            ClearCircletExtendedEquippedState(player, item) |
            ClearHipLanternEquippedState(player, item);

        if ((ReferenceEquals(_lastAdventureBackpackCompatItem, item) || IsAdventureBackpackItem(item)) &&
            TryGetAdventureBackpacksApi(out AdventureBackpacksApi? adventureApi) &&
            adventureApi != null)
        {
            adventureApi.OnCustomBackpackUnequipping(player, item);
            if (ReferenceEquals(_lastAdventureBackpackCompatItem, item))
            {
                _lastAdventureBackpackCompatItem = null;
            }
        }

        if ((ReferenceEquals(_lastSmoothbrainBackpackCompatItem, item) || IsSmoothbrainBackpackItem(item)) &&
            TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? smoothbrainApi) &&
            smoothbrainApi != null)
        {
            smoothbrainApi.SyncEquippedBackpack(player, null);
            if (ReferenceEquals(_lastSmoothbrainBackpackCompatItem, item))
            {
                _lastSmoothbrainBackpackCompatItem = null;
            }
        }

        if (TryGetRustyBagsApi(out RustyBagsApi? rustyApi) && rustyApi != null)
        {
            if (ReferenceEquals(_lastRustyBagCompatItem, item) || IsRustyBagItem(item))
            {
                rustyApi.ClearBagIfCurrent(player, item);
                if (ReferenceEquals(_lastRustyBagCompatItem, item))
                {
                    _lastRustyBagCompatItem = null;
                }
            }

            if (ReferenceEquals(_lastRustyQuiverCompatItem, item) || IsRustyQuiverItem(item))
            {
                rustyApi.ClearQuiverIfCurrent(player, item);
                if (ReferenceEquals(_lastRustyQuiverCompatItem, item))
                {
                    _lastRustyQuiverCompatItem = null;
                }
            }
        }

        OnMagicSupremacyBeltUnequipping(player, item);
        return externalStateChanged;
    }

    private static void SyncAdventureBackpackCompatState(Player player)
    {
        if (!TryGetAdventureBackpacksApi(out AdventureBackpacksApi? api) || api == null)
        {
            _lastAdventureBackpackCompatItem = null;
            return;
        }

        ItemData? current = FindCustomEquippedItem(player, IsAdventureBackpackItem);
        if (!ReferenceEquals(current, _lastAdventureBackpackCompatItem))
        {
            if (_lastAdventureBackpackCompatItem != null)
            {
                api.OnCustomBackpackUnequipping(player, _lastAdventureBackpackCompatItem);
            }

            _lastAdventureBackpackCompatItem = current;
            if (current != null)
            {
                api.OnCustomBackpackEquipped(player, current);
            }

            return;
        }

        if (current != null && !api.IsBackpackEquipped(player))
        {
            api.OnCustomBackpackEquipped(player, current);
        }
    }

    private static void SyncSmoothbrainBackpackCompatState(Player player)
    {
        if (!TryGetSmoothbrainBackpacksApi(out SmoothbrainBackpacksApi? api) || api == null)
        {
            _lastSmoothbrainBackpackCompatItem = null;
            return;
        }

        ItemData? current = FindCustomEquippedItem(player, IsSmoothbrainBackpackItem);
        api.SyncEquippedBackpack(player, current);
        _lastSmoothbrainBackpackCompatItem = current;
    }

    private static void SyncRustyBagsCompatState(Player player)
    {
        if (!TryGetRustyBagsApi(out RustyBagsApi? api) || api == null)
        {
            _lastRustyBagCompatItem = null;
            _lastRustyQuiverCompatItem = null;
            return;
        }

        ItemData? currentBag = FindCustomEquippedItem(player, IsRustyBagItem);
        ItemData? currentQuiver = FindCustomEquippedItem(player, IsRustyQuiverItem);
        api.SyncBag(player, currentBag);
        api.SyncQuiver(player, currentQuiver);
        _lastRustyBagCompatItem = currentBag;
        _lastRustyQuiverCompatItem = currentQuiver;
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


}
