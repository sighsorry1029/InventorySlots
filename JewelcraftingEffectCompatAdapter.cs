using System;
using System.Reflection;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void RefreshExternalEquipmentEffects(Player? player)
    {
        ResetEpicLootEquipmentEffectCache(player);
        RecalculateJewelcraftingEffects(player);
    }

    private static void RecalculateJewelcraftingEffects(Player? player)
    {
        if (player == null || IsUnityNull(player) || player.m_isLoading || !HasJewelcraftingActive)
        {
            return;
        }

        if (TryGetJewelcraftingEffectApi(out JewelcraftingEffectApi? api) && api != null)
        {
            api.Recalculate(player);
        }
    }

    private static bool TryGetJewelcraftingEffectApi(out JewelcraftingEffectApi? api)
    {
        const string capability = "Jewelcrafting effects";
        return TryGetCompatApi(
            JewelcraftingGuid,
            capability,
            CompatRuntime.JewelcraftingEffect,
            JewelcraftingEffectApi.TryCreate,
            "Jewelcrafting equipment effect refresh disabled",
            out api);
    }

    private sealed class JewelcraftingEffectApi
    {
        private readonly MethodInfo _calculateEffectsMethod;
        private bool _warningLogged;

        private JewelcraftingEffectApi(MethodInfo calculateEffectsMethod)
        {
            _calculateEffectsMethod = calculateEffectsMethod;
        }

        public static bool TryCreate(Assembly assembly, out JewelcraftingEffectApi? api, out string detail)
        {
            api = null;
            Type? trackEquipmentChangesType = assembly.GetType("Jewelcrafting.GemEffects.TrackEquipmentChanges");
            MethodInfo? calculateEffectsMethod = trackEquipmentChangesType?.GetMethod(
                "CalculateEffects",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Player) },
                null);

            if (calculateEffectsMethod == null)
            {
                detail = "TrackEquipmentChanges.CalculateEffects(Player) was not found";
                return false;
            }

            api = new JewelcraftingEffectApi(calculateEffectsMethod);
            detail = "";
            return true;
        }

        public void Recalculate(Player player)
        {
            if (player == null || IsUnityNull(player))
            {
                return;
            }

            try
            {
                _calculateEffectsMethod.Invoke(null, new object[] { player });
            }
            catch (Exception ex)
            {
                if (_warningLogged)
                {
                    return;
                }

                _warningLogged = true;
                Log.LogWarning($"Failed to recalculate Jewelcrafting equipment effects: {ex.GetBaseException().Message}");
            }
        }
    }
}
