using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static partial class Program
{
    private static int _checks;
    private static void Check(bool result, string name)
    {
        _checks++;
        if (!result) throw new InvalidOperationException(name);
    }
    private static bool Prefix(string type, params object[] args)
    {
        MethodInfo method = typeof(Plugin).Assembly.GetType(typeof(Plugin).Namespace + "." + type)!
            .GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.ReturnType == typeof(bool) ? (bool)method.Invoke(null, args)! : InvokeVoid(method, args);
    }
    private static bool InvokeVoid(MethodInfo method, object[] args) { method.Invoke(null, args); return true; }
    private static void Chord(string face) => ZInput.Press("JoyRStick", face);
    private static void Dispatch() => Plugin.UpdateInventoryControllerInput(InventoryGui.instance);
    private static bool NoActions => Plugin.TestFavorites + Plugin.TestPlayerSorts + Plugin.TestContainerSorts + Plugin.TestRuleOpens == 0;

    private static void CheckSameFrameInputRefresh(string face)
    {
        InventoryGui gui = Plugin.TestReset();
        GameObject heldItem = gui.m_dragGo = new GameObject();
        Time.frameCount++;
        int frame = Time.frameCount;
        // ProcessNavigation can see the physical held state before ZInput's
        // game tick has calculated the semantic button-down state.
        ZInput.Held.UnionWith(new[] { "JoyRStick", face, "Inventory", "Use", "JoyUse" });
        Check(!Prefix("InventoryControllerEventNavigationPatch"), "Early navigation reserves held chord " + face);
        Check(NoActions && ZInput.Down.Count == 0 && ZInput.ResetCalls.Count == 0,
            "Early held-only input does not dispatch or consume " + face);

        // Simulate the later ZInput tick without advancing Unity's frame.
        ZInput.Down.UnionWith(new[] { face, "Inventory", "Use", "JoyUse" });
        Plugin.OnControllerInputUpdated();
        Prefix("InventoryControllerUpdatePatch", gui);
        Check(Time.frameCount == frame && Plugin.TestRuleOpens == 1 && Plugin.TestLastRestock == (face == "JoyButtonY") &&
            ReferenceEquals(Plugin.TestLastRuleDragObject, heldItem),
            "Later same-frame button-down opens rules for held item " + face);
        Check(!ZInput.GetButtonDown(face) && !ZInput.GetButton("Inventory") && !ZInput.GetButtonDown("Inventory") &&
            !ZInput.GetButton("Use") && !ZInput.GetButtonDown("Use") &&
            !ZInput.GetButton("JoyUse") && !ZInput.GetButtonDown("JoyUse"),
            "Later same-frame action consumes face and close/use aliases " + face);
        Check(ZInput.GetButton("JoyRStick"), "Later same-frame action preserves held modifier " + face);

        // A second entry point must remain blocked even if the game's input
        // state is populated again before this frame finishes.
        int consumed = ZInput.ResetCalls.Count;
        ZInput.Press(face);
        Plugin.OnControllerInputUpdated();
        Prefix("InventoryControllerEventNavigationPatch");
        Prefix("InventoryControllerUpdatePatch", gui);
        Prefix("InventoryControllerGridInputPatch");
        UIGamePad shortcut = new(); shortcut.transform.parent = gui.transform;
        Check(!Prefix("InventoryControllerButtonInputPatch", shortcut, true), "Consumed frame still suppresses shortcut " + face);
        Check(Plugin.TestRuleOpens == 1 && ZInput.ResetCalls.Count == consumed,
            "Consumed action is not dispatched twice in same frame " + face);
    }

    private static void CheckStalePreviousFrameInput(string face)
    {
        InventoryGui gui = Plugin.TestReset();
        gui.m_dragGo = new GameObject();
        ZInput.Press("JoyRStick", face, "Inventory", "Use", "JoyUse");
        Time.frameCount++;
        Check(!Prefix("InventoryControllerEventNavigationPatch"), "Early navigation reserves stale held chord " + face);
        Check(NoActions && ZInput.ResetCalls.Count == 0,
            "Previous-frame button-down cannot dispatch before current input tick " + face);
        ZInput.Down.Clear();
        Plugin.OnControllerInputUpdated();
        Prefix("InventoryControllerUpdatePatch", gui);
        Check(NoActions && ZInput.ResetCalls.Count == 0,
            "Cleared previous-frame button-down never becomes a fresh action " + face);
    }

    private static void CheckSameFrameGateRefresh(bool gamepadInactive)
    {
        InventoryGui gui = Plugin.TestReset();
        string gate = gamepadInactive ? "gamepad mode" : "grid focus";
        int frame = Time.frameCount;
        Chord("JoyButtonA");
        if (gamepadInactive) ZInput.Exclusive = false;
        else gui.m_playerGrid.m_uiGroup.IsActive = false;
        Check(Prefix("InventoryControllerEventNavigationPatch") && NoActions && !Plugin.IsInventoryControllerInputReserved(),
            "Early inactive " + gate + " leaves input alone");
        Check(ZInput.GetButtonDown("JoyButtonA"), "Early inactive " + gate + " retains pending action");

        ZInput.Exclusive = true;
        gui.m_playerGrid.m_uiGroup.IsActive = true;
        Prefix("InventoryControllerUpdatePatch", gui);
        Check(Time.frameCount == frame && Plugin.TestFavorites == 1 && !ZInput.GetButtonDown("JoyButtonA"),
            "Later same-frame ready " + gate + " dispatches pending action");
        Prefix("InventoryControllerGridInputPatch");
        Check(Plugin.TestFavorites == 1, "Later same-frame ready " + gate + " dispatches only once");
    }

    private static void Main()
    {
        InventoryGui gui = Plugin.TestReset();
        Check(Plugin.TestInventoryDefault == "JoyRStick", "Default inventory modifier");
        Check(Plugin.TestWorldDefault == "JoyAltKeys", "Default world modifier");
        Chord("JoyButtonA");
        Prefix("InventoryControllerEventNavigationPatch");
        Prefix("InventoryControllerUpdatePatch", gui);
        Prefix("InventoryControllerGridInputPatch");
        UIGamePad shortcut = new(); shortcut.transform.parent = gui.transform;
        Check(!Prefix("InventoryControllerButtonInputPatch", shortcut, true), "UIGamePad suppressed after first dispatch");
        Check(Plugin.TestFavorites == 1, "All four entry points dispatch once in same frame");
        Check(Plugin.TestLastFavorite == new Vector2i(3, 2), "Empty cell favorites use selected coordinates");
        Check(Plugin.IsInventoryControllerInputReserved(), "Favorite reserves grid action");
        Check(ZInput.GetButton("JoyRStick"), "Held inventory modifier survives consume");
        Check(!ZInput.GetButtonDown("JoyButtonA"), "A does not pick up after favorite");
        Check(!Prefix("InventoryControllerGuiInputPatch"), "Tab group handler reserved");

        gui = Plugin.TestReset();
        ZInput.Press("JoyButtonA"); Dispatch();
        Check(NoActions && !Plugin.IsInventoryControllerInputReserved(), "No modifier leaves vanilla A alone");
        Check(ZInput.GetButtonDown("JoyButtonA"), "Vanilla unmodified A retained");
        Plugin.TestReset(); Plugin.TestSetInventoryModifier("Off"); Chord("JoyButtonX"); Dispatch();
        Check(NoActions && !Plugin.IsInventoryControllerInputReserved(), "Off does not consume sort chord");
        Plugin.TestReset(); Plugin.TestEnable(false); Chord("JoyButtonY"); Dispatch();
        Check(NoActions && !Plugin.IsInventoryControllerInputReserved(), "Disabled controller leaves inventory unchanged");
        Plugin.TestReset(); ZInput.Exclusive = false; Chord("JoyButtonB"); Dispatch();
        Check(NoActions && !Plugin.IsInventoryControllerInputReserved(), "Mouse-mode gamepad not intercepted");
        Plugin.TestReset(); ZInput.Press("JoyRStick"); Dispatch();
        Check(NoActions && Plugin.IsInventoryControllerInputReserved(), "Modifier alone reserves conflicting native shortcuts");
        foreach (string modifier in new[] { "JoyLStick", "JoyLTrigger", "JoyRTrigger" })
        {
            Plugin.TestReset(); Plugin.TestSetInventoryModifier(modifier); ZInput.Press(modifier, "JoyButtonA"); Dispatch();
            Check(Plugin.TestFavorites == 1 && ZInput.GetButton(modifier), "Configured modifier dispatches and stays held: " + modifier);
        }
#if INVENTORY_SLOTS
        Plugin.TestReset(); Plugin.TestLegacyFavoriteHeld = true; Dispatch();
        Check(NoActions && Plugin.IsInventoryControllerInputReserved(), "Legacy favorite modifier reserves before A press");
        Time.frameCount++; ZInput.Press("JoyButtonA"); Plugin.OnControllerInputUpdated(); Dispatch();
        Check(Plugin.TestFavorites == 1, "Legacy favorite modifier activates focused cell");
#endif

        gui = Plugin.TestReset(); Chord("JoyButtonX"); Dispatch();
        Check(Plugin.TestPlayerSorts == 1 && Plugin.TestContainerSorts == 0, "Player focus sorts player");
        gui = Plugin.TestReset(); gui.m_playerGrid.m_uiGroup.IsActive = false; gui.ContainerGrid.m_uiGroup.IsActive = true;
        Chord("JoyButtonX"); Dispatch();
        Check(Plugin.TestContainerSorts == 1 && Plugin.TestPlayerSorts == 0, "Container focus sorts container");
        gui = Plugin.TestReset(); gui.m_playerGrid.m_uiGroup.IsActive = false; gui.ContainerGrid.m_uiGroup.IsActive = true;
        Chord("JoyButtonA"); Dispatch();
        Check(NoActions && Plugin.IsInventoryControllerInputReserved(), "Container A cannot favorite or take item");
        gui = Plugin.TestReset(); gui.m_playerGrid.m_uiGroup.IsActive = false;
        Chord("JoyButtonX"); Dispatch();
        Check(NoActions && !Plugin.IsInventoryControllerInputReserved(), "Crafting/no grid focus left alone");

        foreach (string face in new[] { "JoyButtonA", "JoyButtonX" })
        {
            gui = Plugin.TestReset(); gui.m_dragGo = new GameObject(); Chord(face); Dispatch();
            Check(NoActions && Plugin.IsInventoryControllerInputReserved(), "Dragging blocks mutation " + face);
        }
        foreach (string face in new[] { "JoyButtonY", "JoyButtonB" })
        {
            gui = Plugin.TestReset(); gui.m_dragGo = new GameObject(); Chord(face);
            ZInput.Press("Inventory", "Use", "JoyUse"); Dispatch();
            Check(Plugin.TestRuleOpens == 1 && Plugin.TestLastRestock == (face == "JoyButtonY"), "Dragging permits rule registration " + face);
            Check(!ZInput.GetButtonDown(face) && !ZInput.GetButton("Inventory") && !ZInput.GetButton("Use") && !ZInput.GetButton("JoyUse"), "Rule opening consumes close/use aliases " + face);
            Check(ZInput.GetButton("JoyRStick"), "Rule opening preserves modifier " + face);
        }

        foreach (string face in new[] { "JoyButtonY", "JoyButtonB" })
        {
            CheckSameFrameInputRefresh(face);
            CheckStalePreviousFrameInput(face);
        }
        CheckSameFrameGateRefresh(gamepadInactive: true);
        CheckSameFrameGateRefresh(gamepadInactive: false);

        gui = Plugin.TestReset(); gui.m_playerGrid.m_selected = new Vector2i(8, 0); Chord("JoyButtonA"); Dispatch();
        Check(Plugin.TestFavorites == 0, "Out of bounds favorite rejected");
        gui = Plugin.TestReset(); gui.m_playerGrid.Inventory = new Inventory(); Chord("JoyButtonA"); Dispatch();
        Check(Plugin.TestFavorites == 0, "Foreign player grid inventory cannot be favorited");
        Plugin.TestReset(); Plugin.TestCanFavorite = false; Chord("JoyButtonA"); Dispatch();
        Check(Plugin.TestFavorites == 0, "Protected slot policy preserved");

        Action[] blockers =
        {
            () => Plugin.TestClosing = true,
            () => InventoryGui.Visible = false,
            () => Plugin.TestBlocked = true,
            () => Plugin.TestDedicated = true,
            () => Plugin.TestPluginActive(false),
            () => Plugin.TestCanShow = false,
            () => Player.m_localPlayer.Loading = true,
            () => Player.m_localPlayer.Teleporting = true,
            () => InventoryGui.instance.m_splitDialog = new Component(),
            () => InventoryGui.instance.m_variantDialog = new Component(),
            () => InventoryGui.instance.IsSkillsPanelOpen = true,
            () => InventoryGui.instance.IsTextPanelOpen = true,
            () => InventoryGui.instance.IsTrophisPanelOpen = true,
            () => InventoryGui.instance.IsAchievementsPanelOpen = true,
            Plugin.TestSetTrashDialog,
            () => { GameObject input = new(); input.Components[typeof(TMPro.TMP_InputField)] = new TMPro.TMP_InputField(); EventSystem.current!.currentSelectedGameObject = input; },
            () => { GameObject input = new(); input.Components[typeof(UnityEngine.UI.InputField)] = new UnityEngine.UI.InputField(); EventSystem.current!.currentSelectedGameObject = input; }
        };
        for (int i = 0; i < blockers.Length; i++)
        {
            Plugin.TestReset(); blockers[i](); Chord("JoyButtonA"); Dispatch();
            Check(NoActions && !Plugin.IsInventoryControllerInputReserved(), "Modal/lifecycle blocker " + i);
        }

        Plugin.TestReset(); Plugin.TestRulesPinned = true; Chord("JoyButtonA");
        Check(!Prefix("InventoryControllerEventNavigationPatch") && NoActions, "Rules panel blocks direct InputSystem submit");
        Plugin.TestReset(); Plugin.TestRulesClosedFrame = Time.frameCount;
        Check(!Prefix("InventoryControllerEventNavigationPatch"), "Rules close frame still blocks direct InputSystem submit");
        Time.frameCount++;
        Check(Prefix("InventoryControllerEventNavigationPatch"), "Rules navigation restored next frame");
        gui = Plugin.TestReset(); Chord("JoyButtonA"); Dispatch();
        Check(!Prefix("InventoryControllerEventNavigationPatch"), "Chord blocks direct InputSystem submit");
        shortcut = new UIGamePad();
        Check(Prefix("InventoryControllerButtonInputPatch", shortcut, true), "Unrelated external UI shortcuts untouched");

        // Layout-specific physical wiring is supplied by the game. This tests
        // the shared semantic aliases used by every layout, without pretending
        // the stubs emulate an actual device or the Unity input action assets.
        Plugin.TestReset(); InventoryGui.Visible = false;
        ZInput.Press("JoyAltKeys", "JoyUse");
        for (int frame = 0; frame < 10; frame++)
        {
            Time.frameCount++;
            Check(Plugin.TestWorldHeld(), "World hold remains active frame " + frame);
            Dispatch();
        }
        Check(ZInput.ResetCalls.Count == 0 && ZInput.GetButton("JoyUse"), "World hold never resets held alias");
        ZInput.Held.Remove("JoyAltKeys"); Check(!Plugin.TestWorldHeld(), "Releasing modifier ends restock chord");
        ZInput.Held.Add("JoyAltKeys"); ZInput.Held.Remove("JoyUse"); Check(!Plugin.TestWorldHeld(), "Releasing Use ends restock chord");
        ZInput.Held.Add("JoyUse"); Plugin.TestSetWorldModifier("Off"); Check(!Plugin.TestWorldHeld(), "World Off disables chord");
        Plugin.TestSetWorldModifier("JoyAltKeys"); Plugin.TestEnable(false); Check(!Plugin.TestWorldHeld(), "Global Off disables world chord");
        Plugin.TestEnable(true); ZInput.Exclusive = false; Check(!Plugin.TestWorldHeld(), "World chord respects exclusive gamepad");

        CheckButtonNavigation();
        CheckControllerMenuAndSort();
        CheckItemRuleControllerState();
        CheckControllerFeatureGuide();
        Console.WriteLine($"{typeof(Plugin).Namespace}: {_checks} controller dispatcher/navigation checks passed (source-linked fake host; no Unity/game/device execution).");
    }
}
