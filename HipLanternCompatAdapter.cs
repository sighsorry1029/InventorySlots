using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string HipLanternPrefabName = "HipLantern";
    private const int HipLanternSlotStateNone = 0;
    private const int HipLanternSlotStateValid = 1;
    private const int HipLanternSlotStateInvalid = 2;
    private static Player? _hipLanternCompatibilityPlayer;
    private static ItemData? _lastHipLanternCompatItem;
    private static bool _lastHipLanternCustomSlotEnabled;
    private static int _hipLanternCustomSlotState = -1;

    private static void InitializeHipLanternCompatibility()
    {
        _lastHipLanternCustomSlotEnabled = IsHipLanternCustomSlotEnabled(out _);
    }

    private static bool TryGetHipLanternApi(out HipLanternApi? api)
    {
        return TryGetCompatApi(
            HipLanternGuid,
            "HipLantern",
            CompatRuntime.HipLantern,
            HipLanternApi.TryCreate,
            "HipLantern compatibility disabled",
            out api);
    }

    private static bool TryAddHipLanternCompatSlot(YamlSlot slot, string id)
    {
        if (!string.Equals(NormalizeSlotId(id), HipLanternSlotId, StringComparison.Ordinal))
        {
            return false;
        }

        if (SlotDefinitions.Any(existing => existing.Id == HipLanternSlotId))
        {
            return true;
        }

        if (!IsHipLanternCustomSlotEnabled(out _))
        {
            return true;
        }

        string name = string.IsNullOrWhiteSpace(slot.Name) ? "Hip Lantern" : slot.Name.Trim();
        SlotDefinitions.Add(new SlotDefinition(
            HipLanternSlotId,
            name,
            SlotKind.CustomEquipment,
            IsHipLanternCustomSlotItem));
        return true;
    }

    private static bool IsHipLanternCustomSlotEnabled(out HipLanternApi? api)
    {
        api = null;
        return HasHipLanternActive &&
               TryGetHipLanternApi(out api) &&
               api != null &&
               api.TryGetUseUtilitySlot(out bool useUtilitySlot) &&
               !useUtilitySlot;
    }

    private static bool IsHipLanternPrefab(ItemData? item)
    {
        return item != null &&
               string.Equals(
                   GetItemPrefabName(item),
                   HipLanternPrefabName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHipLanternCustomSlotItem(ItemData? item)
    {
        return item != null && TryGetHipLanternOwnerApi(item, out _);
    }

    private static bool TryGetHipLanternOwnerApi(ItemData item, out HipLanternApi? api)
    {
        bool pluginActive = HasHipLanternActive;
        bool isHipLanternPrefab = IsHipLanternPrefab(item);
        api = null;
        bool compatReady =
            pluginActive &&
            isHipLanternPrefab &&
            TryGetHipLanternApi(out api) &&
            api != null;
        bool useUtilitySlot = true;
        bool isHipLanternItem = false;
        if (compatReady)
        {
            compatReady = api!.TryGetUseUtilitySlot(out useUtilitySlot);
        }

        if (compatReady && !useUtilitySlot)
        {
            compatReady = api!.TryIsLanternItem(item, out isHipLanternItem);
        }

        return InventorySlotSafetyCore.ShouldDelegateHipLanternOwnership(
            pluginActive,
            compatReady,
            useUtilitySlot,
            isHipLanternPrefab,
            isHipLanternItem);
    }

    private static bool CanUseHipLanternCustomSlot(ItemData item, SlotDefinition slot)
    {
        if (slot.Kind != SlotKind.CustomEquipment)
        {
            return true;
        }

        bool pluginActive = HasHipLanternActive;
        bool isHipLanternPrefab = IsHipLanternPrefab(item);
        bool delegatesOwnership = TryGetHipLanternOwnerApi(item, out _);
        return InventorySlotSafetyCore.CanUseCustomHipLanternSlot(
            pluginActive,
            isHipLanternPrefab,
            delegatesOwnership);
    }

    private static bool ShouldSuppressInventorySlotsHipLanternVisual(ItemData item)
    {
        return HasHipLanternActive && IsHipLanternPrefab(item);
    }

    private static bool OnHipLanternCustomEquipmentEquipped(Player player, ItemData item)
    {
        if (player == null ||
            item == null ||
            !TryGetHipLanternOwnerApi(item, out HipLanternApi? api) ||
            api == null)
        {
            return false;
        }

        _hipLanternCompatibilityPlayer = player;
        _lastHipLanternCompatItem = item;
        if (InventorySafety.RoutingEquipToDedicatedSlot)
        {
            // HipLantern's Humanoid.EquipItem postfix owns this route. Pre-setting
            // the native item would make that postfix unequip the new slot marker.
            return false;
        }

        return api.TrySetLantern((Humanoid)player, item, out bool changed) && changed;
    }

    private static bool SynchronizeHipLanternEquippedState(Player player, ItemData item)
    {
        if (player == null ||
            item == null ||
            !TryGetHipLanternOwnerApi(item, out HipLanternApi? api) ||
            api == null)
        {
            return false;
        }

        _hipLanternCompatibilityPlayer = player;
        _lastHipLanternCompatItem = item;
        return api.TrySetLantern((Humanoid)player, item, out bool changed) && changed;
    }

    private static bool ClearHipLanternEquippedState(Player? player, ItemData item)
    {
        if (player == null ||
            item == null ||
            !HasHipLanternActive ||
            !IsHipLanternPrefab(item) ||
            !TryGetHipLanternApi(out HipLanternApi? api) ||
            api == null)
        {
            return false;
        }

        bool succeeded = api.TryClearLantern((Humanoid)player, item, out bool changed);
        if (ReferenceEquals(_lastHipLanternCompatItem, item))
        {
            _lastHipLanternCompatItem = null;
        }

        return succeeded && changed;
    }

    private static ItemData? CaptureHipLanternEquippedState(Player player)
    {
        if (player == null ||
            !HasHipLanternActive ||
            !TryGetHipLanternApi(out HipLanternApi? api) ||
            api == null ||
            !api.TryGetLantern((Humanoid)player, out ItemData? item))
        {
            return null;
        }

        return item;
    }

    private static bool RestoreHipLanternEquippedState(Player player, ItemData item)
    {
        if (player == null ||
            item == null ||
            !TryGetHipLanternOwnerApi(item, out HipLanternApi? api) ||
            api == null)
        {
            return false;
        }

        _hipLanternCompatibilityPlayer = player;
        _lastHipLanternCompatItem = item;
        return api.TrySetLantern((Humanoid)player, item, out bool changed) && changed;
    }

    private static bool TryGetCurrentHipLanternApi(
        Player player,
        ItemData item,
        out HipLanternApi? api)
    {
        api = null;
        return player != null &&
               HasHipLanternActive &&
               IsHipLanternPrefab(item) &&
               TryGetHipLanternApi(out api) &&
               api != null &&
               api.TryIsCurrentLantern((Humanoid)player, item, out bool isCurrent) &&
               isCurrent;
    }

    private static bool ShouldDelegateHipLanternWeight(Player player, ItemData item)
    {
        return TryGetCurrentHipLanternApi(player, item, out HipLanternApi? api) &&
               api != null &&
               api.TryGetUseUtilitySlot(out bool useUtilitySlot) &&
               !useUtilitySlot;
    }

    private static bool ShouldDelegateHipLanternDurability(Player player, ItemData item)
    {
        // HipLantern's UpdateEquipment finalizer drains, heat-multiplies, or
        // recharges the native current item even while a config transition is
        // waiting for InventorySlots' full-integrity reconciliation.
        return TryGetCurrentHipLanternApi(player, item, out _);
    }

    private static void RefreshHipLanternCompatibilityState(Player player)
    {
        if (player == null)
        {
            return;
        }

        if (_hipLanternCompatibilityPlayer != player)
        {
            _hipLanternCompatibilityPlayer = player;
            _lastHipLanternCompatItem = null;
            _hipLanternCustomSlotState = -1;
        }

        if (!HasHipLanternActive)
        {
            _lastHipLanternCompatItem = null;
            _hipLanternCustomSlotState = HipLanternSlotStateNone;
            return;
        }

        bool customSlotEnabled = IsHipLanternCustomSlotEnabled(out _);
        if (_lastHipLanternCustomSlotEnabled != customSlotEnabled)
        {
            _lastHipLanternCustomSlotEnabled = customSlotEnabled;
            RebuildSlotDefinitions();
            RequestInventoryStateEnsure(
                player,
                InventoryStateEnsureReason.ConfigChanged,
                InventoryStateAuditLevel.FullIntegrity);
            UpdateCustomEquipmentVisuals(player);
        }

        ItemData? customItem = FindCustomEquippedItem(player, IsHipLanternPrefab);
        int currentState = HipLanternSlotStateNone;
        if (customItem != null)
        {
            SlotDefinition? slot = GetSlotFromItemMarker(customItem);
            currentState =
                slot != null && CanUseHipLanternCustomSlot(customItem, slot)
                    ? HipLanternSlotStateValid
                    : HipLanternSlotStateInvalid;
        }

        int previousState = _hipLanternCustomSlotState;
        _hipLanternCustomSlotState = currentState;
        if (previousState != currentState &&
            (currentState == HipLanternSlotStateInvalid ||
             previousState == HipLanternSlotStateInvalid && currentState == HipLanternSlotStateValid))
        {
            InvalidateCustomEquipmentProjectionCache();
            RequestInventoryStateEnsure(
                player,
                InventoryStateEnsureReason.ConfigChanged,
                InventoryStateAuditLevel.FullIntegrity);
        }

        bool nativeStateChanged = false;
        if (currentState == HipLanternSlotStateValid && customItem != null)
        {
            nativeStateChanged = SynchronizeHipLanternEquippedState(player, customItem);
        }
        else if (currentState == HipLanternSlotStateInvalid && customItem != null)
        {
            nativeStateChanged = ClearHipLanternEquippedState(player, customItem);
        }
        else if (_lastHipLanternCompatItem != null &&
                 (!IsInventorySlotsCustomEquipped(_lastHipLanternCompatItem) ||
                  !((Humanoid)player).GetInventory().ContainsItem(_lastHipLanternCompatItem)))
        {
            ItemData stale = _lastHipLanternCompatItem;
            nativeStateChanged = ClearHipLanternEquippedState(player, stale);
            _lastHipLanternCompatItem = null;
        }

        if (nativeStateChanged)
        {
            ((Humanoid)player).SetupEquipment();
        }
    }

    private sealed class HipLanternApi
    {
        private delegate ItemData? GetLanternDelegate(Humanoid humanoid);
        private delegate ItemData? SetLanternDelegate(Humanoid humanoid, ItemData? item);
        private delegate bool IsLanternDelegate(ItemData item);

        private readonly GetLanternDelegate _getLantern;
        private readonly SetLanternDelegate _setLantern;
        private readonly IsLanternDelegate _isLantern;
        private readonly ConfigEntry<bool> _useUtilitySlot;
        private bool _failed;

        private HipLanternApi(
            GetLanternDelegate getLantern,
            SetLanternDelegate setLantern,
            IsLanternDelegate isLantern,
            ConfigEntry<bool> useUtilitySlot)
        {
            _getLantern = getLantern;
            _setLantern = setLantern;
            _isLantern = isLantern;
            _useUtilitySlot = useUtilitySlot;
        }

        public static bool TryCreate(
            Assembly assembly,
            out HipLanternApi? api,
            out string detail)
        {
            api = null;
            Type? humanoidExtensionType = assembly.GetType("HipLantern.HumanoidExtension");
            Type? lanternItemType = assembly.GetType("HipLantern.LanternItem");
            Type? pluginType = assembly.GetType("HipLantern.HipLantern");
            MethodInfo? getLanternMethod = humanoidExtensionType?.GetMethod(
                "GetHipLantern",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Humanoid) },
                modifiers: null);
            MethodInfo? setLanternMethod = humanoidExtensionType?.GetMethod(
                "SetHipLantern",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Humanoid), typeof(ItemData) },
                modifiers: null);
            MethodInfo? isLanternMethod = lanternItemType?.GetMethod(
                "IsLanternItem",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(ItemData) },
                modifiers: null);
            FieldInfo? useUtilitySlotField = pluginType?.GetField(
                "itemSlotUtility",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (getLanternMethod?.ReturnType != typeof(ItemData) ||
                setLanternMethod?.ReturnType != typeof(ItemData) ||
                isLanternMethod?.ReturnType != typeof(bool) ||
                useUtilitySlotField == null ||
                !typeof(ConfigEntryBase).IsAssignableFrom(useUtilitySlotField.FieldType))
            {
                detail = "expected HumanoidExtension, LanternItem, or itemSlotUtility members were not found";
                return false;
            }

            try
            {
                ConfigEntry<bool>? useUtilitySlot = useUtilitySlotField.GetValue(null) as ConfigEntry<bool>;
                if (useUtilitySlot == null)
                {
                    detail = "itemSlotUtility was not initialized";
                    return false;
                }

                api = new HipLanternApi(
                    (GetLanternDelegate)Delegate.CreateDelegate(typeof(GetLanternDelegate), getLanternMethod),
                    (SetLanternDelegate)Delegate.CreateDelegate(typeof(SetLanternDelegate), setLanternMethod),
                    (IsLanternDelegate)Delegate.CreateDelegate(typeof(IsLanternDelegate), isLanternMethod),
                    useUtilitySlot);
                detail = "";
                return true;
            }
            catch (Exception ex)
            {
                detail = $"failed to bind HipLantern delegates: {ex.Message}";
                return false;
            }
        }

        public bool TryGetUseUtilitySlot(out bool useUtilitySlot)
        {
            useUtilitySlot = true;
            if (_failed)
            {
                return false;
            }

            try
            {
                useUtilitySlot = _useUtilitySlot.Value;
                return true;
            }
            catch (Exception ex)
            {
                return Fail("reading itemSlotUtility", ex);
            }
        }

        public bool TryIsLanternItem(ItemData item, out bool isLantern)
        {
            isLantern = false;
            if (_failed)
            {
                return false;
            }

            try
            {
                isLantern = _isLantern(item);
                return true;
            }
            catch (Exception ex)
            {
                return Fail("checking the HipLantern item type", ex);
            }
        }

        public bool TrySetLantern(Humanoid humanoid, ItemData item, out bool changed)
        {
            changed = false;
            if (!TryGetLantern(humanoid, out ItemData? current))
            {
                return false;
            }

            if (ReferenceEquals(current, item))
            {
                return true;
            }

            return TrySetLanternValue(humanoid, item, out changed);
        }

        public bool TryIsCurrentLantern(Humanoid humanoid, ItemData item, out bool isCurrent)
        {
            isCurrent = false;
            if (!TryGetLantern(humanoid, out ItemData? current))
            {
                return false;
            }

            isCurrent = ReferenceEquals(current, item);
            return true;
        }

        public bool TryClearLantern(Humanoid humanoid, ItemData item, out bool changed)
        {
            changed = false;
            if (!TryGetLantern(humanoid, out ItemData? current))
            {
                return false;
            }

            if (!ReferenceEquals(current, item))
            {
                return true;
            }

            return TrySetLanternValue(humanoid, null, out changed);
        }

        public bool TryGetLantern(Humanoid humanoid, out ItemData? item)
        {
            item = null;
            if (_failed)
            {
                return false;
            }

            try
            {
                item = _getLantern(humanoid);
                return true;
            }
            catch (Exception ex)
            {
                return Fail("reading the equipped HipLantern", ex);
            }
        }

        private bool TrySetLanternValue(Humanoid humanoid, ItemData? item, out bool changed)
        {
            changed = false;
            if (_failed)
            {
                return false;
            }

            try
            {
                ItemData? result = _setLantern(humanoid, item);
                if (!ReferenceEquals(result, item))
                {
                    return Fail("SetHipLantern returned an unexpected item");
                }

                changed = true;
                return true;
            }
            catch (Exception ex)
            {
                return Fail("updating the equipped HipLantern", ex);
            }
        }

        private bool Fail(string operation, Exception? exception = null)
        {
            if (!_failed)
            {
                string detail = exception?.InnerException?.Message ?? exception?.Message ?? "unknown result";
                Log.LogWarning($"HipLantern compatibility disabled while {operation}: {detail}");
            }

            _failed = true;
            return false;
        }
    }
}
