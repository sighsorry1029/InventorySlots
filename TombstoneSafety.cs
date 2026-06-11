using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static void EnsureTombstoneContainerHeight(Container? container, bool reloadInventory, bool persistHeight)
    {
        if (IsUnityNull(container) || !IsTombstoneContainer(container!))
        {
            return;
        }

        Inventory inventory = container!.GetInventory();
        int targetHeight = GetFullHeightForWidth(container!.m_width);
        if (inventory != null)
        {
            targetHeight = GetInventoryPreservationHeight(inventory, targetHeight);
        }

        bool changed = false;

        if (container.m_height < targetHeight)
        {
            container.m_height = targetHeight;
            changed = true;
        }

        if (inventory != null && inventory.m_height < targetHeight)
        {
            inventory.m_height = targetHeight;
            changed = true;
        }

        if (changed && reloadInventory)
        {
            container.m_lastRevision = 0u;
            container.m_lastDataString = "";
            container.Load();
        }

        if (persistHeight)
        {
            PersistContainerHeight(container, targetHeight);
        }
    }

    internal static bool TombstoneCanFitInventory(TombStone tombstone, Player player)
    {
        if (player == null)
        {
            return true;
        }

        Inventory tombInventory = tombstone.m_container != null ? tombstone.m_container.GetInventory() : null!;
        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (tombInventory == null || playerInventory == null)
        {
            return true;
        }

        EnsureInventoryState(player, InventoryStateEnsureReason.Tombstone);
        if (playerInventory.GetTotalWeight() + tombInventory.GetTotalWeight() > player.GetMaxCarryWeight())
        {
            return false;
        }

        HashSet<Vector2i> claimed = new();
        Dictionary<string, int> stackSpace = new(StringComparer.Ordinal);
        foreach (ItemData item in playerInventory.GetAllItems())
        {
            if (item == null)
            {
                continue;
            }

            claimed.Add(item.m_gridPos);
            if (item.m_shared.m_maxStackSize > 1)
            {
                string key = GetStackKey(item);
                stackSpace[key] = stackSpace.TryGetValue(key, out int current) ? current + item.m_shared.m_maxStackSize - item.m_stack : item.m_shared.m_maxStackSize - item.m_stack;
            }
        }

        foreach (ItemData item in tombInventory.GetAllItemsInGridOrder())
        {
            if (item == null)
            {
                continue;
            }

            if (item.m_shared.m_maxStackSize > 1)
            {
                string key = GetStackKey(item);
                if (stackSpace.TryGetValue(key, out int freeStack) && freeStack >= item.m_stack)
                {
                    stackSpace[key] = freeStack - item.m_stack;
                    continue;
                }
            }

            if (!TryFindVirtualFitCell(player, playerInventory, item, claimed, out Vector2i pos))
            {
                return false;
            }

            claimed.Add(pos);
        }

        return true;
    }

    internal static void CleanupTombstoneFloatingBodyForAutoPickup(TombStone? tombstone)
    {
        if (IsUnityNull(tombstone) || !Player.m_enableAutoPickup)
        {
            return;
        }

        Rigidbody? body = tombstone!.m_body;
        if (IsUnityNull(body))
        {
            return;
        }

        try
        {
            GameObject bodyGameObject = body!.gameObject;
            GameObject tombstoneGameObject = tombstone.gameObject;
            Transform bodyRoot = body.transform.root;
            if (!IsUnityNull(bodyRoot) && bodyRoot.gameObject == tombstoneGameObject)
            {
                return;
            }

            if (!tombstone.TryGetComponent(out FloatingTerrain floatingTerrain))
            {
                return;
            }

            floatingTerrain.m_lastHeightmap = null;
            if (!IsUnityNull(bodyGameObject))
            {
                UnityEngine.Object.Destroy(bodyGameObject);
                Log.LogDebug("Destroyed detached tombstone body after take-all to prevent AutoPickup from reading stale floating terrain.");
            }
        }
        catch (Exception ex)
        {
            Log.LogDebug($"Tombstone floating body cleanup skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TryFindVirtualFitCell(Player player, Inventory inventory, ItemData item, HashSet<Vector2i> claimed, out Vector2i pos)
    {
        if (item.m_gridPos.y >= GetFixedRegularRows() && !claimed.Contains(item.m_gridPos) && CanUseCell(player, inventory, item, item.m_gridPos))
        {
            pos = item.m_gridPos;
            return true;
        }

        foreach (SlotDefinition slot in SlotDefinitions.Where(slot => slot.Kind != SlotKind.Quick && CanUseSpecialSlot(player, inventory, item, slot)))
        {
            Vector2i candidate = GetSlotGridPos(inventory, slot);
            if (!claimed.Contains(candidate))
            {
                pos = candidate;
                return true;
            }
        }

        int usableRows = GetUsableRegularRows(player);
        for (int y = 0; y < usableRows; y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                Vector2i candidate = new(x, y);
                if (!claimed.Contains(candidate) && IsUsableRegularCell(inventory, player, candidate))
                {
                    pos = candidate;
                    return true;
                }
            }
        }

        foreach (SlotDefinition slot in SlotDefinitions.Where(slot => slot.Kind == SlotKind.Quick && CanUseSpecialSlot(player, inventory, item, slot)))
        {
            Vector2i candidate = GetSlotGridPos(inventory, slot);
            if (!claimed.Contains(candidate))
            {
                pos = candidate;
                return true;
            }
        }

        pos = new Vector2i(-1, -1);
        return false;
    }

    private static string GetStackKey(ItemData item)
    {
        return $"{item.m_shared.m_name}|{item.m_quality}|{item.m_worldLevel}";
    }

    private static bool IsTombstoneContainer(Container container)
    {
        return container.m_name == "Grave" || container.GetComponent<TombStone>() != null || container.GetComponentInParent<TombStone>() != null;
    }

    private static void PersistContainerHeight(Container container, int height)
    {
        ZNetView nview = container.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner() || container.GetComponent<TombStone>() == null)
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        string typeName = container.GetType().Name;
        int hasFieldsKey = StringExtensionMethods.GetStableHashCode("HasFields" + typeName);
        string heightKey = typeName + ".m_height";
        if (!zdo.GetBool("HasFields", false))
        {
            zdo.Set("HasFields", true);
        }

        if (!zdo.GetBool(hasFieldsKey, false))
        {
            zdo.Set(hasFieldsKey, true);
        }

        if (zdo.GetInt(heightKey) != height)
        {
            zdo.Set(heightKey, height);
        }
    }
}
