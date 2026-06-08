using System;
using System.Reflection;
using System.Text;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class ItemRequiresSkillLevelApi
    {
        private readonly MethodInfo _tryGetReqForItemMethod;
        private readonly MethodInfo _tryGetReqForPrefabNameMethod;
        private readonly MethodInfo _getTextEquipMethod;
        private readonly MethodInfo _getTextCraftMethod;

        private ItemRequiresSkillLevelApi(
            MethodInfo tryGetReqForItemMethod,
            MethodInfo tryGetReqForPrefabNameMethod,
            MethodInfo getTextEquipMethod,
            MethodInfo getTextCraftMethod)
        {
            _tryGetReqForItemMethod = tryGetReqForItemMethod;
            _tryGetReqForPrefabNameMethod = tryGetReqForPrefabNameMethod;
            _getTextEquipMethod = getTextEquipMethod;
            _getTextCraftMethod = getTextCraftMethod;
        }

        public static bool TryCreate(Assembly assembly, out ItemRequiresSkillLevelApi? api, out string detail)
        {
            api = null;
            Type? patchesType = assembly.GetType("ItemRequiresSkillLevel.Patches");
            MethodInfo? tryGetReqForItemMethod = patchesType?.GetMethod("TryGetReqForItem", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo? tryGetReqForPrefabNameMethod = patchesType?.GetMethod("TryGetReqForPrefabName", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo? getTextEquipMethod = patchesType?.GetMethod("GetTextEquip", BindingFlags.Public | BindingFlags.Static);
            MethodInfo? getTextCraftMethod = patchesType?.GetMethod("GetTextCraft", BindingFlags.Public | BindingFlags.Static);

            if (patchesType == null ||
                tryGetReqForItemMethod == null ||
                tryGetReqForPrefabNameMethod == null ||
                getTextEquipMethod == null ||
                getTextCraftMethod == null)
            {
                detail = "ItemRequiresSkillLevel tooltip methods were not found";
                return false;
            }

            api = new ItemRequiresSkillLevelApi(
                tryGetReqForItemMethod,
                tryGetReqForPrefabNameMethod,
                getTextEquipMethod,
                getTextCraftMethod);
            detail = "";
            return true;
        }

        public string GetTooltipText(ItemData? item, string? prefabName, bool includeEquip, bool includeCraft)
        {
            if (!includeEquip && !includeCraft)
            {
                return "";
            }

            if (!TryGetRequirement(item, prefabName, out object? requirement) || requirement == null)
            {
                return "";
            }

            StringBuilder text = new();
            if (includeEquip)
            {
                text.Append(GetText(_getTextEquipMethod, requirement));
            }

            if (includeCraft)
            {
                text.Append(GetText(_getTextCraftMethod, requirement));
            }

            return text.ToString();
        }

        private bool TryGetRequirement(ItemData? item, string? prefabName, out object? requirement)
        {
            requirement = null;
            if (item != null && TryGetRequirementForItem(item, out requirement))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(prefabName) && TryGetRequirementForPrefabName(prefabName!, out requirement);
        }

        private bool TryGetRequirementForItem(ItemData item, out object? requirement)
        {
            requirement = null;
            try
            {
                object?[] args = { item, null };
                bool found = _tryGetReqForItemMethod.Invoke(null, args) is true;
                requirement = args[1];
                return found && requirement != null;
            }
            catch
            {
                requirement = null;
                return false;
            }
        }

        private bool TryGetRequirementForPrefabName(string prefabName, out object? requirement)
        {
            requirement = null;
            try
            {
                object?[] args = { prefabName, null };
                bool found = _tryGetReqForPrefabNameMethod.Invoke(null, args) is true;
                requirement = args[1];
                return found && requirement != null;
            }
            catch
            {
                requirement = null;
                return false;
            }
        }

        private static string GetText(MethodInfo method, object requirement)
        {
            try
            {
                return method.Invoke(null, new[] { requirement }) as string ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
