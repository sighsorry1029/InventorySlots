using System;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static string LocalizeUi(string token, string fallback)
    {
        if (Localization.instance == null || string.IsNullOrWhiteSpace(token))
            return fallback;

        string localized = Localization.instance.Localize(token);
        // Valheim returns [key], rather than $key, when a word is missing.
        // Match this token exactly so valid translations containing brackets survive.
        bool missing = token[0] == '$' &&
            string.Equals(localized, "[" + token.Substring(1) + "]", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(localized) ||
            string.Equals(localized, token, StringComparison.Ordinal) || missing ? fallback : localized;
    }
}
