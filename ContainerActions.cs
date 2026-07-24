using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string ContainerActionSuccessFxPrefabName = "fx_HildirChest_Unlock";
    private const int ContainerActionSuccessVfxLimit = 10;
    private const float ContainerActionSuccessSfxLifetime = 5f;
    private delegate bool TryGetContainerHoldContext(Player player, out Container container);

    private static List<Container> GetActionContainers(Player player, Container currentContainer, bool areaForQuickStack)
    {
        List<Container> containers = new();
        HashSet<Container> seen = new();
        if (currentContainer != null && currentContainer.m_inventory != null && seen.Add(currentContainer))
        {
            containers.Add(currentContainer);
        }

        float range = areaForQuickStack ? _areaQuickStackRange?.Value ?? 0f : _areaRestockRange?.Value ?? 0f;
        if (range <= 0f)
        {
            return containers;
        }

        if (currentContainer == null || IsUnityNull(currentContainer))
        {
            return containers;
        }

        Vector3 origin = currentContainer.transform.position;
        float rangeSq = range * range;
        List<(Container Container, float DistanceSq)> areaContainers = new();
        for (int i = InventoryContainers.KnownContainers.Count - 1; i >= 0; i--)
        {
            Container container = InventoryContainers.KnownContainers[i];
            if (container == null || IsUnityNull(container))
            {
                InventoryContainers.KnownContainers.RemoveAt(i);
                continue;
            }

            if (seen.Contains(container))
            {
                continue;
            }

            if (IsAreaContainerAllowed(player, container, currentContainer, origin, rangeSq, out float distanceSq))
            {
                areaContainers.Add((container, distanceSq));
                seen.Add(container);
            }
        }

        areaContainers.Sort((left, right) => left.DistanceSq.CompareTo(right.DistanceSq));
        foreach ((Container container, _) in areaContainers)
        {
            containers.Add(container);
        }

        return containers;
    }

    private static int RunContainerTransferAcrossContainers(
        Player localPlayer,
        Container anchorContainer,
        bool includeArea,
        bool areaForQuickStack,
        Func<Container, int> transfer,
        Action onMoved)
    {
        if (localPlayer == null || anchorContainer == null || transfer == null)
        {
            return 0;
        }

        int vfxLimit = includeArea && IsContainerActionSuccessFxEnabled() ? ContainerActionSuccessVfxLimit : 0;
        int changedContainerVfxCount = 0;
        List<Container> containers = includeArea
            ? GetActionContainers(localPlayer, anchorContainer, areaForQuickStack)
            : new List<Container> { anchorContainer };

        return ContainerTransferCore.Run(
            containers,
            container => !IsUnityNull(container) && container.m_inventory != null,
            transfer,
            (container, _) => changedContainerVfxCount = TryPlayChangedContainerActionSuccessVfx(container, vfxLimit, changedContainerVfxCount),
            () =>
            {
                onMoved?.Invoke();
                if (vfxLimit > 0)
                {
                    PlayContainerActionSuccessSfx(anchorContainer);
                }
            });
    }

    private static void ShowContainerActionResult(Player player, string actionToken, string actionFallback, int moved)
    {
        if (player == null)
        {
            return;
        }

        string action = LocalizeUi(actionToken, actionFallback);
        string format = LocalizeUi("$inventoryslots_action_result_format", "{action}: {count}");
        string message = format
            .Replace("{action}", action)
            .Replace("{count}", moved.ToString());
        player.Message(MessageHud.MessageType.Center, message, 0, null);
    }

    private static List<Vector2i> GetPlayerActionSlots(Player player, Inventory inventory, bool includeHotbar, bool blockFavorites = false)
    {
        List<Vector2i> slots = new();
        int fixedRows = Math.Min(GetFixedRegularRows(), inventory.GetHeight());
        HashSet<Vector2i> blockedSlots = blockFavorites ? GetFavoriteBlockedSlots(player, inventory) : new HashSet<Vector2i>();
        for (int y = 0; y < fixedRows; y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                Vector2i pos = new(x, y);
                if (blockedSlots.Contains(pos))
                {
                    continue;
                }

                InventoryCellKind kind = GetInventoryCellKind(player, inventory, pos);
                if (InventoryActionCellPolicyCore.CanUseContainerActionSource(kind, includeHotbar))
                {
                    slots.Add(pos);
                }
            }
        }

        return slots;
    }

    private static HashSet<Vector2i> GetFavoriteBlockedSlots(Player player, Inventory inventory)
    {
        HashSet<Vector2i> blocked = new();
        EnsureFavoritesLoaded(player);
        foreach (ItemData item in inventory.m_inventory)
        {
            if (item?.m_shared == null)
            {
                continue;
            }

            if (FavoriteSlots.Contains(item.m_gridPos))
            {
                blocked.Add(item.m_gridPos);
            }
        }

        return blocked;
    }

    private static bool TryGetActionContext(Player? player, out Player localPlayer, out Inventory playerInventory, out Container container, out Inventory containerInventory)
    {
        localPlayer = null!;
        playerInventory = null!;
        container = null!;
        containerInventory = null!;

        if (player == null || player.m_isLoading || InventoryGui.instance == null)
        {
            return false;
        }

        Container currentContainer = InventoryGui.instance.m_currentContainer;
        if (currentContainer == null || currentContainer.m_inventory == null)
        {
            return false;
        }

        if (!CanMutateContainerDirectly(currentContainer, allowLocalWithoutZNetView: true))
        {
            player.Message(MessageHud.MessageType.Center, LocalizeUi("$inventoryslots_container_not_ready", "Container is not ready."), 0, null);
            return false;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return false;
        }

        localPlayer = player;
        playerInventory = inventory;
        container = currentContainer;
        containerInventory = currentContainer.m_inventory;
        return true;
    }

    private static bool HasNoCustomData(ItemData item)
    {
        return item.m_customData == null || item.m_customData.Count == 0;
    }

    private static bool CanUseContainerActionStacking(ItemData item)
    {
        return item?.m_shared != null && (HasNoCustomData(item) || IsTrustedCustomDataStackingItem(item));
    }

    private static bool IsTrustedCustomDataStackingItem(ItemData? item)
    {
        return IsJewelcraftingOrbItem(item) || IsEpicLootStackableMaterial(item);
    }

    private static bool IsJewelcraftingOrbItem(ItemData? item)
    {
        if (item?.m_shared == null || !HasPlugin(JewelcraftingGuid))
        {
            return false;
        }

        string prefabName = GetItemPrefabName(item);
        if (prefabName.StartsWith("JC_Orb_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string sharedName = item.m_shared.m_name ?? "";
        return sharedName.StartsWith("$jc_orb_", StringComparison.OrdinalIgnoreCase) ||
               sharedName.IndexOf("orb_of_", StringComparison.OrdinalIgnoreCase) >= 0 &&
               prefabName.StartsWith("JC_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEpicLootStackableMaterial(ItemData? item)
    {
        if (item?.m_shared == null || !HasPlugin(EpicLootGuid))
        {
            return false;
        }

        string prefabName = GetItemPrefabName(item);
        if (IsEpicLootStackableMaterialToken(prefabName))
        {
            return true;
        }

        if (!IsUnityNull(item.m_dropPrefab) && IsEpicLootStackableMaterialToken(item.m_dropPrefab.name))
        {
            return true;
        }

        return IsEpicLootStackableMaterialToken(item.m_shared.m_name ?? "");
    }

    private static bool IsEpicLootStackableMaterialToken(string token)
    {
        return token.StartsWith("Shard", StringComparison.OrdinalIgnoreCase) ||
               token.StartsWith("Essence", StringComparison.OrdinalIgnoreCase) ||
               token.StartsWith("Dust", StringComparison.OrdinalIgnoreCase) ||
               token.StartsWith("Reagent", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountMovedFromContainerSource(Inventory sourceInventory, ItemData sourceItem, int before, int requestedAmount, bool moveSucceeded)
    {
        int after = sourceInventory.m_inventory.Contains(sourceItem) ? sourceItem.m_stack : 0;
        return ContainerActionCore.CountMovedAmount(before, after, requestedAmount, moveSucceeded, useMoveSucceededFallback: false);
    }

    private static void RemoveItemIfStillOwned(Inventory inventory, ItemData item)
    {
        if (inventory != null && item != null && inventory.m_inventory.Contains(item))
        {
            inventory.RemoveItem(item);
        }
    }

    private static int CompareGridOrder(Vector2i a, Vector2i b)
    {
        return ContainerActionCore.CompareGridOrder(a.x, a.y, b.x, b.y);
    }

    private static bool IsContainerActionSuccessFxEnabled() =>
        _containerActionSuccessFx == null || _containerActionSuccessFx.Value == Toggle.On;

    private static void HandleContainerHoldHotkey(
        Player player,
        ContainerHoldActionState hold,
        TryGetContainerHoldContext tryGetContext,
        Func<Player, Container, bool> executeAction)
    {
        if (!tryGetContext(player, out Container container))
        {
            ResetContainerHold(hold);
            return;
        }

        if (IsUnityNull(hold.Container) || hold.Container != container)
        {
            hold.Container = container;
            hold.StartTime = Time.time;
            hold.Triggered = false;
        }

        if (hold.Triggered || Time.time - hold.StartTime < ContainerHoverHoldDuration)
        {
            return;
        }

        if (executeAction(player, container))
        {
            hold.Triggered = true;
        }
        else
        {
            ResetContainerHold(hold);
        }
    }

    private static void ResetContainerHold(ContainerHoldActionState hold)
    {
        hold.Container = null;
        hold.StartTime = -1f;
        hold.Triggered = false;
    }

    private static int TryPlayChangedContainerActionSuccessVfx(Container container, int limit, int played)
    {
        if (limit <= 0 || played >= limit)
        {
            return played;
        }

        PlayContainerActionSuccessVfx(container);
        return played + 1;
    }

    private static void PlayContainerActionSuccessVfx(Container container)
    {
        if (container == null ||
            IsUnityNull(container) ||
            ZNetScene.instance == null)
        {
            return;
        }

        GameObject? prefab = ZNetScene.instance.GetPrefab(ContainerActionSuccessFxPrefabName);
        if (prefab == null || IsUnityNull(prefab))
        {
            return;
        }

        Vector3 position = container.transform.position;
        Quaternion rotation = container.transform.rotation;
        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        foreach (ZSFX sfx in instance.GetComponentsInChildren<ZSFX>(includeInactive: true))
        {
            if (sfx == null || IsUnityNull(sfx))
            {
                continue;
            }

            sfx.Stop();
            sfx.gameObject.SetActive(false);
        }
    }

    private static void PlayContainerActionSuccessSfx(Container container)
    {
        if (container == null || IsUnityNull(container) || ZNetScene.instance == null)
        {
            return;
        }

        GameObject? prefab = ZNetScene.instance.GetPrefab(ContainerActionSuccessFxPrefabName);
        if (prefab == null || IsUnityNull(prefab))
        {
            return;
        }

        Transform? sfxRoot = prefab.transform.Find("SFX");
        if (sfxRoot == null || IsUnityNull(sfxRoot))
        {
            return;
        }

        GameObject instance = UnityEngine.Object.Instantiate(
            sfxRoot.gameObject,
            container.transform.position,
            container.transform.rotation);
        UnityEngine.Object.Destroy(instance, ContainerActionSuccessSfxLifetime);
    }
}
