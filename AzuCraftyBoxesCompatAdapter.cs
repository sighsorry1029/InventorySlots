using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class AzuCraftyBoxesApi
    {
        private readonly MethodInfo _getNearbyContainersMethod;
        private readonly MethodInfo _canItemBePulledMethod;
        private readonly MethodInfo? _shouldPreventMethod;
        private readonly FieldInfo? _rangeField;
        private readonly FieldInfo? _leaveOneField;
        private readonly FieldInfo? _resourceStringField;
        private readonly FieldInfo? _flashColorField;
        private readonly FieldInfo? _unFlashColorField;
        private readonly Dictionary<Type, MethodInfo?> _getPrefabNameMethods = new();
        private readonly Dictionary<Type, MethodInfo?> _itemCountMethods = new();
        private ConfigEntryBase? _rangeConfig;
        private ConfigEntryBase? _leaveOneConfig;
        private ConfigEntryBase? _resourceStringConfig;
        private ConfigEntryBase? _flashColorConfig;
        private ConfigEntryBase? _unFlashColorConfig;

        private AzuCraftyBoxesApi(
            MethodInfo getNearbyContainersMethod,
            MethodInfo canItemBePulledMethod,
            MethodInfo? shouldPreventMethod,
            FieldInfo? rangeField,
            FieldInfo? leaveOneField,
            FieldInfo? resourceStringField,
            FieldInfo? flashColorField,
            FieldInfo? unFlashColorField)
        {
            _getNearbyContainersMethod = getNearbyContainersMethod;
            _canItemBePulledMethod = canItemBePulledMethod;
            _shouldPreventMethod = shouldPreventMethod;
            _rangeField = rangeField;
            _leaveOneField = leaveOneField;
            _resourceStringField = resourceStringField;
            _flashColorField = flashColorField;
            _unFlashColorField = unFlashColorField;
        }

        public static bool TryCreate(Assembly assembly, out AzuCraftyBoxesApi? api, out string detail)
        {
            api = null;
            Type? apiType = assembly.GetType("AzuCraftyBoxes.API");
            Type? pluginType = assembly.GetType("AzuCraftyBoxes.AzuCraftyBoxesPlugin");
            Type? miscFunctionsType = assembly.GetType("AzuCraftyBoxes.Util.Functions.MiscFunctions");

            MethodInfo? nearbyDefinition = apiType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "GetNearbyContainers" && method.IsGenericMethodDefinition && method.GetParameters().Length == 2);
            MethodInfo? getNearbyContainersMethod = nearbyDefinition?.MakeGenericMethod(typeof(Player));
            MethodInfo? canItemBePulledMethod = apiType?.GetMethod("CanItemBePulled", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);

            if (getNearbyContainersMethod == null || canItemBePulledMethod == null)
            {
                detail = "required API methods were not found";
                return false;
            }

            api = new AzuCraftyBoxesApi(
                getNearbyContainersMethod,
                canItemBePulledMethod,
                miscFunctionsType?.GetMethod("ShouldPrevent", BindingFlags.NonPublic | BindingFlags.Static),
                pluginType?.GetField("mRange", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static),
                pluginType?.GetField("leaveOne", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static),
                pluginType?.GetField("resourceString", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static),
                pluginType?.GetField("flashColor", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static),
                pluginType?.GetField("unFlashColor", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static));
            detail = "";
            return true;
        }

        public IEnumerable? GetNearbyContainers(Player player, float range) =>
            _getNearbyContainersMethod.Invoke(null, new object[] { player, range }) as IEnumerable;

        public bool CanItemBePulled(string containerPrefab, string itemPrefab)
        {
            try
            {
                return _canItemBePulledMethod.Invoke(null, new object[] { containerPrefab, itemPrefab }) is true;
            }
            catch
            {
                return false;
            }
        }

        public bool ShouldPrevent()
        {
            if (_shouldPreventMethod == null)
            {
                return false;
            }

            try
            {
                return _shouldPreventMethod.Invoke(null, null) is true;
            }
            catch
            {
                return false;
            }
        }

        public float GetRange()
        {
            object? value = GetCachedConfigEntryValue(_rangeField, ref _rangeConfig);
            if (value == null)
            {
                return 20f;
            }

            try
            {
                return Convert.ToSingle(value);
            }
            catch
            {
                return 20f;
            }
        }

        public bool GetLeaveOne()
        {
            string text = GetCachedConfigEntryValue(_leaveOneField, ref _leaveOneConfig)?.ToString() ?? "";
            return string.Equals(text, "On", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "True", StringComparison.OrdinalIgnoreCase);
        }

        public string InvokeString(object instance, string methodName)
        {
            Type type = instance.GetType();
            if (!_getPrefabNameMethods.TryGetValue(type, out MethodInfo? method))
            {
                method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                _getPrefabNameMethods[type] = method;
            }

            try
            {
                return method?.Invoke(instance, null) as string ?? "";
            }
            catch
            {
                return "";
            }
        }

        public int InvokeInt(object instance, string methodName, string argument)
        {
            Type type = instance.GetType();
            if (!_itemCountMethods.TryGetValue(type, out MethodInfo? method))
            {
                method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                _itemCountMethods[type] = method;
            }

            try
            {
                return method?.Invoke(instance, new object[] { argument }) is int value ? value : 0;
            }
            catch
            {
                return 0;
            }
        }

        public bool TryFormatRequirementAmount(int available, int required, out string text)
        {
            text = "";
            string format = GetCachedConfigEntryValue(_resourceStringField, ref _resourceStringConfig) as string ?? "";
            if (string.IsNullOrWhiteSpace(format))
            {
                text = required.ToString();
                return true;
            }

            try
            {
                text = string.Format(format, FormatThousands(available), required);
                return true;
            }
            catch
            {
                text = $"{FormatThousands(available)}/{required}";
                return true;
            }
        }

        public bool TryGetRequirementFlashColor(out Color color)
        {
            color = default;
            FieldInfo? field = Mathf.Sin(Time.time * 10f) > 0f ? _flashColorField : _unFlashColorField;
            object? value = field == _flashColorField
                ? GetCachedConfigEntryValue(field, ref _flashColorConfig)
                : GetCachedConfigEntryValue(field, ref _unFlashColorConfig);
            if (value is Color configuredColor)
            {
                color = configuredColor;
                return true;
            }

            return false;
        }

        private object? GetCachedConfigEntryValue(FieldInfo? field, ref ConfigEntryBase? config)
        {
            if (field == null)
            {
                return null;
            }

            try
            {
                config ??= field.GetValue(null) as ConfigEntryBase;
                return config?.BoxedValue;
            }
            catch
            {
                config = null;
                return null;
            }
        }

        private static string FormatThousands(int number)
        {
            if (number >= 1000000)
            {
                return ((double)number / 1000000.0).ToString("0.#") + "M";
            }

            return number >= 1000
                ? ((double)number / 1000.0).ToString("0.#") + "K"
                : number.ToString();
        }
    }
}
