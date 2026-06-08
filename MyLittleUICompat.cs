using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

internal sealed class MyLittleUICraftingObjectMarker : MonoBehaviour
{
}

public sealed partial class InventorySlotsPlugin
{
    private const float MyLittleUICraftingObjectScanInterval = 0.5f;

    private static readonly string[] MyLittleUIConflictingPatchTypeNames =
    {
        "MyLittleUI.CraftFilter+StoreGui_Awake_InitializePanel",
        "MyLittleUI.CraftFilter+InventoryGui_Show_ClearCache",
        "MyLittleUI.CraftFilter+InventoryGui_OnDestroy_ClearCache",
        "MyLittleUI.CraftFilter+Chat_HasFocus_FocusOverride",
        "MyLittleUI.CraftFilter+Player_GetAvailableRecipes_FilterRecipeList",
        "MyLittleUI.CraftFilter+InventoryGui_UpdateRecipe_HandleFieldFocus",
        "MyLittleUI.CraftSort+Game_SpawnPlayer_InitializePanel",
        "MyLittleUI.CraftSort+InventoryGui_Update_SortingPanelVisibility",
        "MyLittleUI.CraftSort+InventoryGui_UpdateCraftingPanel_HideWithOtherMods",
        "MyLittleUI.CraftSort+InventoryGui_Update_HideWithOtherMods",
        "MyLittleUI.CraftSort+InventoryGui_UpdateRecipeList_FilteringAndSorting",
        "MyLittleUI.CraftSort+InventoryGui_Update_GamepadControls",
        "MyLittleUI.MultiCraft+InventoryGui_UpdateRecipe_MulticraftShowButtons",
        "MyLittleUI.MultiCraft+InventoryGui_Awake_MulticraftCreateButtons",
        "MyLittleUI.MultiCraft+InventoryGui_SetRecipe_ResetMulticraftAfterRecipeChange",
        "MyLittleUI.Crafting.CraftNew+InventoryGui_UpdateRecipeList_CraftNewRecipeMark",
        "MyLittleUI.MyLittleUI+InventoryGui_SetupRequirement_AddAvailableAmount",
        "MyLittleUI.ItemTooltip+ItemDropItemData_GetTooltip_ItemTooltip",
        "MyLittleUI.ItemTooltip+InventoryGui_Awake_ItemTooltipCraftingFontSize"
    };

    private static readonly string[] MyLittleUICraftingObjectNames =
    {
        "MLUI_FilterField",
        "MLUI_SortingPanels",
        "MLUI_Multicraft"
    };

    private static readonly string[] MyLittleUICraftingObjectPrefixes =
    {
        "MLUI_SortingPanel_",
        "selected (MLUI_SortingPanel_"
    };

    private static readonly List<Transform> CachedMyLittleUICraftingObjects = new();
    private static int _myLittleUICraftingObjectRootSignature;
    private static float _nextMyLittleUICraftingObjectScanTime;
    private static bool _myLittleUIInventoryVisibleLastFrame;
    private static bool _myLittleUIPluginPresenceResolved;
    private static bool _myLittleUIPluginDetected;
    private static bool _myLittleUICraftingSuppressionActive;
    private static string _myLittleUICraftingSuppressionSignature = "";

    private static void ApplyMyLittleUICraftingCompatibility()
    {
        if (CompatRuntime.MyLittleUICraftingCompatibilityApplied || !IsMyLittleUIActive())
        {
            return;
        }

        int removed = 0;
        foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToArray())
        {
            Patches patchInfo = Harmony.GetPatchInfo(original);
            if (patchInfo == null)
            {
                continue;
            }

            removed += UnpatchMyLittleUIConflictingPatches(original, patchInfo.Prefixes);
            removed += UnpatchMyLittleUIConflictingPatches(original, patchInfo.Postfixes);
            removed += UnpatchMyLittleUIConflictingPatches(original, patchInfo.Transpilers);
            removed += UnpatchMyLittleUIConflictingPatches(original, patchInfo.Finalizers);
        }

