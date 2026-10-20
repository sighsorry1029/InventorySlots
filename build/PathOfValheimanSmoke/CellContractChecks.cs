using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

internal static partial class Program
{
    private static IReadOnlyList<string> fixtureStorageCells = null!;
    private static bool FutureStorageCells(ref IReadOnlyList<string> __result)
    { __result = fixtureStorageCells; return false; }

    private static void CheckCellContracts(Assembly pov, Type bridgeType)
    {
        Type equipment = pov.GetType("Valheiman.Api.Equipment", true)!;
        Type storage = pov.GetType("Valheiman.Api.Storage", true)!;
        Type info = pov.GetType("Valheiman.Api.CellInfo", true)!;
        var descriptors = (IList)equipment.GetProperty("Cells")!.GetValue(null)!;
        var originals = descriptors.Cast<object>().ToArray();
        string[] storageIds = ((IEnumerable<string>)storage.GetProperty("Cells")!.GetValue(null)!).ToArray();
        object future = Activator.CreateInstance(info)!;
        FieldInfo nativeSlot = info.GetField("Slot", Instances)!;
        void Set(string name, object value) => info.GetProperty(name)!.GetSetMethod(true)!.Invoke(future, new[] { value });
        Set("Id", "utility.future"); Set("Kind", Enum.Parse(info.GetProperty("Kind")!.PropertyType, "Utility"));
        Set("Worn", false); nativeSlot.SetValue(future, Enum.ToObject(nativeSlot.FieldType, 60));
        descriptors.Add(future);
        fixtureStorageCells = storageIds.Concat(new[] { "utility.future" }).ToArray();
        var fixture = new Harmony("smoke.pov.future.descriptors");
        var validation = new Harmony("smoke.pov.future.validation");
        fixture.Patch(storage.GetProperty("Cells")!.GetGetMethod(), prefix: new HarmonyMethod(typeof(Program), nameof(FutureStorageCells)));
        object Validate()
        {
            object bridge = Activator.CreateInstance(bridgeType, Instances, null, new object[] { pov }, null)!;
            bridgeType.GetMethod("InstallFixes", Instances)!.Invoke(bridge, new object[] { validation });
            return bridge;
        }
        bool Rejected()
        {
            try { Validate(); return false; }
            catch (TargetInvocationException error) { return error.InnerException is NotSupportedException; }
        }
        try
        {
            object bridge = Validate();
            Check(((HashSet<int>)bridgeType.GetField("UnsupportedOrdinals", Instances)!.GetValue(bridge)!).Contains(60) &&
                  !(bool)bridgeType.GetMethod("SupportsCell", Statics)!.Invoke(null, new object[] { "utility.future" })!,
                "additive future storage descriptor is accepted for recovery but never hosted");
            Set("Worn", true);
            Check(Rejected(), "new worn role that can bypass provider still fails safely");
            Set("Worn", false);
            Set("Id", "helmet");
            Check(Rejected(), "duplicate cell ID cannot replace an existing role");
            Set("Id", "utility.future");
            nativeSlot.SetValue(future, Enum.ToObject(nativeSlot.FieldType, 0));
            Check(Rejected(), "duplicate native coordinate cannot alias a supported slot");
            nativeSlot.SetValue(future, Enum.ToObject(nativeSlot.FieldType, 60));
            object helmet = originals.First(c => (string)info.GetProperty("Id")!.GetValue(c)! == "helmet");
            info.GetProperty("Worn")!.GetSetMethod(true)!.Invoke(helmet, new object[] { false });
            try { Check(Rejected(), "changed supported equipment-effect contract is rejected"); }
            finally { info.GetProperty("Worn")!.GetSetMethod(true)!.Invoke(helmet, new object[] { true }); }
            fixtureStorageCells = storageIds.Concat(new[] { "utility.future", "unknown.storage" }).ToArray();
            Check(Rejected(), "storage ID without a native descriptor is rejected");
        }
        finally
        {
            validation.UnpatchSelf(); fixture.UnpatchSelf();
            descriptors.Clear(); foreach (object cell in originals) descriptors.Add(cell);
        }
    }
}
