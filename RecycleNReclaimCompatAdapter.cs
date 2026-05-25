using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class RecycleNReclaimApi
    {
        private readonly PropertyInfo _recyclingTabButtonHolderProperty;
        private readonly MethodInfo _inRecycleTabMethod;
        private readonly MethodInfo? _updateRecipeMethod;
        private readonly FieldInfo? _recyclingAnalysisContextsField;
        private readonly PropertyInfo? _recyclingImpedimentsProperty;
        private readonly PropertyInfo? _entriesProperty;
        private readonly FieldInfo? _entryRecipeItemDataField;
        private readonly FieldInfo? _entryAmountField;

        private RecycleNReclaimApi(
            PropertyInfo recyclingTabButtonHolderProperty,
            MethodInfo inRecycleTabMethod,
            MethodInfo? updateRecipeMethod,
            FieldInfo? recyclingAnalysisContextsField,
            PropertyInfo? recyclingImpedimentsProperty,
            PropertyInfo? entriesProperty,
            FieldInfo? entryRecipeItemDataField,
            FieldInfo? entryAmountField)
        {
            _recyclingTabButtonHolderProperty = recyclingTabButtonHolderProperty;
            _inRecycleTabMethod = inRecycleTabMethod;
            _updateRecipeMethod = updateRecipeMethod;
            _recyclingAnalysisContextsField = recyclingAnalysisContextsField;
            _recyclingImpedimentsProperty = recyclingImpedimentsProperty;
            _entriesProperty = entriesProperty;
            _entryRecipeItemDataField = entryRecipeItemDataField;
            _entryAmountField = entryAmountField;
        }

        public static bool TryCreate(Assembly assembly, out RecycleNReclaimApi? api, out string detail)
        {
            api = null;
            Type? pluginType = assembly.GetType("Recycle_N_Reclaim.Recycle_N_ReclaimPlugin");
            Type? tabHolderType = assembly.GetType("Recycle_N_Reclaim.GamePatches.UI.StationRecyclingTabHolder");
            PropertyInfo? holderProperty = pluginType?.GetProperty("RecyclingTabButtonHolder", BindingFlags.Public | BindingFlags.Static);
            MethodInfo? inRecycleTabMethod = tabHolderType?.GetMethod("InRecycleTab", BindingFlags.Public | BindingFlags.Instance);
            if (holderProperty == null || tabHolderType == null || inRecycleTabMethod == null)
            {
                detail = "Recycle_N_Reclaim tab holder API was not found";
                return false;
            }

            FieldInfo? contextsField = tabHolderType.GetField("_recyclingAnalysisContexts", BindingFlags.NonPublic | BindingFlags.Instance);
            Type? contextType = assembly.GetType("Recycle_N_Reclaim.GamePatches.Recycling.RecyclingAnalysisContext");
            Type? yieldEntryType = contextType?.GetNestedType("ReclaimingYieldEntry", BindingFlags.Public);
            PropertyInfo? recyclingImpedimentsProperty = contextType?.GetProperty("RecyclingImpediments", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? entriesProperty = contextType?.GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance);
            api = new RecycleNReclaimApi(
                holderProperty,
                inRecycleTabMethod,
                tabHolderType.GetMethod("UpdateRecipe", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Player), typeof(float) }, null),
                contextsField,
                recyclingImpedimentsProperty,
                entriesProperty,
                yieldEntryType?.GetField("RecipeItemData", BindingFlags.Public | BindingFlags.Instance),
                yieldEntryType?.GetField("Amount", BindingFlags.Public | BindingFlags.Instance));
            detail = "";
            return true;
        }

        public bool IsRecycleTabActive()
        {
            object? holder = GetHolder();
            if (holder == null)
            {
                return false;
            }

            try
            {
                return _inRecycleTabMethod.Invoke(holder, null) is true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryRefreshSelectedRecipeUi()
        {
            if (_updateRecipeMethod == null || Player.m_localPlayer == null)
            {
                return false;
            }

            object? holder = GetHolder();
            if (holder == null)
            {
                return false;
            }

            try
            {
                _updateRecipeMethod.Invoke(holder, new object[] { Player.m_localPlayer, 0f });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetRecyclingImpedimentCount(int index, out int count)
        {
            count = 0;
            if (_recyclingImpedimentsProperty == null || !TryGetContext(index, out object? context))
            {
                return false;
            }

            try
            {
                if (_recyclingImpedimentsProperty.GetValue(context, null) is not ICollection impediments)
                {
                    return false;
                }

                count = impediments.Count;
                return true;
            }
            catch
            {
                count = 0;
                return false;
            }
        }

        public bool TryGetReclaimSummary(int index, List<string> impediments, List<RecycleNReclaimYieldTextEntry> yields)
        {
            impediments.Clear();
            yields.Clear();
            if (!TryGetContext(index, out object? context))
            {
                return false;
            }

            try
            {
                if (_recyclingImpedimentsProperty?.GetValue(context, null) is IEnumerable rawImpediments)
                {
                    foreach (object? impediment in rawImpediments)
                    {
                        string text = impediment?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            impediments.Add(text);
                        }
                    }
                }

                if (_entriesProperty?.GetValue(context, null) is not IEnumerable entries ||
                    _entryRecipeItemDataField == null ||
                    _entryAmountField == null)
                {
                    return true;
                }

                foreach (object? entry in entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    int amount = _entryAmountField.GetValue(entry) is int rawAmount ? rawAmount : 0;
                    if (amount <= 0)
                    {
                        continue;
                    }

                    if (_entryRecipeItemDataField.GetValue(entry) is not ItemDrop.ItemData item || item.m_shared == null)
                    {
                        continue;
                    }

                    yields.Add(new RecycleNReclaimYieldTextEntry(item.m_shared.m_name ?? "", amount, item));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetContextSignature()
        {
            object? holder = GetHolder();
            if (holder == null || _recyclingAnalysisContextsField == null)
            {
                return "no-context";
            }

            try
            {
                if (_recyclingAnalysisContextsField.GetValue(holder) is not IList contexts)
                {
                    return "no-list";
                }

                unchecked
                {
                    int hash = 17;
                    for (int i = 0; i < contexts.Count; i++)
                    {
                        object? context = contexts[i];
                        hash = hash * 31 + (context != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(context) : 0);
                        if (_recyclingImpedimentsProperty?.GetValue(context, null) is ICollection impediments)
                        {
                            hash = hash * 31 + impediments.Count;
                        }
                    }

                    return $"{contexts.Count}:{hash}";
                }
            }
            catch
            {
                return "error";
            }
        }

        private object? GetHolder()
        {
            try
            {
                return _recyclingTabButtonHolderProperty.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private bool TryGetContext(int index, out object? context)
        {
            context = null;
            if (index < 0 || _recyclingAnalysisContextsField == null)
            {
                return false;
            }

            object? holder = GetHolder();
            if (holder == null)
            {
                return false;
            }

            try
            {
                if (_recyclingAnalysisContextsField.GetValue(holder) is not IList contexts || index >= contexts.Count)
                {
                    return false;
                }

                context = contexts[index];
                return context != null;
            }
            catch
            {
                context = null;
                return false;
            }
        }
    }
}