        CompatRuntime.MyLittleUICraftingCompatibilityApplied = true;
        if (removed > 0)
        {
            Log.LogInfo($"Disabled {removed} MyLittleUI conflicting UI patch(es) for InventorySlots compatibility.");
        }
    }

    private static int UnpatchMyLittleUIConflictingPatches(MethodBase original, IEnumerable<Patch> patches)
    {
        int removed = 0;
        foreach (Patch patch in patches.ToArray())
        {
            if (!string.Equals(patch.owner, MyLittleUIGuid, StringComparison.Ordinal) ||
                !IsMyLittleUIConflictingPatchMethod(patch.PatchMethod))
            {
                continue;
            }

            try
            {
                _instance._harmony.Unpatch(original, patch.PatchMethod);
                removed++;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to disable MyLittleUI compatibility patch {patch.PatchMethod.FullDescription()}: {ex.Message}");
            }
        }

        return removed;
    }

    private static bool IsMyLittleUIConflictingPatchMethod(MethodInfo method)
    {
        string typeName = method.DeclaringType?.FullName ?? "";
        return MyLittleUIConflictingPatchTypeNames.Contains(typeName, StringComparer.Ordinal);
    }

    private static void UpdateMyLittleUICraftingObjectSuppression(InventoryGui? gui, bool shouldSuppress)
    {
        UpdateMyLittleUICraftingObjectSuppression(gui, shouldSuppress, GetCraftingTabAdapterState(gui));
    }

    private static void UpdateMyLittleUICraftingObjectSuppression(InventoryGui? gui, bool shouldSuppress, CraftingTabAdapterState adapter)
    {
        if (!shouldSuppress)
        {
            ResetMyLittleUICraftingObjectCache();
            return;
        }

        DisableMyLittleUICraftingObjects(gui, adapter);
    }

    private static void DisableMyLittleUICraftingObjects(InventoryGui? gui, CraftingTabAdapterState adapter)
    {
        if (!IsMyLittleUIActive() || gui == null || IsUnityNull(gui) || !InventoryGui.IsVisible())
        {
            ResetMyLittleUICraftingObjectCache();
            return;
        }

        int signature = GetMyLittleUICraftingObjectRootSignature(gui);
        string suppressionSignature = GetMyLittleUICraftingSuppressionSignature(gui, signature, adapter);
        bool shouldScan =
            !_myLittleUIInventoryVisibleLastFrame ||
            !string.Equals(_myLittleUICraftingSuppressionSignature, suppressionSignature, StringComparison.Ordinal) ||
            signature != _myLittleUICraftingObjectRootSignature ||
            Time.unscaledTime >= _nextMyLittleUICraftingObjectScanTime && !HasCachedMyLittleUICraftingObjects();

        if (shouldScan)
        {
            CachedMyLittleUICraftingObjects.Clear();
            if (gui.m_crafting != null)
            {
                CacheMyLittleUICraftingObjects(gui.m_crafting);
            }

            _myLittleUICraftingObjectRootSignature = signature;
            _myLittleUICraftingSuppressionSignature = suppressionSignature;
            _nextMyLittleUICraftingObjectScanTime = Time.unscaledTime + MyLittleUICraftingObjectScanInterval;
        }

        DisableCachedMyLittleUICraftingObjects();
        _myLittleUIInventoryVisibleLastFrame = true;
        _myLittleUICraftingSuppressionActive = true;
    }

    private static bool HasCachedMyLittleUICraftingObjects()
    {
        for (int i = CachedMyLittleUICraftingObjects.Count - 1; i >= 0; i--)
        {
            Transform transform = CachedMyLittleUICraftingObjects[i];
            if (transform == null || IsUnityNull(transform))
            {
                CachedMyLittleUICraftingObjects.RemoveAt(i);
                continue;
            }

            return true;
        }

        return false;
    }

    private static int GetMyLittleUICraftingObjectRootSignature(InventoryGui gui)
    {
        unchecked
        {
            int hash = 17;
            AddRoot(gui.m_crafting);
            return hash;

            void AddRoot(Transform? root)
            {
                if (root == null || IsUnityNull(root))
                {
                    hash = hash * 31 - 1;
                    return;
                }

                hash = hash * 31 + root.GetInstanceID();
                hash = hash * 31 + root.childCount;
                hash = hash * 31 + (root.gameObject.activeInHierarchy ? 1 : 0);
            }
        }
    }

    private static string GetMyLittleUICraftingSuppressionSignature(InventoryGui gui, int rootSignature, CraftingTabAdapterState adapter)
    {
        return $"{rootSignature}|{adapter.Kind}|applied={_craftingRedesignApplied}|craft={SafeReadBool(() => gui.InCraftTab())}|upgrade={SafeReadBool(() => gui.InUpradeTab())}";
    }

    private static void CacheMyLittleUICraftingObjects(Transform root)
    {
        foreach (MyLittleUICraftingObjectMarker marker in root.GetComponentsInChildren<MyLittleUICraftingObjectMarker>(includeInactive: true))
        {
            if (marker != null && !IsUnityNull(marker))
            {
                AddMyLittleUICraftingObject(marker.transform);
            }
        }

        CacheMyLittleUICraftingObjectsRecursive(root);
    }

    private static void CacheMyLittleUICraftingObjectsRecursive(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (IsMyLittleUICraftingObjectName(child.name))
            {
                AddMyLittleUICraftingObject(child);
            }

            CacheMyLittleUICraftingObjectsRecursive(child);
        }
    }

    private static void AddMyLittleUICraftingObject(Transform transform)
    {
        if (transform == null || IsUnityNull(transform))
        {
            return;
        }

        if (!CachedMyLittleUICraftingObjects.Contains(transform))
        {
            CachedMyLittleUICraftingObjects.Add(transform);
        }

        if (transform.GetComponent<MyLittleUICraftingObjectMarker>() == null)
        {
            transform.gameObject.AddComponent<MyLittleUICraftingObjectMarker>();
        }
    }

    private static void DisableCachedMyLittleUICraftingObjects()
    {
        for (int i = CachedMyLittleUICraftingObjects.Count - 1; i >= 0; i--)
        {
            Transform transform = CachedMyLittleUICraftingObjects[i];
            if (transform == null || IsUnityNull(transform))
            {
                CachedMyLittleUICraftingObjects.RemoveAt(i);
                continue;
            }

            if (transform.gameObject.activeSelf)
            {
                transform.gameObject.SetActive(false);
            }
        }
    }

    private static void ResetMyLittleUICraftingObjectCache()
    {
        if (!_myLittleUICraftingSuppressionActive &&
            CachedMyLittleUICraftingObjects.Count == 0 &&
            _myLittleUICraftingObjectRootSignature == 0 &&
            string.IsNullOrEmpty(_myLittleUICraftingSuppressionSignature))
        {
            return;
        }

        CachedMyLittleUICraftingObjects.Clear();
        _myLittleUICraftingObjectRootSignature = 0;
        _myLittleUICraftingSuppressionSignature = "";
        _nextMyLittleUICraftingObjectScanTime = 0f;
        _myLittleUIInventoryVisibleLastFrame = false;
        _myLittleUICraftingSuppressionActive = false;
    }

    private static bool IsMyLittleUICraftingObjectName(string name)
    {
        return MyLittleUICraftingObjectNames.Contains(name, StringComparer.Ordinal) ||
               MyLittleUICraftingObjectPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsMyLittleUIActive()
    {
        if (!_myLittleUIPluginPresenceResolved)
        {
            _myLittleUIPluginDetected = HasPlugin(MyLittleUIGuid);
            _myLittleUIPluginPresenceResolved = true;
        }

        return _myLittleUIPluginDetected;
    }
}
