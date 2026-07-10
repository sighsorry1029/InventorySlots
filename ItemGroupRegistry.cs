using System;
using System.Collections.Generic;
using System.Linq;

namespace InventorySlots;

internal sealed class BuiltInItemGroupSection
{
    public BuiltInItemGroupSection(string yamlName, string id, string tab, string label, string iconPrefab, params string[] subgroups)
    {
        YamlName = yamlName;
        Id = id;
        Tab = tab;
        Label = label;
        IconPrefab = iconPrefab;
        Subgroups = subgroups;
    }

    public string YamlName { get; }
    public string Id { get; }
    public string Tab { get; }
    public string Label { get; }
    public string IconPrefab { get; }
    public IReadOnlyList<string> Subgroups { get; }
}

internal static class ItemGroupRegistry
{
    public static readonly IReadOnlyList<BuiltInItemGroupSection> Sections = new List<BuiltInItemGroupSection>
    {
        new("Melee", "melee", "Combat", "Melee", "AxeStone", "sword", "axe", "club", "knife", "spear", "polearm", "fists", "shield", "pickaxe", "tool"),
        new("Ranged", "ranged", "Combat", "Range", "Bow", "bow", "arrow", "crossbow", "bolt", "ammo", "bomb"),
        new("Magic", "magic", "Combat", "Magic", "Eitr", "elementalmagic", "bloodmagic"),
        new("Equipment", "armor", "Equipment", "Equipment", "HelmetLeather", "helmet", "chest", "legs", "cape", "utility", "trinket"),
        new("Food", "food", "Equipment", "Food", "CookedMeat", "healthfood", "staminafood", "eitrfood", "feast"),
        new("Consumable", "consumable", "Equipment", "Consumable", "MeadHealthMinor", "mead", "potion"),
        new("Meadbase", "meadbase", "Crafting", "Base", "MeadBaseHealthMinor", "meadbase"),
        new("Misc", "misc", "Crafting", "Misc", "Dandelion", "trophy", "valuable")
    };

    public static readonly IReadOnlyList<string> DefaultKeepOnDeath = new[] { "Melee", "Ranged", "Magic", "Equipment" };
    public static readonly IReadOnlyList<string> DefaultQuickSlots = new[] { "Melee", "Ranged", "Magic", "healthfood", "staminafood", "eitrfood", "mead", "potion" };

    private static readonly HashSet<string> BuiltInGroupIds = new(
        new[] { "all", "favorite", "equipment" }
            .Concat(Sections.Select(section => section.Id))
            .Concat(Sections.SelectMany(section => section.Subgroups))
            .Select(NormalizeId),
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
        BuiltInGroupIds.Contains(NormalizeId(groupId));

    public static IEnumerable<string> GlobalSubgroupOrder() =>
        new[] { "favorite" }.Concat(Sections.SelectMany(section => section.Subgroups)).Distinct(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        return new string(id!.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
