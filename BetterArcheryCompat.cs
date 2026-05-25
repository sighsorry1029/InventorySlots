using System;
using System.Reflection;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool IsBetterArcheryQuiverCell(Vector2i pos, bool includeRestockableSlots)
    {
        if (!TryGetBetterArcheryQuiverApi(out BetterArcheryQuiverApi? api) || api == null)
        {
            return false;
        }

        try
        {
            return api.IsQuiverCell(pos, includeRestockableSlots);
        }
        catch (Exception ex)
        {
            return MarkCompatReflectionFailed(
                CompatRuntime.BetterArcheryQuiver,
                "BetterArchery quiver",
                ex.Message,
                "BetterArchery quiver compatibility disabled");
        }
    }

    private static bool TryGetBetterArcheryQuiverApi(out BetterArcheryQuiverApi? api)
    {
        const string capability = "BetterArchery quiver";
        return TryGetCompatApi(
            BetterArcheryGuid,
            capability,
            CompatRuntime.BetterArcheryQuiver,
            BetterArcheryQuiverApi.TryCreate,
            "BetterArchery quiver compatibility disabled",
            out api);
    }
}
