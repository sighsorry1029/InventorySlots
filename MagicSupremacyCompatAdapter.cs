using System;
using System.Reflection;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class MagicSupremacyApi
    {
        private readonly MethodInfo _getMatchingSlotDefinitionMethod;
        private readonly MethodInfo _getDefinitionBySlotIdMethod;
        private readonly MethodInfo _getEquippedItemMethod;
        private readonly MethodInfo _setEquippedItemMethod;
        private readonly MethodInfo _getOrCreateItemGuidMethod;
        private readonly MethodInfo _setSavedEquippedGuidMethod;
        private readonly MethodInfo _clearSavedEquippedGuidMethod;
        private readonly FieldInfo _slotIdField;
        private object? _beltDefinition;

        private MagicSupremacyApi(
            MethodInfo getMatchingSlotDefinitionMethod,
            MethodInfo getDefinitionBySlotIdMethod,
            MethodInfo getEquippedItemMethod,
            MethodInfo setEquippedItemMethod,
            MethodInfo getOrCreateItemGuidMethod,
            MethodInfo setSavedEquippedGuidMethod,
            MethodInfo clearSavedEquippedGuidMethod,
            FieldInfo slotIdField)
        {
            _getMatchingSlotDefinitionMethod = getMatchingSlotDefinitionMethod;
            _getDefinitionBySlotIdMethod = getDefinitionBySlotIdMethod;
            _getEquippedItemMethod = getEquippedItemMethod;
            _setEquippedItemMethod = setEquippedItemMethod;
            _getOrCreateItemGuidMethod = getOrCreateItemGuidMethod;
            _setSavedEquippedGuidMethod = setSavedEquippedGuidMethod;
            _clearSavedEquippedGuidMethod = clearSavedEquippedGuidMethod;
            _slotIdField = slotIdField;
        }

        public static bool TryCreate(Assembly assembly, out MagicSupremacyApi? api, out string detail)
        {
            api = null;
            Type? customSlotSystemType = assembly.GetType("Magic_Supremacy.CustomSlotSystem");
            Type? customSlotDefinitionType = customSlotSystemType?.GetNestedType("CustomSlotDefinition", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo? getMatchingSlotDefinitionMethod = customSlotSystemType?.GetMethod(
                "GetMatchingSlotDefinition",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ItemData) },
                null);
            MethodInfo? getDefinitionBySlotIdMethod = customSlotSystemType?.GetMethod(
                "GetDefinitionBySlotId",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo? getEquippedItemMethod = customSlotSystemType?.GetMethod(
                "GetEquippedItem",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Humanoid), typeof(string) },
                null);
            MethodInfo? setEquippedItemMethod = customSlotSystemType?.GetMethod(
                "SetEquippedItem",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Humanoid), typeof(string), typeof(ItemData) },
                null);
            MethodInfo? getOrCreateItemGuidMethod = customSlotSystemType?.GetMethod(
                "GetOrCreateItemGuid",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(ItemData) },
                null);
            MethodInfo? setSavedEquippedGuidMethod = FindMethod(customSlotSystemType, "SetSavedEquippedGuid", 3);
            MethodInfo? clearSavedEquippedGuidMethod = FindMethod(customSlotSystemType, "ClearSavedEquippedGuid", 2);
            FieldInfo? slotIdField = customSlotDefinitionType?.GetField("SlotId", BindingFlags.Public | BindingFlags.Instance);

            if (customSlotSystemType == null ||
                customSlotDefinitionType == null ||
                getMatchingSlotDefinitionMethod == null ||
                getDefinitionBySlotIdMethod == null ||
                getEquippedItemMethod == null ||
                setEquippedItemMethod == null ||
                getOrCreateItemGuidMethod == null ||
                setSavedEquippedGuidMethod == null ||
                clearSavedEquippedGuidMethod == null ||
                slotIdField == null)
            {
                detail = "Magic_Supremacy.CustomSlotSystem belt slot methods were not found";
                return false;
            }

            api = new MagicSupremacyApi(
                getMatchingSlotDefinitionMethod,
                getDefinitionBySlotIdMethod,
                getEquippedItemMethod,
                setEquippedItemMethod,
                getOrCreateItemGuidMethod,
                setSavedEquippedGuidMethod,
                clearSavedEquippedGuidMethod,
                slotIdField);
            detail = "";
            return true;
        }

        public bool IsBeltItem(ItemData? item)
        {
            if (item == null)
            {
                return false;
            }

            try
            {
                object? definition = _getMatchingSlotDefinitionMethod.Invoke(null, new object[] { item });
                return IsBeltDefinition(definition);
            }
            catch
            {
                return false;
            }
        }

        public bool IsBeltEquipped(Player player, ItemData item)
        {
            if (player == null || item == null)
            {
                return false;
            }

            try
            {
                object? current = _getEquippedItemMethod.Invoke(null, new object[] { player, MagicSupremacyNativeBeltSlotId });
                return ReferenceEquals(current, item);
            }
            catch
            {
                return false;
            }
        }

        public void SyncBelt(Player player, ItemData item)
        {
            if (player == null || item == null || !IsBeltItem(item))
            {
                return;
            }

            try
            {
                object? definition = GetBeltDefinition();
                if (definition == null)
                {
                    return;
                }

                object? guid = _getOrCreateItemGuidMethod.Invoke(null, new object[] { item });
                if (guid is string guidString && !string.IsNullOrWhiteSpace(guidString))
                {
                    _setSavedEquippedGuidMethod.Invoke(null, new object[] { player, definition, guidString });
                }

                _setEquippedItemMethod.Invoke(null, new object[] { player, MagicSupremacyNativeBeltSlotId, item });
            }
            catch (Exception)
            {
            }
        }

        public void ClearBeltIfCurrent(Player player, ItemData item)
        {
            if (player == null || item == null)
            {
                return;
            }

            try
            {
                object? current = _getEquippedItemMethod.Invoke(null, new object[] { player, MagicSupremacyNativeBeltSlotId });
                if (current != null && !ReferenceEquals(current, item))
                {
                    return;
                }

                object? definition = GetBeltDefinition();
                _setEquippedItemMethod.Invoke(null, new object?[] { player, MagicSupremacyNativeBeltSlotId, null });
                if (definition != null)
                {
                    _clearSavedEquippedGuidMethod.Invoke(null, new object[] { player, definition });
                }
            }
            catch (Exception)
            {
            }
        }

        private object? GetBeltDefinition()
        {
            if (_beltDefinition != null)
            {
                return _beltDefinition;
            }

            _beltDefinition = _getDefinitionBySlotIdMethod.Invoke(null, new object[] { MagicSupremacyNativeBeltSlotId });
            return IsBeltDefinition(_beltDefinition) ? _beltDefinition : null;
        }

        private bool IsBeltDefinition(object? definition)
        {
            if (definition == null)
            {
                return false;
            }

            try
            {
                return _slotIdField.GetValue(definition) is string slotId &&
                       string.Equals(slotId, MagicSupremacyNativeBeltSlotId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo? FindMethod(Type? type, string name, int parameterCount)
        {
            if (type == null)
            {
                return null;
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.Name == name && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
