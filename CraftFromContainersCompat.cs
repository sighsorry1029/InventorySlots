using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Type? CraftFromContainersType =>
        Chainloader.PluginInfos.TryGetValue(CraftFromContainersGuid, out var plugin) && plugin.Instance != null
            ? plugin.Instance.GetType()
            : null;

    private static MethodInfo? _craftFromContainersNearby;
    private static MethodInfo? _craftFromContainersAllowed;
    private static ConfigEntry<bool>? _craftFromContainersEnabled;
    private static bool _craftFromContainersResolved;

    private static bool HasCraftFromContainersCraftingSource()
    {
        Type? type = CraftFromContainersType;
        if (type == null) return false;
        if (!_craftFromContainersResolved)
        {
            _craftFromContainersNearby = AccessTools.Method(type, "GetNearbyContainers", new[] { typeof(Vector3) });
            _craftFromContainersAllowed = AccessTools.Method(type, "AllowByKey", Type.EmptyTypes);
            _craftFromContainersEnabled = AccessTools.Field(type, "modEnabled")?.GetValue(null) as ConfigEntry<bool>;
            _craftFromContainersResolved = true;
        }

        return _craftFromContainersNearby != null && _craftFromContainersAllowed != null &&
               _craftFromContainersEnabled != null;
    }

    private static bool TryGetCraftFromContainersAvailableAmount(string sharedName, out int amount)
    {
        amount = 0;
        if (!HasCraftFromContainersCraftingSource()) return false;

        // Use CFC's own filtering (keys, range, access, wards and occupied chests).
        if (!_craftFromContainersEnabled!.Value ||
            _craftFromContainersAllowed!.Invoke(null, Array.Empty<object>()) is not true) return true;

        Player? player = Player.m_localPlayer;
        if (player == null) return true;
        if (_craftFromContainersNearby!.Invoke(null, new object[] { player.transform.position }) is IEnumerable<Container> containers)
        {
            HashSet<Inventory> counted = new();
            foreach (Container container in containers)
            {
                if (container == null) continue;
                Inventory inventory = container.GetInventory();
                if (inventory == null || !counted.Add(inventory)) continue;
                int count = inventory.CountItems(sharedName, -1, true);
                amount = (int)Math.Min(int.MaxValue, (long)amount + Math.Max(0, count));
            }
        }
        return true;
    }

}
