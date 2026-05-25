using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace InventorySlots;

internal sealed class YamlRoot
{
    [YamlMember(Alias = "Groups", ApplyNamingConventions = false)]
    public Dictionary<string, List<string>> Groups { get; set; } = new();

    [YamlMember(Alias = "KeepOnDeath", ApplyNamingConventions = false)]
    public List<string> KeepOnDeath { get; set; } = new();

    [YamlMember(Alias = "QuickSlots", ApplyNamingConventions = false)]
    public List<string> QuickSlots { get; set; } = new();

    public List<YamlResourceTier> ResourceMap { get; set; } = new();

    [YamlMember(Alias = "Slots", ApplyNamingConventions = false)]
    public List<YamlSlot> Slots { get; set; } = new();
}

internal sealed class YamlResourceTier
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Biome { get; set; } = "";
    public string Biomes { get; set; } = "";
    public List<string> Materials { get; set; } = new();
}

internal sealed class YamlSlot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Items { get; set; } = new();
}

internal sealed class YamlPredefinedGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public YamlGroupMatch Match { get; set; } = new();
}

internal sealed class YamlGroupMatch
{
    public List<string> Groups { get; set; } = new();
    public List<string> ItemTypes { get; set; } = new();
    public List<string> SkillTypes { get; set; } = new();
    public List<string> Prefabs { get; set; } = new();
    public List<string> PrefabAny { get; set; } = new();
    public List<string> NameAny { get; set; } = new();
    public List<string> AmmoTypes { get; set; } = new();
    public int? MaxStackGreaterThan { get; set; }
    public int? ValueGreaterThan { get; set; }
    public bool? HasFood { get; set; }
    public bool? HasStatusEffect { get; set; }
}
