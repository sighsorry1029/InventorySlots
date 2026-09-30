namespace InventorySlots;

public enum FoodForkColorMode
{
    EitrFirst,
    Vanilla
}

internal enum FoodStat
{
    None,
    Health,
    Stamina,
    Eitr,
    Balanced
}

internal static class FoodStatCore
{
    public static bool TryGetSlotForkDominant(
        bool isConsumable,
        float health,
        float stamina,
        float eitr,
        out FoodStat stat,
        FoodForkColorMode mode = FoodForkColorMode.EitrFirst)
    {
        if (!isConsumable)
        {
            stat = FoodStat.None;
            return false;
        }

        if (mode != FoodForkColorMode.Vanilla)
        {
            return TryGetDominant(health, stamina, eitr, out stat);
        }

        stat = FoodStat.None;
        if (!(health > 0f || stamina > 0f || eitr > 0f)) return false;

        // Match InventoryGrid.UpdateGui, including strict half-value boundaries.
        // Balanced is a visible white fork, distinct from no food icon.
        stat = health < eitr / 2f && stamina < eitr / 2f ? FoodStat.Eitr
            : stamina < health / 2f ? FoodStat.Health
            : health < stamina / 2f ? FoodStat.Stamina
            : FoodStat.Balanced;
        return true;
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
