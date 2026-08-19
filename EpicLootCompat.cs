using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool? _epicLootLoaded;
    private static int _epicLootInventoryGridTooltipUiPatchScope;
    private static GameObject? _epicLootSuppressedOnHoverTooltip;
    private static bool _epicLootComparisonTooltipReflectionResolved;
    private static FieldInfo? _epicLootComparisonTooltipField;
    private static FieldInfo? _epicLootComparisonAddedField;
    private static bool _epicLootPublicApiReflectionResolved;
    private static EpicLootPublicApi? _epicLootPublicApi;
    private static Func<ItemData, bool>? _epicLootSacrificeFilter;
    private static bool _epicLootSacrificeFilterRegistered;
    private static bool _epicLootSacrificeFilterCallbackDisabled;
    private static bool _epicLootPublicCacheInvalidationDisabled;
    private static bool _epicLootMagicItemBackgroundDisabled;
    private static bool _epicLootStackableMaterialQueryDisabled;
    private static bool _epicLootEquipmentEffectCacheResetReflectionResolved;
    private static MethodInfo? _epicLootEquipmentEffectCacheResetMethod;
    private static bool _epicLootEquipmentEffectCacheResetWarningLogged;
    private static bool _epicLootRuntimeItemDataReflectionResolved;
    private static bool _epicLootRuntimeItemDataWarningLogged;
    private static MethodInfo? _epicLootItemDataMethod;
    private static MethodInfo? _epicLootItemInfoLoadAllMethod;
    private static MethodInfo? _epicLootItemInfoGetMethod;
    private static Type? _epicLootMagicItemComponentType;
    private static MethodInfo? _epicLootMagicItemComponentLoadMethod;
    private static int _lastEpicLootRespawnRuntimeReloadFrame = -1;
    private static float _lastEpicLootRespawnRuntimeReloadTime = -1000f;

    private static bool IsEpicLootLoaded()
    {
        _epicLootLoaded ??= Chainloader.PluginInfos.ContainsKey(EpicLootGuid);
        return _epicLootLoaded.Value;
    }

    internal static bool IsEpicLootLoadedForPatches() =>
        IsEpicLootLoaded();

    private static void InitializeEpicLootCompatibility()
    {
        if (!TryGetEpicLootPublicApi(out EpicLootPublicApi? api) ||
            api?.RegisterSacrificeFilter == null ||
            _epicLootSacrificeFilterRegistered)
        {
            return;
        }

        _epicLootSacrificeFilter ??= CanSacrificeEpicLootItem;
        try
        {
            _epicLootSacrificeFilterRegistered = api.RegisterSacrificeFilter(ModGUID, _epicLootSacrificeFilter);
            if (!_epicLootSacrificeFilterRegistered)
            {
                Log.LogWarning("EpicLoot quick-slot sacrifice protection could not be registered; EpicLoot will keep its default sacrifice behavior.");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"EpicLoot quick-slot sacrifice protection could not be registered: {ex.GetBaseException().Message}");
        }
    }

    private static void ShutdownEpicLootCompatibility()
    {
        if (!_epicLootSacrificeFilterRegistered)
        {
            return;
        }

        _epicLootSacrificeFilterRegistered = false;
        if (!TryGetEpicLootPublicApi(out EpicLootPublicApi? api) || api?.UnregisterSacrificeFilter == null)
        {
            return;
        }

        try
        {
            _ = api.UnregisterSacrificeFilter(ModGUID);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"EpicLoot quick-slot sacrifice protection could not be unregistered: {ex.GetBaseException().Message}");
        }
    }

    private static bool CanSacrificeEpicLootItem(ItemData item)
    {
        // Unregistration can fail during shutdown while EpicLoot still retains this delegate. Once
        // inactive, it must become a no-op instead of keeping InventorySlots policy alive.
        if (!_epicLootSacrificeFilterRegistered)
        {
            return true;
        }

        if (_epicLootSacrificeFilterCallbackDisabled)
        {
            return false;
        }

        try
        {
            Player? player = Player.m_localPlayer;
            if (item == null || player == null || IsUnityNull(player))
            {
                return true;
            }

            Inventory? inventory = ((Humanoid)player).GetInventory();
            if (inventory == null ||
                !TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? slot) ||
                slot?.Kind != SlotKind.Quick)
            {
                return true;
            }

            // Grid coordinates repeat across inventories. Only protect the exact instance in the
            // local player's quick slot, never an item from a container or another provider.
            return !ReferenceEquals(inventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y), item);
        }
        catch (Exception ex)
        {
            _epicLootSacrificeFilterCallbackDisabled = true;
            Log.LogWarning($"EpicLoot quick-slot sacrifice protection failed closed: {ex.GetBaseException().Message}");

            return false;
        }
    }

    private static void ResetEpicLootEquipmentEffectCache(Player? player)
    {
        if (player == null || IsUnityNull(player) || !IsEpicLootLoaded())
        {
            return;
        }

        if (!_epicLootPublicCacheInvalidationDisabled &&
            TryGetEpicLootPublicApi(out EpicLootPublicApi? api) &&
            api?.InvalidatePlayerEffectCache != null)
        {
            try
            {
                api.InvalidatePlayerEffectCache(player);
                return;
            }
            catch (Exception ex)
            {
                _epicLootPublicCacheInvalidationDisabled = true;
                Log.LogWarning($"EpicLoot public equipment refresh failed; using the legacy cache reset fallback: {ex.GetBaseException().Message}");
            }
        }

        ResolveEpicLootEquipmentEffectCacheResetMethod();
        if (_epicLootEquipmentEffectCacheResetMethod == null)
        {
            return;
        }

        try
        {
            _epicLootEquipmentEffectCacheResetMethod.Invoke(null, new object[] { player });
        }
        catch (Exception ex)
        {
            if (_epicLootEquipmentEffectCacheResetWarningLogged)
            {
                return;
            }

            _epicLootEquipmentEffectCacheResetWarningLogged = true;
            Log.LogWarning($"Failed to reset EpicLoot equipment effect cache: {ex.GetBaseException().Message}");
        }
    }

    private static void TryApplyEpicLootMagicItemBackground(
        GameObject slotRoot,
        GameObject? equippedOverlay,
        ItemData? item,
        bool inventoryGrid)
    {
        if (slotRoot == null ||
            IsUnityNull(slotRoot) ||
            equippedOverlay == null ||
            IsUnityNull(equippedOverlay) ||
            _epicLootMagicItemBackgroundDisabled ||
            !TryGetEpicLootPublicApi(out EpicLootPublicApi? api) ||
            api?.ApplyMagicItemBackground == null)
        {
            return;
        }

        try
        {
            _ = api.ApplyMagicItemBackground(slotRoot, equippedOverlay, item, inventoryGrid);
        }
        catch (Exception ex)
        {
            _epicLootMagicItemBackgroundDisabled = true;
            Log.LogWarning($"EpicLoot magic item background integration was disabled after an error: {ex.GetBaseException().Message}");
        }
    }

    private static bool TryIsEpicLootStackableMaterialByApi(ItemData item, out bool result)
    {
        result = false;
        if (item == null ||
            _epicLootStackableMaterialQueryDisabled ||
            !TryGetEpicLootPublicApi(out EpicLootPublicApi? api) ||
            api?.IsShardStone == null ||
            api.IsMagicCraftingMaterial == null)
        {
            return false;
        }

        try
        {
            bool shardStone = api.IsShardStone(item);
            bool craftingMaterial = api.IsMagicCraftingMaterial(item);
            result = shardStone || craftingMaterial;
            return true;
        }
        catch (Exception ex)
        {
            _epicLootStackableMaterialQueryDisabled = true;
            Log.LogWarning($"EpicLoot material API query failed; using the prefab-name fallback: {ex.GetBaseException().Message}");

            result = false;
            return false;
        }
    }

    private static bool TryGetEpicLootPublicApi(out EpicLootPublicApi? api)
    {
        api = _epicLootPublicApi;
        if (_epicLootPublicApiReflectionResolved)
        {
            return api != null;
        }

        _epicLootPublicApiReflectionResolved = true;
        if (!IsEpicLootLoaded() ||
            !Chainloader.PluginInfos.TryGetValue(EpicLootGuid, out BepInEx.PluginInfo pluginInfo) ||
            pluginInfo.Instance == null)
        {
            return false;
        }

        try
        {
            Type? apiType = pluginInfo.Instance.GetType().Assembly.GetType("EpicLoot.API");
            MethodInfo? getApiVersion = apiType?.GetMethod(
                "GetApiVersion",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (getApiVersion == null ||
                getApiVersion.ReturnType != typeof(int) ||
                getApiVersion.Invoke(null, Array.Empty<object>()) is not int apiVersion ||
                apiVersion < 1)
            {
                // EpicLoot 0.12 and earlier do not expose the integration API. Keep the existing
                // reflection fallbacks and otherwise leave their behavior unchanged.
                return false;
            }

            _epicLootPublicApi = new EpicLootPublicApi(apiType!);
            api = _epicLootPublicApi;
            return true;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"EpicLoot public API compatibility was disabled: {ex.GetBaseException().Message}");

            return false;
        }
    }

    private sealed class EpicLootPublicApi
    {
        public readonly Func<string, Func<ItemData, bool>, bool>? RegisterSacrificeFilter;
        public readonly Func<string, bool>? UnregisterSacrificeFilter;
        public readonly Action<Player>? InvalidatePlayerEffectCache;
        public readonly Func<GameObject, GameObject, ItemData?, bool, bool>? ApplyMagicItemBackground;
        public readonly Func<ItemData, bool>? IsShardStone;
        public readonly Func<ItemData, bool>? IsMagicCraftingMaterial;

        public EpicLootPublicApi(Type apiType)
        {
            RegisterSacrificeFilter = (Func<string, Func<ItemData, bool>, bool>?)CreateDelegate(
                apiType,
                "RegisterSacrificeFilter",
                typeof(bool),
                typeof(Func<string, Func<ItemData, bool>, bool>),
                typeof(string),
                typeof(Func<ItemData, bool>));
            UnregisterSacrificeFilter = (Func<string, bool>?)CreateDelegate(
                apiType,
                "UnregisterSacrificeFilter",
                typeof(bool),
                typeof(Func<string, bool>),
                typeof(string));
            InvalidatePlayerEffectCache = (Action<Player>?)CreateDelegate(
                apiType,
                "InvalidatePlayerEffectCache",
                typeof(void),
                typeof(Action<Player>),
                typeof(Player));
            ApplyMagicItemBackground = (Func<GameObject, GameObject, ItemData?, bool, bool>?)CreateDelegate(
                apiType,
                "ApplyMagicItemBackground",
                typeof(bool),
                typeof(Func<GameObject, GameObject, ItemData, bool, bool>),
                typeof(GameObject),
                typeof(GameObject),
                typeof(ItemData),
                typeof(bool));
            IsShardStone = (Func<ItemData, bool>?)CreateDelegate(
                apiType,
                "IsShardStone",
                typeof(bool),
                typeof(Func<ItemData, bool>),
                typeof(ItemData));
            IsMagicCraftingMaterial = (Func<ItemData, bool>?)CreateDelegate(
                apiType,
                "IsMagicCraftingMaterial",
                typeof(bool),
                typeof(Func<ItemData, bool>),
                typeof(ItemData));
        }

        private static Delegate? CreateDelegate(
            Type apiType,
            string methodName,
            Type returnType,
            Type delegateType,
            params Type[] parameterTypes)
        {
            MethodInfo? method = apiType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            return method?.ReturnType == returnType
                ? Delegate.CreateDelegate(delegateType, method)
                : null;
        }
    }

    private static void ScheduleEpicLootRespawnRuntimeReload(Player? player)
    {
        if (player == null ||
            IsUnityNull(player) ||
            player != Player.m_localPlayer ||
            !IsEpicLootLoaded() ||
            _instance == null ||
            IsUnityNull(_instance) ||
            Time.frameCount == _lastEpicLootRespawnRuntimeReloadFrame ||
            Time.unscaledTime - _lastEpicLootRespawnRuntimeReloadTime < 0.25f)
        {
            return;
        }

        _lastEpicLootRespawnRuntimeReloadFrame = Time.frameCount;
        _lastEpicLootRespawnRuntimeReloadTime = Time.unscaledTime;
        try
        {
            _instance.StartCoroutine(DelayedEpicLootRespawnRuntimeReload(player));
        }
        catch (Exception ex)
        {
            if (!_epicLootRuntimeItemDataWarningLogged)
            {
                _epicLootRuntimeItemDataWarningLogged = true;
                Log.LogWarning($"Failed to schedule EpicLoot runtime item reload: {ex.GetBaseException().Message}");
            }
        }
    }

    private static IEnumerator DelayedEpicLootRespawnRuntimeReload(Player player)
    {
        yield return new WaitForSecondsRealtime(0.35f);

        if (player != null && !IsUnityNull(player) && player == Player.m_localPlayer)
        {
            ReloadEpicLootRuntimeItemData(player);
        }
    }

    private static void ReloadEpicLootRuntimeItemData(Player? player)
    {
        if (player == null || IsUnityNull(player) || !IsEpicLootLoaded())
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory?.m_inventory == null)
        {
            return;
        }

        ResolveEpicLootRuntimeItemDataMethods();
        if (_epicLootItemDataMethod == null)
        {
            return;
        }

        foreach (ItemData item in inventory.m_inventory)
        {
            if (HasNonNullEpicLootMagicData(item))
            {
                LoadEpicLootRuntimeItemData(item);
            }
        }

        ResetEpicLootEquipmentEffectCache(player);
    }

    private static bool HasNonNullEpicLootMagicData(ItemData? item)
    {
        if (item?.m_customData == null ||
            !item.m_customData.TryGetValue("randyknapp.mods.epicloot#EpicLoot.MagicItemComponent", out string value))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(value) &&
               !string.Equals(value.Trim(), "null", StringComparison.OrdinalIgnoreCase);
    }

    private static void LoadEpicLootRuntimeItemData(ItemData item)
    {
        try
        {
            object? itemInfo = _epicLootItemDataMethod?.Invoke(null, new object[] { item });
            if (itemInfo == null)
            {
                return;
            }

            _epicLootItemInfoLoadAllMethod?.Invoke(itemInfo, Array.Empty<object>());
            if (_epicLootItemInfoGetMethod == null || _epicLootMagicItemComponentType == null)
            {
                return;
            }

            object? component = _epicLootItemInfoGetMethod
                .MakeGenericMethod(_epicLootMagicItemComponentType)
                .Invoke(itemInfo, new object[] { "" });
            if (component != null)
            {
                _epicLootMagicItemComponentLoadMethod?.Invoke(component, Array.Empty<object>());
            }
        }
        catch (Exception ex)
        {
            if (_epicLootRuntimeItemDataWarningLogged)
            {
                return;
            }

            _epicLootRuntimeItemDataWarningLogged = true;
            Log.LogWarning($"Failed to reload EpicLoot runtime item data: {ex.GetBaseException().Message}");
        }
    }

    private static void ResolveEpicLootRuntimeItemDataMethods()
    {
        if (_epicLootRuntimeItemDataReflectionResolved)
        {
            return;
        }

        _epicLootRuntimeItemDataReflectionResolved = true;
        _epicLootItemDataMethod = AccessTools.Method(
            "EpicLoot.Data.ItemExtensions:Data",
            new[] { typeof(ItemData) });
        _epicLootMagicItemComponentType = AccessTools.TypeByName("EpicLoot.MagicItemComponent");
        if (_epicLootMagicItemComponentType != null)
        {
            _epicLootMagicItemComponentLoadMethod = AccessTools.Method(_epicLootMagicItemComponentType, "Load");
        }

        Type? itemInfoType = _epicLootItemDataMethod?.ReturnType;
        if (itemInfoType == null)
        {
            return;
        }

        _epicLootItemInfoLoadAllMethod = AccessTools.Method(itemInfoType, "LoadAll");
        foreach (MethodInfo method in itemInfoType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.Name == "Get" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
            {
                _epicLootItemInfoGetMethod = method;
                break;
            }
        }
    }

    private static void ResolveEpicLootEquipmentEffectCacheResetMethod()
    {
        if (_epicLootEquipmentEffectCacheResetReflectionResolved)
        {
            return;
        }

        _epicLootEquipmentEffectCacheResetReflectionResolved = true;
        _epicLootEquipmentEffectCacheResetMethod = AccessTools.Method(
            "EpicLoot.EquipmentEffectCache:Reset",
            new[] { typeof(Player) });
    }

    internal static bool ShouldUpdateInventorySlotsOwnedHoverTooltip(UITooltip tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip))
        {
            return false;
        }

        if (_inventorySlotsOwnedHoverTooltipSource != null &&
            !IsUnityNull(_inventorySlotsOwnedHoverTooltipSource) &&
            _inventorySlotsOwnedHoverTooltipSource == tooltip)
        {
            return true;
        }

        return UITooltip.m_current == tooltip && ShouldUseInventorySlotsOwnedHoverTooltip(tooltip);
    }

    internal static bool ShouldSuppressEpicLootTooltipLayoutPatch(GameObject? hovered)
    {
        if (!IsEpicLootLoaded())
        {
            return false;
        }

        if (_epicLootInventoryGridTooltipUiPatchScope > 0)
        {
            return true;
        }

        if (hovered != null &&
            !IsUnityNull(hovered) &&
            IsInventoryContainerGridTransform(hovered.transform))
        {
            return true;
        }

        if (ShouldSuppressEpicLootTooltipLayoutPatchForTooltip(UITooltip.m_current))
        {
            return true;
        }

        UITooltip? hoveredTooltip = hovered != null && !IsUnityNull(hovered)
            ? hovered.GetComponentInParent<UITooltip>()
            : null;
        return ShouldSuppressEpicLootTooltipLayoutPatchForTooltip(hoveredTooltip);
    }

    internal static bool ShouldRunEpicLootComparisonTooltipPatch()
    {
        bool suppress = ShouldSuppressEpicLootTooltipLayoutPatch(UITooltip.m_hovered);
        if (suppress)
        {
            ClearEpicLootComparisonTooltip();
        }

        return !suppress;
    }

    internal static bool ShouldRunEpicLootOnHoverPostfix(GameObject? hovered)
    {
        if (IsUnsafeEpicLootTooltipObject(UITooltip.m_tooltip, out _))
        {
            return false;
        }

        return !ShouldSuppressEpicLootTooltipLayoutPatch(hovered);
    }

    internal static bool ShouldRunEpicLootAddScrollbarPatch(GameObject? tooltipObject, RectTransform? hoverTransform)
    {
        if (IsUnsafeEpicLootTooltipObject(tooltipObject, out _))
        {
            return false;
        }

        return !ShouldSuppressEpicLootTooltipLayoutPatch(
            hoverTransform != null
                ? hoverTransform.gameObject
                : null);
    }

    internal static void HideTooltipFromEpicLootOnHoverPostfix(GameObject? hovered)
    {
        if (!IsEpicLootLoaded() || _epicLootSuppressedOnHoverTooltip != null)
        {
            return;
        }

        GameObject? tooltipObject = UITooltip.m_tooltip;
        if (tooltipObject == null || IsUnityNull(tooltipObject))
        {
            return;
        }

        bool suppress = ShouldSuppressEpicLootTooltipLayoutPatch(hovered);
        if (!suppress && IsUnsafeEpicLootTooltipObject(tooltipObject, out _))
        {
            suppress = true;
        }

        if (!suppress)
        {
            return;
        }

        _epicLootSuppressedOnHoverTooltip = tooltipObject;
        UITooltip.m_tooltip = null;
    }

    internal static void RestoreTooltipAfterEpicLootOnHoverPostfix()
    {
        if (_epicLootSuppressedOnHoverTooltip == null)
        {
            return;
        }

        if (UITooltip.m_tooltip == null || IsUnityNull(UITooltip.m_tooltip))
        {
            UITooltip.m_tooltip = _epicLootSuppressedOnHoverTooltip;
        }

        _epicLootSuppressedOnHoverTooltip = null;
    }

    internal static void SuppressEpicLootInventoryContainerTooltipArtifacts()
    {
        if (!IsEpicLootLoaded())
        {
            return;
        }

        ClearEpicLootComparisonTooltip();
        HideEpicLootScrollArtifacts(UITooltip.m_tooltip);
    }

    internal static void BeginEpicLootInventoryGridTooltipUiPatchScope(InventoryGrid grid)
    {
        if (IsEpicLootLoaded() && IsInventoryContainerGrid(grid))
        {
            _epicLootInventoryGridTooltipUiPatchScope++;
        }
    }

    internal static void EndEpicLootInventoryGridTooltipUiPatchScope(InventoryGrid grid)
    {
        if (IsEpicLootLoaded() &&
            IsInventoryContainerGrid(grid) &&
            _epicLootInventoryGridTooltipUiPatchScope > 0)
        {
            _epicLootInventoryGridTooltipUiPatchScope--;
        }
    }

    private static bool ShouldSuppressEpicLootTooltipLayoutPatchForTooltip(UITooltip? tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip))
        {
            return false;
        }

        return IsInventorySlotsOwnedTooltipLayoutSource(tooltip) ||
               HoverTooltipSourceCore.SuppressesEpicLootTooltipLayout(ResolveHoverTooltipSourceKind(tooltip));
    }

    private static bool IsInventorySlotsOwnedTooltipLayoutSource(UITooltip tooltip)
    {
        return (_inventoryContainerHoverTooltipSource != null &&
                !IsUnityNull(_inventoryContainerHoverTooltipSource) &&
                _inventoryContainerHoverTooltipSource == tooltip) ||
               (_inventorySlotsOwnedHoverTooltipSource != null &&
                !IsUnityNull(_inventorySlotsOwnedHoverTooltipSource) &&
                _inventorySlotsOwnedHoverTooltipSource == tooltip);
    }

    private static bool IsInventoryContainerGrid(InventoryGrid? grid)
    {
        InventoryGui? gui = InventoryGui.instance;
        return grid != null &&
               !IsUnityNull(grid) &&
               gui != null &&
               !IsUnityNull(gui) &&
               (grid == gui.m_playerGrid || grid == gui.m_containerGrid);
    }

    private static bool IsInventoryContainerGridTransform(Transform? transform)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (transform == null || gui == null || IsUnityNull(gui))
        {
            return false;
        }

        return IsTooltipSourceInGrid(transform, gui.m_playerGrid) ||
               IsTooltipSourceInGrid(transform, gui.m_containerGrid) ||
               IsTooltipSourceInInventorySlotsPanel(transform);
    }

    private static bool IsUnsafeEpicLootTooltipObject(GameObject? tooltipObject, out string reason)
    {
        reason = "";
        if (tooltipObject == null || IsUnityNull(tooltipObject))
        {
            return false;
        }

        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || IsUnityNull(gui))
        {
            return false;
        }

        Transform tooltipTransform = tooltipObject.transform;
        if (gui.m_inventoryRoot != null && !IsUnityNull(gui.m_inventoryRoot) && tooltipTransform == gui.m_inventoryRoot)
        {
            reason = "tooltipObject-is-inventoryRoot";
            return true;
        }

        if (gui.m_player != null && !IsUnityNull(gui.m_player) && tooltipTransform == gui.m_player)
        {
            reason = "tooltipObject-is-playerPanel";
            return true;
        }

        Transform? tooltipBkg = tooltipTransform.Find("Bkg");
        Transform? playerBkg = gui.m_player != null && !IsUnityNull(gui.m_player) ? gui.m_player.Find("Bkg") : null;
        if (tooltipBkg != null && playerBkg != null && tooltipBkg == playerBkg)
        {
            reason = "tooltipObject-would-destroy-playerBkg";
            return true;
        }

        return false;
    }

    private static void ClearEpicLootComparisonTooltip()
    {
        if (!IsEpicLootLoaded())
        {
            return;
        }

        ResolveEpicLootComparisonTooltipFields();
        if (_epicLootComparisonTooltipField == null)
        {
            return;
        }

        if (_epicLootComparisonTooltipField.GetValue(null) is GameObject comparisonTooltip &&
            !IsUnityNull(comparisonTooltip))
        {
            UnityEngine.Object.Destroy(comparisonTooltip);
        }

        _epicLootComparisonTooltipField.SetValue(null, null);
        _epicLootComparisonAddedField?.SetValue(null, false);
    }

    private static void HideEpicLootScrollArtifacts(GameObject? tooltipObject)
    {
        if (tooltipObject == null || IsUnityNull(tooltipObject))
        {
            return;
        }

        foreach (ScrollRect scrollRect in tooltipObject.GetComponentsInChildren<ScrollRect>(includeInactive: true))
        {
            if (scrollRect == null || IsUnityNull(scrollRect))
            {
                continue;
            }

            if (IsEpicLootTooltipScrollArtifact(scrollRect.transform))
            {
                scrollRect.enabled = false;
                HideEpicLootArtifactObject(scrollRect.gameObject);
            }
        }

        foreach (Scrollbar scrollbar in tooltipObject.GetComponentsInChildren<Scrollbar>(includeInactive: true))
        {
            if (scrollbar == null || IsUnityNull(scrollbar))
            {
                continue;
            }

            if (IsEpicLootTooltipScrollArtifact(scrollbar.transform))
            {
                scrollbar.enabled = false;
                HideEpicLootArtifactObject(scrollbar.gameObject);
            }
        }
    }

    private static bool IsEpicLootTooltipScrollArtifact(Transform transform)
    {
        for (Transform? current = transform; current != null; current = current.parent)
        {
            string name = current.name;
            if (string.Equals(name, "Scroll View", StringComparison.Ordinal) ||
                string.Equals(name, "Scrollbar", StringComparison.Ordinal) ||
                string.Equals(name, "Sliding Area", StringComparison.Ordinal) ||
                string.Equals(name, "Handle", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void HideEpicLootArtifactObject(GameObject artifact)
    {
        CanvasGroup group = artifact.GetComponent<CanvasGroup>() ?? artifact.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.ignoreParentGroups = false;
    }

    private static void ResolveEpicLootComparisonTooltipFields()
    {
        if (_epicLootComparisonTooltipReflectionResolved)
        {
            return;
        }

        _epicLootComparisonTooltipReflectionResolved = true;
        Type? patchOnHoverFixType = AccessTools.TypeByName("EpicLoot.PatchOnHoverFix");
        if (patchOnHoverFixType == null)
        {
            return;
        }

        _epicLootComparisonTooltipField = AccessTools.Field(patchOnHoverFixType, "ComparisonTT");
        _epicLootComparisonAddedField = AccessTools.Field(patchOnHoverFixType, "ComparisonAdded");
    }

    private static bool HasInventorySlotsCraftingTooltipRoot(Transform transform)
    {
        for (Transform? current = transform; current != null; current = current.parent)
        {
            string name = current.name;
            if (name.StartsWith(CraftingGroupButtonNamePrefix, StringComparison.Ordinal) ||
                name.StartsWith(CraftingPinnedTooltipNamePrefix, StringComparison.Ordinal) ||
                string.Equals(name, CraftingTooltipRecipeOverlayName, StringComparison.Ordinal) ||
                string.Equals(name, CraftingUpgradeProgressionName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(UITooltip), "OnHoverStart")]
[HarmonyPriority(Priority.First)]
[HarmonyBefore(new[] { "randyknapp.mods.epicloot" })]
internal static class UITooltipOnHoverStartHideTooltipFromEpicLootPatch
{
    private static void Postfix(GameObject go)
    {
        InventorySlotsPlugin.HideTooltipFromEpicLootOnHoverPostfix(go);
    }
}

[HarmonyPatch(typeof(UITooltip), "OnHoverStart")]
[HarmonyPriority(Priority.Last)]
[HarmonyAfter(new[] { "randyknapp.mods.epicloot" })]
internal static class UITooltipOnHoverStartRestoreTooltipAfterEpicLootPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.RestoreTooltipAfterEpicLootOnHoverPostfix();
    }
}

[HarmonyPatch]
internal static class EpicLootPatchOnHoverFixScrollbarInventorySlotsPatch
{
    private static bool Prepare() =>
        InventorySlotsPlugin.IsEpicLootLoadedForPatches() &&
        AccessTools.Method("EpicLoot.PatchOnHoverFix:Postfix", new[] { typeof(GameObject) }) != null;

    private static MethodBase TargetMethod() =>
        AccessTools.Method("EpicLoot.PatchOnHoverFix:Postfix", new[] { typeof(GameObject) });

    private static bool Prefix(GameObject go) =>
        InventorySlotsPlugin.ShouldRunEpicLootOnHoverPostfix(go);
}

[HarmonyPatch]
internal static class EpicLootPatchOnHoverFixComparisonInventorySlotsPatch
{
    private static bool Prepare() =>
        InventorySlotsPlugin.IsEpicLootLoadedForPatches() &&
        AccessTools.Method("EpicLoot.PatchOnHoverFix:AddComparisonTooltip") != null;

    private static MethodBase TargetMethod() =>
        AccessTools.Method("EpicLoot.PatchOnHoverFix:AddComparisonTooltip");

    private static bool Prefix() =>
        InventorySlotsPlugin.ShouldRunEpicLootComparisonTooltipPatch();
}

[HarmonyPatch]
internal static class EpicLootPatchOnHoverFixAddScrollbarInventorySlotsPatch
{
    private static bool Prepare() =>
        InventorySlotsPlugin.IsEpicLootLoadedForPatches() &&
        AccessTools.Method("EpicLoot.PatchOnHoverFix:AddScrollbar", new[] { typeof(GameObject), typeof(RectTransform) }) != null;

    private static MethodBase TargetMethod() =>
        AccessTools.Method("EpicLoot.PatchOnHoverFix:AddScrollbar", new[] { typeof(GameObject), typeof(RectTransform) });

    private static bool Prefix(GameObject tooltipObject, RectTransform hoverTransform) =>
        InventorySlotsPlugin.ShouldRunEpicLootAddScrollbarPatch(tooltipObject, hoverTransform);
}
