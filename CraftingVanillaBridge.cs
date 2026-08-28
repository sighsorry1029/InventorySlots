using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool IsForeignCraftingUiTransform(InventoryGui gui, Transform transform)
    {
        if (transform == null || gui.m_crafting == null || !transform.IsChildOf(gui.m_crafting))
        {
            return false;
        }

        if (IsOwnedCraftingUiTransform(transform))
        {
            return false;
        }

        if (IsKnownVanillaCraftingTransform(gui, transform))
        {
            return false;
        }

        if (IsVneiUiTransform(transform, gui.m_crafting))
        {
            return true;
        }

        Transform? cursor = transform;
        while (cursor != null && cursor != gui.m_crafting)
        {
            string lowerName = cursor.name.ToLowerInvariant();
            if (lowerName.Contains("jewelcrafting") ||
                lowerName.Contains("augment") ||
                lowerName.Contains("synergy"))
            {
                return true;
            }

            foreach (MonoBehaviour behaviour in cursor.GetComponents<MonoBehaviour>())
            {
                Type? type = behaviour != null ? behaviour.GetType() : null;
                string ns = type?.Namespace ?? "";
                if (ns.StartsWith("Jewelcrafting", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            cursor = cursor.parent;
        }

        return false;
    }

    private static bool IsKnownVanillaCraftingTransform(InventoryGui gui, Transform transform)
    {
        if (transform == gui.m_recipeListRoot)
        {
            return true;
        }

        if (gui.m_availableRecipes != null)
        {
            foreach (InventoryGui.RecipeDataPair pair in gui.m_availableRecipes)
            {
                Transform? recipeElement = pair.InterfaceElement != null && !IsUnityNull(pair.InterfaceElement)
                    ? pair.InterfaceElement.transform
                    : null;
                if (recipeElement != null && transform.IsChildOf(recipeElement))
                {
                    return true;
                }
            }
        }

        foreach (Transform? root in GetKnownVanillaCraftingRoots(gui))
        {
            if (root != null && !IsUnityNull(root) && transform.IsChildOf(root))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Transform?> GetKnownVanillaCraftingRoots(InventoryGui gui)
    {
        yield return gui.m_recipeListScroll != null ? gui.m_recipeListScroll.transform : null;
        yield return gui.m_recipeIcon != null ? gui.m_recipeIcon.transform : null;
        yield return gui.m_recipeName != null ? gui.m_recipeName.transform : null;
        yield return gui.m_recipeDecription != null ? gui.m_recipeDecription.transform : null;
        yield return gui.m_itemCraftType != null ? gui.m_itemCraftType.transform : null;
        yield return gui.m_variantButton != null ? gui.m_variantButton.transform : null;
        yield return gui.m_minStationLevelIcon != null ? gui.m_minStationLevelIcon.transform : null;
        yield return gui.m_minStationLevelText != null ? gui.m_minStationLevelText.transform : null;
        yield return gui.m_craftButton != null ? gui.m_craftButton.transform : null;
        yield return gui.m_repairButton != null ? gui.m_repairButton.transform : null;
        yield return gui.m_repairButtonGlow != null ? gui.m_repairButtonGlow.transform : null;
        yield return gui.m_tabCraft != null ? gui.m_tabCraft.transform : null;
        yield return gui.m_tabUpgrade != null ? gui.m_tabUpgrade.transform : null;
    }

    private static bool IsOwnedCraftingUiTransform(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        if (transform.name.StartsWith("InventorySlots_", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (RectTransform? root in new[]
                 {
                     _craftingRecipeGrid,
                     CraftingUi.TooltipRecipeOverlay,
                      _craftingRecipeScrollbar,
                      _craftingGroupRail,
                      _craftingCountInputRect,
                      CraftingUi.SearchInputRect,
                      _craftingSortModeButtonGroup,
                      _craftingControlsBackground
                  })
        {
            if (root != null && !IsUnityNull(root) && transform.IsChildOf(root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVneiCraftingTransform(Transform? transform)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (transform == null || gui == null || IsUnityNull(gui) || gui.m_crafting == null || !transform.IsChildOf(gui.m_crafting))
        {
            return false;
        }

        return IsVneiUiTransform(transform, stopAt: gui.m_crafting);
    }

    private static bool IsVneiCraftingTabActive(InventoryGui gui)
    {
        if (gui.m_tabCraft == null || gui.m_tabUpgrade == null)
        {
            return false;
        }

        Transform? tabRoot = gui.m_tabCraft.transform.parent;
        if (tabRoot == null)
        {
            return false;
        }

        foreach (Button button in tabRoot.GetComponentsInChildren<Button>(includeInactive: true))
        {
            if (button == null ||
                IsUnityNull(button) ||
                button == gui.m_tabCraft ||
                button == gui.m_tabUpgrade ||
                button.interactable ||
                !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (IsVneiUiTransform(button.transform, stopAt: tabRoot.parent))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVneiUiTransform(Transform? transform, Transform? stopAt = null)
    {
        for (Transform? cursor = transform; cursor != null && cursor != stopAt; cursor = cursor.parent)
        {
            string lowerName = cursor.name.ToLowerInvariant();
            if (lowerName.Contains("vnei"))
            {
                return true;
            }

            foreach (MonoBehaviour behaviour in cursor.GetComponents<MonoBehaviour>())
            {
                Type? type = behaviour != null ? behaviour.GetType() : null;
                string ns = type?.Namespace ?? "";
                string assemblyName = type?.Assembly.GetName().Name ?? "";
                if (ns.StartsWith("VNEI", StringComparison.Ordinal) ||
                    assemblyName.IndexOf("VNEI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void SetCraftingVanillaDetailVisible(InventoryGui gui, bool visible)
    {
        if (gui.m_recipeIcon != null)
        {
            gui.m_recipeIcon.gameObject.SetActive(visible);
        }

        if (gui.m_recipeName != null)
        {
            gui.m_recipeName.gameObject.SetActive(visible);
        }

        if (gui.m_recipeDecription != null)
        {
            gui.m_recipeDecription.gameObject.SetActive(visible);
        }

        if (gui.m_itemCraftType != null)
        {
            gui.m_itemCraftType.gameObject.SetActive(visible);
        }

        if (gui.m_variantButton != null)
        {
            gui.m_variantButton.gameObject.SetActive(visible);
        }

        if (gui.m_minStationLevelIcon != null)
        {
            gui.m_minStationLevelIcon.gameObject.SetActive(visible);
        }

        if (gui.m_minStationLevelText != null)
        {
            gui.m_minStationLevelText.gameObject.SetActive(visible);
        }
    }

    private static void EnsureCraftingVanillaRecipeElementsHidden(InventoryGui gui)
    {
        if (_craftingVanillaRecipeElementsHidden &&
            (gui.m_recipeListRoot == null || IsUnityNull(gui.m_recipeListRoot) || !gui.m_recipeListRoot.gameObject.activeSelf))
        {
            return;
        }

        SetCraftingVanillaRecipeListRootVisible(gui, visible: false);
        SetCraftingVanillaRecipeElementsVisible(gui, visible: false);
        _craftingVanillaRecipeElementsHidden = true;
    }

    internal static void InvalidateCraftingVanillaHiddenState()
    {
        _craftingVanillaRecipeElementsHidden = false;
        _craftingVanillaPanelBackgroundsHidden = false;
    }

    private static void SetCraftingVanillaRecipeListRootVisible(InventoryGui gui, bool visible)
    {
        if (gui.m_recipeListRoot != null && !IsUnityNull(gui.m_recipeListRoot))
        {
            gui.m_recipeListRoot.gameObject.SetActive(visible);
        }
    }

    private static void SetCraftingVanillaRecipeElementsVisible(InventoryGui gui, bool visible)
    {
        SetCraftingVanillaRecipeListRootVisible(gui, visible);

        if (gui.m_availableRecipes == null)
        {
            return;
        }

        foreach (InventoryGui.RecipeDataPair pair in gui.m_availableRecipes)
        {
            if (pair.InterfaceElement != null && !IsUnityNull(pair.InterfaceElement))
            {
                pair.InterfaceElement.SetActive(visible);
            }
        }
    }

    private static void EnsureCraftingVanillaPanelBackgroundsHidden(InventoryGui gui)
    {
        SuppressCraftingRequiredStationLevelOriginalBackground(gui);

        if (_craftingVanillaPanelBackgroundsHidden && CraftingVanillaPanelBackgroundStates.Count > 0)
        {
            return;
        }

        SetCraftingVanillaPanelBackgroundsVisible(gui, visible: false);
        _craftingVanillaPanelBackgroundsHidden = true;
    }

    private static void SetCraftingVanillaPanelBackgroundsVisible(InventoryGui gui, bool visible)
    {
        if (visible)
        {
            foreach (KeyValuePair<Image, bool> state in CraftingVanillaPanelBackgroundStates.ToArray())
            {
                Image image = state.Key;
                if (image == null || IsUnityNull(image))
                {
                    continue;
                }

                image.enabled = state.Value;
            }

            CraftingVanillaPanelBackgroundStates.Clear();
            _craftingVanillaPanelBackgroundsHidden = false;
            return;
        }

        foreach (Image image in FindCraftingVanillaPanelBackgroundImages(gui))
        {
            if (image == null || IsUnityNull(image))
            {
                continue;
            }

            if (!CraftingVanillaPanelBackgroundStates.ContainsKey(image))
            {
                CraftingVanillaPanelBackgroundStates[image] = image.enabled;
            }

            image.enabled = false;
        }
    }

    private static IEnumerable<Image> FindCraftingVanillaPanelBackgroundImages(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            yield break;
        }

        HashSet<Image> yielded = new();
        foreach (Image image in FindCraftingPanelNamedBackgroundImages(gui))
        {
            if (yielded.Add(image))
            {
                yield return image;
            }
        }

        foreach (Image image in FindCraftingPanelFieldBackgroundImages(gui))
        {
            if (yielded.Add(image))
            {
                yield return image;
            }
        }
    }

    private static IEnumerable<Image> FindCraftingPanelNamedBackgroundImages(InventoryGui gui)
    {
        foreach (string panelName in new[] { "RecipeList", "Decription", "Description" })
        {
            Transform? panel = gui.m_crafting.Find(panelName);
            if (panel == null)
            {
                continue;
            }

            Image? panelImage = panel.GetComponent<Image>();
            if (panelImage != null && ShouldHideCraftingPanelBackgroundImage(gui, panelImage))
            {
                yield return panelImage;
            }

            foreach (Image image in panel.GetComponentsInChildren<Image>(includeInactive: true))
            {
                if (image != panelImage && ShouldHideCraftingPanelBackgroundImage(gui, image) && IsCraftingBackgroundLikeImage(image.transform))
                {
                    yield return image;
                }
            }
        }
    }

    private static IEnumerable<Image> FindCraftingPanelFieldBackgroundImages(InventoryGui gui)
    {
        foreach (Transform? anchor in new[]
                 {
                     gui.m_recipeListRoot,
                     gui.m_recipeListScroll != null ? gui.m_recipeListScroll.transform : null,
                     gui.m_minStationLevelIcon != null ? gui.m_minStationLevelIcon.transform : null,
                     gui.m_minStationLevelText != null ? gui.m_minStationLevelText.transform : null
                 })
        {
            Transform? cursor = anchor;
            while (cursor != null && cursor != gui.m_crafting)
            {
                foreach (Image image in cursor.GetComponents<Image>())
                {
                    if (ShouldHideCraftingPanelBackgroundImage(gui, image))
                    {
                        yield return image;
                    }
                }

                Transform? parent = cursor.parent;
                if (parent != null && parent != gui.m_crafting)
                {
                    foreach (Image siblingImage in parent.GetComponentsInChildren<Image>(includeInactive: true))
                    {
                        if (ShouldHideCraftingPanelBackgroundImage(gui, siblingImage) && IsNearCraftingFieldBackground(anchor, siblingImage.transform))
                        {
                            yield return siblingImage;
                        }
                    }
                }

                cursor = cursor.parent;
            }
        }
    }

    private static bool ShouldHideCraftingPanelBackgroundImage(InventoryGui gui, Image image)
    {
        if (image == null || IsUnityNull(image) || image == gui.m_minStationLevelIcon)
        {
            return false;
        }

        Transform transform = image.transform;
        if (transform == gui.m_crafting ||
            IsOwnedCraftingUiTransform(transform))
        {
            return false;
        }

        if (gui.m_recipeListScroll != null && transform.IsChildOf(gui.m_recipeListScroll.transform))
        {
            return false;
        }

        return transform.IsChildOf(gui.m_crafting);
    }

    private static bool IsCraftingBackgroundLikeImage(Transform imageTransform)
    {
        string lowerName = imageTransform.name.ToLowerInvariant();
        return lowerName.Contains("bkg") ||
               lowerName.Contains("background") ||
               lowerName.Contains("frame") ||
               lowerName.Contains("border") ||
               lowerName.Contains("panel");
    }

    private static bool IsNearCraftingFieldBackground(Transform? anchor, Transform imageTransform)
    {
        if (anchor == null)
        {
            return false;
        }

        if (imageTransform == anchor || imageTransform.IsChildOf(anchor) || anchor.IsChildOf(imageTransform))
        {
            return true;
        }

        if (IsCraftingBackgroundLikeImage(imageTransform))
        {
            return true;
        }

        return false;
    }
}

