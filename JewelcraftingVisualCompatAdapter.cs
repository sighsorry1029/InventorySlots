using System;
using System.Collections;
using System.Reflection;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class JewelcraftingVisualApi
    {
        private readonly FieldInfo _visualsField;
        private readonly FieldInfo _equippedFingerItemField;
        private readonly FieldInfo _equippedNeckItemField;
        private readonly FieldInfo? _currentFingerItemHashField;
        private readonly FieldInfo? _currentNeckItemHashField;
        private readonly MethodInfo? _setFingerItemMethod;
        private readonly MethodInfo? _setNeckItemMethod;
        private readonly MethodInfo? _setFingerEquippedMethod;
        private readonly MethodInfo? _setNeckEquippedMethod;

        private JewelcraftingVisualApi(
            FieldInfo visualsField,
            FieldInfo equippedFingerItemField,
            FieldInfo equippedNeckItemField,
            FieldInfo? currentFingerItemHashField,
            FieldInfo? currentNeckItemHashField,
            MethodInfo? setFingerItemMethod,
            MethodInfo? setNeckItemMethod,
            MethodInfo? setFingerEquippedMethod,
            MethodInfo? setNeckEquippedMethod)
        {
            _visualsField = visualsField;
            _equippedFingerItemField = equippedFingerItemField;
            _equippedNeckItemField = equippedNeckItemField;
            _currentFingerItemHashField = currentFingerItemHashField;
            _currentNeckItemHashField = currentNeckItemHashField;
            _setFingerItemMethod = setFingerItemMethod;
            _setNeckItemMethod = setNeckItemMethod;
            _setFingerEquippedMethod = setFingerEquippedMethod;
            _setNeckEquippedMethod = setNeckEquippedMethod;
        }

        public static bool TryCreate(Assembly assembly, out JewelcraftingVisualApi? api, out string detail)
        {
            api = null;
            Type? visualType = assembly.GetType("Jewelcrafting.Visual");
            FieldInfo? visualsField = visualType?.GetField("visuals", BindingFlags.Public | BindingFlags.Static);
            FieldInfo? equippedFingerItemField = visualType?.GetField("equippedFingerItem", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo? equippedNeckItemField = visualType?.GetField("equippedNeckItem", BindingFlags.Public | BindingFlags.Instance);

            if (visualsField == null || equippedFingerItemField == null || equippedNeckItemField == null)
            {
                detail = "Visual fields were not found";
                return false;
            }

            api = new JewelcraftingVisualApi(
                visualsField,
                equippedFingerItemField,
                equippedNeckItemField,
                visualType?.GetField("currentFingerItemHash", BindingFlags.Public | BindingFlags.Instance),
                visualType?.GetField("currentNeckItemHash", BindingFlags.Public | BindingFlags.Instance),
                visualType?.GetMethod("setFingerItem", BindingFlags.NonPublic | BindingFlags.Instance),
                visualType?.GetMethod("setNeckItem", BindingFlags.NonPublic | BindingFlags.Instance),
                visualType?.GetMethod("setFingerEquipped", BindingFlags.NonPublic | BindingFlags.Instance),
                visualType?.GetMethod("setNeckEquipped", BindingFlags.NonPublic | BindingFlags.Instance));
            detail = "";
            return true;
        }

        public bool TryGetVisual(VisEquipment visEquipment, out object? visual)
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

        public void ClearSlot(object visual, bool isRing)
        {
            FieldInfo equippedField = isRing ? _equippedFingerItemField : _equippedNeckItemField;
            FieldInfo? hashField = isRing ? _currentFingerItemHashField : _currentNeckItemHashField;
            MethodInfo? setItemMethod = isRing ? _setFingerItemMethod : _setNeckItemMethod;
            MethodInfo? setEquippedMethod = isRing ? _setFingerEquippedMethod : _setNeckEquippedMethod;

            equippedField.SetValue(visual, null);
            setItemMethod?.Invoke(visual, new object[] { "" });
            if (setEquippedMethod != null)
            {
                setEquippedMethod.Invoke(visual, new object[] { 0 });
            }
            else
            {
                hashField?.SetValue(visual, 0);
            }
        }
    }
}
