using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void PositionQuickSlotPanel(InventoryGrid playerGrid, RectTransform quickPanel, Vector3 targetGridLocalPosition, float elementSpace)
    {
        // IsVisible stays true briefly after Hide, and vanilla can update the
        // grid again in that frame. Do not reattach an outgoing panel to root.
        if (InventoryPanels.QuickSlotPanelOutroActive)
        {
            return;
        }

        RectTransform? stableParent = GetQuickSlotPanelStableParent(playerGrid);
        if (stableParent != null && quickPanel.parent != stableParent)
        {
            quickPanel.SetParent(stableParent, false);
        }

        // Keep the quick slots beside Player in the native inventory layer.
        // Appending them to the canvas renders them above every inventory dialog.
        if (stableParent != null && stableParent != playerGrid.m_gridRoot)
        {
            Transform? playerBranch = playerGrid.m_gridRoot;
            while (playerBranch != null && playerBranch.parent != stableParent)
            {
                playerBranch = playerBranch.parent;
            }

            if (playerBranch != null)
            {
                int currentIndex = quickPanel.GetSiblingIndex();
                int playerIndex = playerBranch.GetSiblingIndex();
                // Removing a sibling before Player shifts Player one index left.
                int targetIndex = playerIndex + (currentIndex > playerIndex ? 1 : 0);
                if (currentIndex != targetIndex)
                {
                    quickPanel.SetSiblingIndex(targetIndex);
                }
            }
        }

        DestroyDuplicateQuickSlotPanels(playerGrid, quickPanel);

        Vector3 targetPosition = targetGridLocalPosition;
        if (quickPanel.parent != playerGrid.m_gridRoot && quickPanel.parent is RectTransform parentRect)
        {
            Vector3 targetWorldPosition = playerGrid.m_gridRoot.TransformPoint(targetGridLocalPosition);
            targetPosition = parentRect.InverseTransformPoint(targetWorldPosition);
        }

        CaptureQuickSlotHudAnchor(quickPanel, targetPosition, elementSpace);
        quickPanel.localPosition = ApplyQuickSlotPanelIntroAnimation(quickPanel, targetPosition, elementSpace);
    }

    private static void CaptureQuickSlotHudAnchor(RectTransform quickPanel, Vector3 targetPosition, float elementSpace)
    {
        if (_quickSlotHudFollowsPanel != null && _quickSlotHudFollowsPanel.Value == Toggle.Off)
        {
            return;
        }

        if (quickPanel == null || quickPanel.parent == null || Hud.instance == null || Hud.instance.m_rootObject == null)
        {
            return;
        }

        // Vanilla hides the HUD by moving its root offscreen. Capturing against
        // that root would cancel the hidden offset and persist the wrong position.
        if (Hud.IsUserHidden() || !Hud.instance.IsVisible())
        {
            return;
        }

        RectTransform? hudRoot = Hud.instance.m_rootObject.GetComponent<RectTransform>();
        if (hudRoot == null)
        {
            return;
        }

        Vector3 currentPosition = quickPanel.localPosition;
        quickPanel.localPosition = targetPosition;
        Vector3[] worldCorners = new Vector3[4];
        quickPanel.GetWorldCorners(worldCorners);
        quickPanel.localPosition = currentPosition;

        Vector3 anchoredPosition = hudRoot.InverseTransformPoint(worldCorners[1]);
        float hudElementSpace = Mathf.Max(1f, elementSpace);
        bool shouldSave =
            !InventoryPanels.QuickSlotHudAnchorValid ||
            (InventoryPanels.QuickSlotHudAnchoredPosition - anchoredPosition).sqrMagnitude > 0.0001f ||
            Mathf.Abs(InventoryPanels.QuickSlotHudElementSpace - hudElementSpace) > 0.01f;

        InventoryPanels.QuickSlotHudAnchoredPosition = anchoredPosition;
        InventoryPanels.QuickSlotHudElementSpace = hudElementSpace;
        InventoryPanels.QuickSlotHudAnchorValid = true;
        if (shouldSave && !InventoryPanels.DraggingQuickSlotsPanelOffset)
        {
            SaveQuickSlotHudAnchor();
        }
    }

    private static void OnQuickSlotHudFollowsPanelChanged()
    {
        if (_quickSlotHudFollowsPanel.Value == Toggle.Off)
        {
            SaveQuickSlotHudAnchor();
        }
    }

    private static RectTransform? GetQuickSlotPanelStableParent(InventoryGrid playerGrid)
    {
        // The full-screen root does not share Player's slide animation, but its
        // child order keeps native dialogs and inventory popups above the slots.
        InventoryGui? gui = InventoryGui.instance;
        if (gui != null && gui.m_inventoryRoot is RectTransform inventoryRoot)
        {
            return inventoryRoot;
        }

        return playerGrid.m_gridRoot;
    }

    private static void DestroyDuplicateQuickSlotPanels(InventoryGrid playerGrid, RectTransform current)
    {
        HashSet<Transform> parents = new();
        if (current.parent != null)
        {
            parents.Add(current.parent);
        }

        if (playerGrid.m_gridRoot != null)
        {
            parents.Add(playerGrid.m_gridRoot);
        }

        foreach (Transform parent in parents)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == current || !string.Equals(child.name, QuickSlotPanelName, StringComparison.Ordinal))
                {
                    continue;
                }

                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    private static Vector3 ApplyQuickSlotPanelIntroAnimation(RectTransform panel, Vector3 targetPosition, float elementSpace)
    {
        if (!InventoryPanels.QuickSlotPanelIntroActive)
        {
            return targetPosition;
        }

        float duration = Mathf.Max(0.01f, InventoryPanels.QuickSlotPanelIntroDuration);
        float elapsed = Time.unscaledTime - InventoryPanels.QuickSlotPanelIntroStartTime;
        if (elapsed >= duration)
        {
            InventoryPanels.QuickSlotPanelIntroActive = false;
            return targetPosition;
        }

        float t = Mathf.Clamp01(elapsed / duration);
        t = t * t * (3f - 2f * t);
        float yOffset = Mathf.Lerp(-GetQuickSlotPanelOffscreenDistance(panel, elementSpace), 0f, t);
        return targetPosition + new Vector3(0f, yOffset, 0f);
    }

    private static float GetQuickSlotPanelOffscreenDistance(RectTransform panel, float elementSpace)
    {
        float parentHeight = panel.parent is RectTransform parentRect ? parentRect.rect.height : Screen.height;
        return Mathf.Max(parentHeight + Mathf.Max(1f, elementSpace) * QuickSlotPanelRows, Mathf.Max(1f, elementSpace) * 12f);
    }

    internal static void StartQuickSlotPanelIntroAnimation()
    {
        InventoryPanels.QuickSlotPanelOutroActive = false;
        InventoryPanels.QuickSlotPanelOutroStartPositions.Clear();
        InventoryPanels.QuickSlotPanelIntroStartTime = Time.unscaledTime;
        InventoryPanels.QuickSlotPanelIntroDuration = GetInventoryGuiAnimationDuration();
        InventoryPanels.QuickSlotPanelIntroActive = true;
    }

    internal static void StopQuickSlotPanelIntroAnimation()
    {
        InventoryPanels.QuickSlotPanelIntroActive = false;
    }

    internal static void StartQuickSlotPanelOutroAnimation()
    {
        StopQuickSlotPanelIntroAnimation();
        InventoryPanels.QuickSlotPanelOutroStartPositions.Clear();
        InventoryGui? gui = InventoryGui.instance;
        Canvas? canvas = gui != null ? gui.GetComponentInParent<Canvas>() : null;
        foreach (RectTransform panel in InventoryPanels.QuickSlotPanels.Values)
        {
            if (IsUnityNull(panel) || !panel.gameObject.activeSelf)
            {
                continue;
            }

            // Vanilla disables m_inventoryRoot before our independent slide ends.
            // Detach only for the outro, preserving world position and staying
            // behind the native root; PositionQuickSlotPanel restores the parent.
            if (canvas != null)
            {
                panel.SetParent(canvas.transform, true);
                Transform? inventoryRoot = gui!.m_inventoryRoot;
                if (inventoryRoot != null && inventoryRoot.parent == canvas.transform &&
                    panel.GetSiblingIndex() > inventoryRoot.GetSiblingIndex())
                {
                    panel.SetSiblingIndex(inventoryRoot.GetSiblingIndex());
                }
            }

            InventoryPanels.QuickSlotPanelOutroStartPositions[panel.GetInstanceID()] = panel.localPosition;
        }

        if (InventoryPanels.QuickSlotPanelOutroStartPositions.Count == 0)
        {
            HideQuickSlotInventoryPanels();
            return;
        }

        InventoryPanels.QuickSlotPanelOutroStartTime = Time.unscaledTime;
        InventoryPanels.QuickSlotPanelOutroDuration = GetInventoryGuiAnimationDuration();
        InventoryPanels.QuickSlotPanelOutroActive = true;
    }

    internal static void UpdateQuickSlotInventoryPanelsWhileHidden()
    {
        if (!InventoryPanels.QuickSlotPanelOutroActive)
        {
            HideQuickSlotInventoryPanels();
            return;
        }

        float duration = Mathf.Max(0.01f, InventoryPanels.QuickSlotPanelOutroDuration);
        float elapsed = Time.unscaledTime - InventoryPanels.QuickSlotPanelOutroStartTime;
        if (elapsed >= duration)
        {
            HideQuickSlotInventoryPanels();
            return;
        }

        float t = Mathf.Clamp01(elapsed / duration);
        t = t * t * (3f - 2f * t);
        foreach (RectTransform panel in InventoryPanels.QuickSlotPanels.Values)
        {
            if (IsUnityNull(panel))
            {
                continue;
            }

            int key = panel.GetInstanceID();
            if (!InventoryPanels.QuickSlotPanelOutroStartPositions.TryGetValue(key, out Vector3 startPosition))
            {
                startPosition = panel.localPosition;
                InventoryPanels.QuickSlotPanelOutroStartPositions[key] = startPosition;
            }

            float elementSpace = panel.sizeDelta.x > 1f ? panel.sizeDelta.x / QuickSlotPanelColumns : 70f;
            float yOffset = Mathf.Lerp(0f, -GetQuickSlotPanelOffscreenDistance(panel, elementSpace), t);
            panel.localPosition = startPosition + new Vector3(0f, yOffset, 0f);
            panel.gameObject.SetActive(true);
        }
    }

    internal static void HideQuickSlotInventoryPanels()
    {
        StopQuickSlotPanelIntroAnimation();
        InventoryPanels.QuickSlotPanelOutroActive = false;
        InventoryPanels.QuickSlotPanelOutroStartPositions.Clear();
        foreach (RectTransform panel in InventoryPanels.QuickSlotPanels.Values)
        {
            if (!IsUnityNull(panel))
            {
                panel.gameObject.SetActive(false);
            }
        }
    }

    private static float GetInventoryGuiAnimationDuration()
    {
        Animator? animator = InventoryGui.instance != null ? InventoryGui.instance.m_animator : null;
        if (animator == null)
        {
            return QuickSlotPanelIntroFallbackDuration;
        }

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller != null)
        {
            AnimationClip? clip = controller.animationClips
                .Where(c => c != null && c.length > 0.05f && c.length < 2f)
                .OrderByDescending(c => IsLikelyInventoryOpenClip(c.name))
                .ThenBy(c => Mathf.Abs(c.length - QuickSlotPanelIntroFallbackDuration))
                .FirstOrDefault();
            if (clip != null)
            {
                return Mathf.Clamp(clip.length, 0.05f, 1f);
            }
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.length > 0.05f && state.length < 2f ? Mathf.Clamp(state.length, 0.05f, 1f) : QuickSlotPanelIntroFallbackDuration;
    }

    private static bool IsLikelyInventoryOpenClip(string clipName)
    {
        return !string.IsNullOrWhiteSpace(clipName) &&
               clipName.IndexOf("invent", StringComparison.OrdinalIgnoreCase) >= 0 &&
               (clipName.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clipName.IndexOf("show", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clipName.IndexOf("in", StringComparison.OrdinalIgnoreCase) >= 0);
    }

}
