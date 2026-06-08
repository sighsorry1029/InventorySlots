using System;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool SetCompatCapabilityState(ref CompatCapabilityState field, CompatCapabilityState state, string capability, string detail = "")
    {
        if (field != state)
        {
            field = state;
            string suffix = string.IsNullOrWhiteSpace(detail) ? "" : $": {detail}";
            Log.LogDebug($"Compatibility capability {capability}: {state}{suffix}");
        }

        return state == CompatCapabilityState.Available;
    }

    private static bool IsSyncedStateReady()
    {
        if (ZNet.instance == null || ZNet.IsSinglePlayer || ConfigSync.IsSourceOfTruth)
        {
            return true;
        }

        return ConfigSync.InitialSyncDone;
    }
}
