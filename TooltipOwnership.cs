using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private readonly struct TooltipCanvasGroupVisualSnapshot
    {
        public TooltipCanvasGroupVisualSnapshot(bool hadCanvasGroup, float alpha, bool interactable, bool blocksRaycasts, bool ignoreParentGroups)
        {
            HadCanvasGroup = hadCanvasGroup;
            Alpha = alpha;
            Interactable = interactable;
            BlocksRaycasts = blocksRaycasts;
            IgnoreParentGroups = ignoreParentGroups;
        }

        public bool HadCanvasGroup { get; }
        public float Alpha { get; }
        public bool Interactable { get; }
        public bool BlocksRaycasts { get; }
        public bool IgnoreParentGroups { get; }
    }

    private static readonly Dictionary<GameObject, TooltipCanvasGroupVisualSnapshot> VanillaTooltipVisualSnapshots = new();
    private static readonly Dictionary<CanvasGroup, TooltipCanvasGroupVisualSnapshot> VanillaTooltipChildCanvasGroupSnapshots = new();

    private static void HideVanillaTooltipVisual(GameObject? tooltipObject)
    {
        if (tooltipObject == null || IsUnityNull(tooltipObject))
        {
            return;
        }

        CanvasGroup? existingGroup = tooltipObject.GetComponent<CanvasGroup>();
        bool firstHide = !VanillaTooltipVisualSnapshots.ContainsKey(tooltipObject);
        if (firstHide)
        {
            VanillaTooltipVisualSnapshots[tooltipObject] = existingGroup != null && !IsUnityNull(existingGroup)
                ? new TooltipCanvasGroupVisualSnapshot(true, existingGroup.alpha, existingGroup.interactable, existingGroup.blocksRaycasts, existingGroup.ignoreParentGroups)
                : new TooltipCanvasGroupVisualSnapshot(false, 1f, true, true, false);
        }

        CanvasGroup group = existingGroup != null && !IsUnityNull(existingGroup)
            ? existingGroup
            : tooltipObject.AddComponent<CanvasGroup>();
        HideVanillaTooltipCanvasGroup(group);

        if (!firstHide)
        {
            return;
        }

        foreach (CanvasGroup childGroup in tooltipObject.GetComponentsInChildren<CanvasGroup>(includeInactive: true))
        {
            if (childGroup == null || IsUnityNull(childGroup) || childGroup == group)
            {
                continue;
            }

            if (!VanillaTooltipChildCanvasGroupSnapshots.ContainsKey(childGroup))
            {
                VanillaTooltipChildCanvasGroupSnapshots[childGroup] = new TooltipCanvasGroupVisualSnapshot(
                    hadCanvasGroup: true,
                    childGroup.alpha,
                    childGroup.interactable,
                    childGroup.blocksRaycasts,
                    childGroup.ignoreParentGroups);
            }

            HideVanillaTooltipCanvasGroup(childGroup);
        }
    }

    private static void HideVanillaTooltipCanvasGroup(CanvasGroup group)
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.ignoreParentGroups = false;
    }

    private static void RestoreVanillaTooltipVisual(GameObject? tooltipObject)
    {
        if (tooltipObject == null || IsUnityNull(tooltipObject))
        {
            return;
        }

        if (!VanillaTooltipVisualSnapshots.TryGetValue(tooltipObject, out TooltipCanvasGroupVisualSnapshot snapshot))
        {
            return;
        }

        VanillaTooltipVisualSnapshots.Remove(tooltipObject);
        RestoreVanillaTooltipChildCanvasGroups(tooltipObject);
        CanvasGroup? group = tooltipObject.GetComponent<CanvasGroup>();
        if (group == null || IsUnityNull(group))
        {
            return;
        }

        if (snapshot.HadCanvasGroup)
        {
            group.alpha = snapshot.Alpha;
            group.interactable = snapshot.Interactable;
            group.blocksRaycasts = snapshot.BlocksRaycasts;
            group.ignoreParentGroups = snapshot.IgnoreParentGroups;
            return;
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = false;
        UnityEngine.Object.Destroy(group);
    }

    private static void RestoreVanillaTooltipChildCanvasGroups(GameObject tooltipObject)
    {
        List<CanvasGroup> restore = new();
        foreach (CanvasGroup group in VanillaTooltipChildCanvasGroupSnapshots.Keys)
        {
            if (group != null &&
                !IsUnityNull(group) &&
                group.transform != null &&
                (group.gameObject == tooltipObject || group.transform.IsChildOf(tooltipObject.transform)))
            {
                restore.Add(group);
            }
        }

        foreach (CanvasGroup group in restore)
        {
            TooltipCanvasGroupVisualSnapshot snapshot = VanillaTooltipChildCanvasGroupSnapshots[group];
            VanillaTooltipChildCanvasGroupSnapshots.Remove(group);
            if (group == null || IsUnityNull(group))
            {
                continue;
            }

            group.alpha = snapshot.Alpha;
            group.interactable = snapshot.Interactable;
            group.blocksRaycasts = snapshot.BlocksRaycasts;
            group.ignoreParentGroups = snapshot.IgnoreParentGroups;
        }
    }

    private static void RestartInventoryContainerHoverTooltip(UITooltip? tooltip, GameObject? hovered)
    {
        if (tooltip == null ||
            IsUnityNull(tooltip) ||
            hovered == null ||
            IsUnityNull(hovered) ||
            !hovered.activeInHierarchy ||
            !IsInventoryContainerTooltipSource(tooltip))
        {
            return;
        }

        tooltip.OnHoverStart(hovered);
    }

    private static void HideInventoryOwnedHoverTooltips()
    {
        _inventoryContainerHoverTooltipSource = null;
        _inventorySlotsOwnedHoverTooltipSource = null;
        HideInventoryContainerCustomTooltip();
        ForceHideInventorySimpleNameTooltip();
        RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
    }
}
