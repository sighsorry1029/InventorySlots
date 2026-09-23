using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if INVENTORY_SLOTS
using ControllerPlugin = InventorySlots.InventorySlotsPlugin;
namespace InventorySlots;
#else
using ControllerPlugin = InventoryActions.InventoryActionsPlugin;
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    // Physical face buttons stay consistent in the inventory; world interaction
    // follows Valheim's semantic Use/AltKeys bindings and selected pad layout.
    private enum InventoryControllerModifier { Off, JoyRStick, JoyLStick, JoyLTrigger, JoyRTrigger }
    private enum RestockControllerModifier { Off, JoyAltKeys, JoyRStick, JoyLStick, JoyLTrigger, JoyRTrigger }
#if !INVENTORY_SLOTS
    private const string ControllerInputConfigSection = "4 - Controller Input";
    private static ConfigEntry<Toggle> _enableControllerHotkeys = null!;
#endif
    private static ConfigEntry<InventoryControllerModifier> _inventoryActionModifier = null!;
    private static ConfigEntry<RestockControllerModifier> _favoriteRestockModifier = null!;
    private static int _controllerInputUpdateFrame = -1;
    private static int _controllerDispatchFrame = -1;
    private static int _controllerReservedFrame = -1;

    private static class InventoryControllerAccess
    {
        internal static readonly AccessTools.FieldRef<InventoryGrid, Vector2i> Selection =
            AccessTools.FieldRefAccess<InventoryGrid, Vector2i>("m_selected");
        internal static readonly AccessTools.FieldRef<InventoryGui, GameObject> DragObject =
            AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");
    }

    private static void BindInventoryControllerConfig()
    {
#if !INVENTORY_SLOTS
        _enableControllerHotkeys = ConfigEntry(ControllerInputConfigSection, "Enable Controller Hotkeys", Toggle.On,
            "Enable the inventory action chords and favorite restock controller shortcut below. Client-only.", synchronizedSetting: false);
#endif
        _inventoryActionModifier = ConfigEntry(ControllerInputConfigSection, "Inventory Action Modifier", InventoryControllerModifier.JoyRStick,
            "Hold this while a player/container inventory grid is selected, then press A to toggle a player favorite, X to sort the selected inventory, Y to open Restock targets, or B to open Auto Pickup Exclude. Pick up a player-inventory item first to register it with Y/B. Off disables these chords. Without holding any other button, a short right-stick click and release opens the selected slot's actions menu; S and the inventory buttons can also be selected with directions. These direct UI controls remain available when hotkeys are Off. Uses physical face-button positions in every controller layout. Client-only.", synchronizedSetting: false);
        _favoriteRestockModifier = ConfigEntry(ControllerInputConfigSection, "Favorite Restock Modifier", RestockControllerModifier.JoyAltKeys,
            "Hold this together with the game's controller Use button while looking at a container to restock favorites. JoyAltKeys follows the game's alternative-action binding (normally LT; LB in the Alternative 1 layout). Off disables this chord. Uses the same targets, limits, leave-one rule and range as keyboard restock. Client-only.", synchronizedSetting: false);
    }

    private static bool InventoryControllerEnabled => _enableControllerHotkeys?.Value == Toggle.On;
    private static bool IsInventoryControllerActive() => _instance != null && _instance.isActiveAndEnabled && !IsDedicatedServer &&
        ZInput.IsExclusiveGamepadActive();
    private static bool UseInventoryControllerHints() => InventoryControllerEnabled && IsInventoryControllerActive();
    private static string GetInventoryControllerActionDisplay(string action) =>
        ZInput.instance != null ? ZInput.instance.GetBoundKeyString(action, true) : action;
    private static string GetInventoryControllerChordDisplay(string action) =>
        !InventoryControllerEnabled || _inventoryActionModifier == null || _inventoryActionModifier.Value == InventoryControllerModifier.Off
            ? "" : GetInventoryControllerActionDisplay(_inventoryActionModifier.Value.ToString()) + " + " + GetInventoryControllerActionDisplay(action);
    private static string GetFavoriteRestockControllerDisplay() =>
        !InventoryControllerEnabled || _favoriteRestockModifier == null || _favoriteRestockModifier.Value == RestockControllerModifier.Off
            ? "" : GetInventoryControllerActionDisplay(_favoriteRestockModifier.Value.ToString()) + " + " + GetInventoryControllerActionDisplay("JoyUse");
    private static bool IsFavoriteRestockControllerHeld() => UseInventoryControllerHints() &&
        _favoriteRestockModifier != null && _favoriteRestockModifier.Value != RestockControllerModifier.Off &&
        ZInput.GetButton(_favoriteRestockModifier.Value.ToString()) && ZInput.GetButton("JoyUse");

    // Also queried by Slots' independent hotkey/scroll updates, which can run
    // before InventoryGui.Update. Keep this free of global input guards.
    private static bool IsInventoryControllerChordHeld()
    {
        InventoryGui gui = InventoryGui.instance;
        return UseInventoryControllerHints() && gui != null && InventoryGui.IsVisible() && !IsInventoryPanelClosing(gui) &&
            _inventoryActionModifier != null && _inventoryActionModifier.Value != InventoryControllerModifier.Off &&
            ZInput.GetButton(_inventoryActionModifier.Value.ToString()) && CanShowItemRules(gui) &&
            (IsControllerGridActive(gui.m_playerGrid) || IsControllerGridActive(gui.ContainerGrid));
    }

    private static bool CanUseInventoryControllerUi(InventoryGui gui)
    {
        if (!IsInventoryControllerActive() || !InventoryGui.IsVisible() || IsInventoryPanelClosing(gui) ||
            Player.m_localPlayer == null || FavoriteMemoryAccess.IsLoading(Player.m_localPlayer) ||
            Player.m_localPlayer.IsTeleporting() || ShouldBlockGlobalHotkeys(Player.m_localPlayer) || !CanShowItemRules(gui) ||
            (gui.m_splitDialog != null && gui.m_splitDialog.gameObject.activeInHierarchy) ||
            (gui.m_variantDialog != null && gui.m_variantDialog.gameObject.activeInHierarchy) ||
            gui.IsSkillsPanelOpen || gui.IsTextPanelOpen || gui.IsTrophisPanelOpen || gui.IsAchievementsPanelOpen)
            return false;
#if INVENTORY_SLOTS
        if (_inventoryTrashConfirmDialog != null && _inventoryTrashConfirmDialog.activeInHierarchy) return false;
#else
        if (Runtime.TrashConfirmDialog != null && Runtime.TrashConfirmDialog.activeInHierarchy) return false;
#endif
        GameObject? selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected == null || (selected.GetComponent<TMP_InputField>() == null && selected.GetComponent<InputField>() == null);
    }

    private static InventoryGrid? GetControllerActionGrid(InventoryGui gui, bool requireHotkeys = true)
    {
        if (requireHotkeys && !InventoryControllerEnabled || !CanUseInventoryControllerUi(gui)) return null;
        InventoryGrid? playerGrid = gui.m_playerGrid;
        if (IsControllerGridActive(playerGrid)) return playerGrid;
        return IsControllerGridActive(gui.ContainerGrid) ? gui.ContainerGrid : null;
    }

    private static bool IsControllerGridActive(InventoryGrid? grid) => grid != null && grid.isActiveAndEnabled &&
        grid.m_uiGroup != null && grid.m_uiGroup.IsActive && grid.GetInventory() != null;

    internal static bool IsControllerInputUpdated() => _controllerInputUpdateFrame == Time.frameCount;

    internal static void OnControllerInputUpdated()
    {
        _controllerInputUpdateFrame = Time.frameCount;
        if (InventoryGui.instance != null) UpdateInventoryControllerInput(InventoryGui.instance);
    }

    // Gui.Update closes on B/Y before it updates grids. UIGamePad components
    // also run independently, so all three entry points share one dispatch.
    internal static void UpdateInventoryControllerInput(InventoryGui gui)
    {
        UpdateItemRuleControllerInput();
        if (gui != null && UpdateControllerFeatureGuide(gui)) return;
        if (gui != null && UpdateControllerItemMenu(gui)) return;
        if (gui != null && UpdateInventoryButtonNavigation(gui)) return;
        if (_controllerDispatchFrame == Time.frameCount || gui == null) return;
        InventoryGrid? grid = GetControllerActionGrid(gui);
        if (grid == null) return;
        bool modifier = _inventoryActionModifier != null && _inventoryActionModifier.Value != InventoryControllerModifier.Off &&
            ZInput.GetButton(_inventoryActionModifier.Value.ToString());
#if INVENTORY_SLOTS
        bool favoriteOnly = !modifier && grid == gui.m_playerGrid && IsControllerHotkeyHeld(_controllerFavoriteModifierButton);
#else
        const bool favoriteOnly = false;
#endif
        if (!modifier && !favoriteOnly) return;
        // UI navigation can run before Game.Update ticks ZInput. Reserve its
        // direct submit immediately, but wait for this frame's button-downs.
        _controllerReservedFrame = Time.frameCount;
        if (!IsControllerInputUpdated()) return;
        bool favorite = ZInput.GetButtonDown("JoyButtonA");
        bool sort = modifier && ZInput.GetButtonDown("JoyButtonX");
        bool restock = modifier && ZInput.GetButtonDown("JoyButtonY");
        bool exclude = modifier && ZInput.GetButtonDown("JoyButtonB");
        // A no-input/gated poll must not prevent a later caller from seeing
        // input or grid focus updated during the same frame.
        if (!favorite && !sort && !restock && !exclude) return;
        _controllerDispatchFrame = Time.frameCount;
        // Capture first, then consume aliases. Never reset the held modifier or
        // world JoyUse hold: ResetButtonStatus requires a physical re-press.
        if (favorite) ZInput.ResetButtonStatus("JoyButtonA");
        if (sort) ZInput.ResetButtonStatus("JoyButtonX");
        if (restock) ZInput.ResetButtonStatus("JoyButtonY");
        if (exclude) ZInput.ResetButtonStatus("JoyButtonB");
        if (favorite || sort || restock || exclude)
        {
            ZInput.ResetButtonStatus("Inventory");
            ZInput.ResetButtonStatus("Use");
            ZInput.ResetButtonStatus("JoyUse");
        }
        if (InventoryControllerAccess.DragObject(gui) != null)
        {
            if (restock || exclude) OpenControllerItemRules(restock);
            return;
        }
        if (favorite && grid == gui.m_playerGrid)
        {
            Inventory inventory = grid.GetInventory();
            Vector2i pos = InventoryControllerAccess.Selection(grid);
            if (CanControllerFavoriteCell(gui, grid, pos, inventory)) ToggleFavoriteSlot(Player.m_localPlayer, pos);
        }
        else if (sort)
        {
            if (grid == gui.m_playerGrid) SortPlayerInventory(Player.m_localPlayer);
            else SortCurrentContainer(Player.m_localPlayer);
        }
        else if (restock || exclude) OpenControllerItemRules(restock);
    }

    private static bool CanControllerFavoriteCell(InventoryGui gui, InventoryGrid grid, Vector2i cell, Inventory inventory)
    {
        if (grid != gui.m_playerGrid || inventory != ((Humanoid)Player.m_localPlayer).GetInventory() || IsOutOfBounds(inventory, cell)) return false;
#if INVENTORY_SLOTS
        return CanFavoriteSlot(Player.m_localPlayer, inventory, cell);
#else
        return CanFavoriteCell(inventory, cell);
#endif
    }

    internal static bool IsInventoryControllerInputReserved() => _controllerReservedFrame == Time.frameCount;

    internal static bool IsInventoryControllerNavigationReserved() => IsInventoryControllerInputReserved() ||
        (ZInput.IsExclusiveGamepadActive() && (IsItemRuleInputBlocked() || IsControllerItemMenuOpen()));

    private static void RemoveClonedControllerShortcuts(Button button)
    {
        RemoveClonedInventoryButtonHints(button);
    }
}

