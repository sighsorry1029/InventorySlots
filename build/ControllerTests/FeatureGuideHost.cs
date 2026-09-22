using BepInEx.Configuration;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static readonly ConfigEntry<Toggle> _showFeatureGuide = new(Toggle.On);
    public static int TestGuideToggles;
    public static bool TestGuideReady = true;
    public static bool TestGuideCollapsed;
    public static bool TestGuideBindingShown() => ShowControllerFeatureGuideToggle();
    public static void TestShowGuide(bool show) => _showFeatureGuide.Value = show ? Toggle.On : Toggle.Off;
    private static bool CanInteractWithFeatureGuideToggle() => TestGuideReady && InventoryGui.instance.m_dragGo == null;
    private static void ToggleFeatureGuideCollapsed() { TestGuideToggles++; TestGuideCollapsed = !TestGuideCollapsed; }
    private static void TestResetGuideAdapter()
    {
        TestGuideToggles = 0;
        TestGuideCollapsed = false;
        TestGuideReady = true;
        TestShowGuide(true);
    }
}
