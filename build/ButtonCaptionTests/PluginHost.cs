using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    public static IEnumerable<T> CaptionTexts<T>(Button button) where T : Component =>
        GetInventoryButtonCaptionTexts<T>(button);

    public static void RemoveHints(Button button) => RemoveClonedInventoryButtonHints(button);
}
