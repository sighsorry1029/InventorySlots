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
        private readonly MethodInfo _isBackpackMethod;
        private readonly MethodInfo? _equipItemPostfixMethod;
        private readonly MethodInfo? _unequipItemPrefixMethod;
        private readonly MethodInfo? _isBackpackEquippedMethod;
        private readonly MethodInfo? _isThisBackpackEquippedMethod;
        private readonly MethodInfo? _getEquippedBackpackMethod;
        private readonly MethodInfo? _reorderBonesMethod;
        private readonly FieldInfo? _backpackEquippedField;

        private AdventureBackpacksApi(
            MethodInfo isBackpackMethod,
            MethodInfo? equipItemPostfixMethod,
            MethodInfo? unequipItemPrefixMethod,
            MethodInfo? isBackpackEquippedMethod,
            MethodInfo? isThisBackpackEquippedMethod,
            MethodInfo? getEquippedBackpackMethod,
            MethodInfo? reorderBonesMethod,
            FieldInfo? backpackEquippedField)
        {
            _isBackpackMethod = isBackpackMethod;
            _equipItemPostfixMethod = equipItemPostfixMethod;
            _unequipItemPrefixMethod = unequipItemPrefixMethod;
            _isBackpackEquippedMethod = isBackpackEquippedMethod;
            _isThisBackpackEquippedMethod = isThisBackpackEquippedMethod;
            _getEquippedBackpackMethod = getEquippedBackpackMethod;
            _reorderBonesMethod = reorderBonesMethod;
            _backpackEquippedField = backpackEquippedField;
        }

        public static bool TryCreate(Assembly assembly, out AdventureBackpacksApi? api, out string detail)
        {
            api = null;
            Type? abApiType = assembly.GetType("AdventureBackpacks.API.ABAPI");
            MethodInfo? isBackpackMethod = abApiType?.GetMethod(
                "IsBackpack",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ItemData) },
                null);
            if (isBackpackMethod == null)
            {
                detail = "AdventureBackpacks.API.ABAPI.IsBackpack was not found";
                return false;
            }

            Type? humanoidPatchesType = assembly.GetType("AdventureBackpacks.Patches.HumanoidPatches");
            Type? equipPatchType = humanoidPatchesType?.GetNestedType("HumanoidEquipItemPatch", BindingFlags.NonPublic);
            Type? unequipPatchType = humanoidPatchesType?.GetNestedType("HumanoidUnequipItemPatch", BindingFlags.NonPublic);
            Type? playerExtensionsType = assembly.GetType("AdventureBackpacks.Extensions.PlayerExtensions");
            Type? inventoryGuiPatchesType = assembly.GetType("AdventureBackpacks.Patches.InventoryGuiPatches");
            Type? boneReorderType = assembly.GetType("Vapok.Common.Tools.BoneReorder");

            api = new AdventureBackpacksApi(
                isBackpackMethod,
                equipPatchType?.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static),
                unequipPatchType?.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static),
                playerExtensionsType?.GetMethod("IsBackpackEquipped", BindingFlags.Public | BindingFlags.Static),
                playerExtensionsType?.GetMethod("IsThisBackpackEquipped", BindingFlags.Public | BindingFlags.Static),
                playerExtensionsType?.GetMethod("GetEquippedBackpack", BindingFlags.Public | BindingFlags.Static),
                AccessTools.Method(boneReorderType, "ReorderBones"),
                inventoryGuiPatchesType?.GetField("BackpackEquipped", BindingFlags.Public | BindingFlags.Static));
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
                return _isBackpackMethod.Invoke(null, new object[] { item }) is true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsBackpackEquippedFlagSet()
        {
            try
            {
                return _backpackEquippedField?.GetValue(null) is true;
            }
            catch
            {
                return false;
            }
        }

        public void OnCustomBackpackEquipped(Player player, ItemData item)
        {
            if (player == null || item == null || !IsBackpack(item))
            {
                return;
            }

            try
            {
                _equipItemPostfixMethod?.Invoke(null, new object[] { item, true });
                _backpackEquippedField?.SetValue(null, true);
            }
            catch (Exception)
            {
            }
        }

        public void OnCustomBackpackUnequipping(Player player, ItemData item)
        {
            if (player == null || item == null || !IsBackpack(item))
            {
                return;
            }

            Humanoid humanoid = player;
            ItemData? originalShoulderItem = humanoid.m_shoulderItem;
            try
            {
                humanoid.m_shoulderItem = item;
                _unequipItemPrefixMethod?.Invoke(null, new object[] { item });
            }
            catch (Exception)
            {
            }
            finally
            {
                humanoid.m_shoulderItem = originalShoulderItem;
            }
        }

        public void ReorderBones(VisEquipment visEquipment, int itemHash, List<GameObject>? instances)
        {
            if (_reorderBonesMethod == null || IsUnityNull(visEquipment) || instances == null || instances.Count == 0)
            {
                return;
            }

            try
            {
                _reorderBonesMethod.Invoke(null, new object[] { visEquipment, itemHash, instances });
            }
            catch
            {
            }
        }

        public void ApplyPatches(Harmony harmony)
        {
            PatchOptional(
                harmony,
                _isBackpackEquippedMethod,
                postfix: nameof(AdventureBackpackIsBackpackEquippedPostfix),
                label: "AdventureBackpacks IsBackpackEquipped");
            PatchOptional(
                harmony,
                _isThisBackpackEquippedMethod,
                postfix: nameof(AdventureBackpackIsThisBackpackEquippedPostfix),
                label: "AdventureBackpacks IsThisBackpackEquipped");
            PatchOptional(
                harmony,
                _getEquippedBackpackMethod,
                prefix: nameof(AdventureBackpackGetEquippedBackpackPrefix),
                postfix: nameof(AdventureBackpackGetEquippedBackpackPostfix),
                label: "AdventureBackpacks GetEquippedBackpack");
        }

        private static void PatchOptional(Harmony harmony, MethodInfo? target, string? prefix = null, string? postfix = null, string? label = null)
        {
            if (harmony == null || target == null)
            {
                return;
            }

            MethodInfo? prefixMethod = prefix == null ? null : AccessTools.Method(typeof(InventorySlotsPlugin), prefix);
            MethodInfo? postfixMethod = postfix == null ? null : AccessTools.Method(typeof(InventorySlotsPlugin), postfix);
            harmony.Patch(
                target,
                prefixMethod == null ? null : new HarmonyMethod(prefixMethod),
                postfixMethod == null ? null : new HarmonyMethod(postfixMethod));
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
