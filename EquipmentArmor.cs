using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static float GetProjectedEquipmentArmor(Player player)
    {
        if (player == null)
        {
            return 0f;
        }

        // Native armor slots already contribute through Player.GetBodyArmor.
        // Accessory references are read live so native equips do not depend on
        // the custom equipment cache being invalidated first.
        return GetCachedCustomEquipmentArmor(player) +
               GetBuiltInAccessoryArmor(player, "utility") +
               GetBuiltInAccessoryArmor(player, "trinket");
    }

    private static float GetBuiltInAccessoryArmor(Player player, string slotId)
    {
        if (!TryGetSlotById(slotId, out SlotDefinition? slot) || slot == null || !slot.ApplyArmor)
        {
            return 0f;
        }

        ItemData? item = GetBuiltInEquipmentSlotItem(player, slot);
        if (item == null || !item.m_equipped || IsInventorySlotsCustomEquipped(item) ||
            !ContainsExactItemReference(((Humanoid)player).GetInventory(), item))
        {
            return 0f;
        }

        return GetSlotItemArmor(item, slot);
    }

    private static float GetSlotItemArmor(ItemData? item, SlotDefinition? slot)
    {
        return slot?.ApplyArmor == true && item?.m_shared != null && item.m_shared.m_armor > 0f
            ? item.GetArmor()
            : 0f;
    }
}
