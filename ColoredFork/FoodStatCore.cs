namespace ColoredFork;

internal enum FoodStat
{
    None,
    Health,
    Stamina,
    Eitr
}

internal static class FoodStatCore
{
    public static bool TryGetDominant(float health, float stamina, float eitr, out FoodStat stat)
    {
        stat = FoodStat.None;
        if (health <= 0f && stamina <= 0f && eitr <= 0f)
        {
            return false;
        }

        if (health >= stamina && health >= eitr)
        {
            stat = FoodStat.Health;
            return true;
        }

        if (stamina >= health && stamina >= eitr)
        {
            stat = FoodStat.Stamina;
            return true;
        }

        stat = FoodStat.Eitr;
        return true;
    }
}
