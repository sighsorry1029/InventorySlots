using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ColoredFork;

[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInProcess("valheim.exe")]
[BepInDependency(InventorySlotsGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ColoredForkPlugin : BaseUnityPlugin
{
    internal const string ModName = "ColoredFork";
    internal const string ModVersion = "1.0.0";
    internal const string Author = "sighsorry";
    internal const string ModGuid = $"{Author}.{ModName}";
    private const string InventorySlotsGuid = "sighsorry.InventorySlots";

    private void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey(InventorySlotsGuid))
        {
            Logger.LogInfo("InventorySlots is loaded; ColoredFork will remain inactive.");
            return;
        }

        new Harmony(ModGuid).PatchAll(typeof(ColoredForkPlugin).Assembly);
        Logger.LogInfo($"{ModName} {ModVersion} loaded.");
    }
}
