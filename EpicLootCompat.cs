using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool? _epicLootLoaded;
    private static int _epicLootInventoryGridTooltipUiPatchScope;
    private static GameObject? _epicLootSuppressedOnHoverTooltip;
    private static bool _epicLootComparisonTooltipReflectionResolved;
    private static FieldInfo? _epicLootComparisonTooltipField;
    private static FieldInfo? _epicLootComparisonAddedField;

    private static bool IsEpicLootLoaded()
    {
        _epicLootLoaded ??= Chainloader.PluginInfos.ContainsKey(EpicLootGuid);
        return _epicLootLoaded.Value;
    }

    internal static bool IsEpicLootLoadedForPatches() =>
        IsEpicLootLoaded();

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
