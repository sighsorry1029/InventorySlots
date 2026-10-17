using System;

namespace InventorySlots;

// Player-owned bonus, independent of the physical inventory's special-slot storage tail.
internal sealed class PathOfValheimanRows
{
    internal const string SaveKey = "pt.invrows";
    internal bool Loaded { get; private set; }
    private int retained;

    internal static int ClampBonus(int rows) => Math.Max(0, Math.Min(3, rows));

    internal void Load(string? stored)
    {
        retained = int.TryParse(stored, out int rows) ? ClampBonus(rows) : 0;
        Loaded = true;
    }

    internal int TotalRows(int baseRows, int limit) => Math.Min(limit, baseRows + retained);

    internal void Update(int desired, int baseRows, int highestOccupiedBonusRow)
    {
        // Caller considers only currently usable regular rows, never equipment or quick slots.
        retained = Math.Max(ClampBonus(desired), ClampBonus(highestOccupiedBonusRow - baseRows + 1));
        Loaded = true;
    }
}
