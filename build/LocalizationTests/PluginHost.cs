#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    internal static string TestLocalize(string token, string fallback) => LocalizeUi(token, fallback);
}
