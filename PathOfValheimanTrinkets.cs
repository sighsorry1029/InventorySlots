using System;
using System.Collections.Generic;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static readonly string[] PovTrinketRoles = { "trinket", "trinket.1", "trinket.2", "trinket.hand" };
    private static Player? _povTrinketsReadyPlayer;

    private static bool IsPathOfValheimanTrinketSlot(SlotDefinition? slot) =>
        _pov?.HasExtraTrinkets == true && slot?.PovCell != null && Array.IndexOf(PovTrinketRoles, slot.PovCell) >= 0;

    private static bool IsPathOfValheimanTrinketItem(ItemData? item) =>
        _pov?.HasExtraTrinkets == true && item != null && _pov.Accepts("trinket", item);

    private static void OnPathOfValheimanTrinketsSpawned(Player __0)
    {
        if (__0 != Player.m_localPlayer) return;
        _povTrinketsReadyPlayer = __0;
        RequestLocalInventoryState(InventoryStateEnsureReason.EquipmentChanged, InventoryStateAuditLevel.FullIntegrity);
    }

    private static bool CanWithdrawPathOfValheimanTrinket(Player player, Inventory inventory, ItemData item, SlotDefinition slot) =>
        IsPathOfValheimanTrinketSlot(slot) && _pov!.ProviderId() == ModGUID && player == Player.m_localPlayer &&
        !player.m_isLoading && !IsPathOfValheimanMutationPending(player) && inventory == player.GetInventory() &&
        ContainsExactItemReference(inventory, item) && TryGetSlotGridPos(inventory, slot, out Vector2i pos) &&
        item.m_gridPos == pos && inventory.GetItemAt(pos.x, pos.y) == item;

    private static bool TryEquipPathOfValheimanTrinket(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (!PovCanMutate(player) || _povTrinketsReadyPlayer != player ||
            inventory != player.GetInventory() || !ContainsExactItemReference(inventory, item) ||
            IsInventorySlotsCustomEquipped(item) ||
            !IsPathOfValheimanTrinketSlot(slot) || !CanUseSpecialSlot(player, inventory, item, slot)) return false;
        bool accepted = false;
        _povMutationDepth++;
        BeginSlotAutoEquipSuppression();
        try { accepted = _pov!.Equip(player, slot.PovCell!, item); }
        catch (Exception error)
        {
            // A callback can fail after changing equipment. Reconcile its actual
            // result; do not pretend the previous effects/seat were restored.
            Log.LogWarning("PoV trinket request failed: " + error.GetBaseException().Message);
        }
        finally { CompleteSlotAutoEquipSuppression(); _povMutationDepth--; }
        if (ProjectPathOfValheimanTrinkets(player, inventory)) NotifyPathOfValheimanCommit(inventory);
        return accepted && (player.IsItemEquiped(item) || player.IsEquipActionQueued(item));
    }

    private static void RemovePathOfValheimanTrinket(Player player, ItemData item)
    {
        if (_pov == null || !player.IsItemEquiped(item)) return;
        // TakeOut may queue an action, and removing the base can promote another
        // trinket. Choose by current identity, never the item's old grid position.
        if (_pov.Enabled())
            foreach (string role in PovTrinketRoles)
                if (ReferenceEquals(_pov.Equipped(player, role), item))
                { _pov.Unequip(player, role); return; }
        // Off/closed roles are hidden by the public API. Let PoV's Humanoid hook
        // remove this exact item and its effects, without inventing a seat.
        player.UnequipItem(item, true);
    }

    private static bool RestorePathOfValheimanTrinket(Player player, Inventory inventory, ItemData item)
    {
        if (_pov == null) return false;
        // Upgrade removal compacts PoV's list. Replacing the old role would
        // unequip the peer promoted into it; restoration must use an empty role.
        if (!player.IsItemEquiped(item))
            foreach (string role in PovTrinketRoles)
                if (PovSlots.TryGetValue(role, out var slot) && _pov.Open(player, role) && _pov.TrinketItem(player, role) == null)
                {
                    TryEquipPathOfValheimanTrinket(player, inventory, item, slot);
                    break;
                }
        if (ProjectPathOfValheimanTrinkets(player, inventory)) NotifyPathOfValheimanCommit(inventory);
        return IsPathOfValheimanTrinketProjected(player, inventory, item);
    }

    private static bool IsPathOfValheimanTrinketProjected(Player player, Inventory inventory, ItemData item)
    {
        if (_pov == null || !inventory.ContainsItem(item) || !player.IsItemEquiped(item)) return false;
        foreach (string role in PovTrinketRoles)
            if (ReferenceEquals(_pov.TrinketItem(player, role), item) && PovSlots.TryGetValue(role, out var slot) &&
                TryGetSlotGridPos(inventory, slot, out Vector2i pos) && item.m_gridPos == pos && CellContainsOnly(inventory, pos, item)) return true;
        return false;
    }

    private static bool ProjectPathOfValheimanTrinkets(Player player, Inventory inventory)
    {
        if (_pov?.HasExtraTrinkets != true || player != Player.m_localPlayer || _povTrinketsReadyPlayer != player ||
            player.m_isLoading || IsPathOfValheimanMutationPending(player) ||
            _pov.ProviderId() != ModGUID || _pov.Suspended()) return false;

        // Plan all roles together: removing the base or changing nodes compacts
        // PoV's worn list, so assigning one cell at a time can overwrite a peer.
        var desired = new Dictionary<ItemData, Vector2i>();
        var seats = new HashSet<Vector2i>();
        foreach (string role in PovTrinketRoles)
        {
            if (!PovSlots.TryGetValue(role, out var slot) || !TryGetSlotGridPos(inventory, slot, out Vector2i pos)) continue;
            seats.Add(pos);
            ItemData? worn = _pov.TrinketItem(player, role);
            if (worn != null && ContainsExactItemReference(inventory, worn))
            {
                if (desired.ContainsKey(worn)) return false;
                desired.Add(worn, pos);
            }
        }
        var displaced = new List<ItemData>();
        var occupied = new HashSet<Vector2i>(desired.Values);
        foreach (ItemData candidate in inventory.m_inventory)
        {
            if (desired.ContainsKey(candidate)) continue;
            if (seats.Contains(candidate.m_gridPos)) displaced.Add(candidate);
            else occupied.Add(candidate.m_gridPos);
        }
        foreach (ItemData candidate in displaced)
        {
            bool placed = false;
            // Pending equip/unequip keeps its original cell until completion.
            if (player.IsEquipActionQueued(candidate) && !occupied.Contains(candidate.m_gridPos))
            { desired.Add(candidate, candidate.m_gridPos); occupied.Add(candidate.m_gridPos); continue; }
            for (int y = 0; y < GetUsableRegularRows(player) && !placed; y++)
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    var pos = new Vector2i(x, y);
                    if (!IsUsableRegularCell(inventory, player, pos) || !occupied.Add(pos)) continue;
                    desired.Add(candidate, pos); placed = true; break;
                }
            if (placed) continue;
            // Full backpack after node removal: preserve a withdrawable reserved
            // cell, without re-equipping or granting use of a closed role.
            if (occupied.Add(candidate.m_gridPos)) { desired.Add(candidate, candidate.m_gridPos); continue; }
            foreach (Vector2i pos in seats)
                if (occupied.Add(pos)) { desired.Add(candidate, pos); placed = true; break; }
            if (!placed) return false; // No coordinates were changed.
        }
        bool changed = false;
        foreach (var pair in desired)
            if (pair.Key.m_gridPos != pair.Value) { pair.Key.m_gridPos = pair.Value; changed = true; }
        return changed;
    }
}
