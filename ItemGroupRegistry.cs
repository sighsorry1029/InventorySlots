using System;
using System.Collections.Generic;
using System.Linq;

namespace InventorySlots;

internal sealed class BuiltInItemGroupSection
{
    public BuiltInItemGroupSection(string yamlName, string id, string label, string iconPrefab, params string[] subgroups)
    {
        YamlName = yamlName;
        Id = id;
        Label = label;
        IconPrefab = iconPrefab;
        Subgroups = subgroups;
    }

    public string YamlName { get; }
    public string Id { get; }
    public string Label { get; }
    public string IconPrefab { get; }
    public IReadOnlyList<string> Subgroups { get; }
}

internal static class ItemGroupRegistry
{
    public static readonly IReadOnlyList<BuiltInItemGroupSection> Sections = new List<BuiltInItemGroupSection>
    {
        new("Melee", "melee", "Melee", "AxeStone", "sword", "axe", "club", "knife", "spear", "polearm", "fists", "shield", "pickaxe", "tool"),
        new("Ranged", "ranged", "Range", "Bow", "bow", "arrow", "crossbow", "bolt", "ammo", "bomb"),
        new("Magic", "magic", "Magic", "Eitr", "elementalmagic", "bloodmagic"),
        new("Equipment", "armor", "Equipment", "HelmetLeather", "helmet", "chest", "legs", "cape", "utility", "trinket"),
        new("Food", "food", "Food", "CookedMeat", "healthfood", "staminafood", "eitrfood", "feast"),
        new("Consumable", "consumable", "Consumable", "MeadHealthMinor", "mead", "potion"),
        new("Meadbase", "meadbase", "Base", "MeadBaseHealthMinor", "meadbase"),
        new("Misc", "misc", "Misc", "Dandelion", "trophy", "valuable")
    };

    private static readonly HashSet<string> BuiltInGroupIds = new(
        new[] { "all", "favorite", "equipment" }
            .Concat(Sections.Select(section => section.Id))
            .Concat(Sections.SelectMany(section => section.Subgroups))
            .Select(InventorySlotsConfigCore.NormalizeGroupId),
        StringComparer.OrdinalIgnoreCase);

    public static bool TryNormalizeSectionId(string? rawId, out string sectionId)
    {
        sectionId = "";
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return false;
        }

        string trimmed = rawId!.Trim();
        BuiltInItemGroupSection? section = Sections.FirstOrDefault(candidate =>
            string.Equals(candidate.YamlName, trimmed, StringComparison.Ordinal));
        if (section == null)
        {
            return false;
        }

        sectionId = section.Id;
        return true;
    }

    public static bool IsBuiltInGroupId(string groupId) =>
        BuiltInGroupIds.Contains(InventorySlotsConfigCore.NormalizeGroupId(groupId));

    public static IEnumerable<string> GlobalSubgroupOrder() =>
        new[] { "favorite" }.Concat(Sections.SelectMany(section => section.Subgroups)).Distinct(StringComparer.OrdinalIgnoreCase);
}
