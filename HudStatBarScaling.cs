using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float HudStatBarPixelsPer25 = 32f;
    private const float HudAdrenalineBarPixelsPer25 = HudStatBarPixelsPer25 * 2f;
    private const float HudStatBarLogBaseline = 100f;

    internal static void ApplyHudHealthBarLengthScaling(ref float size) =>
        ApplyHudStatBarLengthScaling(ref size, HudStatBarPixelsPer25);

    internal static void ApplyHudStaminaBarLengthScaling(ref float size) =>
        ApplyHudStatBarLengthScaling(ref size, HudStatBarPixelsPer25);

    internal static void ApplyHudEitrBarLengthScaling(ref float size) =>
        ApplyHudStatBarLengthScaling(ref size, HudStatBarPixelsPer25);

    internal static void ApplyHudAdrenalineBarLengthScaling(ref float size) =>
        ApplyHudStatBarLengthScaling(ref size, HudAdrenalineBarPixelsPer25);

    internal static void ApplyHudFoodBarLengthScaling(Hud hud, Player player)
    {
        if (!ShouldUseLogarithmicHudStatBarLengthScaling() || hud == null || player == null)
        {
            return;
        }

        float baseFoodSize = GetScaledHudStatBarLength(player.GetBaseFoodHP(), HudStatBarPixelsPer25);
        float maxHealthSize = Mathf.Ceil(GetScaledHudStatBarLength(player.GetMaxHealth(), HudStatBarPixelsPer25));
        hud.m_foodBaseBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseFoodSize);
        hud.m_foodBarRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxHealthSize);
    }

    private static void ApplyHudStatBarLengthScaling(ref float size, float pixelsPer25)
    {
        if (!ShouldUseLogarithmicHudStatBarLengthScaling() || size <= 0f || pixelsPer25 <= 0f)
        {
            return;
        }

        float value = size * 25f / pixelsPer25;
        size = GetScaledHudStatBarLength(value, pixelsPer25);
    }

    private static float GetScaledHudStatBarLength(float value, float pixelsPer25) =>
        GetLogarithmicHudStatBarValue(value) / 25f * pixelsPer25;

    private static float GetLogarithmicHudStatBarValue(float value)
    {
        if (value <= HudStatBarLogBaseline)
        {
            return Mathf.Max(0f, value);
        }

        return HudStatBarLogBaseline +
               HudStatBarLogBaseline * Mathf.Log(1f + (value - HudStatBarLogBaseline) / HudStatBarLogBaseline);
    }

    private static bool ShouldUseLogarithmicHudStatBarLengthScaling() =>
        _playerStatBarLengthScaling != null &&
        _playerStatBarLengthScaling.Value == PlayerStatBarLengthScaling.Logarithmic;
}

[HarmonyPatch(typeof(Hud), nameof(Hud.SetHealthBarSize))]
internal static class HudHealthBarLengthScalingPatch
{
    private static void Prefix(ref float size)
    {
        InventorySlotsPlugin.ApplyHudHealthBarLengthScaling(ref size);
    }
}

[HarmonyPatch(typeof(Hud), nameof(Hud.SetStaminaBarSize))]
internal static class HudStaminaBarLengthScalingPatch
{
    private static void Prefix(ref float size)
    {
        InventorySlotsPlugin.ApplyHudStaminaBarLengthScaling(ref size);
    }
}

[HarmonyPatch(typeof(Hud), nameof(Hud.SetEitrBarSize))]
internal static class HudEitrBarLengthScalingPatch
{
    private static void Prefix(ref float size)
    {
        InventorySlotsPlugin.ApplyHudEitrBarLengthScaling(ref size);
    }
}

[HarmonyPatch(typeof(Hud), nameof(Hud.SetAdrenalineBarSize))]
internal static class HudAdrenalineBarLengthScalingPatch
{
    private static void Prefix(ref float size)
    {
        InventorySlotsPlugin.ApplyHudAdrenalineBarLengthScaling(ref size);
    }
}

[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateFood))]
internal static class HudFoodBarLengthScalingPatch
{
    private static void Postfix(Hud __instance, Player player)
    {
        InventorySlotsPlugin.ApplyHudFoodBarLengthScaling(__instance, player);
    }
}
