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
    private static Func<ItemData, Recipe, List<(ItemDrop, int)>>? _povSalvageReturns;
    private static PoVDisenchantReturns? _povDisenchantReturns;
    private static Func<ItemData, string[]>? _povSocketContents;
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
    private static readonly Dictionary<ItemData, PoVCraftingPreview> PoVCraftingPreviews = new();

    private sealed class PoVCraftingPreview
    {
        internal Recipe Recipe = null!;
        internal bool Disenchant;
        internal float Expires;
        internal int LocalizationVersion;
        internal string Summary = "";
        internal readonly List<(ItemDrop Item, int Count)> Returns = new();
    }

    private static void InitializePathOfValheimanCrafting(Assembly assembly)
    {
        InstallPathOfValheimanCapability("crafting-tabs", _ => BindPathOfValheimanCrafting(assembly));
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
            RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.ChestRadius", true)!, "get_Taken", typeof(bool)));
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
        var tab = PathOfValheimanStaticField<Button>(salvage, "tab");
        var runecarver = (Func<Player, bool>)Delegate.CreateDelegate(typeof(Func<Player, bool>), RequirePathOfValheimanMethod(salvage, "Runecarver", typeof(bool), typeof(Player)));
        var returns = (Func<ItemData, Recipe, List<(ItemDrop, int)>>)Delegate.CreateDelegate(typeof(Func<ItemData, Recipe, List<(ItemDrop, int)>>),
            RequirePathOfValheimanMethod(salvage, "Returns", typeof(List<(ItemDrop, int)>), typeof(ItemData), typeof(Recipe)));
        var disenchant = (PoVDisenchantReturns)Delegate.CreateDelegate(typeof(PoVDisenchantReturns),
            RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.Loot.Disenchant", true)!, "Returns", typeof(void), typeof(ItemData), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()));
        var contents = (Func<ItemData, string[]>)Delegate.CreateDelegate(typeof(Func<ItemData, string[]>),
            RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.Loot.ItemSockets", true)!, "Contents", typeof(string[]), typeof(ItemData)));
        var text = (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>),
            RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.L", true)!, "Id", typeof(string), typeof(string), typeof(string)));
        _povSalvageTab = tab;
        _povRunecarver = runecarver;
        _povSalvageReturns = returns;
        _povDisenchantReturns = disenchant;
        _povSocketContents = contents;
        _povText = text;
        _povSalvaging = active;
    }

    private static bool IsPathOfValheimanSalvageTab(InventoryGui? gui)
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

    private static void WarnPathOfValheimanCrafting(Exception error)
    {
        if (_povCraftingWarning) return;
        _povCraftingWarning = true;
        Log.LogWarning($"Path of Valheiman crafting display failed: {error.Message}");
    }

    private static int GetPathOfValheimanCraftingStock(Player player, string name, Recipe? recipe, int otherTotal)
    {
        if (_povCountEverywhere == null || recipe == null || recipe.m_requireOnlyOneIngredient) return otherTotal;
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
            foreach (var entry in _povSalvageReturns!(item, pair.Recipe)) AddReturn(entry.Item1, entry.Item2);
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
            preview.Returns.Add((drop, count));
            summary.Append("\n  ").Append(drop.m_itemData.m_shared.m_name).Append(" x").Append(count);
        }
    }

    private static string GetPathOfValheimanSalvageTooltip(InventoryGui.RecipeDataPair pair)
    {
        string body = GetLocalizedStaticItemTooltip(pair.ItemData, Math.Max(1, pair.ItemData.m_quality), crafting: false, Math.Max(1, pair.ItemData.m_stack));
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
                entry.Count > 0 ? entry.Count.ToString() : "", true, slotSize,
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
        if (_povSalvageTab == null || gui != InventoryGui.instance || gui.m_tabCraft == null) return;
        try
        {
            Button? tab = _povSalvageTab();
            if (tab == null || !tab.gameObject.activeInHierarchy || tab.transform is not RectTransform rect ||
                rect.parent != gui.m_tabCraft.transform.parent) return;
            _povCraftingGui = gui;
            if (_povTabOriginal == null || _povTabOriginal.Rect != rect) _povTabOriginal = new RectTransformSnapshot(rect);
            Transform parent = rect.parent;
            Transform? vnei = null;
            float right = float.NegativeInfinity;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == rect || child is not RectTransform other || !child.gameObject.activeInHierarchy ||
                    child.GetComponent<Button>() == null) continue;
                if (IsVneiUiTransform(child, parent)) { vnei = child; continue; }
                right = Mathf.Max(right, parent.InverseTransformPoint(other.TransformPoint(new Vector3(other.rect.xMax, 0f))).x);
            }
            if (!float.IsNegativeInfinity(right))
            {
                float left = parent.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, 0f))).x;
                if (Mathf.Abs(right + 4f - left) > 0.1f)
                    rect.localPosition += new Vector3(right + 4f - left, 0f);
            }
            // VNEI positions itself after its preceding active sibling. PoV only
            // moves its own tab, preserving other mods' callbacks and tab state.
            if (vnei != null)
            {
                int index = vnei.GetSiblingIndex();
                int target = rect.GetSiblingIndex() < index ? index - 1 : index;
                if (rect.GetSiblingIndex() != target) rect.SetSiblingIndex(target);
            }
        }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); }
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
        _povTabOriginal?.Restore();
        _povTabOriginal = null;
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
