using System;
using System.Reflection;
using BepInEx.Configuration;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string CircletExtendedPrefabName = "HelmetDverger";
    private const int CircletExtendedSlotStateNone = 0;
    private const int CircletExtendedSlotStateValid = 1;
    private const int CircletExtendedSlotStateInvalid = 2;
    private const int CircletExtendedSlotStateLegacyHelmet = 3;
    private static Player? _circletExtendedCompatibilityPlayer;
    private static int _circletExtendedCustomSlotState = -1;

    private static bool TryGetCircletExtendedApi(out CircletExtendedApi? api)
    {
        return TryGetCompatApi(
            CircletExtendedGuid,
            "CircletExtended",
            CompatRuntime.CircletExtended,
            CircletExtendedApi.TryCreate,
            "CircletExtended compatibility disabled",
            out api);
    }

    private static bool IsCircletExtendedPrefab(ItemData? item)
    {
        return item != null &&
               string.Equals(
                   GetItemPrefabName(item),
                   CircletExtendedPrefabName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSuppressInventorySlotsCircletVisual(ItemData item)
    {
        // CircletExtended patches AttachItem even when its overlay option is off.
        // If its API changed, omitting this custom visual is safer than entering an
        // unknown AttachItem -> UpdateVisuals recursion.
        return HasCircletExtendedActive && IsCircletExtendedPrefab(item);
    }

    private static bool TryGetCircletExtendedOwnerApi(ItemData item, out CircletExtendedApi? api)
    {
        bool pluginActive = HasCircletExtendedActive;
        bool isCircletPrefab = IsCircletExtendedPrefab(item);
        api = null;
        bool compatReady =
            pluginActive &&
            isCircletPrefab &&
            TryGetCircletExtendedApi(out api) &&
            api != null;
        bool putOnTopEnabled = false;
        bool isCircletCustomType = false;
        if (compatReady)
        {
            compatReady = api!.TryGetPutOnTopEnabled(out putOnTopEnabled);
        }

        if (compatReady && putOnTopEnabled)
        {
            compatReady = api!.TryIsCircletCustomType(item, out isCircletCustomType);
        }

        return InventorySlotSafetyCore.ShouldDelegateCircletOwnership(
            pluginActive,
            compatReady,
            putOnTopEnabled,
            isCircletPrefab,
            isCircletCustomType);
    }

    private static bool TryGetCurrentCircletExtendedApi(
        Player player,
        ItemData item,
        out CircletExtendedApi? api)
    {
        api = null;
        return player != null &&
               HasCircletExtendedActive &&
               IsCircletExtendedPrefab(item) &&
               TryGetCircletExtendedApi(out api) &&
               api != null &&
               api.TryIsCurrentCirclet((Humanoid)player, item, out bool isCurrent) &&
               isCurrent;
    }

    private static bool ShouldDelegateCircletExtendedWeight(Player player, ItemData item)
    {
        return TryGetCurrentCircletExtendedApi(player, item, out CircletExtendedApi? api) &&
               api != null &&
               api.TryGetPutOnTopEnabled(out bool enabled) &&
               enabled;
    }

    private static bool ShouldDelegateCircletExtendedDurability(Player player, ItemData item)
    {
        return TryGetCurrentCircletExtendedApi(player, item, out _) &&
               ((Humanoid)player).m_helmetItem != item;
    }

    private static bool CanUseCircletExtendedCustomSlot(
        Player player,
        ItemData item,
        SlotDefinition slot)
    {
        if (slot.Kind != SlotKind.CustomEquipment)
        {
            return true;
        }

        bool pluginActive = HasCircletExtendedActive;
        bool isCircletPrefab = IsCircletExtendedPrefab(item);
        bool delegatesOwnership =
            TryGetCircletExtendedOwnerApi(item, out CircletExtendedApi? api) &&
            api != null;
        bool helmetCompatible = false;
        if (delegatesOwnership && player != null)
        {
            ItemData? helmet = ((Humanoid)player).m_helmetItem;
            if (helmet != null &&
                !ReferenceEquals(helmet, item) &&
                (!api!.TryIsCircletCustomType(helmet, out bool helmetIsCustomCirclet) || helmetIsCustomCirclet))
            {
                delegatesOwnership = false;
            }

            if (delegatesOwnership)
            {
                delegatesOwnership = api!.TryCanEquipWithHelmet(helmet, out helmetCompatible);
            }
        }

        return InventorySlotSafetyCore.CanUseCustomCircletSlot(
            pluginActive,
            isCircletPrefab,
            delegatesOwnership,
            helmetCompatible);
    }

    private static bool SynchronizeCircletExtendedEquippedState(Player player, ItemData item)
    {
        if (player == null ||
            item == null ||
            !TryGetCircletExtendedOwnerApi(item, out CircletExtendedApi? api) ||
            api == null ||
            !api.TryCanEquipWithHelmet(((Humanoid)player).m_helmetItem, out bool helmetCompatible) ||
            !helmetCompatible)
        {
            return false;
        }

        return api.TrySetCirclet((Humanoid)player, item, out bool changed) && changed;
    }

    private static bool ReconcileCircletExtendedLegacyHelmetState(Player player, Inventory inventory)
    {
        if (player == null || inventory == null)
        {
            return false;
        }

        Humanoid humanoid = player;
        ItemData? helmet = humanoid.m_helmetItem;
        if (helmet == null ||
            !inventory.ContainsItem(helmet) ||
            IsInventorySlotsCustomEquipped(helmet) ||
            !TryGetCircletExtendedOwnerApi(helmet, out _))
        {
            return false;
        }

        ClearCircletExtendedEquippedState(player, helmet);
        if (!ClearVanillaEquipmentReferences(humanoid, helmet))
        {
            return false;
        }

        helmet.m_equipped = false;
        humanoid.SetupEquipment();
        return true;
    }

    private static bool ClearCircletExtendedEquippedState(Player? player, ItemData item)
    {
        if (player == null ||
            item == null ||
            !HasCircletExtendedActive ||
            !IsCircletExtendedPrefab(item) ||
            !TryGetCircletExtendedApi(out CircletExtendedApi? api) ||
            api == null)
        {
            return false;
        }

        return api.TryClearCirclet((Humanoid)player, item, out bool changed) && changed;
    }

    private static bool RestoreCircletExtendedEquippedState(Player player, ItemData item)
    {
        if (player == null ||
            item == null ||
            !HasCircletExtendedActive ||
            !IsCircletExtendedPrefab(item) ||
            !TryGetCircletExtendedApi(out CircletExtendedApi? api) ||
            api == null)
        {
            return false;
        }

        return api.TrySetCirclet((Humanoid)player, item, out bool changed) && changed;
    }

    private static void RefreshCircletExtendedCompatibilityState(Player player)
    {
        if (player == null)
        {
            return;
        }

        if (_circletExtendedCompatibilityPlayer != player)
        {
            _circletExtendedCompatibilityPlayer = player;
            _circletExtendedCustomSlotState = -1;
        }

        if (!HasCircletExtendedActive)
        {
            _circletExtendedCustomSlotState = CircletExtendedSlotStateNone;
            return;
        }

        Humanoid humanoid = player;
        ItemData? legacyHelmet = humanoid.m_helmetItem;
        int currentState =
            legacyHelmet != null &&
            !IsInventorySlotsCustomEquipped(legacyHelmet) &&
            TryGetCircletExtendedOwnerApi(legacyHelmet, out _)
                ? CircletExtendedSlotStateLegacyHelmet
                : CircletExtendedSlotStateNone;
        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory != null)
        {
            foreach (ItemData item in inventory.m_inventory)
            {
                if (!IsInventorySlotsCustomEquipped(item) || !IsCircletExtendedPrefab(item))
                {
                    continue;
                }

                SlotDefinition? slot = GetSlotFromItemMarker(item);
                if (slot == null || !CanUseCircletExtendedCustomSlot(player, item, slot))
                {
                    currentState = CircletExtendedSlotStateInvalid;
                    break;
                }

                if (currentState == CircletExtendedSlotStateNone)
                {
                    currentState = CircletExtendedSlotStateValid;
                }
            }
        }

        int previousState = _circletExtendedCustomSlotState;
        _circletExtendedCustomSlotState = currentState;
        if (previousState != currentState &&
            (currentState >= CircletExtendedSlotStateInvalid ||
             previousState >= CircletExtendedSlotStateInvalid && currentState == CircletExtendedSlotStateValid))
        {
            InvalidateCustomEquipmentProjectionCache();
            RequestInventoryStateEnsure(
                player,
                InventoryStateEnsureReason.ConfigChanged,
                InventoryStateAuditLevel.FullIntegrity);
        }
    }

    private sealed class CircletExtendedApi
    {
        private delegate ItemData? GetCircletDelegate(Humanoid humanoid);
        private delegate ItemData? SetCircletDelegate(Humanoid humanoid, ItemData? item);
        private delegate bool IsCircletDelegate(ItemData item);
        private delegate bool CanEquipWithHelmetDelegate(ItemData? helmet);

        private readonly GetCircletDelegate _getCirclet;
        private readonly SetCircletDelegate _setCirclet;
        private readonly IsCircletDelegate _isCirclet;
        private readonly CanEquipWithHelmetDelegate _canEquipWithHelmet;
        private readonly ConfigEntry<bool> _enablePutOnTop;
        private bool _failed;

        private CircletExtendedApi(
            GetCircletDelegate getCirclet,
            SetCircletDelegate setCirclet,
            IsCircletDelegate isCirclet,
            CanEquipWithHelmetDelegate canEquipWithHelmet,
            ConfigEntry<bool> enablePutOnTop)
        {
            _getCirclet = getCirclet;
            _setCirclet = setCirclet;
            _isCirclet = isCirclet;
            _canEquipWithHelmet = canEquipWithHelmet;
            _enablePutOnTop = enablePutOnTop;
        }

        public static bool TryCreate(
            Assembly assembly,
            out CircletExtendedApi? api,
            out string detail)
        {
            api = null;
            Type? humanoidExtensionType = assembly.GetType("CircletExtended.HumanoidExtension");
            Type? circletItemType = assembly.GetType("CircletExtended.CircletItem");
            Type? pluginType = assembly.GetType("CircletExtended.CircletExtended");
            MethodInfo? getCircletMethod = humanoidExtensionType?.GetMethod(
                "GetCirclet",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Humanoid) },
                modifiers: null);
            MethodInfo? setCircletMethod = humanoidExtensionType?.GetMethod(
                "SetCirclet",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Humanoid), typeof(ItemData) },
                modifiers: null);
            MethodInfo? isCircletMethod = circletItemType?.GetMethod(
                "IsCircletItem",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(ItemData) },
                modifiers: null);
            MethodInfo? canEquipWithHelmetMethod = circletItemType?.GetMethod(
                "CanCircletBeEquippedWithHelmet",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(ItemData) },
                modifiers: null);
            FieldInfo? enablePutOnTopField = pluginType?.GetField(
                "enablePutOnTop",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (getCircletMethod?.ReturnType != typeof(ItemData) ||
                setCircletMethod?.ReturnType != typeof(ItemData) ||
                isCircletMethod?.ReturnType != typeof(bool) ||
                canEquipWithHelmetMethod?.ReturnType != typeof(bool) ||
                enablePutOnTopField == null ||
                !typeof(ConfigEntryBase).IsAssignableFrom(enablePutOnTopField.FieldType))
            {
                detail = "expected HumanoidExtension, CircletItem, or enablePutOnTop members were not found";
                return false;
            }

            try
            {
                ConfigEntry<bool>? enablePutOnTop = enablePutOnTopField.GetValue(null) as ConfigEntry<bool>;
                if (enablePutOnTop == null)
                {
                    detail = "enablePutOnTop was not initialized";
                    return false;
                }

                api = new CircletExtendedApi(
                    (GetCircletDelegate)Delegate.CreateDelegate(typeof(GetCircletDelegate), getCircletMethod),
                    (SetCircletDelegate)Delegate.CreateDelegate(typeof(SetCircletDelegate), setCircletMethod),
                    (IsCircletDelegate)Delegate.CreateDelegate(typeof(IsCircletDelegate), isCircletMethod),
                    (CanEquipWithHelmetDelegate)Delegate.CreateDelegate(typeof(CanEquipWithHelmetDelegate), canEquipWithHelmetMethod),
                    enablePutOnTop);
                detail = "";
                return true;
            }
            catch (Exception ex)
            {
                detail = $"failed to bind CircletExtended delegates: {ex.Message}";
                return false;
            }
        }

        public bool TryGetPutOnTopEnabled(out bool enabled)
        {
            enabled = false;
            if (_failed)
            {
                return false;
            }

            try
            {
                enabled = _enablePutOnTop.Value;
                return true;
            }
            catch (Exception ex)
            {
                return Fail("reading enablePutOnTop", ex);
            }
        }

        public bool TryIsCircletCustomType(ItemData item, out bool isCirclet)
        {
            isCirclet = false;
            if (_failed)
            {
                return false;
            }

            try
            {
                isCirclet = _isCirclet(item);
                return true;
            }
            catch (Exception ex)
            {
                return Fail("checking the Circlet item type", ex);
            }
        }

        public bool TryCanEquipWithHelmet(ItemData? helmet, out bool compatible)
        {
            compatible = false;
            if (_failed)
            {
                return false;
            }

            try
            {
                compatible = _canEquipWithHelmet(helmet);
                return true;
            }
            catch (Exception ex)
            {
                return Fail("checking helmet compatibility", ex);
            }
        }

        public bool TrySetCirclet(Humanoid humanoid, ItemData item, out bool changed)
        {
            changed = false;
            if (!TryGetCirclet(humanoid, out ItemData? current))
            {
                return false;
            }

            if (ReferenceEquals(current, item))
            {
                return true;
            }

            return TrySetCircletValue(humanoid, item, out changed);
        }

        public bool TryIsCurrentCirclet(Humanoid humanoid, ItemData item, out bool isCurrent)
        {
            isCurrent = false;
            if (!TryGetCirclet(humanoid, out ItemData? current))
            {
                return false;
            }

            isCurrent = ReferenceEquals(current, item);
            return true;
        }

        public bool TryClearCirclet(Humanoid humanoid, ItemData item, out bool changed)
        {
            changed = false;
            if (!TryGetCirclet(humanoid, out ItemData? current))
            {
                return false;
            }

            if (!ReferenceEquals(current, item))
            {
                return true;
            }

            return TrySetCircletValue(humanoid, null, out changed);
        }

        private bool TryGetCirclet(Humanoid humanoid, out ItemData? item)
        {
            item = null;
            if (_failed)
            {
                return false;
            }

            try
            {
                item = _getCirclet(humanoid);
                return true;
            }
            catch (Exception ex)
            {
                return Fail("reading the equipped Circlet", ex);
            }
        }

        private bool TrySetCircletValue(Humanoid humanoid, ItemData? item, out bool changed)
        {
            changed = false;
            if (_failed)
            {
                return false;
            }

            try
            {
                ItemData? result = _setCirclet(humanoid, item);
                if (!ReferenceEquals(result, item))
                {
                    return Fail("SetCirclet returned an unexpected item");
                }

                changed = true;
                return true;
            }
            catch (Exception ex)
            {
                return Fail("updating the equipped Circlet", ex);
            }
        }

        private bool Fail(string operation, Exception? exception = null)
        {
            if (!_failed)
            {
                string detail = exception?.InnerException?.Message ?? exception?.Message ?? "unknown result";
                Log.LogWarning($"CircletExtended compatibility disabled while {operation}: {detail}");
            }

            _failed = true;
            return false;
        }
    }
}
