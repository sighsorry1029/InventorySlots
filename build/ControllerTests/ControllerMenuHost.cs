using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private const string ModName = "TestPlugin";
    public static bool TestMenuShown, TestMenuFavorite, TestCellFavorite;
    public static int TestMenuChoice;
    public static string TestGridHint = "";
    public static bool TestPlayerSortVisible, TestContainerSortVisible, TestSortInteractable;
    public static string? TestSortFocus => _controllerSortFocus?.ToString();
    private static void TestResetMenuAdapter()
    {
        TestMenuShown = TestMenuFavorite = TestCellFavorite = false;
        TestMenuChoice = 0;
        TestGridHint = "";
        TestPlayerSortVisible = TestContainerSortVisible = false;
        TestSortInteractable = true;
    }
    private static void ShowControllerItemMenu(InventoryGui gui, InventoryGrid grid, bool offerFavorite, bool favorite, int choice)
    { TestMenuShown = true; TestMenuFavorite = offerFavorite; TestMenuChoice = choice; }
    private static void HideControllerItemMenu() => TestMenuShown = false;
    private static bool IsFavoriteSlot(Player player, Vector2i cell) => TestCellFavorite;
    private static bool IsFavoriteSlot(Player player, Inventory inventory, Vector2i cell) => TestCellFavorite;
    private static void SetControllerGridHelp(InventoryGui gui, string text) => TestGridHint = text;
    private static void HideControllerGridHelp() => TestGridHint = "";
    private static string LocalizeUi(string token, string fallback) => fallback;

    private static bool IsControllerSortButtonVisible(bool container) => container ? TestContainerSortVisible : TestPlayerSortVisible;
    private static void ActivateControllerSortButton(InventoryGui gui, bool container)
    {
        if (!IsControllerSortButtonVisible(container) || !TestSortInteractable || gui.m_dragGo != null) return;
        if (container) SortCurrentContainer(Player.m_localPlayer);
        else SortPlayerInventory(Player.m_localPlayer);
    }
    private static void ShowControllerSortButtonFocus(bool container) => TestShownButton = container ? "ContainerSort" : "PlayerSort";
    private static void FocusControllerSortButton(InventoryGui gui, bool container, Vector2i cell)
    {
        gui.m_playerGrid.m_uiGroup.IsActive = !container;
        gui.ContainerGrid.m_uiGroup.IsActive = container;
        RestoreControllerSortCell(gui, container, cell);
    }
    private static void RestoreControllerSortCell(InventoryGui gui, bool container, Vector2i cell) =>
        (container ? gui.ContainerGrid : gui.m_playerGrid).m_selected = cell;
    private static void FocusControllerSortGrid(InventoryGui gui, bool container, Vector2i cell)
    {
        FocusControllerSortButton(gui, container, cell);
        TestLastGridWasContainer = container;
        TestLastGridReturnCell = cell;
    }
}
