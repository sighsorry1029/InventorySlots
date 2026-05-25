using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static class InventoryDefinitionController
    {
        public static bool TryGetSlotAtGridPos(
            InventoryDefinitionRuntimeState state,
            Inventory inventory,
            Vector2i pos,
            int fixedRegularRows,
            out SlotDefinition? slot)
        {
            slot = null;
            int width = inventory.GetWidth();
            if (pos.y < fixedRegularRows)
            {
                return false;
            }

            int index = (pos.y - fixedRegularRows) * width + pos.x;
            if (index < 0 || index >= state.SlotDefinitions.Count)
            {
                return false;
            }

            slot = state.SlotDefinitions[index];
            return true;
        }

        public static bool TryGetSlotById(InventoryDefinitionRuntimeState state, string id, out SlotDefinition? slot)
        {
            slot = null;
            for (int i = 0; i < state.SlotDefinitions.Count; i++)
            {
                SlotDefinition candidate = state.SlotDefinitions[i];
                if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        public static List<SlotDefinition> GetCustomPanelSlots(InventoryDefinitionRuntimeState state)
        {
            if (state.CustomPanelSlotCacheVersion == state.SlotDefinitionVersion)
            {
                return state.CustomPanelSlotCache;
            }

            state.CustomPanelSlotCache.Clear();
            for (int i = 0; i < state.SlotDefinitions.Count; i++)
            {
                SlotDefinition slot = state.SlotDefinitions[i];
                if (slot.Kind != SlotKind.Quick)
                {
                    state.CustomPanelSlotCache.Add(slot);
                }
            }

            state.CustomPanelSlotCacheVersion = state.SlotDefinitionVersion;
            return state.CustomPanelSlotCache;
        }

        public static List<SlotDefinition> GetQuickPanelSlots(InventoryDefinitionRuntimeState state, int unlockedCount)
        {
            if (state.QuickPanelSlotCacheVersion == state.SlotDefinitionVersion &&
                state.QuickPanelSlotCacheUnlockedCount == unlockedCount)
            {
                return state.QuickPanelSlotCache;
            }

            state.QuickPanelSlotCache.Clear();
            for (int i = 0; i < state.SlotDefinitions.Count; i++)
            {
                SlotDefinition slot = state.SlotDefinitions[i];
                if (slot.Kind == SlotKind.Quick &&
                    slot.QuickSlotIndex >= 0 &&
                    slot.QuickSlotIndex < unlockedCount)
                {
                    state.QuickPanelSlotCache.Add(slot);
                }
            }

            state.QuickPanelSlotCacheVersion = state.SlotDefinitionVersion;
            state.QuickPanelSlotCacheUnlockedCount = unlockedCount;
            return state.QuickPanelSlotCache;
        }

        public static bool TryGetQuickSlotDefinition(InventoryDefinitionRuntimeState state, int quickSlotIndex, out SlotDefinition? slot)
        {
            if (state.QuickSlotDefinitionCacheVersion != state.SlotDefinitionVersion)
            {
                state.QuickSlotDefinitionCache.Clear();
                for (int i = 0; i < state.SlotDefinitions.Count; i++)
                {
                    SlotDefinition candidate = state.SlotDefinitions[i];
                    if (candidate.Kind == SlotKind.Quick && candidate.QuickSlotIndex >= 0)
                    {
                        state.QuickSlotDefinitionCache[candidate.QuickSlotIndex] = candidate;
                    }
                }

                state.QuickSlotDefinitionCacheVersion = state.SlotDefinitionVersion;
            }

            return state.QuickSlotDefinitionCache.TryGetValue(quickSlotIndex, out slot);
        }

        public static Vector2i GetSlotGridPos(InventoryDefinitionRuntimeState state, Inventory inventory, SlotDefinition slot, int fixedRegularRows)
        {
            int index = state.SlotDefinitions.IndexOf(slot);
            int width = inventory.GetWidth();
            return new Vector2i(index % width, fixedRegularRows + index / width);
        }

        public static int GetSlotTailRows(InventoryDefinitionRuntimeState state, int width)
        {
            if (state.SlotDefinitions.Count == 0)
            {
                return 0;
            }

            return Mathf.CeilToInt(state.SlotDefinitions.Count / (float)Mathf.Max(1, width));
        }

        public static void InvalidateCaches(InventoryDefinitionRuntimeState state)
        {
            unchecked
            {
                state.SlotDefinitionVersion++;
            }

            state.CustomPanelSlotCacheVersion = -1;
            state.QuickPanelSlotCacheVersion = -1;
            state.QuickPanelSlotCacheUnlockedCount = -1;
            state.QuickSlotDefinitionCacheVersion = -1;
            state.CustomPanelSlotCache.Clear();
            state.QuickPanelSlotCache.Clear();
            state.QuickSlotDefinitionCache.Clear();
        }
    }
}
