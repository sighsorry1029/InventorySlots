using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace InventorySlots;

internal sealed class YamlRoot
{
    [YamlMember(Alias = "Groups", ApplyNamingConventions = false)]
    public Dictionary<string, List<string>> Groups { get; set; } = new();

    [YamlMember(Alias = "InventoryLimits", ApplyNamingConventions = false)]
    public Dictionary<string, int> InventoryLimits { get; set; } = new();

    [YamlMember(Alias = "KeepOnDeath", ApplyNamingConventions = false)]
    public List<string> KeepOnDeath { get; set; } = new();

    [YamlMember(Alias = "QuickSlots", ApplyNamingConventions = false)]
    public List<string> QuickSlots { get; set; } = new();

    [YamlMember(Alias = "Slots", ApplyNamingConventions = false)]
    public List<YamlSlot> Slots { get; set; } = new();
}

internal sealed class YamlSlot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Items { get; set; } = new();
}
