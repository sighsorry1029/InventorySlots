using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string JewelcraftingGemcutterStationPrefabName = "op_transmution_table";

    private static void AddJewelcraftingSlot(YamlSlot? yamlSlot, string slotId)
    {
        if (!HasJewelcraftingActive)
        {
            return;
        }

        string id = NormalizeSlotId(slotId);
        if (string.Equals(id, JewelcraftingNecklaceSlotId, StringComparison.OrdinalIgnoreCase))
        {
            if (IsJewelcraftingNecklaceSlotEnabled() && SlotDefinitions.All(slot => slot.Id != JewelcraftingNecklaceSlotId))
            {
                string name = yamlSlot == null || string.IsNullOrWhiteSpace(yamlSlot.Name) ? "Necklace" : yamlSlot.Name.Trim();
                SlotDefinitions.Add(new SlotDefinition(JewelcraftingNecklaceSlotId, name, SlotKind.CustomEquipment, IsJewelcraftingNecklaceItem));
            }

            return;
        }

        if (string.Equals(id, JewelcraftingRingSlotId, StringComparison.OrdinalIgnoreCase) &&
            IsJewelcraftingRingSlotEnabled() &&
            SlotDefinitions.All(slot => slot.Id != JewelcraftingRingSlotId))
        {
            string name = yamlSlot == null || string.IsNullOrWhiteSpace(yamlSlot.Name) ? "Ring" : yamlSlot.Name.Trim();
            SlotDefinitions.Add(new SlotDefinition(JewelcraftingRingSlotId, name, SlotKind.CustomEquipment, IsJewelcraftingRingItem));
        }
    }

    private static bool IsJewelcraftingRingSlotEnabled() =>
        TryGetJewelcraftingSlotApi(out JewelcraftingSlotApi? api) && api != null && api.IsRingEnabled();

    private static bool IsJewelcraftingNecklaceSlotEnabled() =>
        TryGetJewelcraftingSlotApi(out JewelcraftingSlotApi? api) && api != null && api.IsNecklaceEnabled();

    private static bool IsJewelcraftingWisplightGemEnabled() =>
        TryGetJewelcraftingSlotApi(out JewelcraftingSlotApi? api) && api != null && api.IsWisplightGemEnabled();

    private static bool IsJewelcraftingWishboneGemEnabled() =>
        TryGetJewelcraftingSlotApi(out JewelcraftingSlotApi? api) && api != null && api.IsWishboneGemEnabled();

    private static bool ShouldSuppressYamlSlotForJewelcraftingGem(string slotId)
    {
        if (!HasJewelcraftingActive || string.IsNullOrWhiteSpace(slotId))
        {
            return false;
        }

        return string.Equals(slotId, "demister", StringComparison.OrdinalIgnoreCase)
            ? IsJewelcraftingWisplightGemEnabled()
            : string.Equals(slotId, "wishbone", StringComparison.OrdinalIgnoreCase) && IsJewelcraftingWishboneGemEnabled();
    }

    private static bool IsJewelcraftingRingItem(ItemData? item) =>
        IsJewelcraftingJewelryItem(item, "JC_Ring_", "$jc_ring_", JewelcraftingRingSlotId);

    private static bool IsJewelcraftingNecklaceItem(ItemData? item) =>
        IsJewelcraftingJewelryItem(item, "JC_Necklace_", "$jc_necklace_", JewelcraftingNecklaceSlotId);

    private static bool IsJewelcraftingDedicatedJewelryItem(ItemData? item) =>
        IsJewelcraftingRingSlotEnabled() && IsJewelcraftingRingItem(item) ||
        IsJewelcraftingNecklaceSlotEnabled() && IsJewelcraftingNecklaceItem(item);

    private static bool IsJewelcraftingUtilityGemBlocked(ItemData? item)
    {
        if (!HasJewelcraftingActive || item?.m_shared == null)
        {
            return false;
        }

        return IsJewelcraftingWisplightGemEnabled() && ItemMatchesExactPrefabOrName(item, "Demister") ||
               IsJewelcraftingWishboneGemEnabled() && ItemMatchesExactPrefabOrName(item, "Wishbone");
    }

    private static bool IsJewelcraftingUtilityGemBlockedForSlot(ItemData? item, SlotDefinition? slot) =>
        slot != null && slot.Kind != SlotKind.Quick && IsJewelcraftingUtilityGemBlocked(item);

    private static bool TryBlockJewelcraftingUtilityGemEquip(Humanoid humanoid, ItemData item, ref bool result)
    {
        if (humanoid != (Humanoid)Player.m_localPlayer || !IsJewelcraftingUtilityGemBlocked(item))
        {
            return false;
        }

        result = false;
        ShowJewelcraftingCannotEquipGemMessage(humanoid);
        return true;
    }

    private static void ShowJewelcraftingCannotEquipGemMessage(Humanoid humanoid)
    {
        if (humanoid is Character character)
        {
            character.Message(MessageHud.MessageType.Center, "$jc_cannot_equip_gem", 0, null);
        }
    }

    internal static bool TryOpenJewelcraftingSocketContainerFromInventorySlotsSlot(InventoryGui gui)
    {
        if (!HasJewelcraftingActive ||
            gui == null ||
            gui.m_playerGrid == null ||
            !InventoryGui.IsVisible() ||
            !IsJewelcraftingSocketInteractPressed() ||
            !TryGetJewelcraftingGemApi(out JewelcraftingGemApi? api) ||
            api == null ||
            !TryGetJewelcraftingSpecialSlotItemUnderMouse(gui.m_playerGrid, out ItemData? item) ||
            item == null ||
            !api.CanOpenSocketContainerFromInventory(item, IsJewelcraftingGemcutterStationActive()))
        {
            return false;
        }

        return api.TryOpenSocketContainer(gui, item);
    }

    private static bool IsJewelcraftingSocketInteractPressed() =>
        ZInput.GetButtonDown("Use") || ZInput.GetButtonDown("JoyUse");

    private static bool TryGetJewelcraftingSpecialSlotItemUnderMouse(InventoryGrid grid, out ItemData? item)
    {
        item = null;
        Inventory? inventory = grid.m_inventory;
        if (inventory == null || grid.m_elements == null)
        {
            return false;
        }

        int width = inventory.GetWidth();
        if (width <= 0)
        {
            return false;
        }

        int count = Math.Min(grid.m_elements.Count, width * Math.Max(0, inventory.GetHeight()));
        for (int i = 0; i < count; i++)
        {
            InventoryGrid.Element element = grid.m_elements[i];
            if (IsUnityNull(element?.m_go) || !element!.m_go.activeInHierarchy)
            {
                continue;
            }

            int y = i / width;
            int x = i - y * width;
            Vector2i pos = new(x, y);
            if (!TryGetSlotAtGridPos(inventory, pos, out SlotDefinition? slot) || slot == null || slot.Kind == SlotKind.Quick)
            {
                continue;
            }

            if (element.m_go.transform is not RectTransform rect || !RectContainsScreenMouse(rect))
            {
                continue;
            }

            item = inventory.GetItemAt(x, y);
            if (item?.m_shared != null)
            {
                return true;
            }
        }

        item = null;
        return false;
    }

    private static bool RectContainsScreenMouse(RectTransform rect)
    {
        Canvas? canvas = rect.GetComponentInParent<Canvas>();
        Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
    }

    private static bool IsJewelcraftingJewelryItem(ItemData? item, string prefabPrefix, string sharedNamePrefix, string slotId)
    {
        if (item?.m_shared == null)
        {
            return false;
        }

        string sharedName = item.m_shared.m_name ?? "";
        if (sharedName.StartsWith(sharedNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefabName = GetItemPrefabName(item);
        if (prefabName.StartsWith(prefabPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        YamlSlot? yamlSlot = GetYamlSlot(slotId);
        List<string> items = GetSlotItems(yamlSlot);
        return items.Count > 0 && ItemMatchesSlotItems(item, items);
    }

    private static void InitializeJewelcraftingSlotCompatibility()
    {
        if (!TryGetJewelcraftingSlotApi(out _))
        {
            return;
        }

        CaptureJewelcraftingSlotConfigState();
    }

    private static bool TryGetJewelcraftingSlotApi(out JewelcraftingSlotApi? api)
    {
        const string capability = "Jewelcrafting slots";
        return TryGetCompatApi(
            JewelcraftingGuid,
            capability,
            CompatRuntime.JewelcraftingSlot,
            JewelcraftingSlotApi.TryCreate,
            "Jewelcrafting slot compatibility disabled",
            out api);
    }

    private static void CaptureJewelcraftingSlotConfigState()
    {
        CompatRuntime.LastJewelcraftingRingSlotEnabled = IsJewelcraftingRingSlotEnabled();
        CompatRuntime.LastJewelcraftingNecklaceSlotEnabled = IsJewelcraftingNecklaceSlotEnabled();
        CompatRuntime.LastJewelcraftingWisplightGemEnabled = IsJewelcraftingWisplightGemEnabled();
        CompatRuntime.LastJewelcraftingWishboneGemEnabled = IsJewelcraftingWishboneGemEnabled();
        CompatRuntime.JewelcraftingSlotStateInitialized = true;
    }

    private static void RefreshJewelcraftingSlotDefinitionsIfNeeded(Player? player)
    {
        if (!HasJewelcraftingActive)
        {
            return;
        }

        bool ringEnabled = IsJewelcraftingRingSlotEnabled();
        bool necklaceEnabled = IsJewelcraftingNecklaceSlotEnabled();
        bool wisplightGemEnabled = IsJewelcraftingWisplightGemEnabled();
        bool wishboneGemEnabled = IsJewelcraftingWishboneGemEnabled();
        if (!CompatRuntime.JewelcraftingSlotStateInitialized)
        {
            CompatRuntime.LastJewelcraftingRingSlotEnabled = ringEnabled;
            CompatRuntime.LastJewelcraftingNecklaceSlotEnabled = necklaceEnabled;
            CompatRuntime.LastJewelcraftingWisplightGemEnabled = wisplightGemEnabled;
            CompatRuntime.LastJewelcraftingWishboneGemEnabled = wishboneGemEnabled;
            CompatRuntime.JewelcraftingSlotStateInitialized = true;
            return;
        }

        if (ringEnabled == CompatRuntime.LastJewelcraftingRingSlotEnabled &&
            necklaceEnabled == CompatRuntime.LastJewelcraftingNecklaceSlotEnabled &&
            wisplightGemEnabled == CompatRuntime.LastJewelcraftingWisplightGemEnabled &&
            wishboneGemEnabled == CompatRuntime.LastJewelcraftingWishboneGemEnabled)
        {
            return;
        }

        CompatRuntime.LastJewelcraftingRingSlotEnabled = ringEnabled;
        CompatRuntime.LastJewelcraftingNecklaceSlotEnabled = necklaceEnabled;
        CompatRuntime.LastJewelcraftingWisplightGemEnabled = wisplightGemEnabled;
        CompatRuntime.LastJewelcraftingWishboneGemEnabled = wishboneGemEnabled;
        RebuildSlotDefinitions();
        if (!IsUnityNull(player))
        {
            EnsureInventoryState(player!, InventoryStateEnsureReason.JewelcraftingSlotRefresh);
            UpdateCustomEquipmentVisuals(player!);
        }
    }

    private static void SuppressJewelcraftingNativeVisualSlots(Player player, Inventory inventory)
    {
        if (!HasJewelcraftingActive || player == null || inventory == null || IsUnityNull(player.m_visEquipment))
        {
            return;
        }

        bool controlsRing = SlotDefinitions.Any(slot => slot.Id == JewelcraftingRingSlotId && slot.Kind == SlotKind.CustomEquipment) && IsJewelcraftingRingSlotEnabled();
        bool controlsNecklace = SlotDefinitions.Any(slot => slot.Id == JewelcraftingNecklaceSlotId && slot.Kind == SlotKind.CustomEquipment) && IsJewelcraftingNecklaceSlotEnabled();
        if (!controlsRing && !controlsNecklace)
        {
            return;
        }

        if (!TryGetJewelcraftingVisual(player, out object? visual))
        {
            return;
        }

        if (controlsRing)
        {
            ClearJewelcraftingVisualSlot(visual!, isRing: true);
        }

        if (controlsNecklace)
        {
            ClearJewelcraftingVisualSlot(visual!, isRing: false);
        }
    }

    private static bool TryGetJewelcraftingVisual(Player player, out object? visual)
    {
        visual = null;
        if (!TryGetJewelcraftingVisualApi(out JewelcraftingVisualApi? api) ||
            api == null ||
            IsUnityNull(player?.m_visEquipment))
        {
            return false;
        }

        return api.TryGetVisual(player!.m_visEquipment, out visual);
    }

    private static void ClearJewelcraftingVisualSlot(object visual, bool isRing)
    {
        try
        {
            CompatRuntime.JewelcraftingVisual.Api?.ClearSlot(visual, isRing);
        }
        catch (Exception)
        {
        }
    }

    private static bool TryGetJewelcraftingVisualApi(out JewelcraftingVisualApi? api)
    {
        const string capability = "Jewelcrafting visuals";
        return TryGetCompatApi(
            JewelcraftingGuid,
            capability,
            CompatRuntime.JewelcraftingVisual,
            JewelcraftingVisualApi.TryCreate,
            "Jewelcrafting visual slot suppression disabled",
            out api);
    }

    internal static bool IsJewelcraftingJewelryEquipped(Player? player, string? prefabName)
    {
        if (player == null || string.IsNullOrWhiteSpace(prefabName) || !HasJewelcraftingActive)
        {
            return false;
        }

        int prefabHash = StringExtensionMethods.GetStableHashCode(prefabName);
        foreach (ItemData item in GetCustomEquippedItems(player))
        {
            if (item?.m_shared == null ||
                !item.m_customData.TryGetValue(SlotIdKey, out string slotId) ||
                slotId is not JewelcraftingRingSlotId and not JewelcraftingNecklaceSlotId)
            {
                continue;
            }

            string itemPrefabName = GetItemPrefabName(item);
            if (!string.IsNullOrWhiteSpace(itemPrefabName) && StringExtensionMethods.GetStableHashCode(itemPrefabName) == prefabHash)
            {
                return true;
            }
        }

        return false;
    }

    private static void SuppressJewelcraftingCraftingSocketUiForRedesign(InventoryGui? gui, bool includeSocketTab = false)
    {
        if (gui == null ||
            !ShouldShowCraftingPanelRedesign(gui) ||
            (!includeSocketTab && IsJewelcraftingSocketTabActive(gui)) ||
            !TryGetJewelcraftingCraftingSocketUiApi(out JewelcraftingCraftingSocketUiApi? api) ||
            api == null)
        {
            return;
        }

        if (api.GetSocketIcons() is Array socketIcons)
        {
            foreach (object? socketIcon in socketIcons)
            {
                if (socketIcon is GameObject go && !IsUnityNull(go))
                {
                    HideJewelcraftingCraftingSocketObject(go);
                }
            }
        }

        if (api.GetSocketingButton() is Button button && !IsUnityNull(button))
        {
            HideJewelcraftingCraftingSocketObject(button.gameObject);
        }
    }

    private static bool IsJewelcraftingGemcutterStationActive()
    {
        if (!HasJewelcraftingActive || IsDedicatedServer || Player.m_localPlayer == null)
        {
            return false;
        }

        CraftingStation? station = Player.m_localPlayer.GetCurrentCraftingStation();
        if (station == null || IsUnityNull(station) || station.gameObject == null)
        {
            return false;
        }

        string prefabName = global::Utils.GetPrefabName(station.gameObject);
        return string.Equals(prefabName, JewelcraftingGemcutterStationPrefabName, StringComparison.OrdinalIgnoreCase) ||
               station.name.StartsWith(JewelcraftingGemcutterStationPrefabName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJewelcraftingGemCuttingRecipe(Recipe? recipe)
    {
        if (recipe == null ||
            recipe.m_resources == null ||
            recipe.m_resources.Length == 0 ||
            recipe.m_resources[0] == null ||
            recipe.m_resources[0].m_amount != recipe.m_amount ||
            !TryGetJewelcraftingGemCuttingApi(out JewelcraftingGemCuttingApi? api) ||
            api == null)
        {
            return false;
        }

        return api.IsGemCuttingRecipe(recipe);
    }

    private static bool ShouldHideJewelcraftingSocketRequirements(InventoryGui.RecipeDataPair pair)
    {
        return pair.ItemData != null && IsJewelcraftingSocketCostItemMayBreak();
    }

    private static bool IsJewelcraftingSocketCostItemMayBreak()
    {
        return TryGetJewelcraftingCraftingSocketUiApi(out JewelcraftingCraftingSocketUiApi? api) &&
               api != null &&
               string.Equals(api.GetSocketCostMode(), "ItemMayBreak", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetJewelcraftingGemCuttingApi(out JewelcraftingGemCuttingApi? api)
    {
        const string capability = "Jewelcrafting gem cutting";
        return TryGetCompatApi(
            JewelcraftingGuid,
            capability,
            CompatRuntime.JewelcraftingGemCutting,
            JewelcraftingGemCuttingApi.TryCreate,
            "Jewelcrafting gem cutting compatibility disabled",
            out api);
    }

    private static void HideJewelcraftingCraftingSocketObject(GameObject go)
    {
        if (!go.activeSelf)
        {
            return;
        }

        go.SetActive(false);
        foreach (Graphic graphic in go.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            graphic.raycastTarget = false;
        }
    }

    private static void RestoreJewelcraftingCraftingSocketUiForVanilla(InventoryGui? gui)
    {
        if (gui == null ||
            (!IsJewelcraftingGemcutterStationActive() && !IsJewelcraftingSocketTabActive(gui)) ||
            !TryGetJewelcraftingCraftingSocketUiApi(out JewelcraftingCraftingSocketUiApi? api) ||
            api == null)
        {
            return;
        }

        if (api.GetSocketingButton() is Button button && !IsUnityNull(button))
        {
            RestoreJewelcraftingCraftingSocketButton(button);
        }
    }

    private static void RestoreJewelcraftingCraftingSocketButton(Button button)
    {
        if (button.targetGraphic != null && !IsUnityNull(button.targetGraphic))
        {
            button.targetGraphic.raycastTarget = true;
        }

        foreach (Graphic graphic in button.gameObject.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            graphic.raycastTarget = true;
        }
    }

    private static bool TryGetJewelcraftingCraftingSocketUiApi(out JewelcraftingCraftingSocketUiApi? api)
    {
        const string capability = "Jewelcrafting crafting socket UI";
        return TryGetCompatApi(
            JewelcraftingGuid,
            capability,
            CompatRuntime.JewelcraftingCraftingSocketUi,
            JewelcraftingCraftingSocketUiApi.TryCreate,
            "Jewelcrafting crafting socket UI compatibility disabled",
            out api);
    }

    internal static bool TryGetJewelcraftingApiIsJewelryEquippedMethod(out MethodBase? method)
    {
        method = null;
        if (!TryGetJewelcraftingSlotApi(out JewelcraftingSlotApi? api) || api == null)
        {
            return false;
        }

        return api.TryGetIsJewelryEquippedMethod(out method);
    }
}

[HarmonyPatch]
internal static class JewelcraftingApiIsJewelryEquippedInventorySlotsPatch
{
    private static bool Prepare()
    {
        return InventorySlotsPlugin.TryGetJewelcraftingApiIsJewelryEquippedMethod(out _);
    }

    private static MethodBase TargetMethod()
    {
        InventorySlotsPlugin.TryGetJewelcraftingApiIsJewelryEquippedMethod(out MethodBase? method);
        return method!;
    }

    private static void Postfix(Player player, string prefabName, ref bool __result)
    {
        if (!__result)
        {
            __result = InventorySlotsPlugin.IsJewelcraftingJewelryEquipped(player, prefabName);
        }
    }
}
