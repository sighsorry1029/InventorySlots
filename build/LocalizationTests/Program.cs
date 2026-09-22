using System;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static class Program
{
    private static int _checks;

    private static void Equal(string expected, string actual, string name)
    {
        _checks++;
        if (actual != expected) throw new InvalidOperationException(name + ": " + actual);
    }

    private static void Main()
    {
        string prefix = typeof(Plugin).Namespace!.ToLowerInvariant();
        string token = "$" + prefix + "_rules_pad_browse";
        const string fallback = "↑↓: item · ←→: control\n{submit}: {action} · {close}: close";
        Localization.instance = null;
        Equal(fallback, Plugin.TestLocalize(token, fallback), "Localization unavailable");

        Localization localization = Localization.instance = new();
        Equal(fallback, Plugin.TestLocalize(token, fallback), "Missing controller token uses fallback");
        Equal("{close}: close", Plugin.TestLocalize("$" + prefix + "_rules_pad_empty", "{close}: close"),
            "Missing empty-panel token uses fallback");
        Equal("Guide", Plugin.TestLocalize("$" + prefix + "_feature_guide_controller", "Guide"),
            "Missing controller guide uses fallback");
        Equal("Cancel", Plugin.TestLocalize("$menu_cancel", "Cancel"), "Missing base-game token uses fallback");
        Equal("Legacy fallback", Plugin.TestLocalize("$" + prefix + "_rules_pad_restock", "Legacy fallback"),
            "Old missing controller token does not leak as brackets");
        Equal("Empty", Plugin.TestLocalize("", "Empty"), "Empty token");
        Equal("Empty", Plugin.TestLocalize(" \t", "Empty"), "Whitespace token");

        localization.Translate = _ => token;
        Equal(fallback, Plugin.TestLocalize(token, fallback), "Unchanged token uses fallback");
        localization.Translate = _ => " \t";
        Equal(fallback, Plugin.TestLocalize(token, fallback), "Blank translation uses fallback");
        localization.Translate = _ => "↑↓: 항목 · ←→: 조작 선택\n{submit}: {action} · {close}: 닫기";
        Equal("↑↓: 항목 · ←→: 조작 선택\n{submit}: {action} · {close}: 닫기",
            Plugin.TestLocalize(token, fallback), "Translated controller guide preserves placeholders");
        localization.Translate = _ => "[A] Confirm / [B] Close";
        Equal("[A] Confirm / [B] Close", Plugin.TestLocalize(token, fallback), "Bracketed button hints are valid");
        localization.Translate = _ => "[Translated label]";
        Equal("[Translated label]", Plugin.TestLocalize(token, fallback), "Bracketed translation is valid");
        localization.Translate = _ => "[" + prefix + "_rules_pad_empty]";
        Equal("[" + prefix + "_rules_pad_empty]", Plugin.TestLocalize(token, fallback),
            "Only the requested missing-token marker is rejected");
        localization.Translate = text => text;
        Equal("Literal item", Plugin.TestLocalize("Literal item", "Literal item"), "Literal item name is preserved");
        Console.WriteLine($"{typeof(Plugin).Namespace}: {_checks} localization checks passed.");
    }
}

// Only the external game's result is simulated; the mod helper is compiled from source.
internal sealed class Localization
{
    public static Localization? instance;
    public Func<string, string> Translate = token => token.StartsWith("$", StringComparison.Ordinal)
        ? "[" + token.Substring(1) + "]" : token;
    public string Localize(string token) => Translate(token);
}
