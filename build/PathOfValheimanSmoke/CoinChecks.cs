using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static void CheckCoinBenefits(Assembly pov, Type plugin, Player player, Inventory bag,
        ConfigEntry<bool> enabled, ConfigFile config)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        var items = (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        ItemData[] oldItems = items.ToArray();
        Type wallet = pov.GetType("PassiveTree.Gear.Wallet", true)!;
        FieldInfo gate = wallet.GetField("Gate", Statics)!, weightless = wallet.GetField("Weightless", Statics)!;
        object? oldGate = gate.GetValue(null), oldWeightless = weightless.GetValue(null);
        FieldInfo yamlField = plugin.GetField("_yamlConfig", Statics)!;
        object yaml = yamlField.GetValue(null)!;
        PropertyInfo keep = yaml.GetType().GetProperty("KeepOnDeath")!;
        object? oldKeep = keep.GetValue(yaml);
        FieldInfo keepOption = plugin.GetField("_deathKeepRulesEnabled", Statics)!;
        object? oldOption = keepOption.GetValue(null);
        FieldInfo deathGate = plugin.GetField("_povHasDeathKeepStat", Statics)!;
        object? oldDeathGate = deathGate.GetValue(null);
        bool oldEnabled = enabled.Value;
        var coins = new ItemData { m_gridPos = new Vector2i(2, 0), m_stack = 7,
            m_shared = new ItemData.SharedData { m_name = "Coins", m_weight = 2f, m_maxStackSize = 999 } };
        var ordinary = new ItemData { m_gridPos = new Vector2i(3, 0), m_stack = 2,
            m_shared = new ItemData.SharedData { m_name = "Wood", m_weight = 3f } };
        MethodInfo recalculate = AccessTools.Method(typeof(Inventory), "UpdateTotalWeight");
        float Weight(Inventory inv) { recalculate.Invoke(inv, null); return inv.GetTotalWeight(); }
        IList Prepare()
        {
            object?[] args = { player, null };
            return (IList)Call("PrepareKeepOnDeathItems", args)!;
        }
        // Rendering/effect reconciliation is outside this standalone fixture;
        // the actual item selection, snapshots, restore and finalizer code runs.
        var effects = new Harmony("smoke.pov.coin-effects");
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(plugin).Where(m =>
            m.Name is "EnsureInventoryState" or "ReloadEpicLootRuntimeItemData" or "RefreshExternalEquipmentEffects"))
            effects.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
        // Exercise PoV's real native weight callback alongside the new hook.
        effects.Patch(recalculate, postfix: new HarmonyMethod(wallet.GetNestedType("Weight", BindingFlags.NonPublic)!.GetMethod("Postfix", Statics)!));
        try
        {
            items.Clear(); items.Add(coins); items.Add(ordinary); coinFixtureItem = coins;
            keep.SetValue(yaml, new List<string>());
            bool node = false, free = true;
            gate.SetValue(null, new Func<Player, bool>(_ => node));
            weightless.SetValue(null, new Func<Player, bool>(_ => free));
            enabled.Value = true;
            Check(Math.Abs(Weight(bag) - 20f) < .001 && Prepare().Count == 0, "locked Magic Purse leaves ordinary Coins weight/death behavior unchanged");
            node = true;
            Check(Math.Abs(Weight(bag) - 6f) < .001 && Math.Abs(Weight(bag) - 6f) < .001,
                "normal inventory Coins are exempt once per recomputation, including with native PoV weight patch");
            Check(coins.GetWeight() == 14f, "coin item weight remains unchanged for ground/container tooltips");
            var chest = new Inventory("Chest", null, 8, 4);
            ((List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(chest)).Add(coins);
            Check(Math.Abs(Weight(chest) - 14f) < .001, "container inventory receives no coin exemption");
            free = false;
            Check(Math.Abs(Weight(bag) - 20f) < .001 && (bool)Call("KeepPathOfValheimanCoinsOnDeath", player)!,
                "weight and death use separate public policies");
            free = true; Call("RefreshPathOfValheimanCoinWeight", player);
            Check(Math.Abs(bag.GetTotalWeight() - 6f) < .001, "live node gain updates weight without moving items");
            Call("BeginPathOfValheimanLoad", player);
            Call("EndPathOfValheimanLoad", player, null);
            AccessTools.Field(typeof(Inventory), "m_totalWeight").SetValue(bag, 20f);
            Call("RefreshPathOfValheimanCoinWeight", player);
            Check(Math.Abs(bag.GetTotalWeight() - 6f) < .001, "same-inventory reload recalculates even when the old node cache was already true");
            var nextBag = new Inventory("Inventory", null, 8, 4);
            ((List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(nextBag)).Add(coins);
            AccessTools.Field(typeof(Inventory), "m_totalWeight").SetValue(nextBag, 14f);
            AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, nextBag);
            try
            {
                Call("RefreshPathOfValheimanCoinWeight", player);
                Check(Math.Abs(nextBag.GetTotalWeight()) < .001, "replacement inventory recalculates with unchanged node state");
            }
            finally { AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, bag); }
            node = false; Call("RefreshPathOfValheimanCoinWeight", player);
            Check(Math.Abs(bag.GetTotalWeight() - 20f) < .001, "live node removal restores carried weight without moving items");
            node = true; enabled.Value = false;
            Check(Math.Abs(Weight(bag) - 20f) < .001 && Prepare().Count == 0, "Equipment Off disables both coin benefits");
            enabled.Value = true;
            // YAML can independently be Off; PoV's public node policy still applies.
            Type toggle = plugin.GetNestedType("Toggle", BindingFlags.Public)!;
            MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod &&
                m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[3].ParameterType == typeof(string));
            object option = bind.MakeGenericMethod(toggle).Invoke(config, new object[] { "test", "death-rules", Enum.Parse(toggle, "Off"), "" })!;
            keepOption.SetValue(null, option);
            IList held = Prepare();
            Check(held.Count == 1 && !items.Contains(coins) && items.Contains(ordinary), "PoV keeps only Coins even with YAML death rules Off");
            Call("RestoreKeepOnDeathItems", player, held);
            Call("RestoreKeepOnDeathItems", player, held);
            Check(held.Count == 0 && items.Count(i => ReferenceEquals(i, coins)) == 1 && coins.m_gridPos == new Vector2i(2, 0) && coins.m_stack == 7,
                "death restoration is idempotent and preserves original coin stack/reference/cell");
            keep.SetValue(yaml, new List<string> { "Coins" });
            option.GetType().GetProperty("Value")!.SetValue(option, Enum.Parse(toggle, "On"));
            // Match the prefab token without requiring a native GameObject.
            effects.Patch(AccessTools.Method(plugin, "GetItemPrefabName"), prefix: new HarmonyMethod(typeof(Program), nameof(QuiverPrefab)));
            held = Prepare();
            Check(held.Count == 1, "Magic Purse protects Coins even while BoundGear/YAML is locked");
            Call("RestoreKeepOnDeathItems", player, held);
            deathGate.SetValue(null, (Func<string, bool>)(stat => stat == "BoundGear"));
            held = Prepare();
            Check(held.Count == 1, "unlocked overlapping YAML and PoV rules collect one snapshot only");
            Call("RestoreKeepOnDeathItems", player, held);
            // A skipped/no-op death body retains the other 31 items: no duplication
            // or free-capacity requirement is allowed to discard the escrowed coin.
            items.Clear(); items.Add(coins);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++)
                if (x != 2 || y != 0) items.Add(new ItemData { m_gridPos = new Vector2i(x, y), m_shared = ordinary.m_shared });
            held = Prepare();
            Call("RestoreKeepOnDeathItems", player, held);
            Check(held.Count == 0 && items.Count == 32 && items.Count(i => ReferenceEquals(i, coins)) == 1 && coins.m_gridPos == new Vector2i(2, 0),
                "full-backpack/no-op death restores exact coin reference without extra storage");
            FieldInfo local = AccessTools.Field(typeof(Player), "m_localPlayer");
            local.SetValue(null, FormatterServices.GetUninitializedObject(typeof(Player)));
            keep.SetValue(yaml, new List<string>());
            try { Check(!(bool)Call("KeepPathOfValheimanCoinsOnDeath", player)! && Prepare().Count == 0 && Weight(bag) > 14f, "remote player inventory receives no PoV coin benefits"); }
            finally { local.SetValue(null, player); }
        }
        finally
        {
            items.Clear(); items.AddRange(oldItems); coinFixtureItem = null;
            gate.SetValue(null, oldGate); weightless.SetValue(null, oldWeightless);
            keep.SetValue(yaml, oldKeep); keepOption.SetValue(null, oldOption); enabled.Value = oldEnabled;
            deathGate.SetValue(null, oldDeathGate);
            effects.UnpatchSelf();
        }
    }
}