// ZInput computes Pressed in Update, separately from physical input callbacks.
// Consume chords here before vanilla InventoryGui can treat Y/B as Close.
[HarmonyPatch(typeof(ZInput), nameof(ZInput.Update))]
internal static class InventoryControllerInputUpdatePatch
{
    private static void Postfix() => ControllerPlugin.OnControllerInputUpdated();
}

// Unity's input module reads InputActions directly, independently of ZInput
// and UIGamePad. Reserve navigation/submit only; pointer processing continues.
[HarmonyPatch]
internal static class InventoryControllerEventNavigationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        System.Type? module = AccessTools.TypeByName("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        MethodInfo? navigation = module?.GetMethod("ProcessNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
        if (navigation != null) yield return navigation;
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix()
    {
        if (InventoryGui.instance == null) return true;
        ControllerPlugin.UpdateInventoryControllerInput(InventoryGui.instance);
        return !ControllerPlugin.IsInventoryControllerNavigationReserved();
    }
}

[HarmonyPatch(typeof(InventoryGui), "Update")]
internal static class InventoryControllerUpdatePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(InventoryGui __instance) => ControllerPlugin.UpdateInventoryControllerInput(__instance);
}

[HarmonyPatch(typeof(InventoryGui), "UpdateGamepad")]
internal static class InventoryControllerGuiInputPatch
{
    private static bool Prefix() => !ControllerPlugin.IsInventoryControllerInputReserved();
}

[HarmonyPatch(typeof(InventoryGrid), "UpdateGamepad")]
internal static class InventoryControllerGridInputPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix()
    {
        if (InventoryGui.instance != null) ControllerPlugin.UpdateInventoryControllerInput(InventoryGui.instance);
        return !ControllerPlugin.IsInventoryControllerInputReserved();
    }
}

[HarmonyPatch(typeof(UIGamePad), nameof(UIGamePad.ButtonPressed))]
internal static class InventoryControllerButtonInputPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(UIGamePad __instance, ref bool __result)
    {
        InventoryGui gui = InventoryGui.instance;
        if (gui == null || !__instance.transform.IsChildOf(gui.transform)) return true;
        ControllerPlugin.UpdateInventoryControllerInput(gui);
        if (!ControllerPlugin.IsInventoryControllerInputReserved()) return true;
        __result = false;
        return false;
    }
}
