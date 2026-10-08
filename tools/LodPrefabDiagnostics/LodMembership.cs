using System.Collections.Generic;

namespace LodPrefabDiagnostics;

// One renderer in several levels of the SAME group is normal. Only separate
// group identities count as multiple owners; names are not unique identities.
internal sealed class LodMembership
{
    internal readonly Dictionary<int, Dictionary<int, SortedSet<int>>> Renderers = new();

    internal void Add(int rendererId, int groupId, int level)
    {
        if (!Renderers.TryGetValue(rendererId, out var groups))
            Renderers.Add(rendererId, groups = new Dictionary<int, SortedSet<int>>());
        if (!groups.TryGetValue(groupId, out var levels))
            groups.Add(groupId, levels = new SortedSet<int>());
        levels.Add(level);
    }
}
