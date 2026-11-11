using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace InventorySlots;

// A UI-only optimization must not change the lifetime expected by an unreviewed
// row decorator. Check both row and list hooks, including late registrations.
internal static class CraftingRecipeRowReuseHooks
{
    private static readonly MethodInfo RowMethod = AccessTools.Method(typeof(InventoryGui), "AddRecipeToList",
        new[] { typeof(Player), typeof(Recipe), typeof(ItemDrop.ItemData), typeof(bool) });
    private static readonly MethodInfo ListMethod = AccessTools.Method(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) });
    // Loaded assemblies are immutable for this process. Cache original contracts
    // and resolved helpers; re-inspect transformed IL only when patch metadata
    // changes, at the next rebuild boundary (never once per row or frame).
    private static readonly Dictionary<MethodInfo, Contract> Contracts = new();
    private static readonly MethodInfo TypeLookup = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName), new[] { typeof(string) });
    private static readonly MethodInfo FieldLookup = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Field), new[] { typeof(Type), typeof(string) });
    private static readonly MethodInfo CategorySuppression = AccessTools.Method(typeof(InventorySlotsPlugin), "SuppressPathOfValheimanCraftCategories");
    private const string LookupType = "InteropFixes.Modules.PathOfValheimanLookup.OptionalTypeLookup";
    private const string UpgradeCleanup = "PassiveTree.Loot.ItemAscension+UpgradeListClean::Postfix";
    private static int _generation;

    private sealed class Contract
    {
        internal bool OriginalValid, Inspected, BodyValid, Valid;
        internal int Generation;
        internal Patches? Patches;
        internal readonly List<MethodInfo> Lookups = new();
        internal MethodInfo[]? Helpers;
    }

    // Values identify related method bodies, never a whole DLL/MVID/version.
    // PoV 6.0.8, VeiledRecipes 1.1.7, Server devcommands 1.116, WarfareTweaks 1.0.5.
    // A changed contract disables reuse only; every original hook still runs.
    private static readonly Dictionary<string, string> Shapes = new()
    {
        ["PassiveTree.Loot.ItemAscension+UpgradeRow::Prefix"] = "2C4A279B1CC809A5953B0DFCF17DB37B06FF735077EF4E26FBB2A9273526EEDC",
        ["PassiveTree.Loot.ItemAscension+UpgradeList::Prefix"] = "8EC5BE07679D642057DA1A22E9728BF17147D96F84FC0B6D4AAA827F8B4AFE81",
        ["PassiveTree.Loot.ItemAscension+UpgradeListClean::Postfix"] = "FDB6524736213A1AFFC8CBE1FA4C799A41FB9C2E5AB55F1D9DBBD205802733D3",
        ["PassiveTree.Loot.ItemAscension::Pairs"] = "8E3F1FE513B0E59D4587D84339CEC40EF4C384358EE3D2A6EC059E3ACF94EE03",
        ["PassiveTree.Loot.ItemAscension::UpgradeRecipe"] = "F57334CC4D4A80B59AB80C7C6F44C8151EDB659F40207B0F239EEF831AA50886",
        ["PassiveTree.Loot.ItemAscension::IsUpgradeClone"] = "1C69B4CC3D3D7FE3E09C6C4C7F299D5AB93B4E59EE3D640587F081BF1872B614",
        ["PassiveTree.CraftSalvage+Gui_List::Prefix"] = "6181C0C9A2E01EC4C99B682DBBFB881551602862DCEB4A0FAE08684E651BB12A",
        ["PassiveTree.CraftSmith+Gui_List::Prefix"] = "EA0B3615D62627CD87153C19C0B8CBFC5C8DEF06609C4F188D3CC50CAD2AECEF",
        ["PassiveTree.UI.CraftCategories+Gui_List::Postfix"] = "EC22930939F26177A3DD9E66A4FF8BEDA347FE6091D47D5413F17D1CCCC49DF4",
        ["PassiveTree.UI.CraftCategories::get_On"] = "E6866F671ACABB5B37A519204046F29EEC34172EE216F47A3D0AFB69B22792FE",
        ["PassiveTree.UI.CraftCategories::Active"] = "BAB17650344E2BF6284C65C09CD462DDA0D2B707D0B0044B68517E72406F987D",
        ["PassiveTree.UI.CraftCategories::Build"] = "3F4C08469D74015F55B7EC591B638D8072312ED2D41F2D7283F716F1EC377D63",
        ["PassiveTree.UI.CraftCategories::Clear"] = "6F6AC2A15C4DF6C2B79B13BDB3EBFF1893AAAC73A1EF437875568D702C44B5FC",
        ["VeiledRecipes.InventoryGuiAddRecipeToListPatch::Prefix"] = "EA4CAA097102A36FAE93B606287266A5791E127AC1D78D5A42887370D04EE036",
        ["VeiledRecipes.InventoryGuiAddRecipeToListPatch::Postfix"] = "2C7ABE30C1F3FEEF4952096585C79F70B3C417441C93DD19F9C96DBB56609566",
        ["VeiledRecipes.InventoryGuiAddRecipeToListPatch::FindRecipeElement"] = "CBED9A5268381D7EF3F35612667C5593B7DBABA1B0F6B0F50ECE0203E1AC81B6",
        ["VeiledRecipes.InventoryGuiAddRecipeToListPatch::MaskRecipeListElement"] = "ECCF4FF73802DF4D6D3AF5488BA9396AF4E68BE21A152166689D7FE4B9A41784",
        ["VeiledRecipes.InventoryGuiUpdateRecipeListPatch::Postfix"] = "CD004CB1122B11E34158662623F254566F86BC26DCBA425D65ABEFCF3184C3B7",
        ["VeiledRecipes.VeiledRecipeAaaCraftingCompat::MaskRecipeList"] = "F2DA6CC8679A29BDE2F641278CA654F62366E568D8AB2D26409A72104A7FE80F",
        ["ServerDevcommands.LimitRecipesToStationPatch::Prefix"] = "1A90DFFAAD237BA347C22DBC570574519BA5707DAA020780CEF1111613628DBF",
        ["WarfareTweaks.InventoryGuiUpdateRecipeListWarfareThrowableUpgradePatch::Prefix"] = "E20D4FDF0A86AA71530BE8D0EAD3AA47AA2DE2448F039ECCE378C09FDE216B2C",
        [LookupType + "::Resolve"] = "B6AFFC4A9121D702C9F7D5DAFDB878926089C9537D90AE6E1D3DC34ED2C14F44",
        [LookupType + "::FieldIfPresent"] = "1949444283131D1FBFF0521C54833EBA4FE6AB327117629C71A7A16DD1D8DCEC"
    };

    // Follow the registered hook's reviewed row/pairing dependencies, not every
    // optional integration in the same assembly. Foreign replacement tabs remain
    // outside the native reuse path. This is not a general IL side-effect analyzer.
    private static readonly Dictionary<string, string[]> Dependencies = new()
    {
        ["PassiveTree.Loot.ItemAscension+UpgradeRow::Prefix"] = new[] { "PassiveTree.Loot.ItemAscension::Pairs" },
        ["PassiveTree.Loot.ItemAscension+UpgradeList::Prefix"] = new[] { "PassiveTree.Loot.ItemAscension::UpgradeRecipe" },
        [UpgradeCleanup] = new[] { "PassiveTree.Loot.ItemAscension::Pairs" },
        ["PassiveTree.Loot.ItemAscension::Pairs"] = new[] { "PassiveTree.Loot.ItemAscension::UpgradeRecipe", "PassiveTree.Loot.ItemAscension::IsUpgradeClone" },
        ["PassiveTree.UI.CraftCategories+Gui_List::Postfix"] = new[] { "PassiveTree.UI.CraftCategories::Build", "PassiveTree.UI.CraftCategories::Clear" },
        ["PassiveTree.UI.CraftCategories::Build"] = new[] { "PassiveTree.UI.CraftCategories::Active", "PassiveTree.UI.CraftCategories::Clear" },
        ["PassiveTree.UI.CraftCategories::Active"] = new[] { "PassiveTree.UI.CraftCategories::get_On" },
        ["VeiledRecipes.InventoryGuiAddRecipeToListPatch::Postfix"] = new[] { "VeiledRecipes.InventoryGuiAddRecipeToListPatch::FindRecipeElement", "VeiledRecipes.InventoryGuiAddRecipeToListPatch::MaskRecipeListElement" },
        ["VeiledRecipes.InventoryGuiUpdateRecipeListPatch::Postfix"] = new[] { "VeiledRecipes.VeiledRecipeAaaCraftingCompat::MaskRecipeList" },
        ["VeiledRecipes.VeiledRecipeAaaCraftingCompat::MaskRecipeList"] = new[] { "VeiledRecipes.InventoryGuiAddRecipeToListPatch::MaskRecipeListElement" }
    };

    internal static bool Permit()
    {
        unchecked { _generation++; }
        try { return Check(RowMethod, row: true) && Check(ListMethod, row: false); }
        catch (Exception error)
        { CraftingRecipeRowReuse.Warn("Hook inspection failed: " + error.Message); return false; }
    }

    private static bool Check(MethodInfo method, bool row)
    {
        Patches? patches = Harmony.GetPatchInfo(method);
        if (patches == null) return true;
        if (patches.ILManipulators.Count != 0)
        { CraftingRecipeRowReuse.Warn("Unverified recipe UI IL manipulator: " + method.Name); return false; }
        return Check(patches.Prefixes, "Prefix", row) && Check(patches.Postfixes, "Postfix", row) &&
            Check(patches.Transpilers, "Transpiler", row) && Check(patches.Finalizers, "Finalizer", row);
    }

    private static bool Check(IEnumerable<Patch> patches, string kind, bool row)
    {
        foreach (Patch patch in patches)
        {
            if (patch.owner == "sighsorry.InventorySlots" || patch.owner == "sighsorry.InventoryUiDiagnostics") continue;
            MethodInfo method = patch.PatchMethod;
            string type = method.DeclaringType?.FullName ?? "";
            string key = type + "::" + method.Name;
            string? scope = patch.owner switch
            {
                "treextr.pathofvalheiman" => "PassiveTree.",
                "sighsorry.VeiledRecipes" => "VeiledRecipes.",
                "server_devcommands" => "ServerDevcommands.",
                "sighsorry.WarfareTweaks" => "WarfareTweaks.",
                _ => null
            };
            bool rowHook = type == "PassiveTree.Loot.ItemAscension+UpgradeRow" || type == "VeiledRecipes.InventoryGuiAddRecipeToListPatch";
            if (scope == null || !type.StartsWith(scope, StringComparison.Ordinal) || row != rowHook ||
                !method.IsStatic || kind != method.Name || !Shapes.ContainsKey(key) ||
                !CheckContract(method))
            { CraftingRecipeRowReuse.Warn("Unverified " + (row ? "row" : "list") + " hook: " + patch.owner + "/" + key); return false; }
        }
        return true;
    }

    private static bool CheckContract(MethodInfo method)
    {
        string key = method.DeclaringType?.FullName + "::" + method.Name;
        if (!Shapes.TryGetValue(key, out string expected)) return false;
        if (!Contracts.TryGetValue(method, out Contract contract))
        {
            contract = new Contract { OriginalValid = InventorySlotsPlugin.RecipeRowHookShape(method) == expected };
            contract.Helpers = ResolveHelpers(method, key);
            Contracts[method] = contract;
        }
        if (!contract.OriginalValid)
        { CraftingRecipeRowReuse.Warn("Recipe UI contract changed: " + key); return false; }
        if (contract.Generation == _generation) return contract.Valid;
        contract.Generation = _generation;
        contract.Valid = false;
        Patches? patches = Harmony.GetPatchInfo(method);
        if (!contract.Inspected || !SamePatches(contract.Patches, patches))
        {
            contract.Lookups.Clear();
            contract.BodyValid = CheckBody(method, key, expected, patches, contract.Lookups);
            contract.Patches = patches;
            contract.Inspected = true;
        }
        if (!contract.BodyValid)
        { CraftingRecipeRowReuse.Warn("Recipe UI lifetime contract changed by active patches: " + key); return false; }
        // Even if the caller's patches have not changed, its helpers may have.
        foreach (MethodInfo lookup in contract.Lookups)
            if (!CheckContract(lookup)) return false;
        if (contract.Helpers == null)
        { CraftingRecipeRowReuse.Warn("Recipe UI dependency is missing: " + key); return false; }
        foreach (MethodInfo helper in contract.Helpers)
            if (!CheckContract(helper)) return false;
        return contract.Valid = true;
    }

    private static MethodInfo[]? ResolveHelpers(MethodInfo method, string key)
    {
        if (!Dependencies.TryGetValue(key, out string[] dependencies)) return Array.Empty<MethodInfo>();
        var helpers = new MethodInfo[dependencies.Length];
        for (int i = 0; i < dependencies.Length; i++)
        {
            string dependency = dependencies[i];
            int split = dependency.IndexOf("::", StringComparison.Ordinal);
            Type? type = method.Module.Assembly.GetType(dependency.Substring(0, split), false);
            MethodInfo? helper = type?.GetMethod(dependency.Substring(split + 2), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (helper == null) return null;
            helpers[i] = helper;
        }
        return helpers;
    }

    private static bool CheckBody(MethodInfo method, string key, string expected, Patches? patches, List<MethodInfo> lookups)
    {
        if (patches == null) return true;
        // GetCurrentInstructions includes transpilers, NOT execution wrappers or
        // HarmonyX IL manipulators. A body match cannot certify those additions.
        if (patches.Prefixes.Count != 0 || patches.Finalizers.Count != 0 || patches.ILManipulators.Count != 0 ||
            patches.Postfixes.Any(p => key != "PassiveTree.UI.CraftCategories::get_On" || p.PatchMethod != CategorySuppression)) return false;
        if (patches.Transpilers.Count == 0) return true;
        var code = PatchProcessor.GetCurrentInstructions(method);
        if (key == UpgradeCleanup)
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.opcode != OpCodes.Call || instruction.operand is not MethodInfo call ||
                    call.DeclaringType?.FullName != LookupType) continue;
                MethodInfo? original = call.Name == "Resolve" ? TypeLookup : call.Name == "FieldIfPresent" ? FieldLookup : null;
                if (original == null || !call.IsStatic || call.ReturnType != original.ReturnType ||
                    !call.GetParameters().Select(p => p.ParameterType).SequenceEqual(original.GetParameters().Select(p => p.ParameterType))) return false;
                // Only these two verified lookup contracts are substitutable.
                // The rest of the body (cleanup, selection, branches and exception
                // regions) must still match, regardless of the transpiler's owner.
                lookups.Add(call);
                instruction.operand = original;
            }
        return InventorySlotsPlugin.RecipeRowHookShape(method, code) == expected;
    }

    private static bool SamePatches(Patches? a, Patches? b)
    {
        if (a == null || b == null) return a == b;
        return Same(a.Prefixes, b.Prefixes) && Same(a.Postfixes, b.Postfixes) && Same(a.Transpilers, b.Transpilers) &&
            Same(a.Finalizers, b.Finalizers) && Same(a.ILManipulators, b.ILManipulators);
    }

    private static bool Same(IList<Patch> a, IList<Patch> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].PatchMethod != b[i].PatchMethod || a[i].owner != b[i].owner || a[i].index != b[i].index ||
                a[i].priority != b[i].priority || !a[i].before.SequenceEqual(b[i].before) || !a[i].after.SequenceEqual(b[i].after)) return false;
        return true;
    }
}

public sealed partial class InventorySlotsPlugin
{
    internal static string RecipeRowHookShape(MethodBase method) =>
        PathOfValheimanOverlayShape(method, PatchProcessor.GetOriginalInstructions(method));
    internal static string RecipeRowHookShape(MethodBase method, IEnumerable<CodeInstruction> instructions) =>
        PathOfValheimanOverlayShape(method, instructions);
}
