using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class JewelcraftingTooltipApi
    {
        private readonly MethodInfo _fillItemContainerTooltipMethod;
        private readonly FieldInfo _socketTooltipField;
        private readonly FieldInfo? _advancedTooltipKeyField;

        private JewelcraftingTooltipApi(MethodInfo fillItemContainerTooltipMethod, FieldInfo socketTooltipField, FieldInfo? advancedTooltipKeyField)
        {
            _fillItemContainerTooltipMethod = fillItemContainerTooltipMethod;
            _socketTooltipField = socketTooltipField;
            _advancedTooltipKeyField = advancedTooltipKeyField;
        }

        public static bool TryCreate(Assembly assembly, out JewelcraftingTooltipApi? api, out string detail)
        {
            api = null;
            Type? apiType = assembly.GetType("Jewelcrafting.API");
            Type? setupType = assembly.GetType("Jewelcrafting.GemStoneSetup");
            Type? pluginType = assembly.GetType("Jewelcrafting.Jewelcrafting");
            MethodInfo? fillItemContainerTooltipMethod = apiType?.GetMethod(
                "FillItemContainerTooltip",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ItemDrop.ItemData), typeof(Transform), typeof(bool) },
                null);
            FieldInfo? socketTooltipField = setupType?.GetField("SocketTooltip", BindingFlags.Public | BindingFlags.Static);
            if (fillItemContainerTooltipMethod == null || socketTooltipField == null)
            {
                detail = "required API members were not found";
                return false;
            }

            api = new JewelcraftingTooltipApi(
                fillItemContainerTooltipMethod,
                socketTooltipField,
                pluginType?.GetField("advancedTooltipKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
            detail = "";
            return true;
        }

        public void FillItemContainerTooltip(ItemDrop.ItemData item, RectTransform root, bool showInteract) =>
            _fillItemContainerTooltipMethod.Invoke(null, new object[] { item, root, showInteract });

        public GameObject? GetSocketTooltip() => _socketTooltipField.GetValue(null) as GameObject;

        public bool TryIsAdvancedTooltipPressed(out bool pressed)
        {
            pressed = false;
            if (_advancedTooltipKeyField?.GetValue(null) is not ConfigEntry<KeyboardShortcut> shortcut)
            {
                return false;
            }

            pressed = shortcut.Value.IsPressed();
            return true;
        }
    }
}
