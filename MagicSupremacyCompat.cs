using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static ItemData? _lastMagicSupremacyBeltCompatItem;
    [ThreadStatic] private static ItemData? _magicSupremacyHandledEquipItem;
    private static bool _magicSupremacyEquipGuardFailed;

    private static void InitializeMagicSupremacyCompatibility()
    {
        if (!TryGetMagicSupremacyApi(out MagicSupremacyApi? api) || api == null) return;
        try
        {
            _instance._harmony.Patch(api.NativeEquipPrefix,
                prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(AllowMagicSupremacyNativeEquipPrefix)));
        }
        catch (Exception ex)
        {
            _magicSupremacyEquipGuardFailed = true;
            Log.LogWarning($"Magic Supremacy compatibility disabled: could not guard its native equip prefix: {ex.GetBaseException().Message}");
        }
    }

    private static void ShutdownMagicSupremacyCompatibility()
    {
        // Keep the guard for the same lifetime as our existing Harmony routing
        // patches. Removing only this guard would let native postfixes re-equip
        // an item after a failed InventorySlots transaction.
        _magicSupremacyHandledEquipItem = null;
        _lastMagicSupremacyBeltCompatItem = null;
    }

    internal static ItemData? BeginMagicSupremacyEquipScope()
    {
        ItemData? previous = _magicSupremacyHandledEquipItem;
        _magicSupremacyHandledEquipItem = null;
        return previous;
    }

    internal static void SuppressMagicSupremacyNativeEquip(ItemData item) => _magicSupremacyHandledEquipItem = item;
    internal static void EndMagicSupremacyEquipScope(ItemData? previous) => _magicSupremacyHandledEquipItem = previous;

    // HarmonyX runs every prefix even when our outer prefix skips EquipItem.
    // Guard the external prefix body itself; its default __state then keeps its
    // postfix inactive. Calls not handled by InventorySlots remain untouched.
    private static bool AllowMagicSupremacyNativeEquipPrefix(ItemData item) =>
        !ReferenceEquals(item, _magicSupremacyHandledEquipItem);

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

    // A Tome in the Utility cell still belongs to Magic Supremacy's native tome slot.
    // Giving it the vanilla utility reference would also project its effects/visuals there.
    private static bool UsesCustomEquipmentState(ItemData item, SlotDefinition slot) =>
        slot.Kind == SlotKind.CustomEquipment || slot.Id == "utility" && IsMagicSupremacyBeltItem(item);

    private static ItemData? CaptureMagicSupremacyEquippedState(Player player) =>
        TryGetMagicSupremacyApi(out MagicSupremacyApi? api) && api != null ? api.GetEquippedBelt(player) : null;

    private static void RestoreMagicSupremacyEquippedState(Player player, ItemData? previous)
    {
        if (!TryGetMagicSupremacyApi(out MagicSupremacyApi? api) || api == null)
        {
            return;
        }

        ItemData? current = api.GetEquippedBelt(player);
        if (current != null && !ReferenceEquals(current, previous))
        {
            api.ClearBeltIfCurrent(player, current);
        }

        if (previous != null)
        {
            api.SyncBelt(player, previous);
        }

        // A failed unrelated equip can snapshot a Tome owned only by the external
        // mod. Do not adopt it into our tracker and clear it on the next custom sync.
        _lastMagicSupremacyBeltCompatItem = previous != null && IsInventorySlotsCustomEquipped(previous) &&
            previous.m_customData.TryGetValue(EquippedByKey, out string owner) && owner == GetPlayerId(player)
                ? previous
                : null;
    }

    private static void SyncMagicSupremacyCompatState(Player player)
    {
        if (player == null)
        {
            return;
        }

        if (!TryGetMagicSupremacyApi(out MagicSupremacyApi? api) || api == null)
        {
            _lastMagicSupremacyBeltCompatItem = null;
            return;
        }

        ItemData? current = FindCustomEquippedItem(player, IsMagicSupremacyBeltItem);
        if (!ReferenceEquals(current, _lastMagicSupremacyBeltCompatItem))
        {
            if (_lastMagicSupremacyBeltCompatItem != null)
            {
                api.ClearBeltIfCurrent(player, _lastMagicSupremacyBeltCompatItem);
            }

            _lastMagicSupremacyBeltCompatItem = current;
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
            _lastMagicSupremacyBeltCompatItem = item;
        }
    }

    private static void OnMagicSupremacyBeltUnequipping(Player player, ItemData item)
    {
        if (player == null || item == null)
        {
            return;
        }

        if ((ReferenceEquals(_lastMagicSupremacyBeltCompatItem, item) || IsMagicSupremacyBeltItem(item)) &&
            TryGetMagicSupremacyApi(out MagicSupremacyApi? api) &&
            api != null)
        {
            api.ClearBeltIfCurrent(player, item);
            if (ReferenceEquals(_lastMagicSupremacyBeltCompatItem, item))
            {
                _lastMagicSupremacyBeltCompatItem = null;
            }
        }
    }

    private static bool TryGetMagicSupremacyApi(out MagicSupremacyApi? api)
    {
        if (_magicSupremacyEquipGuardFailed)
        {
            api = null;
            return false;
        }
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
