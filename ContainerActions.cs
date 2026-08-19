using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string ContainerActionSuccessFxPrefabName = "fx_HildirChest_Unlock";
    private const string ContainerActionSuccessFxRpc =
        "InventorySlots_ContainerActionTransientFxV1";
    private const int ContainerActionSuccessVfxKind = 1;
    private const int ContainerActionSuccessSfxKind = 2;
    private const int ContainerActionSuccessVfxLimit = 10;
    private const float ContainerActionSuccessFxLifetime = 5f;
    private const float ContainerActionSuccessFxReceiveRange = 64f;
    private const int ContainerActionSuccessFxReceiveLimit = 32;
    private const float ContainerActionSuccessFxReceiveWindow = 1f;
    private static float _containerActionSuccessFxReceiveWindowStartedAt = -1f;
    private static int _containerActionSuccessFxReceivedInWindow;
    private delegate bool TryGetContainerHoldContext(Player player, out Container container);

    private static List<Container> GetActionContainers(
        Player player,
        Container currentContainer,
        bool areaForQuickStack,
        bool includeBuiltInRemote = false)
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

            if (IsAreaContainerAllowed(
                    player,
                    container,
                    currentContainer,
                    origin,
                    rangeSq,
                    includeBuiltInRemote,
                    out float distanceSq))
            {
                areaContainers.Add((container, distanceSq));
                seen.Add(container);
            }
        }

        areaContainers.Sort((left, right) =>
        {
            int distanceComparison =
                left.DistanceSq.CompareTo(right.DistanceSq);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            ZDO? leftZdo = left.Container.m_nview?.GetZDO();
            ZDO? rightZdo = right.Container.m_nview?.GetZDO();
            if (leftZdo == null || rightZdo == null)
            {
                return 0;
            }

            return leftZdo.m_uid.CompareTo(rightZdo.m_uid);
        });
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
            (container, _) => changedContainerVfxCount = TryBroadcastChangedContainerActionSuccessVfx(container, vfxLimit, changedContainerVfxCount),
            () =>
            {
                onMoved?.Invoke();
                if (vfxLimit > 0)
                {
                    BroadcastContainerActionSuccessFx(
                        anchorContainer,
                        ContainerActionSuccessSfxKind);
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

    private static Container? GetHoveredContainer(Player player)
    {
        GameObject? hoverObject = player != null ? player.GetHoverObject() : null;
        return IsUnityNull(hoverObject) ? null : hoverObject!.GetComponentInParent<Container>();
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

        if (IsMultiUserContainerAreaBatchActive())
        {
            ShowMultiUserContainerNotReady();
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

    private static bool CanUseContainerActionStacking(ItemData item)
    {
        return item?.m_shared != null &&
               (CanUseStackMetadataAutomaticStacking(item) ||
                IsTrustedCustomDataStackingItem(item));
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

        if (TryIsEpicLootStackableMaterialByApi(item, out bool isStackableMaterial))
        {
            return isStackableMaterial;
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

        return IsEpicLootStackableMaterialToken(item.m_shared.m_name ?? "") ||
               (item.m_shared.m_ammoType ?? "").EndsWith("ShardStone", StringComparison.Ordinal);
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

    private static int TryBroadcastChangedContainerActionSuccessVfx(
        Container container,
        int limit,
        int played)
    {
        if (limit <= 0 || played >= limit)
        {
            return played;
        }

        BroadcastContainerActionSuccessFx(
            container,
            ContainerActionSuccessVfxKind);
        return played + 1;
    }

    private static void BroadcastContainerActionSuccessFx(
        Container container,
        int effectKind)
    {
        if (container == null || IsUnityNull(container))
        {
            return;
        }

        ZNetView? nview = container.m_nview;
        if (nview == null ||
            IsUnityNull(nview) ||
            !nview.IsValid() ||
            ZRoutedRpc.instance == null)
        {
            RenderContainerActionSuccessFxLocal(container, effectKind);
            return;
        }

        nview.InvokeRPC(
            ZNetView.Everybody,
            ContainerActionSuccessFxRpc,
            effectKind);
    }

    private static void RPC_ContainerActionSuccessFx(
        Container container,
        int effectKind) =>
        RenderContainerActionSuccessFxLocal(container, effectKind);

    private static void RenderContainerActionSuccessFxLocal(
        Container container,
        int effectKind)
    {
        if (!CanRenderContainerActionSuccessFx(container, effectKind) ||
            !TryConsumeContainerActionSuccessFxReceiveBudget())
        {
            return;
        }

        if (effectKind == ContainerActionSuccessVfxKind)
        {
            RenderContainerActionSuccessVfxLocal(container);
        }
        else
        {
            RenderContainerActionSuccessSfxLocal(container);
        }
    }

    private static bool CanRenderContainerActionSuccessFx(
        Container container,
        int effectKind)
    {
        if ((effectKind != ContainerActionSuccessVfxKind &&
             effectKind != ContainerActionSuccessSfxKind) ||
            IsDedicatedServer ||
            !IsContainerActionSuccessFxEnabled() ||
            container == null ||
            IsUnityNull(container))
        {
            return false;
        }

        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null ||
            IsUnityNull(localPlayer) ||
            localPlayer.m_isLoading)
        {
            return false;
        }

        Vector3 offset =
            localPlayer.transform.position - container.transform.position;
        return offset.sqrMagnitude <=
               ContainerActionSuccessFxReceiveRange *
               ContainerActionSuccessFxReceiveRange;
    }

    private static bool TryConsumeContainerActionSuccessFxReceiveBudget()
    {
        float now = Time.unscaledTime;
        if (_containerActionSuccessFxReceiveWindowStartedAt < 0f ||
            now < _containerActionSuccessFxReceiveWindowStartedAt ||
            now - _containerActionSuccessFxReceiveWindowStartedAt >=
            ContainerActionSuccessFxReceiveWindow)
        {
            _containerActionSuccessFxReceiveWindowStartedAt = now;
            _containerActionSuccessFxReceivedInWindow = 0;
        }

        if (_containerActionSuccessFxReceivedInWindow >=
            ContainerActionSuccessFxReceiveLimit)
        {
            return false;
        }

        _containerActionSuccessFxReceivedInWindow++;
        return true;
    }

    private static void RenderContainerActionSuccessVfxLocal(
        Container container)
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
        bool previousForceDisableInit = ZNetView.m_forceDisableInit;
        GameObject instance;
        try
        {
            ZNetView.m_forceDisableInit = true;
            instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
        finally
        {
            ZNetView.m_forceDisableInit = previousForceDisableInit;
        }

        UnityEngine.Object.Destroy(instance, ContainerActionSuccessFxLifetime);
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

    private static void RenderContainerActionSuccessSfxLocal(
        Container container)
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
        UnityEngine.Object.Destroy(instance, ContainerActionSuccessFxLifetime);
    }
}
