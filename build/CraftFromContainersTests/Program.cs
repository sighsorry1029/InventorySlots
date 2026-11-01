using System.Reflection;
using InventorySlots;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

int checks = 0;
void Check(bool success, string label) { if (!success) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
bool Count(out int amount) => InventorySlotsPlugin.Count(out amount);
var repository = new DirectoryInfo(AppContext.BaseDirectory);
while (repository != null && !File.Exists(Path.Combine(repository.FullName, "CraftingRequirementStrip.cs"))) repository = repository.Parent;
if (repository == null) throw new Exception("Cannot locate production requirement strip");
string MethodBody(string source) {
    const string signature = "private static int GetAvailableCraftingRequirementAmount(Requirement requirement, Recipe? recipe)";
    int start = source.IndexOf(signature, StringComparison.Ordinal);
    if (start < 0) throw new Exception("Production display signature changed");
    int brace = source.IndexOf('{', start), depth = 1, end = brace + 1;
    while (depth > 0 && end < source.Length) { if (source[end] == '{') depth++; if (source[end] == '}') depth--; end++; }
    return source[start..end].Replace("\r\n", "\n");
}
Check(MethodBody(File.ReadAllText(Path.Combine(repository.FullName, "CraftingRequirementStrip.cs"))) ==
      MethodBody(File.ReadAllText(Path.Combine(repository.FullName, "build/CraftFromContainersTests/RequirementDisplay.cs"))),
      "display fixture exactly matches the production method");
Check(!Count(out var amount) && amount == 0, "absent CFC permits the existing provider fallback");
Chainloader.PluginInfos["aedenthorn.CraftFromContainers"] = new() { Instance = new FakeCfc() };
Player.m_localPlayer = new();
var first = new Inventory { Amount = 20 };
var second = new Inventory { Amount = 30 };
FakeCfc.Containers = new() { new(first), new(first), new(second), null! };
Check(Count(out amount) && amount == 50, "actual adapter counts multiple chests once each and skips null containers");
Check(first.LastName == "$item_resin" && second.LastName == "$item_resin", "counts the requested shared item name");
FakeCfc.modEnabled.Value = false;
Check(Count(out amount) && amount == 0, "disabled CFC contributes no chest materials");
FakeCfc.modEnabled.Value = true;
FakeCfc.Allowed = false;
Check(Count(out amount) && amount == 0, "CFC prevent key contributes no chest materials");
FakeCfc.Allowed = true;
Player.m_localPlayer = null;
Check(Count(out amount) && amount == 0, "missing local player contributes no chest materials");
Player.m_localPlayer = new();
first.Amount = int.MaxValue; second.Amount = 30;
Check(Count(out amount) && amount == int.MaxValue, "large chest totals saturate instead of overflowing");
first.Amount = -10; second.Amount = 30;
Check(Count(out amount) && amount == 30, "invalid negative counts cannot reduce stock");
FakeCfc.Containers.Clear();
Check(Count(out amount) && amount == 0, "empty nearby container list contributes zero");

FakeCfc.Containers.Add(new(new Inventory { Amount = 30 }));
InventorySlotsPlugin.Azu = true;
InventorySlotsPlugin.ValheimPlusCompat.CraftingActive = true;
InventorySlotsPlugin.Clear();
Check(InventorySlotsPlugin.Display() == 107, "Azu priority is retained without adding CFC or VPlus totals");
InventorySlotsPlugin.Azu = false; InventorySlotsPlugin.Clear(); FakeCfc.Queries = 0;
Check(InventorySlotsPlugin.Display() == 37, "CFC adds chest and backpack stock once, without VPlus overlap");
Check(InventorySlotsPlugin.Display() == 37 && FakeCfc.Queries == 1, "new requirement display uses its cache without rescanning CFC");
FakeCfc.modEnabled.Value = false; InventorySlotsPlugin.Clear();
Check(InventorySlotsPlugin.Display() == 7, "disabled CFC does not fall through into another chest provider");
FakeCfc.modEnabled.Value = true;
Chainloader.PluginInfos.Clear(); InventorySlotsPlugin.Clear();
Check(InventorySlotsPlugin.Display() == 507, "VPlus display is preserved when CFC and Azu are absent");
Chainloader.PluginInfos["aedenthorn.CraftFromContainers"] = new() { Instance = new FakeCfc() };
Check(InventorySlotsPlugin.Display() == 37, "changing provider cannot reuse a different provider's cached count");
Chainloader.PluginInfos.Clear(); InventorySlotsPlugin.ValheimPlusCompat.CraftingActive = false;
Check(InventorySlotsPlugin.Display() == 7, "vanilla inventory count is preserved without chest providers");

Chainloader.PluginInfos["aedenthorn.CraftFromContainers"] = new() { Instance = new FakeCfc() };
FakeCfc.Containers = new() { new(new Inventory { Amount = 30 }) };
InventorySlotsPlugin.Clear();
Check(InventorySlotsPlugin.Display() == 37, "new source counts chest materials");
FakeCfc.Containers.Clear();
UnityEngine.Time.unscaledTime = 1;
Check(InventorySlotsPlugin.Display() == 7, "expired cache refreshes after chest materials are consumed or moved out of range");

Console.WriteLine($"SUCCESS {checks} adapter checks (isolated dependencies; no game simulation)");

public class FakeCfc
{
    public static ConfigEntry<bool> modEnabled = new() { Value = true };
    public static bool Allowed = true;
    public static List<Container> Containers = new();
    private static bool AllowByKey() => Allowed;
    public static int Queries; public static List<Container> GetNearbyContainers(UnityEngine.Vector3 position) { Queries++; return Containers; }
}
public class Inventory { public int Amount; public string? LastName; public int CountItems(string name, int quality, bool match) { LastName = name; return Amount; } }
public class Container(Inventory inventory) { public Inventory GetInventory() => inventory; }
public class Player { public static Player? m_localPlayer; public UnityEngine.Transform transform = new(); public Inventory Bag = new() { Amount = 7 }; public Inventory GetInventory() => Bag; }
namespace UnityEngine { public struct Vector3 { } public class Transform { public Vector3 position; } }
namespace BepInEx.Configuration { public class ConfigEntry<T> { public T Value = default!; } }
namespace BepInEx.Bootstrap { public class PluginInfo { public object? Instance; } public static class Chainloader { public static Dictionary<string, PluginInfo> PluginInfos = new(); } }
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name, Type[] parameters) => type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, parameters);
        public static FieldInfo? Field(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
    {
        private const string CraftFromContainersGuid = "aedenthorn.CraftFromContainers";
        public static bool Count(out int amount) => TryGetCraftFromContainersAvailableAmount("$item_resin", out amount);
    }
}
