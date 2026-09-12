using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
#if INVENTORY_SLOTS
using InventorySlots;
#else
using InventoryActions;
#endif

internal static class Program
{
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Run(args[1]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "rule-config-" + Guid.NewGuid().ToString("N") + ".cfg");
        ConfigFile config = new(path, false) { SaveOnConfigSet = false };
        ConfigEntry<string> entry = config.Bind("test", "rules", "Stone", "");
        string applied = entry.Value;
        entry.SettingChanged += (_, _) => applied = entry.Value;
        config.SaveOnConfigSet = true;
        int checks = 0;
        void Check(string name, bool result) { if (!result) throw new Exception(name); checks++; }
        Check("first save", ItemRuleConfigStore.Save(entry, "Stone", "Resin"));
        Check("value and consumer updated", entry.Value == "Resin" && applied == "Resin");
        Check("saved to disk", File.ReadAllText(path).Contains("rules = Resin"));
        Check("autosave restored", config.SaveOnConfigSet);
        Check("stale editor rejected", !ItemRuleConfigStore.Save(entry, "Stone", "Wood"));
        Check("conflict preserves current value", entry.Value == "Resin" && applied == "Resin");
        File.Delete(path); // Only this test's new, uniquely named config.
        Directory.CreateDirectory(path); // Force FileStream save to fail at this exact path.
        bool failed = false;
        try { ItemRuleConfigStore.Save(entry, "Resin", "Wood"); }
        catch (UnauthorizedAccessException) { failed = true; }
        catch (IOException) { failed = true; }
        Check("IO failure observed", failed);
        Check("value and consumer rolled back", entry.Value == "Resin" && applied == "Resin");
        Check("autosave restored after failure", config.SaveOnConfigSet);
        Directory.Delete(path); // Empty directory created immediately above; never recursive.
        Check("same draft can retry", ItemRuleConfigStore.Save(entry, "Resin", "Wood"));
        Check("retry persisted", File.ReadAllText(path).Contains("rules = Wood") && applied == "Wood");
        config.SaveOnConfigSet = false;
        Check("explicit save works with autosave off", ItemRuleConfigStore.Save(entry, "Wood", "Stone"));
        Check("autosave off preserved", !config.SaveOnConfigSet);
        Console.WriteLine($"PASS: {checks} real BepInEx config save checks (including IO failure and retry)");
    }
}
