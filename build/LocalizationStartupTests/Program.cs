using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
#if INVENTORY_SLOTS
using Localizer = LocalizationManager.Localizer;
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Localizer = InventoryActions.LocalizationManager.Localizer;
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static class Program
{
    private static int _checks;
    private static readonly string ModName = typeof(Plugin).Namespace!;
    private static readonly string TitleKey = ModName.ToLowerInvariant() + "_rules_restock_title";

    private static void Check(bool success, string name)
    {
        _checks++;
        if (!success) throw new InvalidOperationException(name);
    }

    private static void Reset()
    {
        foreach (string field in new[] { "_loaded", "_plugin" })
            typeof(Localizer).GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, field == "_loaded" ? false : null);
        Harmony.Postfixes.Clear();
        PlatformInitializer.PlatformInitialized = false;
        Localization.Reset();
    }

    private static void Main(string[] args)
    {
        // Separate runs let the unpatched code demonstrate both failures independently.
        if (args.Length > 0 && args[0] == "early-ui")
        {
            Reset();
            Check(Plugin.TestLocalize("$" + TitleKey, "Fallback") == "Fallback", "Early UI fallback");
            Check(Localization.GetterCalls == 0, "UI must not create localization before platform startup");
            Console.WriteLine($"{ModName}: {_checks} early UI checks passed.");
            return;
        }

        string testRoot = Path.Combine(Path.GetTempPath(), "InventoryLocalizationTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        Paths.PluginPath = Path.Combine(testRoot, "plugins");
        Paths.ConfigPath = Path.Combine(testRoot, "config");
        BaseUnityPlugin plugin = new(ModName);
        try
        {
            Reset();
            Localizer.Load(plugin);
            Check(Harmony.Postfixes.Count == 2, "Language and menu hooks installed before platform startup");
            Check(Localization.GetterCalls == 0, "Load must not force the lazy singleton");
            Localizer.Load(plugin);
            new FejdStartup().SetupGui();
            Check(Harmony.Postfixes.Count == 2, "Repeated load must not duplicate hooks");
            Check(Localization.GetterCalls == 0, "Early retry must defer too");
            Check(Plugin.TestLocalize("$" + TitleKey, "Fallback") == "Fallback", "Early UI fallback");

            // The dedicated server may never set PlatformInitialized. The game-provided
            // instance must still receive translations without querying preferences.
            Localization serverLocalization = new();
            serverLocalization.SetupLanguage("Korean");
            Check(serverLocalization.Localize("$" + TitleKey) == "보충 대상", "Instance hook works without client platform startup");
            Check(Localization.GetterCalls == 0 && Localization.PreferenceReads == 0, "Instance hook must not query singleton/preferences");

            PlatformInitializer.PlatformInitialized = true;
            Localization localization = Localization.instance;
            Check(Localization.CreatedInstances == 2, "One server fixture plus one lazy client instance; no constructor recursion");
            Check(localization.Localize("$" + TitleKey) == "Restock targets", "Default language after platform initialization");
            Check(Plugin.TestLocalize("$" + TitleKey, "Fallback") == "Restock targets", "UI uses translations after initialization");

            Localization.SelectedLanguage = "Korean";
            localization.SetupLanguage("Korean");
            Check(localization.Localize("$" + TitleKey) == "보충 대상", "Live language change");
            localization.Words.Clear();
            new FejdStartup().SetupGui();
            Check(localization.Localize("$" + TitleKey) == "보충 대상", "Menu hook reloads selected language");
            localization.SetupLanguage("UntranslatedLanguage");
            Check(localization.Localize("$" + TitleKey) == "Restock targets", "English fallback preserved");

            Directory.CreateDirectory(Paths.PluginPath);
            string externalFile = Path.Combine(Paths.PluginPath, ModName + ".Korean.yml");
            File.WriteAllText(externalFile, TitleKey + ": External override\n");
            localization.SetupLanguage("Korean");
            Check(localization.Localize("$" + TitleKey) == "External override", "External translation overrides embedded language");
            File.Delete(externalFile);

            // Late registration must apply translations even if SetupLanguage already ran.
            Reset();
            PlatformInitializer.PlatformInitialized = true;
            Localization.SelectedLanguage = "Korean";
            localization = Localization.instance;
            Check(localization.Words.Count == 0, "Late-load fixture has no mod words yet");
            Localizer.Load(plugin);
            Check(localization.Localize("$" + TitleKey) == "보충 대상", "Late load applies current language immediately");
            Check(Localization.CreatedInstances == 1, "Late load reuses game instance");
            Console.WriteLine($"{ModName}: {_checks} startup localization checks passed.");
        }
        finally
        {
            // Unique directory created by this harness; no user files are removed.
            Directory.Delete(testRoot, recursive: true);
        }
    }
}

#if INVENTORY_SLOTS
namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions
{
    public sealed partial class InventoryActionsPlugin
#endif
    {
        internal static string TestLocalize(string token, string fallback) => LocalizeUi(token, fallback);
    }
}
