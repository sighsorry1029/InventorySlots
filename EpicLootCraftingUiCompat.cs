using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

[HarmonyPatch]
internal static class EpicLootCraftDialogVisibilityPatch
{
    private static bool Prepare() =>
        InventorySlotsPlugin.IsEpicLootLoadedForPatches() && TargetMethods().Any();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Enchant and Rune share CraftSuccessDialog. Augment clones the same
        // vanilla recipe elements, but keeps its own effect-selection policy.
        foreach (string typeName in new[] { "EpicLoot.Crafting.CraftSuccessDialog", "EpicLoot.Crafting.AugmentChoiceDialog" })
        {
            Type? type = AccessTools.TypeByName(typeName);
            if (type == null ||
                AccessTools.Field(type, "NameText")?.FieldType != typeof(TMP_Text) ||
                AccessTools.Field(type, "Description")?.FieldType != typeof(TMP_Text) ||
                AccessTools.Field(type, "Icon")?.FieldType != typeof(Image) ||
                AccessTools.Field(type, "MagicBG")?.FieldType != typeof(Image))
            {
                continue;
            }

            MethodInfo? show = AccessTools.Method(type, "Show");
            if (show != null && !show.IsStatic && show.ReturnType == typeof(void))
            {
                yield return show;
            }
        }
    }

    private static void Prefix(Component __instance, TMP_Text ___NameText, TMP_Text ___Description, Image ___Icon, Image ___MagicBG)
    {
        // The redesign hides recipe GameObjects. Instantiate copies activeSelf,
        // so EpicLoot's Show can fill valid text/icon fields that remain invisible.
        // Restore only the dialog's clones; never toggle the live recipe templates.
        RestoreClonedElement(__instance, ___NameText);
        RestoreClonedElement(__instance, ___Description);
        RestoreClonedElement(__instance, ___Icon);
        RestoreClonedElement(__instance, ___MagicBG);

        if (___Description == null || !___Description.transform.IsChildOf(__instance.transform))
        {
            return;
        }

        Scrollbar? scrollbar = ___Description.GetComponentInParent<ScrollRect>(includeInactive: true)?.verticalScrollbar;
        if (scrollbar == null || !scrollbar.transform.IsChildOf(__instance.transform))
        {
            return;
        }

        // ConvertToScrollingDescription also clones m_recipeListScroll, whose
        // GameObject and Graphics are hidden by our vanilla-scrollbar suppression.
        scrollbar.gameObject.SetActive(true);
        foreach (Graphic graphic in scrollbar.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            graphic.enabled = true;
            graphic.raycastTarget = true;
        }
    }

    private static void RestoreClonedElement(Component dialog, Graphic? element)
    {
        if (element != null && element.transform.IsChildOf(dialog.transform))
        {
            element.gameObject.SetActive(true);
        }
        // EpicLoot's original Show still owns Graphic.enabled (especially the
        // rarity background), text, colors, icons, sounds and action callbacks.
    }
}
