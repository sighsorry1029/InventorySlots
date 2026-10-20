using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static object? deathProgress;
    private static readonly List<ItemData> deathGrave = new();
    private static bool deathBodyThrows;
    private static void DeathProgress(ref object? __result) => __result = deathProgress;
    private static bool DeathPrefab(ItemData item, ref string __result)
    { __result = item.m_shared.m_name; return false; }

    // The Unity tombstone spawn is unavailable here. The real PoV/IS Harmony
    // handlers still collect/restore items around this transfer/error fixture.
    private static void DeathBody(Player player)
    {
        if (deathBodyThrows) throw new InvalidOperationException("fixture death body failure");
        var items = player.GetInventory().GetAllItems();
        deathGrave.AddRange(items);
        items.Clear();
    }

    private static IEnumerable<CodeInstruction> DeathBodyIl()
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Program), nameof(DeathBody)));
        yield return new CodeInstruction(OpCodes.Ret);
    }

    private static void CheckDeathKeep(Assembly pov, Type plugin, Player player, Inventory bag,
        ConfigEntry<bool> enabled, ConfigFile config)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        FieldInfo bridge = plugin.GetField("_pov", Statics)!;
        FieldInfo gate = plugin.GetField("_povHasDeathKeepStat", Statics)!;
        FieldInfo warning = plugin.GetField("_povDeathKeepWarning", Statics)!;
        FieldInfo optionField = plugin.GetField("_deathKeepRulesEnabled", Statics)!;
        object? oldBridge = bridge.GetValue(null), oldGate = gate.GetValue(null),
            oldWarning = warning.GetValue(null), oldOption = optionField.GetValue(null);
        var option = BindProgressionToggle(plugin, config, "_deathKeepRulesEnabled");
        object on = option.BoxedValue, off = Enum.Parse(on.GetType(), "Off");
        object yaml = plugin.GetField("_yamlConfig", Statics)!.GetValue(null)!;
        PropertyInfo keep = yaml.GetType().GetProperty("KeepOnDeath")!;
        object? oldKeep = keep.GetValue(yaml);
        var items = bag.GetAllItems();
        ItemData[] oldItems = items.ToArray();
        bool oldEnabled = enabled.Value;
        FieldInfo local = AccessTools.Field(typeof(Player), "m_localPlayer");
        Type progress = pov.GetType("PassiveTree.Progress", true)!;
        Type stats = pov.GetType("PassiveTree.StatSet", true)!;
        Type stat = pov.GetType("PassiveTree.Stat", true)!;
        object boundGear = Enum.Parse(stat, "BoundGear");
        void Node(bool unlocked)
        {
            deathProgress = FormatterServices.GetUninitializedObject(progress);
            object values = Activator.CreateInstance(stats)!;
            if (unlocked) stats.GetMethod("Add", new[] { stat, typeof(float) })!.Invoke(values, new[] { boundGear, (object)1f });
            progress.GetField("Stats", Instances)!.SetValue(deathProgress, values);
        }
        bool Allowed(Player p) => (bool)Call("CanUsePathOfValheimanDeathKeep", p)!;
        IList Prepare()
        {
            object?[] args = { player, null };
            return (IList)Call("PrepareKeepOnDeathItems", args)!;
        }
        var spare = new ItemData { m_gridPos = new Vector2i(0, 1), m_stack = 1,
            m_shared = new ItemData.SharedData { m_name = "death-spare", m_itemType = (ItemData.ItemType)3 },
            m_customData = new Dictionary<string, string> { { "test.identity", "preserved" } } };
        var weapon = new ItemData { m_gridPos = new Vector2i(0, 0), m_stack = 1,
            m_shared = new ItemData.SharedData { m_name = "death-hotbar", m_itemType = (ItemData.ItemType)3 } };
        var food = new ItemData { m_gridPos = new Vector2i(1, 1), m_stack = 6,
            m_shared = new ItemData.SharedData { m_name = "death-food" } };
        void ResetItems()
        {
            items.Clear(); items.AddRange(new[] { spare, weapon, food });
            spare.m_gridPos = new Vector2i(0, 1);
            weapon.m_gridPos = new Vector2i(0, 0);
            deathGrave.Clear();
        }
        var hooks = new Harmony("smoke.pov.death-keep");
        var native = new Harmony("smoke.pov.death-keep.native");
        MethodInfo tomb = AccessTools.DeclaredMethod(typeof(Player), "CreateTombStone");
        try
        {
            hooks.Patch(AccessTools.PropertyGetter(progress, "Current"),
                postfix: new HarmonyMethod(typeof(Program), nameof(DeathProgress)));
            hooks.Patch(AccessTools.Method(plugin, "GetItemPrefabName"),
                prefix: new HarmonyMethod(typeof(Program), nameof(DeathPrefab)));
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(plugin).Where(m =>
                m.Name is "EnsureInventoryState" or "ReloadEpicLootRuntimeItemData" or
                    "RefreshExternalEquipmentEffects" or "UnequipCustomEquipmentForDeathDrop"))
                hooks.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
            keep.SetValue(yaml, new List<string> { "death-spare" });
            ResetItems();
            bridge.SetValue(null, null);
            IList held = Prepare();
            Check(held.Count == 1 && !items.Contains(spare) && items.Contains(weapon), "Death Keep: absent PoV retains YAML selection of spare backpack gear");
            Call("RestoreKeepOnDeathItems", player, held);
            bridge.SetValue(null, oldBridge);
            Call("BindPathOfValheimanDeathKeep", pov);
            var bound = (Delegate)gate.GetValue(null)!;
            Check(bound.Method == pov.GetType("Valheiman.Api.Stats", true)!.GetMethod("Has") &&
                bound.Method.IsPublic && bound.Method.DeclaringType!.IsVisible, "Death Keep: binds original public Stats.Has");
            deathProgress = null;
            Check(!Allowed(player) && Prepare().Count == 0, "Death Keep: no loaded PoV progression does not unlock YAML rules");
            Node(false);
            Check(!Allowed(player) && Prepare().Count == 0 && items.Count == 3, "Death Keep: unallocated BoundGear leaves all items available for native death handling");
            Node(true);
            held = Prepare();
            Check(held.Count == 1 && !items.Contains(spare) && items.Contains(weapon) && items.Contains(food), "Death Keep: unlocked effect enables only YAML-selected items");
            Node(false); // Do not recheck the gate while returning already escrowed items.
            Call("RestoreKeepOnDeathItems", player, held);
            Call("RestoreKeepOnDeathItems", player, held);
            Check(items.Count == 3 && held.Count == 0 && spare.m_gridPos == new Vector2i(0, 1) &&
                spare.m_customData["test.identity"] == "preserved", "Death Keep: refund during restoration cannot lose, clone or alter the preserved item");
            Check(Prepare().Count == 0, "Death Keep: next death observes refunded effect without reload");
            Node(true);
            enabled.Value = false;
            Check(Allowed(player), "Death Keep: Equipment system Off does not disable BoundGear");
            enabled.Value = true;
            option.BoxedValue = off;
            Check(Prepare().Count == 0, "Death Keep: server-synced YAML toggle Off overrides unlocked effect");
            option.BoxedValue = on;
            var remote = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
            Check(!Allowed(remote), "Death Keep: local PoV progression is never applied to another player");
            local.SetValue(null, remote);
            try { Check(Prepare().Count == 0, "Death Keep: character change cannot reuse the previous local player's grant"); }
            finally { local.SetValue(null, player); }
            bool missing = false;
            try { Call("BindPathOfValheimanDeathKeep", typeof(Program).Assembly); }
            catch (TargetInvocationException error) { missing = error.InnerException is MissingMethodException; }
            Check(missing && !Allowed(player), "Death Keep: missing API fails closed for YAML rules");
            int attempts = 0;
            gate.SetValue(null, (Func<string, bool>)(_ => { attempts++; throw new InvalidOperationException("fixture API failure"); }));
            Check(!Allowed(player) && !Allowed(player) && attempts == 1, "Death Keep: throwing API locks YAML rules without repeated exceptions");
            Call("BindPathOfValheimanDeathKeep", pov);

            Type nativeTomb = pov.GetType("PassiveTree.CraftPathfinder+Player_Tomb", true)!;
            Type slotsTomb = plugin.Assembly.GetType("InventorySlots.PlayerCreateTombStoneKeepOnDeathPatch", true)!;
            hooks.Patch(tomb, transpiler: new HarmonyMethod(typeof(Program), nameof(DeathBodyIl)));
            native.Patch(tomb, prefix: new HarmonyMethod(nativeTomb.GetMethod("Prefix", Statics)!),
                finalizer: new HarmonyMethod(nativeTomb.GetMethod("Finalizer", Statics)!));
            hooks.Patch(tomb, prefix: new HarmonyMethod(slotsTomb.GetMethod("Prefix", Statics)!),
                postfix: new HarmonyMethod(slotsTomb.GetMethod("Postfix", Statics)!),
                finalizer: new HarmonyMethod(slotsTomb.GetMethod("Finalizer", Statics)!));
            foreach (bool unlocked in new[] { false, true })
            {
                Node(unlocked); ResetItems();
                tomb.Invoke(player, null);
                Check(unlocked ? items.Contains(spare) && items.Contains(weapon) && deathGrave.SequenceEqual(new[] { food }) :
                    items.Count == 0 && deathGrave.Count == 3, "Death Keep: combined original PoV/IS death hooks, BoundGear=" + unlocked);
                Check(items.Concat(deathGrave).Distinct().Count() == 3 && items.Count + deathGrave.Count == 3,
                    "Death Keep: combined death keeps exactly one reference to every item");
            }
            Node(true); ResetItems(); option.BoxedValue = off;
            tomb.Invoke(player, null);
            Check(items.SequenceEqual(new[] { weapon }) && deathGrave.Contains(spare), "Death Keep: YAML Off still allows PoV's own hotbar protection");
            option.BoxedValue = on;
            ResetItems(); deathBodyThrows = true;
            bool failed = false;
            try { tomb.Invoke(player, null); }
            catch (TargetInvocationException error) { failed = error.InnerException is InvalidOperationException; }
            Check(failed && items.Count == 3 && items.Distinct().Count() == 3 && deathGrave.Count == 0,
                "Death Keep: both original finalizers restore escrowed references after death-body exception");
            Check(items.Select(i => i.m_gridPos).Distinct().Count() == items.Count,
                "Death Keep: restored native hotbar and YAML spare items occupy distinct cells");
        }
        finally
        {
            native.UnpatchSelf(); hooks.UnpatchSelf();
            deathBodyThrows = false; deathProgress = null; deathGrave.Clear();
            items.Clear(); items.AddRange(oldItems);
            bridge.SetValue(null, oldBridge); gate.SetValue(null, oldGate); warning.SetValue(null, oldWarning);
            keep.SetValue(yaml, oldKeep); optionField.SetValue(null, oldOption); enabled.Value = oldEnabled;
            local.SetValue(null, player);
        }
    }
}
