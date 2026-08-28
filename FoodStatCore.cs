namespace InventorySlots;

internal enum FoodStat
{
    None,
    Health,
    Stamina,
    Eitr
}

internal static class FoodStatCore
{
    public static bool TryGetSlotForkDominant(
        bool isConsumable,
        float health,
        float stamina,
        float eitr,
        out FoodStat stat)
    {
        if (!isConsumable)
        {
            stat = FoodStat.None;
            return false;
        }

        return TryGetDominant(health, stamina, eitr, out stat);
    }

    public static bool TryGetDominant(float health, float stamina, float eitr, out FoodStat stat)
    {
        stat = FoodStat.None;
        if (eitr > 0f)
        {
            stat = FoodStat.Eitr;
            return true;
        }

        float positiveHealth = health > 0f ? health : 0f;
        float positiveStamina = stamina > 0f ? stamina : 0f;
        if (positiveHealth <= 0f && positiveStamina <= 0f)
        {
            return false;
        }

        stat = positiveHealth > positiveStamina
            ? FoodStat.Health
            : FoodStat.Stamina;
        return true;
    }
}
