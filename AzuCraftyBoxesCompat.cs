using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static int GetAzuCraftyBoxesAvailableCraftingRequirementAmount(Requirement requirement, string sharedName)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            requirement.m_resItem == null ||
            !TryGetAzuCraftyBoxesApi(out AzuCraftyBoxesApi? api) ||
            api == null ||
            api.ShouldPrevent())
        {
            return 0;
        }

        try
        {
            IEnumerable? containers = api.GetNearbyContainers(player, api.GetRange());
            if (containers == null)
            {
                return 0;
            }

            string prefabName = GetPrefabNameForAzuCraftyBoxes(requirement.m_resItem.name);
            bool leaveOne = api.GetLeaveOne();
            int total = 0;
            foreach (object? container in containers)
            {
                if (container == null)
                {
                    continue;
                }

                string containerPrefab = api.InvokeString(container, "GetPrefabName");
                if (string.IsNullOrWhiteSpace(containerPrefab) || !api.CanItemBePulled(containerPrefab, prefabName))
                {
                    continue;
                }

                int count = api.InvokeInt(container, "ItemCount", sharedName);
                if (leaveOne && count > 0)
                {
                    count--;
                }

                if (count > 0)
                {
                    total += count;
                }
            }

            return total;
        }
        catch (Exception ex)
        {
            Log.LogDebug($"AzuCraftyBoxes compatibility count failed: {ex.Message}");
            return 0;
        }
    }

    private static bool TryGetAzuCraftyBoxesApi(out AzuCraftyBoxesApi? api)
    {
        const string capability = "AzuCraftyBoxes";
        return TryGetCompatApi(
            AzuCraftyBoxesGuid,
            capability,
            CompatRuntime.AzuCraftyBoxes,
            AzuCraftyBoxesApi.TryCreate,
            "AzuCraftyBoxes compatibility disabled",
            out api);
    }

    private static bool TryFormatAzuCraftyBoxesRequirementAmount(int available, int required, out string text)
    {
        text = "";
        if (!TryGetAzuCraftyBoxesApi(out AzuCraftyBoxesApi? api) || api == null || api.ShouldPrevent())
        {
            return false;
        }

        return api.TryFormatRequirementAmount(available, required, out text);
    }

    private static bool TryGetAzuCraftyBoxesRequirementFlashColor(out Color color)
    {
        color = default;
        if (!TryGetAzuCraftyBoxesApi(out AzuCraftyBoxesApi? api) || api == null || api.ShouldPrevent())
        {
            return false;
        }

        return api.TryGetRequirementFlashColor(out color);
    }

    private static string GetPrefabNameForAzuCraftyBoxes(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        int index = name.IndexOfAny(new[] { '(', ' ' });
        return index >= 0 ? name.Substring(0, index) : name;
    }
}
