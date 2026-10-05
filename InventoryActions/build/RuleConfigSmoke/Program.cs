using System;
using System.IO;
using System.Linq;
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
            foreach (string directory in args.Length > 3 ? new[] { args[0], args[3] } : new[] { args[0] })
            {
                string path = Path.Combine(directory, new AssemblyName(e.Name).Name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        Run(args[1]);
        if (args.Length > 2) CheckGuideConfig(args[1], args[2]);
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

    // Use the actual enum from the built plugin; the controller harness tests
    // routing with a config double, while this checks BepInEx disk persistence.
    private static void CheckGuideConfig(string directory, string modPath)
    {
        Assembly mod = Assembly.LoadFrom(Path.GetFullPath(modPath));
        string modName = mod.GetName().Name!;
        Type stateType = mod.GetType(modName + "." + modName + "Plugin+FeatureGuideState", true)!;
        string path = Path.Combine(Path.GetFullPath(directory), "guide-config-" + Guid.NewGuid().ToString("N") + ".cfg");
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(method =>
            method.Name == "Bind" && method.IsGenericMethodDefinition && method.GetParameters().Length == 4 &&
            method.GetParameters()[0].ParameterType == typeof(string) &&
            method.GetParameters()[3].ParameterType == typeof(ConfigDescription)).MakeGenericMethod(stateType);
        string section = modName == "InventorySlots" ? "5 - Client UI" : "2 - Client";
        ConfigEntryBase Bind(ConfigFile file) => (ConfigEntryBase)bind.Invoke(file, new object[]
            { section, "Feature Guide State", Enum.Parse(stateType, "Expanded"), new ConfigDescription("") })!;
        ConfigFile config = new(path, false) { SaveOnConfigSet = true };
        ConfigEntryBase entry = Bind(config);
        int changes = 0;
        config.SettingChanged += (_, _) => changes++;
        int checks = 0;
        void Check(string name, bool result) { if (!result) throw new Exception(name); checks++; }
        Check("new config starts expanded", entry.BoxedValue.ToString() == "Expanded");
        foreach (string state in new[] { "Hidden", "Collapsed", "Expanded", "Hidden" })
        {
            int before = changes;
            entry.BoxedValue = Enum.Parse(stateType, state);
            Check("config raises change event", changes == before + 1);
            Check("config autosaves state", File.ReadAllText(path).Contains("Feature Guide State = " + state));
            Check("saved state survives fresh bind", Bind(new ConfigFile(path, false)).BoxedValue.ToString() == state);
        }
        config.Reload();
        Check("reload preserves hidden", entry.BoxedValue.ToString() == "Hidden");
        File.Delete(path); // Only this test's uniquely named config.
        Console.WriteLine($"PASS: {modName} {checks} real guide-enum config checks");
    }
}
