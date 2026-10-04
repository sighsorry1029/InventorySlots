using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class AdventureBackpacksApi
    {
        private AdventureBackpacksApi()
        {
        }

        public static bool TryCreate(Assembly assembly, out AdventureBackpacksApi? api, out string detail)
        {
            api = null;
            if (!AdventureBackpacks.API.Client.ABAPIClient.IsAvailable)
            {
                detail = "AdventureBackpacks ABAPI not available";
                return false;
            }

            api = new AdventureBackpacksApi();
            detail = string.Empty;
            return true;
        }

        public bool IsBackpack(ItemData? item)
        {
            if (item == null)
            {
                return false;
            }

            return AdventureBackpacks.API.Client.ABAPIClient.IsBackpack(item);
        }

        public bool IsBackpackEquipped(Player player)
        {
            if (player == null)
            {
                return false;
            }

            return AdventureBackpacks.API.Client.ABAPIClient.IsBackpackEquipped(player);
        }

        public void OnCustomBackpackEquipped(Player player, ItemData item)
        {
        }

        public void OnCustomBackpackUnequipping(Player player, ItemData item)
        {
            if (player == null || item == null || !IsBackpack(item))
            {
                return;
            }

            InventoryGui inventoryGui = InventoryGui.instance;
            if (inventoryGui != null && inventoryGui.IsContainerOpen())
            {
                inventoryGui.CloseContainer();
            }
        }
    }

    private sealed class SmoothbrainBackpacksApi
    {
        private readonly MethodInfo _validateBackpackMethod;
        private readonly FieldInfo _visualsField;
        private readonly FieldInfo _equippedBackpackItemField;
        private readonly FieldInfo? _currentBackpackItemHashField;
        private readonly MethodInfo? _setBackpackItemMethod;
        private readonly MethodInfo? _forceSetBackpackEquippedMethod;

        private SmoothbrainBackpacksApi(
            MethodInfo validateBackpackMethod,
            FieldInfo visualsField,
            FieldInfo equippedBackpackItemField,
            FieldInfo? currentBackpackItemHashField,
            MethodInfo? setBackpackItemMethod,
            MethodInfo? forceSetBackpackEquippedMethod)
        {
            _validateBackpackMethod = validateBackpackMethod;
            _visualsField = visualsField;
            _equippedBackpackItemField = equippedBackpackItemField;
            _currentBackpackItemHashField = currentBackpackItemHashField;
            _setBackpackItemMethod = setBackpackItemMethod;
            _forceSetBackpackEquippedMethod = forceSetBackpackEquippedMethod;
        }

        public static bool TryCreate(Assembly assembly, out SmoothbrainBackpacksApi? api, out string detail)
        {
            api = null;
            Type? backpacksType = assembly.GetType("Backpacks.Backpacks");
            Type? visualType = assembly.GetType("Backpacks.Visual");
            MethodInfo? validateBackpackMethod = backpacksType?.GetMethod(
                "validateBackpack",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(ItemData) },
                null);
            FieldInfo? visualsField = visualType?.GetField("visuals", BindingFlags.Public | BindingFlags.Static);
            FieldInfo? equippedBackpackItemField = visualType?.GetField("equippedBackpackItem", BindingFlags.Public | BindingFlags.Instance);
            if (validateBackpackMethod == null || visualsField == null || equippedBackpackItemField == null)
            {
                detail = "validateBackpack or Visual fields were not found";
                return false;
            }

            api = new SmoothbrainBackpacksApi(
                validateBackpackMethod,
                visualsField,
                equippedBackpackItemField,
                visualType?.GetField("currentBackpackItemHash", BindingFlags.Public | BindingFlags.Instance),
                visualType?.GetMethod("setBackpackItem", BindingFlags.NonPublic | BindingFlags.Instance),
                visualType?.GetMethod("forceSetBackpackEquipped", BindingFlags.Public | BindingFlags.Instance));
            detail = "";
            return true;
        }

        public bool IsBackpack(ItemData? item)
        {
            if (item == null)
            {
                return false;
            }

            try
            {
                return _validateBackpackMethod.Invoke(null, new object[] { item }) is true;
            }
            catch
            {
                return false;
            }
        }

        public void SyncEquippedBackpack(Player player, ItemData? item)
        {
            if (player == null || IsUnityNull(player.m_visEquipment) || !TryGetVisual(player.m_visEquipment, out object? visual) || visual == null)
            {
                return;
            }

            try
            {
                string prefabName = item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";
                int hash = string.IsNullOrWhiteSpace(prefabName) ? 0 : StringExtensionMethods.GetStableHashCode(prefabName);
                object? current = _equippedBackpackItemField.GetValue(visual);
                if (!ReferenceEquals(current, item))
                {
                    _equippedBackpackItemField.SetValue(visual, item);
                }

                _setBackpackItemMethod?.Invoke(visual, new object[] { prefabName });
                if (_forceSetBackpackEquippedMethod != null && GetCurrentHash(visual) != hash)
                {
                    _forceSetBackpackEquippedMethod.Invoke(visual, new object[] { hash });
                }
            }
            catch (Exception)
            {
            }
        }

        private bool TryGetVisual(VisEquipment visEquipment, out object? visual)
        {
            visual = null;
            object? visuals = _visualsField.GetValue(null);
            if (visuals is not IDictionary dictionary || !dictionary.Contains(visEquipment))
            {
                return false;
            }

            visual = dictionary[visEquipment];
            return visual != null;
        }

        private int GetCurrentHash(object visual)
        {
            try
            {
                return _currentBackpackItemHashField?.GetValue(visual) is int hash ? hash : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    private sealed class RustyBagsApi
    {
        private readonly MethodInfo _isBagMethod;
        private readonly MethodInfo _isQuiverMethod;
        private readonly Type _bagEquipmentType;
        private readonly Type _bagType;
        private readonly Type _quiverType;
        private readonly MethodInfo _getBagMethod;
        private readonly MethodInfo _getQuiverMethod;
        private readonly MethodInfo _setBagMethod;
        private readonly MethodInfo _setQuiverMethod;

        private RustyBagsApi(
            MethodInfo isBagMethod,
            MethodInfo isQuiverMethod,
            Type bagEquipmentType,
            Type bagType,
            Type quiverType,
            MethodInfo getBagMethod,
            MethodInfo getQuiverMethod,
            MethodInfo setBagMethod,
            MethodInfo setQuiverMethod)
        {
            _isBagMethod = isBagMethod;
            _isQuiverMethod = isQuiverMethod;
            _bagEquipmentType = bagEquipmentType;
            _bagType = bagType;
            _quiverType = quiverType;
            _getBagMethod = getBagMethod;
            _getQuiverMethod = getQuiverMethod;
            _setBagMethod = setBagMethod;
            _setQuiverMethod = setQuiverMethod;
        }

        public static bool TryCreate(Assembly assembly, out RustyBagsApi? api, out string detail)
        {
            api = null;
            Type? apiType = assembly.GetType("RustyBags.API");
            Type? bagEquipmentType = assembly.GetType("RustyBags.BagEquipment");
            Type? bagType = assembly.GetType("RustyBags.Bag");
            Type? quiverType = assembly.GetType("RustyBags.Quiver");
            MethodInfo? isBagMethod = apiType?.GetMethod(
                "IsBag",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo? isQuiverMethod = apiType?.GetMethod(
                "IsQuiver",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo? getBagMethod = bagEquipmentType?.GetMethod("GetBag", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo? getQuiverMethod = bagEquipmentType?.GetMethod("GetQuiver", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo? setBagMethod = bagEquipmentType?.GetMethod("SetBag", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo? setQuiverMethod = bagEquipmentType?.GetMethod("SetQuiver", BindingFlags.Public | BindingFlags.Instance);
            if (isBagMethod == null ||
                isQuiverMethod == null ||
                bagEquipmentType == null ||
                bagType == null ||
                quiverType == null ||
                getBagMethod == null ||
                getQuiverMethod == null ||
                setBagMethod == null ||
                setQuiverMethod == null)
            {
                detail = "RustyBags API or BagEquipment methods were not found";
                return false;
            }

            api = new RustyBagsApi(
                isBagMethod,
                isQuiverMethod,
                bagEquipmentType,
                bagType,
                quiverType,
                getBagMethod,
                getQuiverMethod,
                setBagMethod,
                setQuiverMethod);
            detail = "";
            return true;
        }

        public bool IsBag(ItemData? item)
        {
            if (item?.m_shared == null)
            {
                return false;
            }

            try
            {
                return _isBagMethod.Invoke(null, new object[] { item.m_shared.m_name }) is true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsQuiver(ItemData? item)
        {
            if (item?.m_shared == null)
            {
                return false;
            }

            try
            {
                return _isQuiverMethod.Invoke(null, new object[] { item.m_shared.m_name }) is true;
            }
            catch
            {
                return false;
            }
        }

        public void SyncBag(Player player, ItemData? item)
        {
            if (item != null && !_bagType.IsInstanceOfType(item))
            {
                return;
            }

            InvokeEquipmentSetter(player, _setBagMethod, item);
        }

        public void SyncQuiver(Player player, ItemData? item)
        {
            if (item != null && !_quiverType.IsInstanceOfType(item))
            {
                return;
            }

            InvokeEquipmentSetter(player, _setQuiverMethod, item);
        }

        public void ClearBagIfCurrent(Player player, ItemData item)
        {
            object? equipment = GetBagEquipment(player);
            if (equipment == null)
            {
                return;
            }

            object? current = _getBagMethod.Invoke(equipment, Array.Empty<object>());
            if (ReferenceEquals(current, item))
            {
                _setBagMethod.Invoke(equipment, new object?[] { null });
            }
        }

        public void ClearQuiverIfCurrent(Player player, ItemData item)
        {
            object? equipment = GetBagEquipment(player);
            if (equipment == null)
            {
                return;
            }

            object? current = _getQuiverMethod.Invoke(equipment, Array.Empty<object>());
            if (ReferenceEquals(current, item))
            {
                _setQuiverMethod.Invoke(equipment, new object?[] { null });
            }
        }

        private void InvokeEquipmentSetter(Player player, MethodInfo setter, ItemData? item)
        {
            object? equipment = GetBagEquipment(player);
            if (equipment == null)
            {
                return;
            }

            try
            {
                setter.Invoke(equipment, new object?[] { item });
            }
            catch (Exception)
            {
            }
        }

        private object? GetBagEquipment(Player player)
        {
            if (player == null)
            {
                return null;
            }

            try
            {
                return ((Component)player).GetComponent(_bagEquipmentType);
            }
            catch
            {
                return null;
            }
        }
    }
}
