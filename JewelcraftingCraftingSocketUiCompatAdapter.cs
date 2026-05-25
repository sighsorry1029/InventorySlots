using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class JewelcraftingCraftingSocketUiApi
    {
        private readonly FieldInfo _socketIconsField;
        private readonly FieldInfo? _socketingButtonField;
        private readonly FieldInfo? _socketTabField;
        private readonly FieldInfo? _socketCostField;
        private readonly MethodInfo? _socketTabOpenMethod;

        private JewelcraftingCraftingSocketUiApi(FieldInfo socketIconsField, FieldInfo? socketingButtonField, FieldInfo? socketTabField, FieldInfo? socketCostField, MethodInfo? socketTabOpenMethod)
        {
            _socketIconsField = socketIconsField;
            _socketingButtonField = socketingButtonField;
            _socketTabField = socketTabField;
            _socketCostField = socketCostField;
            _socketTabOpenMethod = socketTabOpenMethod;
        }

        public static bool TryCreate(Assembly assembly, out JewelcraftingCraftingSocketUiApi? api, out string detail)
        {
            api = null;
            Type? addSocketIconsType = assembly.GetType("Jewelcrafting.GemStones+AddSocketIcons");
            Type? addSocketTabType = assembly.GetType("Jewelcrafting.GemStones+AddSocketAddingTab");
            Type? jewelcraftingType = assembly.GetType("Jewelcrafting.Jewelcrafting");
            FieldInfo? socketIconsField = addSocketIconsType?.GetField("socketIcons", BindingFlags.Public | BindingFlags.Static);
            if (socketIconsField == null)
            {
                detail = "AddSocketIcons.socketIcons was not found";
                return false;
            }

            api = new JewelcraftingCraftingSocketUiApi(
                socketIconsField,
                addSocketIconsType?.GetField("socketingButton", BindingFlags.Public | BindingFlags.Static),
                addSocketTabType?.GetField("tab", BindingFlags.Public | BindingFlags.Static),
                jewelcraftingType?.GetField("socketCost", BindingFlags.Public | BindingFlags.Static),
                addSocketTabType?.GetMethod("TabOpen", BindingFlags.Public | BindingFlags.Static));
            detail = "";
            return true;
        }

        public Array? GetSocketIcons() => _socketIconsField.GetValue(null) as Array;

        public Button? GetSocketingButton() => _socketingButtonField?.GetValue(null) as Button;

        public Transform? GetSocketTab() => _socketTabField?.GetValue(null) as Transform;

        public string GetSocketCostMode()
        {
            try
            {
                return _socketCostField?.GetValue(null) is ConfigEntryBase entry
                    ? entry.BoxedValue?.ToString() ?? ""
                    : "";
            }
            catch
            {
                return "";
            }
        }

        public bool IsSocketTabOpen()
        {
            try
            {
                return _socketTabOpenMethod?.Invoke(null, null) is true;
            }
            catch
            {
                return false;
            }
        }
    }
}
