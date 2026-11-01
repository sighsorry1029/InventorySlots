using UnityEngine;

public class Recipe { }
public class Requirement { public Item m_resItem = new(); }
public class Item { public ItemData m_itemData = new(); }
public class ItemData { public Shared m_shared = new(); }
public class Shared { public string m_name = "$item_resin"; }
namespace UnityEngine { public static class Time { public static float unscaledTime; } }
namespace InventorySlots {
public sealed partial class InventorySlotsPlugin {
    public static bool Azu;
    private const float CraftingRequirementAvailabilityCacheSeconds = 0.2f;
    private record struct CraftingRequirementAvailabilityCacheEntry(int Amount, float ExpiresAt);
    private static class CraftingRequirements { public static Dictionary<string, CraftingRequirementAvailabilityCacheEntry> AvailabilityCache = new(); }
    public static class ValheimPlusCompat {
        public static bool CraftingActive;
        public static bool TryGetCraftingStock(int bag, Requirement requirement, Player player, out int stock) { stock = bag + 500; return true; }
    }
    private static bool HasAzuCraftyBoxesCraftingSource() => Azu;
    private static int GetAzuCraftyBoxesAvailableCraftingRequirementAmount(Requirement r, string name) => 100;
    private static string GetCraftingRequirementAvailabilityCacheKey(Player p, Requirement r, string name) => name;
    private static int GetPathOfValheimanCraftingStock(Player p, string name, Recipe? recipe, int amount) => amount;
    public static void Clear() => CraftingRequirements.AvailabilityCache.Clear();
    public static int Display() => GetAvailableCraftingRequirementAmount(new Requirement(), null);
    private static int GetAvailableCraftingRequirementAmount(Requirement requirement, Recipe? recipe)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || requirement.m_resItem == null || requirement.m_resItem.m_itemData?.m_shared == null)
        {
            return 0;
        }

        string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
        if (string.IsNullOrWhiteSpace(sharedName))
        {
            return 0;
        }

        // Preserve Azu's existing policy, including a valid zero/leave-one result.
        // Never sum two providers' views of the same chest contents.
        int source = HasAzuCraftyBoxesCraftingSource() ? 1 : HasCraftFromContainersCraftingSource() ? 3 : ValheimPlusCompat.CraftingActive ? 2 : 0;
        string cacheKey = GetCraftingRequirementAvailabilityCacheKey(player, requirement, sharedName) + "|" + source;
        float now = Time.unscaledTime;
        if (CraftingRequirements.AvailabilityCache.TryGetValue(cacheKey, out CraftingRequirementAvailabilityCacheEntry cached) &&
            cached.ExpiresAt >= now)
        {
            return GetPathOfValheimanCraftingStock(player, sharedName, recipe, cached.Amount);
        }

        Inventory inventory = player.GetInventory();
        int amount = inventory?.CountItems(sharedName, -1, true) ?? 0;
        if (source == 3 && TryGetCraftFromContainersAvailableAmount(sharedName, out int nearbyAmount))
            amount = (int)Math.Min(int.MaxValue, (long)amount + nearbyAmount);
        else if (source == 1)
            amount += GetAzuCraftyBoxesAvailableCraftingRequirementAmount(requirement, sharedName);
        else if (source == 2 && ValheimPlusCompat.TryGetCraftingStock(amount, requirement, player, out int stock))
            amount = stock;
        CraftingRequirements.AvailabilityCache[cacheKey] = new CraftingRequirementAvailabilityCacheEntry(
            amount,
            now + CraftingRequirementAvailabilityCacheSeconds);
        return GetPathOfValheimanCraftingStock(player, sharedName, recipe, amount);
    }
}}
