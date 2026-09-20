using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static readonly Dictionary<Vector2i, string> FavoriteSlotItems = new();
    // Resolve the original private field only when a live player is inspected,
    // not during the plugin's configuration/static initialization.
    private static class FavoriteMemoryAccess
    {
        internal static readonly AccessTools.FieldRef<Player, bool> IsLoading =
            AccessTools.FieldRefAccess<Player, bool>("m_isLoading");
    }
    private static Inventory? _favoriteMemoryInventory;
    private static bool _favoriteMemoryPending = true;
    private static bool _favoriteMemorySavePending;
    private static float _favoriteMemorySaveRetryAt;

    private static ISet<Vector2i> RememberedFavoriteCells =>
#if INVENTORY_SLOTS
        FavoriteSlots;
#else
        Runtime.FavoriteSlots;
#endif

    internal static void RequestFavoriteSlotMemoryRefresh(Player player)
    {
        if (player == Player.m_localPlayer) _favoriteMemoryPending = true;
    }

    // Observe after synchronous inventory moves, not from inside a Changed
    // callback where swapping two items may still be in progress.
    private void LateUpdate()
    {
        Player? player = Player.m_localPlayer;
        if (player == null || FavoriteMemoryAccess.IsLoading(player)) return;
        Inventory inventory = ((Humanoid)player).GetInventory();
        if (_favoriteMemoryPending || !ReferenceEquals(_favoriteMemoryInventory, inventory) ||
            _favoriteMemorySavePending && Time.unscaledTime >= _favoriteMemorySaveRetryAt)
            RememberFavoriteSlotItems(player);
    }

    internal static void RememberFavoriteSlotItems(Player player, bool flush = false)
    {
        if (!RefreshFavoriteSlotMemory(player, out bool changed)) return;
        if (changed) _favoriteMemorySavePending = true;
        if (_favoriteMemorySavePending && (flush || Time.unscaledTime >= _favoriteMemorySaveRetryAt)) SaveFavorites(player);
    }

    private static bool RefreshFavoriteSlotMemory(Player player, out bool changed)
    {
        changed = false;
        if (player == null || player != Player.m_localPlayer || FavoriteMemoryAccess.IsLoading(player)) return false;
        Inventory inventory = ((Humanoid)player).GetInventory();
#if INVENTORY_SLOTS
        if (IsInventoryLoadPreserving(inventory) || InventorySafety.EnsuringInventoryState ||
            InventorySafety.RestoringSlotBackup || !IsSyncedStateReady() ||
            InventorySafety.DeferredAuditLevel >= InventoryStateAuditLevel.SlotLight ||
            InventorySafety.PendingSlotEquips.Count > 0 || InventorySafety.PendingSlotUnequips.Count > 0 ||
            HasPendingQuickSlotProgressionReset(player)) return false;
#endif
        EnsureFavoritesLoaded(player);
        _favoriteMemoryInventory = inventory;
        _favoriteMemoryPending = false;
        changed = FavoriteSlotMemoryCore.Observe(FavoriteSlotItems, RememberedFavoriteCells, cell =>
        {
            if (!IsEligibleFavoriteRestockCell(player, inventory, cell)) return null;
            ItemData? item = inventory.GetItemAt(cell.x, cell.y);
            return item != null && item.m_stack > 0 ? GetFavoriteMemoryPrefab(item) : null;
        });
        return true;
    }

    private static string GetFavoriteMemoryPrefab(ItemData item) =>
        item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
}

[HarmonyPatch(typeof(Player), "OnInventoryChanged")]
internal static class PlayerFavoriteSlotMemoryChangedPatch
{
    private static void Postfix(Player __instance) =>
#if INVENTORY_SLOTS
        InventorySlotsPlugin.RequestFavoriteSlotMemoryRefresh(__instance);
#else
        InventoryActionsPlugin.RequestFavoriteSlotMemoryRefresh(__instance);
#endif
}

[HarmonyPatch(typeof(Player), nameof(Player.Save), typeof(ZPackage))]
internal static class PlayerSaveFavoriteSlotMemoryPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(Player __instance) =>
#if INVENTORY_SLOTS
        InventorySlotsPlugin.RememberFavoriteSlotItems(__instance, flush: true);
#else
        InventoryActionsPlugin.RememberFavoriteSlotItems(__instance, flush: true);
#endif
}
