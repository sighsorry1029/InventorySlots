using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static partial class Program
{
    private static void CheckControllerFeatureGuide()
    {
        InventoryGui gui = Plugin.TestReset();
        Check(Plugin.TestGuideBindingShown(), "Open controller inventory shows guide binding");
        ButtonFrame("JoyLTrigger", "JoyRStick");
        Dispatch();
        Check(Plugin.TestGuideToggles == 1 && Plugin.TestGuideCollapsed && NoActions,
            "LT + R3 toggles only guide, once across dispatch entry points");
        Check(ZInput.GetButton("JoyLTrigger") && !ZInput.GetButtonDown("JoyRStick"),
            "Guide consumes stick press but preserves held trigger");
        Check(!Prefix("InventoryControllerEventNavigationPatch"), "Guide press reserves native UI actions");
        // Physical hold, without another down edge, must not repeat.
        Time.frameCount++; ZInput.Down.Clear(); ZInput.Held.Add("JoyRStick");
        Plugin.OnControllerInputUpdated();
        Check(Plugin.TestGuideToggles == 1, "Holding guide combination never repeats");
        Time.frameCount++; ZInput.Held.Remove("JoyLTrigger"); Plugin.OnControllerInputUpdated();
        ButtonFrame();
        Check(!Plugin.IsControllerItemMenuOpen(), "Releasing trigger before stick never opens slot menu");
        ButtonFrame("JoyLTrigger", "JoyRStick");
        Check(Plugin.TestGuideToggles == 2 && !Plugin.TestGuideCollapsed, "Next click restores expanded guide");
        ButtonFrame();
        Check(!Plugin.IsControllerItemMenuOpen(), "Guide toggle never leaves a pending R3 tap");

        gui = Plugin.TestReset(); Plugin.TestEnable(false); gui.m_playerGrid.m_uiGroup.IsActive = false;
        ButtonFrame("JoyLTrigger", "JoyRStick");
        Check(Plugin.TestGuideToggles == 1, "Guide works with hotkeys off and crafting/non-grid focus");

        gui = PrepareButtonRow(5); ButtonFrame("JoyDPadDown");
        Check(Plugin.IsInventoryButtonNavigationActive(), "Guide test enters inventory button row");
        ButtonFrame("JoyLTrigger", "JoyRStick");
        Check(Plugin.TestGuideToggles == 1 && Plugin.IsInventoryButtonNavigationActive(), "Guide preserves button focus");

        gui = Plugin.TestReset(); ButtonFrame("JoyRStick");
        Time.frameCount++; ZInput.Down.Clear(); ZInput.Press("JoyLTrigger"); Plugin.OnControllerInputUpdated();
        ButtonFrame();
        Check(Plugin.TestGuideToggles == 0 && !Plugin.IsControllerItemMenuOpen(),
            "Pressing trigger second cancels slot tap without toggling guide");

        gui = Plugin.TestReset(); Time.frameCount++;
        ZInput.Press("JoyLTrigger", "JoyRStick"); Dispatch();
        Check(Plugin.TestGuideToggles == 0 && Plugin.IsInventoryControllerInputReserved(), "Pre-input tick only reserves guide chord");
        Plugin.OnControllerInputUpdated(); Dispatch();
        Check(Plugin.TestGuideToggles == 1, "Refreshed input dispatches guide once in same frame");

        Action[] blockers =
        {
            () => ZInput.Exclusive = false,
            () => InventoryGui.Visible = false,
            () => Plugin.TestClosing = true,
            () => Plugin.TestShowGuide(false),
            () => Plugin.TestGuideReady = false,
            () => Plugin.TestBlocked = true,
            () => Plugin.TestDedicated = true,
            () => Plugin.TestPluginActive(false),
            () => Plugin.TestCanShow = false,
            () => Plugin.TestRulesPinned = true,
            () => Plugin.TestRulesClosedFrame = Time.frameCount + 1,
            () => Player.m_localPlayer.Loading = true,
            () => Player.m_localPlayer.Teleporting = true,
            () => Player.m_localPlayer = null!,
            () => InventoryGui.instance.m_dragGo = new GameObject(),
            () => InventoryGui.instance.m_splitDialog = new Component(),
            () => InventoryGui.instance.m_variantDialog = new Component(),
            () => InventoryGui.instance.IsSkillsPanelOpen = true,
            () => InventoryGui.instance.IsTextPanelOpen = true,
            () => InventoryGui.instance.IsTrophisPanelOpen = true,
            () => InventoryGui.instance.IsAchievementsPanelOpen = true,
            Plugin.TestSetTrashDialog,
            () => { GameObject input = new(); input.Components[typeof(TMPro.TMP_InputField)] = new TMPro.TMP_InputField(); EventSystem.current!.currentSelectedGameObject = input; },
            () => { GameObject input = new(); input.Components[typeof(UnityEngine.UI.InputField)] = new UnityEngine.UI.InputField(); EventSystem.current!.currentSelectedGameObject = input; },
            TapItemMenu
        };
        for (int i = 0; i < blockers.Length; i++)
        {
            Plugin.TestReset(); blockers[i](); ButtonFrame("JoyLTrigger", "JoyRStick");
            Check(Plugin.TestGuideToggles == 0, "Guide blocked by lifecycle/modal state " + i);
        }
        Plugin.TestReset(); InventoryGui.Visible = false;
        Check(!Plugin.TestGuideBindingShown(), "Closed inventory retains mouse toggle");
        InventoryGui.Visible = true; ZInput.Exclusive = false;
        Check(!Plugin.TestGuideBindingShown(), "Mouse mode retains triangle");
    }
}
