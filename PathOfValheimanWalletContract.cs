using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // The game writes stack counts as ushort. A large wallet is enabled only
    // after checking PoV's active serialization hooks AND their called helpers.
    // This checks a narrow behavior surface, not a DLL version/hash allowlist.
    internal static bool InstallPathOfValheimanWallet(PathOfValheimanStorageBridge bridge, Harmony harmony)
    {
        MethodInfo? weight = null, death = null;
        try
        {
            VerifyPathOfValheimanWalletSerialization(bridge.Assembly);
            weight = bridge.Require("PassiveTree.Gear.Wallet+Weight", "Postfix", typeof(void), typeof(Inventory));
            death = bridge.Require("PassiveTree.Gear.Wallet+KeptOnDeath", "Prefix", typeof(void), typeof(Player), typeof(List<ItemData>).MakeByRefType());
            MethodInfo finalizer = bridge.Require("PassiveTree.Gear.Wallet+KeptOnDeath", "Finalizer", typeof(void), typeof(Player), typeof(List<ItemData>));
            CheckWalletMethod(weight, "C81F0419EE9B76E0D827337C06FE3588101432C0E896E9661BD84527D42E31B3");
            CheckWalletMethod(death, "0BB3E7906B7A83058320FEAE12610E394B558BF756F35FB19E96532C75230E1F");
            CheckWalletMethod(finalizer, "F6F2AEF15AF8DC3DF7214116E6B05528A4B21C05D3618BD18D7F7BDE41BBF291");
            harmony.Patch(weight, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(RunNativeWalletWeight)));
            harmony.Patch(death, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(RunNativeWalletDeath)));
            return true;
        }
        catch (Exception error)
        {
            if (weight != null) harmony.Unpatch(weight, AccessTools.DeclaredMethod(typeof(InventorySlotsPlugin), nameof(RunNativeWalletWeight)));
            if (death != null) harmony.Unpatch(death, AccessTools.DeclaredMethod(typeof(InventorySlotsPlugin), nameof(RunNativeWalletDeath)));
            Log.LogWarning("PoV coin wallet cell is unavailable; ordinary coin handling is retained: " + error.GetBaseException().Message);
            return false;
        }
    }

    private static void CheckWalletMethod(MethodInfo method, string shape)
    {
        if (method == null || PathOfValheimanOverlayShape(method, PatchProcessor.GetOriginalInstructions(method)) != shape)
            throw new NotSupportedException("PoV large-stack contract changed: " + method?.DeclaringType?.Name + "." + method?.Name);
    }

    private static void VerifyPathOfValheimanWalletSerialization(Assembly assembly)
    {
        const string prefix = "PassiveTree.Gear.Wallet";
        var methods = new[]
        {
            ("+BigStackSave", "Prefix", "BF0092B9996855273FBCB4E701403EDBA21FF98322721F04738D0D6982163BB3"),
            ("+BigStackSave", "Finalizer", "3F35F98A5415FE0A54A44EF5ED5124CC3DEA1AA96E30D073EE5CF6F337A9CD4B"),
            ("+BigStackLoad", "Postfix", "FBBFBEE4B75405F08DEA721ABE3724A16BB22EC0149E53169C9119E57B5DA6D4"),
            ("+CoinLoad", "Prefix", "7806E53F9278D5B14B9E4B316877E94E5CDB449027E2C414E68FFEF609D61E1C"),
            ("+CoinLoad", "Finalizer", "D43CE77166ADBEA65F0F45A51125EC3F894ED0676B83BB7CF068E27CCCF61B8E"),
            ("+CoinLoadFlag", "Prefix", "7806E53F9278D5B14B9E4B316877E94E5CDB449027E2C414E68FFEF609D61E1C"),
            ("+CoinLoadFlag", "Finalizer", "D43CE77166ADBEA65F0F45A51125EC3F894ED0676B83BB7CF068E27CCCF61B8E"),
            ("", "LiftForLoad", "072D23FAD9DAB2A1A1D581B84E95CDF680ED32DBFE2880699411FB722C02F2A5"),
            ("", "LoadDone", "BC411D4F3879CE69842487F802F2974F90652087DE16F8A3DBF389ABF44CCD32"),
            ("", "get_CoinShared", "A13C7EA13A90DB0D3F5A2AD116551D0C5984F6A25A37557F1D18B1F0C546B2BB"),
            ("", "IsCoins", "E8B0398E41094E9E5CA0D76CA8D3E26DCDFD5E26B87685ECED8FE732B99B54CA")
        };
        Type itemVersion = typeof(Game).Assembly.GetType("Version+Item", true)!;
        var targets = new[]
        {
            AccessTools.DeclaredMethod(typeof(ItemData), "Save", new[] { typeof(ZPackage) }),
            AccessTools.DeclaredMethod(typeof(ItemData), "Save", new[] { typeof(ZPackage) }),
            AccessTools.DeclaredMethod(typeof(ItemData), "Load", new[] { typeof(ZPackage), typeof(ItemData), itemVersion }),
            AccessTools.DeclaredMethod(typeof(Inventory), "Load", new[] { typeof(ZPackage) }),
            AccessTools.DeclaredMethod(typeof(Inventory), "Load", new[] { typeof(ZPackage) }),
            AccessTools.DeclaredMethod(typeof(Inventory), "Load", new[] { typeof(ZPackage), typeof(bool) }),
            AccessTools.DeclaredMethod(typeof(Inventory), "Load", new[] { typeof(ZPackage), typeof(bool) })
        };
        for (int i = 0; i < methods.Length; i++)
        {
            var spec = methods[i];
            MethodInfo method = AccessTools.DeclaredMethod(assembly.GetType(prefix + spec.Item1, true)!, spec.Item2);
            Type[] parameters = i switch
            {
                0 or 1 or 2 or 10 => new[] { typeof(ItemData) },
                3 or 5 or 7 => new[] { typeof(int).MakeByRefType() },
                4 or 6 or 8 => new[] { typeof(Inventory), typeof(int) },
                _ => Type.EmptyTypes
            };
            Type returns = i == 9 ? typeof(ItemData.SharedData) : i == 10 ? typeof(bool) : typeof(void);
            if (method == null || !method.IsStatic || method.ReturnType != returns ||
                !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters))
                throw new NotSupportedException("PoV large-stack signature changed: " + spec.Item2);
            if (i < targets.Length)
            {
                string[] names = i switch
                {
                    0 or 1 => new[] { "__instance" }, 2 => new[] { "itemData" },
                    3 or 5 => new[] { "__state" }, _ => new[] { "__instance", "__state" }
                };
                if (!method.GetParameters().Select(p => p.Name).SequenceEqual(names))
                    throw new NotSupportedException("PoV large-stack injection changed: " + spec.Item2);
            }
            CheckWalletMethod(method, spec.Item3);
            if (i >= targets.Length) continue;
            Patches? info = Harmony.GetPatchInfo(targets[i]);
            IEnumerable<Patch>? patches = spec.Item2 == "Prefix" ? info?.Prefixes : spec.Item2 == "Postfix" ? info?.Postfixes : info?.Finalizers;
            if (patches == null || patches.Count(p => p.owner == PathOfValheimanGuid && p.PatchMethod == method) != 1)
                throw new NotSupportedException("PoV large-stack hook is not active: " + spec.Item1 + "." + spec.Item2);
        }
    }
}
