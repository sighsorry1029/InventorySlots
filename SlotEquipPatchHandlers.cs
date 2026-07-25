using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static bool ShouldAllowHumanoidUseItem(Humanoid humanoid, Inventory inventory, ItemData item)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || humanoid != (Humanoid)player)
        {
            return true;
        }

        if (TryHandleDedicatedSlotUse(player, inventory, item, out bool allowVanilla))
        {
            return allowVanilla;
        }

        if (IsEquipableForInventorySlotsRouting(item))
        {
            return true;
        }

        if (item == null)
        {
            return true;
        }

        TryRouteInventoryUseToDedicatedSlot(player, inventory, item, out bool handled);
        return !handled;
    }

    internal static bool TryOverrideHumanoidEquipItem(Humanoid humanoid, ItemData item, ref bool result)
    {
        if (TryBlockJewelcraftingUtilityGemEquip(humanoid, item, ref result))
        {
            return true;
        }

        if (TryCompletePendingSlotEquip(humanoid, item, out bool pendingResult))
        {
            result = pendingResult;
            return true;
        }

        if (TryRouteHumanoidEquipToDedicatedSlot(humanoid, item))
        {
            result = true;
            return true;
        }

        return false;
    }

    internal static void OnHumanoidIsItemEquipped(Humanoid humanoid, ItemData item, ref bool result)
    {
        if (!result && humanoid == (Humanoid)Player.m_localPlayer && IsInventorySlotsCustomEquipped(item))
        {
            result = true;
        }
    }

    internal static bool PrepareHumanoidUnequipItem(Humanoid humanoid, ItemData item)
    {
        Player? player = Player.m_localPlayer;
        bool hasPendingUnequip = humanoid == (Humanoid)player && player != null && item != null && HasPendingSlotUnequipRequest(item);
        if (hasPendingUnequip)
        {
            SetSlotUnequipInProgress(true);
        }

        return hasPendingUnequip;
    }

    internal static void OnHumanoidUnequipItem(Humanoid humanoid, ItemData item, bool hasPendingUnequip)
    {
        try
        {
            Player? player = Player.m_localPlayer;
            if (humanoid != (Humanoid)player || player == null || item == null)
            {
                return;
            }

            if (TryCompletePendingSlotUnequip(player, item))
            {
                return;
            }

            if (CompleteSlotUnequipToInventory(player, item))
            {
                return;
            }

            if (HasInventorySlotsSlot(item))
            {
                ClearCustomEquipmentState(item);
                humanoid.SetupEquipment();
            }
        }
        finally
        {
            CompleteHumanoidUnequipItem(hasPendingUnequip);
        }
    }

    internal static void CompleteHumanoidUnequipItem(bool hasPendingUnequip)
    {
        if (hasPendingUnequip)
        {
            SetSlotUnequipInProgress(false);
        }
    }

    internal static void OnHumanoidUnequipAllItems(Humanoid humanoid)
    {
        if (!IsLocalPlayerHumanoid(humanoid, out Player? player) || player == null)
        {
            return;
        }

        if (ClearAllCustomEquipmentState(player))
        {
            bool wasSlotAutoEquipSuppressed = InventorySafety.SuppressSlotAutoEquip;
            SetSlotAutoEquipSuppressed(true);
            try
            {
                humanoid.UpdateEquipmentStatusEffects();
                UpdateCustomEquipmentVisuals(player);
            }
            finally
            {
                SetSlotAutoEquipSuppressed(wasSlotAutoEquipSuppressed);
            }
        }
    }

    internal static bool PreparePlayerDeathDropUnequip(Player player)
    {
        bool isLocalPlayer = player == Player.m_localPlayer;
        if (isLocalPlayer)
        {
            ClearPendingSlotActions();
            SetSlotAutoEquipSuppressed(true);
            UnequipCustomEquipmentForDeathDrop(player);
        }

        return isLocalPlayer;
    }

    internal static void CompletePlayerDeathDropUnequip(bool isLocalPlayer)
    {
        if (isLocalPlayer)
        {
            SetSlotAutoEquipSuppressed(false);
        }
    }
}
