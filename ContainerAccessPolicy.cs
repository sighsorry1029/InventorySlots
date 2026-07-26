using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum ContainerAccessMode
    {
        Unavailable,
        DirectOwner,
        MultiUserChestRemote
    }

    private static bool CanHandleContainerRestock(Player player, Container container)
    {
        return player != null &&
               !player.m_isLoading &&
               container != null &&
               container.m_inventory != null &&
               CanMutateContainerDirectly(container, allowLocalWithoutZNetView: true) &&
               HasContainerPlayerAccess(player, container, flashGuardStone: false);
    }

    private static bool IsAreaContainerAllowed(Player player, Container container, Container? currentContainer, Vector3 playerPosition, float rangeSq, out float distanceSq)
    {
        if (!IsAreaContainerCandidate(player, container, currentContainer, playerPosition, rangeSq, out distanceSq) ||
            !CanMutateContainerDirectly(container) ||
            !HasContainerPlayerAccess(player, container, flashGuardStone: true))
        {
            return false;
        }

        if (container.m_piece != null && !container.m_piece.IsPlacedByPlayer())
        {
            return false;
        }

        return HasExternalMultiUserChestActive ||
               (IsBuiltInMultiUserChestEnabled &&
                IsBuiltInMultiUserContainerEligible(container)) ||
               !IsContainerInUse(container);
    }

    private static bool IsAreaContainerCandidate(Player player, Container container, Container? currentContainer, Vector3 playerPosition, float rangeSq, out float distanceSq)
    {
        distanceSq = float.MaxValue;
        if (player == null || container == null || container == currentContainer || container.m_inventory == null || container.m_nview == null)
        {
            return false;
        }

        distanceSq = (container.transform.position - playerPosition).sqrMagnitude;
        if (distanceSq > rangeSq)
        {
            return false;
        }

        return container.GetComponent<TombStone>() == null &&
               container.GetComponentInParent<TombStone>() == null &&
               container.m_nview.GetComponent<Player>() == null &&
               container.transform.root.GetComponentInChildren<Ship>() == null;
    }

    private static bool HasContainerPlayerAccess(Player player, Container container, bool flashGuardStone)
    {
        if (player == null || container == null)
        {
            return false;
        }

        if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, flashGuardStone, false))
        {
            return false;
        }

        return container.CheckAccess(player.GetPlayerID());
    }

    private static bool IsContainerInUse(Container container)
    {
        if (container == null)
        {
            return true;
        }

        if (container.IsInUse() || container.m_wagon != null && container.m_wagon.InUse())
        {
            return true;
        }

        ZDO? zdo = container.m_nview != null ? container.m_nview.GetZDO() : null;
        return zdo != null && zdo.GetInt("InUse", 0) == 1;
    }

    private static ContainerAccessMode GetContainerAccessMode(Container container, bool allowLocalWithoutZNetView = false)
    {
        if (container == null)
        {
            return ContainerAccessMode.Unavailable;
        }

        if (container.m_nview == null || !container.m_nview.IsValid())
        {
            return allowLocalWithoutZNetView
                ? ContainerAccessMode.DirectOwner
                : ContainerAccessMode.Unavailable;
        }

        if (container.m_nview.IsOwner())
        {
            return ContainerAccessMode.DirectOwner;
        }

        return CanUseMultiUserChestRemote(container)
            ? ContainerAccessMode.MultiUserChestRemote
            : ContainerAccessMode.Unavailable;
    }

    private static bool CanMutateContainerDirectly(Container container, bool allowLocalWithoutZNetView = false) =>
        GetContainerAccessMode(container, allowLocalWithoutZNetView) == ContainerAccessMode.DirectOwner;

    private static bool CanProcessContainerSortRpc(Container container, long sender, long requesterPlayerId)
    {
        return sender != 0L &&
               requesterPlayerId != 0L &&
               CanUseMultiUserChestRemote(container) &&
               IsRpcSenderForPlayer(sender, requesterPlayerId) &&
               CanMutateContainerDirectly(container) &&
               container.CheckAccess(requesterPlayerId);
    }

    private static bool CanUseMultiUserChestRemote(Container container)
    {
        if (container == null || IsMultiUserChestIgnored(container))
        {
            return false;
        }

        return HasExternalMultiUserChestActive ||
               IsBuiltInMultiUserChestEnabled &&
               IsBuiltInMultiUserContainerEligible(container);
    }

    private static bool IsRpcSenderForPlayer(long sender, long requesterPlayerId)
    {
        return TryGetRpcSenderPlayer(sender, requesterPlayerId, out _);
    }

    private static bool TryGetRpcSenderPlayer(
        long sender,
        long requesterPlayerId,
        out Player? requester)
    {
        requester = null;
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player == null ||
                player.GetPlayerID() != requesterPlayerId ||
                player.m_nview == null ||
                !player.m_nview.IsValid())
            {
                continue;
            }

            ZDO? playerZdo = player.m_nview.GetZDO();
            if (playerZdo != null && playerZdo.GetOwner() == sender)
            {
                requester = player;
                return true;
            }
        }

        return false;
    }

    private static bool IsMultiUserChestIgnored(Container container)
    {
        ZDO? zdo = container?.m_nview != null ? container.m_nview.GetZDO() : null;
        return zdo != null && zdo.GetBool(MultiUserChestIgnoreZdoKey, false);
    }

}
