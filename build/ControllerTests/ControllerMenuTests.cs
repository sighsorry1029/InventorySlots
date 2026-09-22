using UnityEngine;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static partial class Program
{
    private static void TapItemMenu()
    {
        ButtonFrame("JoyRStick");
        Check(!Plugin.IsControllerItemMenuOpen(), "Stick press alone waits for release, preserving chords");
        Time.unscaledTime += 0.1f;
        ButtonFrame();
    }

    private static void CheckControllerMenuAndSort()
    {
        InventoryGui gui = Plugin.TestReset();
        TapItemMenu();
        Check(Plugin.IsControllerItemMenuOpen() && Plugin.TestMenuShown && Plugin.TestMenuFavorite && Plugin.TestMenuChoice == 0,
            "Standalone short stick release opens favorite/sort menu");
        Check(!Prefix("InventoryControllerEventNavigationPatch") && !Prefix("InventoryControllerGridInputPatch"),
            "Menu reserves native grid and direct InputSystem navigation");
        ButtonFrame("JoyButtonA", "Use", "JoyUse");
        Check(!Plugin.TestMenuShown && Plugin.TestFavorites == 1 && Plugin.TestLastFavorite == new Vector2i(3, 2) && CloseAliasesConsumed,
            "Menu A favorites original selected cell without picking item up");
        Check(!Prefix("InventoryControllerEventNavigationPatch"), "Menu submit consumes closing frame");

        gui = Plugin.TestReset(); TapItemMenu(); ButtonFrame("JoyDPadDown");
        Check(Plugin.TestMenuChoice == 1, "Menu Down selects sort");
        ButtonFrame("JoyButtonA");
        Check(Plugin.TestPlayerSorts == 1 && Plugin.TestFavorites == 0 && !Plugin.TestMenuShown, "Menu sort only runs once");

        gui = Plugin.TestReset(); TapItemMenu(); ButtonFrame("JoyButtonB", "Inventory");
        Check(NoActions && !Plugin.TestMenuShown && CloseAliasesConsumed, "Menu B dismisses without closing inventory or mutating item");

        gui = Plugin.TestReset(); gui.m_playerGrid.m_uiGroup.IsActive = false; gui.ContainerGrid.m_uiGroup.IsActive = true;
        TapItemMenu();
        Check(Plugin.TestMenuShown && !Plugin.TestMenuFavorite, "Container menu never offers favorite");
        ButtonFrame("JoyButtonA");
        Check(Plugin.TestContainerSorts == 1 && Plugin.TestPlayerSorts == 0, "Container menu sorts the selected container");

        gui = Plugin.TestReset(); Plugin.TestCanFavorite = false; TapItemMenu();
        Check(Plugin.TestMenuShown && !Plugin.TestMenuFavorite, "Protected/special cell favorite policy is preserved");
        ButtonFrame("JoyButtonB");
        gui = Plugin.TestReset(); Plugin.TestEnable(false); TapItemMenu();
        Check(Plugin.TestMenuShown, "Direct menu remains available with legacy hotkeys disabled");
        ButtonFrame("JoyButtonB");

        foreach (string face in new[] { "JoyButtonA", "JoyButtonX", "JoyButtonY", "JoyButtonB" })
        {
            gui = Plugin.TestReset(); ButtonFrame("JoyRStick", face); ButtonFrame();
            Check(!Plugin.TestMenuShown, "Held chord never opens menu on release: " + face);
            Check(Plugin.TestFavorites + Plugin.TestPlayerSorts + Plugin.TestRuleOpens == 1, "Existing chord remains functional: " + face);
        }
        gui = Plugin.TestReset(); ButtonFrame("JoyRStick"); Time.unscaledTime += 0.6f; ButtonFrame();
        Check(!Plugin.TestMenuShown, "Long stick hold without action does not accidentally open menu");
        gui = Plugin.TestReset(); ButtonFrame("JoyRStick", "JoyDPadRight"); ButtonFrame();
        Check(!Plugin.TestMenuShown, "Navigation suppresses pending tap");
        gui = Plugin.TestReset(); gui.m_dragGo = new GameObject(); TapItemMenu();
        Check(!Plugin.TestMenuShown, "Held item never opens menu");

        foreach (string invalidation in new[] { "keyboard", "hidden", "rules", "split", "drag", "inventory", "selection", "player" })
        {
            gui = Plugin.TestReset(); TapItemMenu();
            switch (invalidation)
            {
                case "keyboard": ZInput.Exclusive = false; break;
                case "hidden": InventoryGui.Visible = false; break;
                case "rules": Plugin.TestRulesPinned = true; break;
                case "split": gui.m_splitDialog = new Component(); break;
                case "drag": gui.m_dragGo = new GameObject(); break;
                case "inventory": gui.m_playerGrid.Inventory = new Inventory(); break;
                case "selection": gui.m_playerGrid.m_selected = new Vector2i(4, 2); break;
                case "player": Player.m_localPlayer = new Player(); break;
            }
            ButtonFrame();
            Check(!Plugin.TestMenuShown && NoActions, "Menu invalidation closes safely: " + invalidation);
        }
        gui = Plugin.TestReset(); TapItemMenu(); ButtonFrame("JoyTabRight");
        Check(!Plugin.TestMenuShown && !Plugin.IsInventoryControllerInputReserved() && ZInput.GetButtonDown("JoyTabRight"),
            "Menu lets native shoulder group switch pass through");

        foreach (bool container in new[] { false, true })
        {
            gui = PrepareButtonRow(7);
            Plugin.TestPlayerSortVisible = Plugin.TestContainerSortVisible = true;
            Plugin.TestContainerOpen = container;
            InventoryGrid grid = container ? gui.ContainerGrid : gui.m_playerGrid;
            gui.m_playerGrid.m_uiGroup.IsActive = !container; gui.ContainerGrid.m_uiGroup.IsActive = container;
            Vector2i cell = new(7, container ? 0 : 3);
            grid.m_selected = cell;
            ButtonFrame("JoyDPadRight");
            Check(Plugin.TestSortFocus == container.ToString() && Plugin.IsInventoryButtonNavigationActive(),
                "Right from adjacent edge selects S: " + container);
            ButtonFrame("JoyButtonA");
            Check((container ? Plugin.TestContainerSorts : Plugin.TestPlayerSorts) == 1, "S activates selected inventory: " + container);
            ButtonFrame("JoyButtonB", "Inventory");
            Check(!Plugin.IsInventoryButtonNavigationActive() && grid.m_selected == cell && CloseAliasesConsumed,
                "B from S restores exact source cell: " + container);
        }
        gui = PrepareButtonRow(7); Plugin.TestPlayerSortVisible = true; Plugin.TestSortInteractable = false;
        ButtonFrame("JoyDPadRight"); ButtonFrame("JoyButtonA");
        Check(Plugin.TestPlayerSorts == 0, "Disabled S cannot activate");
        gui.m_dragGo = new GameObject(); Plugin.TestSortInteractable = true; ButtonFrame("JoyButtonA");
        Check(Plugin.TestPlayerSorts == 0, "S cannot sort while holding an item");
        gui = PrepareButtonRow(6); Plugin.TestPlayerSortVisible = true;
        ButtonFrame("JoyDPadRight"); gui.m_playerGrid.m_selected = new Vector2i(7, 3); Plugin.OnControllerInputUpdated();
        Check(!Plugin.IsInventoryButtonNavigationActive(), "Arriving at edge cannot enter S twice in same frame");
        ButtonFrame("JoyDPadRight");
        Check(Plugin.IsInventoryButtonNavigationActive(), "Additional Right from edge enters S");
    }
}
