using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    public static string BuildDefaultYamlPrefix()
    {
        StringBuilder builder = new();
        builder.AppendLine("# Item groups control subgroup order for crafting, inventory, and container");
        builder.AppendLine("# sorting. Melee/Ranged/Magic/Equipment/Food/Consumable/Meadbase/Misc are");
        builder.AppendLine("# fixed top-level sections. Other keys define custom prefab groups, which can be");
        builder.AppendLine("# inserted into those sections or referenced by KeepOnDeath and QuickSlots.");
        builder.AppendLine("Groups:");
        foreach (BuiltInItemGroupSection section in Sections)
        {
            builder.AppendLine($"  {section.YamlName}:");
            if (section.Subgroups.Count == 1 && string.Equals(section.Subgroups[0], section.Id, StringComparison.OrdinalIgnoreCase))
            {
                builder.Length -= $"  {section.YamlName}:{Environment.NewLine}".Length;
                builder.AppendLine($"  {section.YamlName}: []");
                continue;
            }

            foreach (string subgroup in section.Subgroups)
            {
                builder.AppendLine($"    - {subgroup}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("# Items matching these built-in groups, custom groups, or exact prefab/internal");
        builder.AppendLine("# names stay in the player inventory instead of moving to the tombstone.");
        builder.AppendLine("KeepOnDeath:");
        foreach (string token in DefaultKeepOnDeath)
        {
            builder.AppendLine($"  - {token}");
        }

        builder.AppendLine();
        builder.AppendLine("# Items matching these top-level groups, subgroups, custom groups, or exact");
        builder.AppendLine("# prefab/internal names can be placed in quick slots.");
        builder.AppendLine("QuickSlots:");
        foreach (string token in DefaultQuickSlots)
        {
            builder.AppendLine($"  - {token}");
        }

        builder.AppendLine("#");
        return builder.ToString();
    }

    private static string NormalizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        return new string(id!.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
