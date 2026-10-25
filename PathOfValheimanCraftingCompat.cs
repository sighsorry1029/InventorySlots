using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private delegate void PoVDisenchantReturns(ItemData item, out int azurite, out int divine);
    private static Func<bool>? _povSalvaging;
    private static Func<Button>? _povSalvageTab;
    private static Func<Player, bool>? _povRunecarver;
    private static Func<ItemData, Recipe, List<(ItemDrop, int, int)>>? _povSalvageReturns;
    private static PoVDisenchantReturns? _povDisenchantReturns;
    private static Func<ItemData, string[]>? _povSocketContents;
    private static Func<string, float>? _povReturnChance;
    private static Func<string, string, string>? _povText;
    private static Func<Button>? _povRadiusButton;
    private static Func<bool>? _povRadiusTaken;
    private static Func<Recipe, bool>? _povChestsFor;
    private static Func<Player, List<Container>>? _povChests;
    private static Func<Player, string, List<Container>, int>? _povCountEverywhere;
    private static bool _povCraftingWarning;
    private static RectTransform? _povReturnsRow;
    private static InventoryGui? _povCraftingGui;
    private static RectTransformSnapshot? _povRadiusOriginal;
    private static RectTransformSnapshot? _povTabOriginal;
    private static RectTransformSnapshot? _povVneiTabOriginal;
    private static readonly Dictionary<ItemData, PoVCraftingPreview> PoVCraftingPreviews = new();

    private sealed class PoVCraftingPreview
    {
        internal Recipe Recipe = null!;
        internal bool Disenchant;
        internal float Expires;
        internal int LocalizationVersion;
        internal string Summary = "";
        internal readonly List<(ItemDrop Item, string Count)> Returns = new();
    }

    private static void InitializePathOfValheimanCrafting(Assembly assembly)
    {
        InstallPathOfValheimanCapability("crafting-tabs", harmony =>
        {
            MethodInfo inUse = RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.CraftSalvage", true)!,
                "InUse", typeof(bool), typeof(Player), typeof(ItemData));
            harmony.Patch(inUse, postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(ProtectPathOfValheimanSalvageItem)));
            BindPathOfValheimanCrafting(assembly);
        });
        InstallPathOfValheimanCapability("crafting-layout", harmony =>
        {
            // Both category rendering and PoV's queue depend on this setting getter.
            // Suppress only while our adapter owns the panel; never rewrite PoV's config.
            MethodInfo enabled = RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.UI.CraftCategories", true)!, "get_On", typeof(bool));
            harmony.Patch(enabled, postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(SuppressPathOfValheimanCraftCategories)));
            MethodInfo queue = RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.UI.CraftQueue+Gui_Update", true)!,
                "Postfix", typeof(void), typeof(InventoryGui), typeof(Player));
            harmony.Patch(queue, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(SkipPathOfValheimanQueueDisplay)));
        });
        InstallPathOfValheimanCapability("crafting-chests", _ => BindPathOfValheimanCraftingChests(assembly));
    }

    private static void BindPathOfValheimanCraftingChests(Assembly assembly)
    {
        Type conveniences = assembly.GetType("PassiveTree.Conveniences", true)!;
        var allowed = (Func<Recipe, bool>)Delegate.CreateDelegate(typeof(Func<Recipe, bool>),
            RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.CraftAttributes", true)!, "ChestsFor", typeof(bool), typeof(Recipe)));
        var chests = (Func<Player, List<Container>>)Delegate.CreateDelegate(typeof(Func<Player, List<Container>>),
            RequirePathOfValheimanMethod(conveniences, "Chests", typeof(List<Container>), typeof(Player)));
        var stock = (Func<Player, string, List<Container>, int>)Delegate.CreateDelegate(typeof(Func<Player, string, List<Container>, int>),
            RequirePathOfValheimanMethod(conveniences, "CountEverywhere", typeof(int), typeof(Player), typeof(string), typeof(List<Container>)));
        var button = PathOfValheimanStaticField<Button>(assembly.GetType("PassiveTree.ChestRadiusButton", true)!, "button");
        var taken = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),
            RequirePathOfValheimanMethod(assembly.GetType("Valheiman.Api.Conveniences", true)!, "get_CraftFromChests", typeof(bool)));
        _povChestsFor = allowed;
        _povChests = chests;
        _povCountEverywhere = stock;
        _povRadiusButton = button;
        _povRadiusTaken = taken;
    }

    // Bind as one unit so an unsupported PoV build cannot leave a half-working tab.
    private static void BindPathOfValheimanCrafting(Assembly assembly)
    {
        Type salvage = assembly.GetType("PassiveTree.CraftSalvage", true)!;
        var active = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), RequirePathOfValheimanMethod(salvage, "get_Salvaging", typeof(bool)));
        var tab = (Func<Button>)Delegate.CreateDelegate(typeof(Func<Button>), RequirePathOfValheimanMethod(salvage, "get_Tab", typeof(Button)));
        var runecarver = (Func<Player, bool>)Delegate.CreateDelegate(typeof(Func<Player, bool>), RequirePathOfValheimanMethod(salvage, "Runecarver", typeof(bool), typeof(Player)));
        var returns = (Func<ItemData, Recipe, List<(ItemDrop, int, int)>>)Delegate.CreateDelegate(typeof(Func<ItemData, Recipe, List<(ItemDrop, int, int)>>),
            RequirePathOfValheimanMethod(salvage, "Returns", typeof(List<(ItemDrop, int, int)>), typeof(ItemData), typeof(Recipe)));
        var disenchant = (PoVDisenchantReturns)Delegate.CreateDelegate(typeof(PoVDisenchantReturns),
            RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.Loot.Disenchant", true)!, "Returns", typeof(void), typeof(ItemData), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()));
        var contents = (Func<ItemData, string[]>)Delegate.CreateDelegate(typeof(Func<ItemData, string[]>),
            RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.Loot.ItemSockets", true)!, "Contents", typeof(string[]), typeof(ItemData)));
        var text = (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>),
            RequirePathOfValheimanMethod(assembly.GetType("Valheiman.Api.Texts", true)!, "Get", typeof(string), typeof(string), typeof(string)));
        var chance = (Func<string, float>)Delegate.CreateDelegate(typeof(Func<string, float>),
            RequirePathOfValheimanMethod(salvage, "ReturnChance", typeof(float), typeof(string)));
        _povSalvageTab = tab;
        _povRunecarver = runecarver;
        _povSalvageReturns = returns;
        _povReturnChance = chance;
        _povDisenchantReturns = disenchant;
        _povSocketContents = contents;
        _povText = text;
        _povSalvaging = active;
    }

    internal static bool IsPathOfValheimanSalvageTab(InventoryGui? gui)
    {
        if (_povSalvaging == null || gui == null || gui != InventoryGui.instance ||
            IsCraftingPrimaryTabSelected(gui)) return false;
        try
        {
            Button? tab = _povSalvageTab!();
            return _povSalvaging() && tab != null && tab.gameObject.activeInHierarchy && !tab.interactable &&
                   tab.transform.parent == gui.m_tabCraft.transform.parent && !IsVneiCraftingTabActive(gui);
        }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); return false; }
    }

    private static void SuppressPathOfValheimanCraftCategories(ref bool __result)
    {
        if (__result && ShouldShowCraftingPanelRedesign(InventoryGui.instance)) __result = false;
    }

    private static bool SkipPathOfValheimanQueueDisplay(InventoryGui __0) => !ShouldShowCraftingPanelRedesign(__0);

    private static void ProtectPathOfValheimanSalvageItem(Player __0, ItemData __1, ref bool __result)
    {
        if (__result || __0 == null || __0 != Player.m_localPlayer || __1 == null) return;
        Inventory inventory = __0.GetInventory();
        if (ContainsExactItemReference(inventory, __1) &&
            (GetInventoryCellKind(__0, inventory, __1.m_gridPos) != InventoryCellKind.RegularUnlocked ||
             IsFavoriteProtected(__0, inventory, __1))) __result = true;
    }

    private static void WarnPathOfValheimanCrafting(Exception error)
    {
        if (_povCraftingWarning) return;
        _povCraftingWarning = true;
        Log.LogWarning($"Path of Valheiman crafting display failed: {error.Message}");
    }

    private static int GetPathOfValheimanCraftingStock(Player player, string name, Recipe? recipe, int otherTotal)
    {
        if (_povCountEverywhere == null || recipe == null) return otherTotal;
        try
        {
            // Use the displayed recipe, not StockOf's current selection: hover and
            // pinned recipes can have a different mastery. Keep PoV's access/cache.
            return _povChestsFor!(recipe)
                ? Math.Max(otherTotal, _povCountEverywhere(player, name, _povChests!(player)))
                : otherTotal;
        }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); return otherTotal; }
    }

    private static PoVCraftingPreview GetPathOfValheimanPreview(InventoryGui.RecipeDataPair pair)
    {
        ItemData item = pair.ItemData;
        bool disenchant = _povRunecarver!(Player.m_localPlayer);
        if (PoVCraftingPreviews.TryGetValue(item, out PoVCraftingPreview cached) &&
            cached.Recipe == pair.Recipe && cached.Disenchant == disenchant &&
            cached.LocalizationVersion == _uiLocalizationVersion && Time.unscaledTime < cached.Expires) return cached;

        var preview = new PoVCraftingPreview
        {
            Recipe = pair.Recipe, Disenchant = disenchant,
            Expires = Time.unscaledTime + 0.25f, LocalizationVersion = _uiLocalizationVersion
        };
        var summary = new StringBuilder(_povText!(disenchant ? "ui.disenchant.returns" : "ui.salvage.returns",
            disenchant ? "Disenchanting returns:" : "Salvaging returns:"));
        if (disenchant)
        {
            _povDisenchantReturns!(item, out int azurite, out int divine);
            AddDust("PT_AzuriteDust", azurite);
            AddDust("PT_DivineAzuriteDust", divine);
            int sockets = 0;
            foreach (string socket in _povSocketContents!(item)) if (!string.IsNullOrEmpty(socket)) sockets++;
            if (sockets > 0) summary.Append('\n').Append(string.Format(_povText!("ui.disenchant.sigils",
                "Socketed sigils are destroyed and cannot be recovered ({0})"), sockets));
        }
        else
        {
            bool hasChance = false;
            foreach (var entry in _povSalvageReturns!(item, pair.Recipe))
            {
                if (entry.Item1 == null || entry.Item2 <= 0 && entry.Item3 <= 0) continue;
                float chance = _povReturnChance!(entry.Item1.name);
                bool bonus = entry.Item3 > 0 && chance > 0f;
                hasChance |= bonus;
                string count = bonus ? $"{entry.Item2}–{entry.Item2 + entry.Item3}" : entry.Item2.ToString();
                preview.Returns.Add((entry.Item1, count));
                summary.Append("\n  ").Append(entry.Item1.m_itemData.m_shared.m_name).Append(" x").Append(count);
                if (bonus) summary.Append(string.Format(_povText!("ui.salvage.chance",
                    " (+{0} chance for each of the other {1})"), Mathf.RoundToInt(chance * 100f) + "%", entry.Item3));
            }
            if (!hasChance) summary.Append('\n').Append(_povText!("ui.salvage.professions",
                "Profession masteries can recover more of the lost materials."));
        }
        preview.Summary = Localization.instance.Localize(summary.ToString());
        if (PoVCraftingPreviews.Count >= 128) PoVCraftingPreviews.Clear();
        PoVCraftingPreviews[item] = preview;
        return preview;

        void AddDust(string prefab, int count)
        {
            if (count <= 0) return;
            ItemDrop? drop = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>();
            if (drop != null) AddReturn(drop, count);
            else summary.Append("\n  ").Append(prefab).Append(" x").Append(count);
        }
        void AddReturn(ItemDrop drop, int count)
        {
            if (drop == null || count <= 0) return;
            preview.Returns.Add((drop, count.ToString()));
            summary.Append("\n  ").Append(drop.m_itemData.m_shared.m_name).Append(" x").Append(count);
        }
    }

    private static string GetPathOfValheimanSalvageTooltip(InventoryGui.RecipeDataPair pair)
    {
        string body = GetLocalizedStaticItemTooltip(pair.ItemData, Math.Max(1, pair.ItemData.m_quality), crafting: false, Math.Max(1, pair.ItemData.m_stack));
        body = AppendPathOfValheimanAffixes(pair.ItemData, body);
        try { return GetPathOfValheimanPreview(pair).Summary + "\n\n" + body; }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); return body; }
    }

    private static void ConfigurePathOfValheimanReturnRow(RectTransform row, InventoryGui.RecipeDataPair pair,
        float slotSize, float gap, bool tooltips)
    {
        PoVCraftingPreview? preview = null;
        try { preview = GetPathOfValheimanPreview(pair); }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); }
        for (int i = 0; i < CraftingTooltipRecipeSlotCount; i++)
        {
            RectTransform cell = EnsureCraftingTooltipRecipeSlot(row, i);
            cell.anchoredPosition = new Vector2(i * (slotSize + gap), 0f);
            var entry = preview != null && i < preview.Returns.Count ? preview.Returns[i] : default;
            ConfigureCraftingTooltipRecipeSlot(row, i, entry.Item?.m_itemData.GetIcon(),
                entry.Count ?? "", true, slotSize,
                entry.Item != null ? Localization.instance.Localize(entry.Item.m_itemData.m_shared.m_name) : "", tooltips);
        }
    }

    private static void LayoutPathOfValheimanSalvageControls(InventoryGui gui, RectTransform grid)
    {
        _povCraftingGui = gui;
        HideRecycleNReclaimHud();
        HideCraftingSocketWarning();
        HideCraftingVanillaRequirementSlots(gui);
        HideOwnedCraftingRequirementSlots();
        if (_craftingCountInputRect != null) _craftingCountInputRect.gameObject.SetActive(false);
        if (_craftingUpgradeProgressionRect != null) _craftingUpgradeProgressionRect.gameObject.SetActive(false);
        if (gui.m_minStationLevelIcon != null) gui.m_minStationLevelIcon.gameObject.SetActive(false);
        if (gui.m_minStationLevelText != null) gui.m_minStationLevelText.gameObject.SetActive(false);
        HideCraftingRequiredStationLevelTooltip();
        if (gui.m_itemCraftType != null) gui.m_itemCraftType.gameObject.SetActive(false);
        bool selected = gui.m_selectedRecipe.ItemData != null &&
                        FindCraftingRecipeViewIndex(GetSelectedCraftingRecipeIndexSafe(gui)) >= 0;
        Vector2 size = new(CraftingRecipeGridCellSize + CraftingRecipeGridCellSpace, 42f);
        Vector2 position = GetCraftingGridCenteredPosition(grid, 0, CraftingBottomControlRow, size, 2) +
                           CraftingBottomControlsFixedOffset + CraftingCraftButtonFixedOffset;
        if (gui.m_craftButton != null)
        {
            SetTopLeftRectLayout(gui.m_crafting, (RectTransform)gui.m_craftButton.transform, position, size);
            gui.m_craftButton.gameObject.SetActive(selected && gui.m_craftTimer < 0f);
            // PoV's UpdateRecipe owns the label and interactable state.
            SetActionButtonTextAutoSize(gui.m_craftButton);
        }
        if (gui.m_craftProgressPanel is RectTransform progress)
        {
            SetTopLeftRectLayout(gui.m_crafting, progress, position, size);
            gui.m_craftProgressBar?.SetWidth(size.x);
        }
        if (_povReturnsRow == null || _povReturnsRow.parent != grid)
            _povReturnsRow = EnsureCraftingTooltipRecipeRow(grid);
        RectTransform row = _povReturnsRow;
        row.gameObject.SetActive(selected);
        if (selected)
        {
            SetTopLeftRectLayout(row, new Vector2(2 * CraftingRecipeGridCellSpace, -CraftingBottomControlRow * CraftingRecipeGridCellSpace) +
                CraftingBottomControlsFixedOffset, new Vector2(5 * 60f, 54f));
            ConfigurePathOfValheimanReturnRow(row, gui.m_selectedRecipe, 54f, 6f, true);
        }
    }

    private static void LayoutPathOfValheimanRadiusButton(InventoryGui gui, RectTransform grid)
    {
        if (_povRadiusButton == null) return;
        try
        {
            Button? button = _povRadiusButton();
            if (button == null || gui != InventoryGui.instance || !button.transform.IsChildOf(gui.m_crafting)) return;
            _povCraftingGui = gui;
            RectTransform rect = (RectTransform)button.transform;
            if (_povRadiusOriginal == null || _povRadiusOriginal.Rect != rect)
                _povRadiusOriginal = new RectTransformSnapshot(rect);
            // Reactivate explicitly after respec; PoV's Watch cannot update an inactive GO.
            button.gameObject.SetActive(_povRadiusTaken!() && GetCraftingTabAdapterState(gui).Kind == CraftingTabAdapterKind.Vanilla);
            // Below the action button, before the full-width warning row.
            SetTopLeftRectLayout(gui.m_crafting, rect,
                GetCraftingGridCellPosition(grid, 0, CraftingBottomControlRow) + CraftingBottomControlsFixedOffset +
                new Vector2(2f, -50f), new Vector2(170f, 22f));
        }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); }
    }

    private static void LayoutPathOfValheimanCraftingTab(InventoryGui gui)
    {
        if (_povSalvageTab == null || _instance == null || !_instance.isActiveAndEnabled ||
            gui == null || gui != InventoryGui.instance || gui.m_tabCraft == null ||
            gui.m_tabCraft.transform is not RectTransform craft || !craft.gameObject.activeInHierarchy) return;
        try
        {
            Transform parent = craft.parent;
            if (parent == null) return;
            Button? tab = _povSalvageTab();
            RectTransform? salvage = tab != null ? tab.transform as RectTransform : null;
            if (salvage == null || salvage.parent != parent || !salvage.gameObject.activeInHierarchy) salvage = null;
            // VNEI 0.17.6 creates this direct tab child; its displayed name is configurable.
            // Do not scan all loaded types or treat its nested content/buttons as tabs.
            RectTransform? vnei = parent.Find("VNEI") as RectTransform;
            if (vnei == null || !vnei.gameObject.activeInHierarchy || vnei.GetComponent<Button>() == null) vnei = null;
            if (salvage == null && vnei == null) return;
            _povCraftingGui = gui;
            float top = GetCraftingTabEdge(parent, craft, right: false).y;
            float right = GetCraftingTabEdge(parent, craft, right: true).x;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == salvage || child == vnei || child is not RectTransform other || !child.gameObject.activeInHierarchy ||
                    child.GetComponent<Button>() == null) continue;
                // Respect other mods' tabs on this row, excluding controls on other rows.
                Vector3 edge = GetCraftingTabEdge(parent, other, right: true);
                if (Mathf.Abs(edge.y - top) <= 2f) right = Mathf.Max(right, edge.x);
            }
            if (salvage != null)
            {
                if (_povTabOriginal == null || _povTabOriginal.Rect != salvage) _povTabOriginal = new RectTransformSnapshot(salvage);
                PlaceCraftingTab(parent, salvage, right + 4f, top);
                right = GetCraftingTabEdge(parent, salvage, right: true).x;
            }
            if (vnei != null)
            {
                if (_povVneiTabOriginal == null || _povVneiTabOriginal.Rect != vnei) _povVneiTabOriginal = new RectTransformSnapshot(vnei);
                if (salvage != null)
                {
                    int index = vnei.GetSiblingIndex();
                    int target = salvage.GetSiblingIndex() < index ? index - 1 : index;
                    if (salvage.GetSiblingIndex() != target) salvage.SetSiblingIndex(target);
                }
                // Reconcile VNEI too, including when the station-only salvage tab is hidden.
                // Local visual edges avoid copying anchoredPosition between unlike anchors.
                PlaceCraftingTab(parent, vnei, right + 4f, top);
            }
        }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); }
    }

    private static Vector3 GetCraftingTabEdge(Transform parent, RectTransform tab, bool right) =>
        parent.InverseTransformPoint(tab.TransformPoint(new Vector3(right ? tab.rect.xMax : tab.rect.xMin, tab.rect.yMax, 0f)));

    private static void PlaceCraftingTab(Transform parent, RectTransform tab, float left, float top)
    {
        Vector3 edge = GetCraftingTabEdge(parent, tab, right: false);
        Vector3 offset = new(left - edge.x, top - edge.y, 0f);
        if (Mathf.Abs(offset.x) > 0.1f || Mathf.Abs(offset.y) > 0.1f) tab.localPosition += offset;
    }

    private static void RestorePathOfValheimanCraftingTabs()
    {
        Transform? parent = _povCraftingGui?.m_tabCraft != null ? _povCraftingGui.m_tabCraft.transform.parent : null;
        // A recreated/destroyed tab or a foreign reparent is no longer ours to restore.
        if (parent != null && _povTabOriginal?.Rect != null && _povTabOriginal.Rect.parent == parent) _povTabOriginal.Restore();
        if (parent != null && _povVneiTabOriginal?.Rect != null && _povVneiTabOriginal.Rect.parent == parent) _povVneiTabOriginal.Restore();
        _povTabOriginal = _povVneiTabOriginal = null;
    }

    private static void ClearPathOfValheimanCraftingPreviews() => PoVCraftingPreviews.Clear();

    private static void HidePathOfValheimanReturns()
    {
        if (_povReturnsRow != null && _povReturnsRow.gameObject.activeSelf) _povReturnsRow.gameObject.SetActive(false);
    }

    private static void RestorePathOfValheimanRadiusButton(InventoryGui? gui)
    {
        if (gui != null && gui != _povCraftingGui) return;
        if (_povRadiusOriginal?.Rect != null)
        {
            try { _povRadiusOriginal.Rect.gameObject.SetActive(_povRadiusTaken!()); }
            catch (Exception error) { WarnPathOfValheimanCrafting(error); }
        }
        _povRadiusOriginal?.Restore();
        _povRadiusOriginal = null;
    }

    internal static void ClearPathOfValheimanCraftingUi(InventoryGui? gui = null)
    {
        if (gui != null && gui != _povCraftingGui) return;
        ClearPathOfValheimanCraftingPreviews();
        RestorePathOfValheimanRadiusButton(gui);
        RestorePathOfValheimanCraftingTabs();
        if (_povReturnsRow != null) UnityEngine.Object.Destroy(_povReturnsRow.gameObject);
        _povReturnsRow = null;
        _povCraftingGui = null;
    }

    private static void ShutdownPathOfValheimanCrafting()
    {
        ClearPathOfValheimanCraftingUi();
        _povSalvaging = null;
        _povSalvageTab = null;
        _povRunecarver = null;
        _povSalvageReturns = null;
        _povReturnChance = null;
        _povDisenchantReturns = null;
        _povSocketContents = null;
        _povText = null;
        _povRadiusButton = null;
        _povRadiusTaken = null;
        _povChestsFor = null;
        _povChests = null;
        _povCountEverywhere = null;
    }
}
