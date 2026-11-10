using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static PathOfValheimanLooksBridge? _povLooks;
    private static PathOfValheimanCharacterPanelFactory? _povSkillsFactory, _povStatsFactory;
    private static Action? _povCloseEnchant;
    private static Func<bool>? _povEnchantOpen;
    private static readonly Action<InventoryGui> PovSelectCraftTab = AccessTools.MethodDelegate<Action<InventoryGui>>(AccessTools.DeclaredMethod(typeof(InventoryGui), "OnTabCraftPressed", Type.EmptyTypes));
    private static readonly AccessTools.FieldRef<InventoryGui, float> PovCharacterCraftTimer = AccessTools.FieldRefAccess<InventoryGui, float>("m_craftTimer");
    private static readonly AccessTools.FieldRef<InventoryGui, ItemData> PovCharacterDragItem = AccessTools.FieldRefAccess<InventoryGui, ItemData>("m_dragItem");
    private static readonly AccessTools.FieldRef<InventoryGui, GameObject> PovCharacterDragGo = AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");
    private static PovCharacterView? _povCharacterView;
    private static InventoryGui? _povCharacterFailedGui;
    private static readonly string[] PovAppearanceRoles = { "helmet", "chest", "legs", "cape", "belt", "trinket", "quiver" };

    private sealed class PovCharacterView
    {
        internal InventoryGui Gui = null!;
        internal Player Player = null!;
        internal RectTransform Root = null!;
        internal Button Toggle = null!;
        internal readonly Button[] Tabs = new Button[3];
        internal readonly RectTransform[] Pages = new RectTransform[3];
        internal readonly bool[] Failed = new bool[3];
        internal readonly PathOfValheimanCharacterPanelFactory.Panel?[] Native = new PathOfValheimanCharacterPanelFactory.Panel?[3];
        internal CanvasGroup CraftingGroup = null!;
        internal bool OwnGroup, OriginalInteractable, OriginalRaycasts, Active, Closing;
        // Remember navigation for this player/GUI, independently of visible UI and preview state.
        internal bool CharacterSelected;
        internal float OriginalAlpha;
        internal int Tab, Page, Language = -1;
        internal Vector2 Size;
        internal TMP_Text Title = null!, RuneCount = null!, Paging = null!;
        internal Image Icon = null!, RuneIcon = null!;
        internal UITooltip RuneTooltip = null!;
        internal ObjectDB? RuneDatabase;
        internal Button Apply = null!, Reset = null!, Previous = null!, Next = null!;
        internal readonly List<Button> Choices = new();
        internal readonly List<TMP_Text> CurrentMarkers = new();
        internal readonly List<Outline> SelectionOutlines = new();
        internal readonly List<UITooltip> ChoiceTooltips = new();
        internal readonly List<ItemData> Looks = new();
        internal ItemData? Target, Picked;
        internal ItemData? CurrentLook, CurrentLookTarget;
        internal string? CurrentLookKey;
        internal Vector2i TargetCell;
        internal float RefreshAt;
        internal bool CanHide;
    }

    private static void InitializePathOfValheimanCharacter(Assembly assembly)
    {
        if (IsDedicatedServer) return;
        InstallPathOfValheimanCapability("character-appearance", _ => _povLooks = new PathOfValheimanLooksBridge(assembly));
        InstallPathOfValheimanCapability("character-preview", InstallPathOfValheimanLookPreview);
        InstallPathOfValheimanCapability("character-skills", harmony =>
        {
            var factory = new PathOfValheimanCharacterPanelFactory(assembly, skills: true);
            MethodInfo show = RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.Gear.Module", true)!, "ShowSkills", typeof(void));
            harmony.Patch(show, postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(OpenPathOfValheimanSkills)));
            _povSkillsFactory = factory;
        });
        InstallPathOfValheimanCapability("character-stats", _ => _povStatsFactory = new PathOfValheimanCharacterPanelFactory(assembly, skills: false));
        InstallPathOfValheimanCapability("character-navigation", _ =>
        {
            var closeEnchant = (Action)Delegate.CreateDelegate(typeof(Action), RequirePathOfValheimanMethod(
                assembly.GetType("PassiveTree.UI.EnchantWindow", true)!, "Close", typeof(void)));
            var enchantOpen = PathOfValheimanStaticField<bool>(assembly.GetType("PassiveTree.UI.EnchantWindow", true)!, "open");
            _povCloseEnchant = closeEnchant;
            _povEnchantOpen = enchantOpen;
        });
    }

    private static string PovCharacterText(string key) => Localization.instance.Localize("$inventoryslots_pov_" + key);

    private static bool CanShowPathOfValheimanCharacter(Player player) =>
        player != null && PovCanMutate(player) && !player.IsDead() && _pov!.Enabled() && _povCloseEnchant != null &&
        (_povLooks != null || _povSkillsFactory != null || _povStatsFactory != null);

    internal static bool IsPathOfValheimanCharacterActive(InventoryGui? gui) =>
        _povCharacterView is { Active: true } view && view.Gui == gui;

    private static void UpdatePathOfValheimanCharacterUi()
    {
        InventoryGui gui = InventoryGui.instance;
        if (gui != null && gui == _povCharacterFailedGui) return;
        try { UpdatePathOfValheimanCharacterUiCore(); }
        catch (Exception error)
        {
            _povCharacterFailedGui = gui;
            Log.LogWarning("PoV character UI unavailable for this GUI: " + error.GetBaseException().Message);
            ClearPathOfValheimanCharacterUi();
        }
    }

    private static void UpdatePathOfValheimanCharacterUiCore()
    {
        if (IsDedicatedServer) return;
        InventoryGui gui = InventoryGui.instance;
        Player player = Player.m_localPlayer;
        if (_povCharacterView != null && (_povCharacterView.Gui != gui || _povCharacterView.Player != player))
            ClearPathOfValheimanCharacterUi();
        // IsVisible ends before the animator's outro. Preserve the page and the
        // masked crafting panel until the actual inventory root has disappeared.
        if (_povCharacterView is { Closing: true } closing && gui != null &&
            gui.m_inventoryRoot != null && gui.m_inventoryRoot.gameObject.activeInHierarchy) return;
        if (gui == null || !CanShowPathOfValheimanCharacter(player) || !InventoryGui.IsVisible())
        {
            ClosePathOfValheimanCharacterUi();
            return;
        }
        if (IsInventoryPanelClosing(gui)) return;
        if (_povCharacterView == null) _povCharacterView = CreatePathOfValheimanCharacterView(gui, player);
        PovCharacterView view = _povCharacterView;
        if (view.Closing) ClosePathOfValheimanCharacterUi();
        if (view.CharacterSelected && !view.Active) SetPathOfValheimanCharacter(view, true, view.Tab);
        RectTransform craft = gui.m_crafting;
        RectTransform toggle = (RectTransform)view.Toggle.transform;
        CopyPovCharacterRect(craft, view.Root);
        toggle.position = craft.TransformPoint(new Vector3(craft.rect.xMin - 4f, craft.rect.yMax, 0f));
        toggle.localScale = craft.localScale;
        view.Toggle.gameObject.SetActive(craft.gameObject.activeInHierarchy);
        view.Toggle.interactable = PovCharacterCraftTimer(gui) < 0f && PovCharacterDragItem(gui) == null &&
            (!view.Failed[0] || !view.Failed[1] || !view.Failed[2]);
        PlacePovCharacterAfter(gui.m_crafting, view.Root);
        PlacePovCharacterAfter(view.Root, toggle);
        if (view.Language != _uiLocalizationVersion)
        {
            view.Language = _uiLocalizationVersion;
            view.Toggle.GetComponentInChildren<TMP_Text>(true).text = PovCharacterText(view.Active ? "character" : "crafting");
            for (int i = 0; i < 3; i++) view.Tabs[i].GetComponentInChildren<TMP_Text>(true).text =
                PovCharacterText(i == 0 ? "appearance" : i == 1 ? "skills" : "stats");
            view.Reset.GetComponentInChildren<TMP_Text>(true).text = PovCharacterText("original_look");
            view.Apply.GetComponentInChildren<TMP_Text>(true).text = PovCharacterText("apply_look");
        }
        if (!view.Active) return;
        ValidatePathOfValheimanLookPreview();
        view.CraftingGroup.alpha = 0f;
        view.CraftingGroup.blocksRaycasts = view.CraftingGroup.interactable = false;
        Vector2 size = craft.rect.size;
        if (view.Size != size)
        {
            view.Size = size;
            LayoutPathOfValheimanCharacter(view);
        }
        try
        {
            if (view.Tab != 0) view.Native[view.Tab]?.Tick(player);
            else if (Time.unscaledTime >= view.RefreshAt)
            {
                view.RefreshAt = Time.unscaledTime + 0.25f;
                RefreshPathOfValheimanLooks(view);
            }
        }
        catch (Exception error) { FailPathOfValheimanCharacterTab(view, view.Tab, error); }
    }

    private static PovCharacterView CreatePathOfValheimanCharacterView(InventoryGui gui, Player player)
    {
        var view = new PovCharacterView { Gui = gui, Player = player };
        // Publish our cleanup owner before any Unity allocation can fail.
        _povCharacterView = view;
        RectTransform craft = gui.m_crafting;
        view.Root = NewPovCharacterRect("InventorySlots PoV Character", craft.parent);
        Image background = view.Root.gameObject.AddComponent<Image>();
        Image? original = craft.GetComponent<Image>();
        if (original != null) { background.sprite = original.sprite; background.type = original.type; background.color = original.color; }
        else background.color = new Color(0.13f, 0.09f, 0.055f, 0.98f);
        view.CraftingGroup = craft.GetComponent<CanvasGroup>();
        view.OwnGroup = view.CraftingGroup == null;
        if (view.OwnGroup) view.CraftingGroup = craft.gameObject.AddComponent<CanvasGroup>();
        view.Toggle = NewPovCharacterButton(gui, craft.parent, "Character mode", () => SetPathOfValheimanCharacter(view, !view.Active, view.Tab));
        RectTransform toggle = (RectTransform)view.Toggle.transform;
        toggle.pivot = new Vector2(1f, 1f);
        toggle.sizeDelta = new Vector2(90f, 30f);
        for (int i = 0; i < 3; i++)
        {
            int tab = i;
            view.Pages[i] = NewPovCharacterRect("Page " + i, view.Root);
            view.Pages[i].gameObject.SetActive(false);
            view.Tabs[i] = NewPovCharacterButton(gui, view.Root, "Tab " + i, () => SetPathOfValheimanCharacter(view, true, tab));
            view.Failed[i] = i == 0 ? _povLooks == null : i == 1 ? _povSkillsFactory == null : _povStatsFactory == null;
            view.Tabs[i].interactable = !view.Failed[i];
        }
        if (view.Failed[0]) view.Tab = view.Failed[1] ? 2 : 1;
        RectTransform appearance = view.Pages[0];
        view.Icon = NewPovCharacterRect("Target icon", appearance).gameObject.AddComponent<Image>();
        view.Icon.raycastTarget = false;
        view.Icon.preserveAspect = true;
        view.Title = NewPovCharacterText(appearance, "Target", 20f);
        view.RuneIcon = NewPovCharacterRect("Rune of Guise", appearance).gameObject.AddComponent<Image>();
        view.RuneIcon.preserveAspect = true;
        view.RuneTooltip = view.RuneIcon.gameObject.AddComponent<UITooltip>();
        EnsureTooltipPrefab(view.RuneTooltip);
        view.RuneCount = NewPovCharacterText(appearance, "Rune count", 18f);
        view.RuneCount.alignment = TextAlignmentOptions.MidlineLeft;
        view.Paging = NewPovCharacterText(appearance, "Page", 16f);
        view.Paging.alignment = TextAlignmentOptions.Center;
        view.Reset = NewPovCharacterButton(gui, appearance, "Original look", () => ApplyPathOfValheimanLook(view, reset: true));
        view.Apply = NewPovCharacterButton(gui, appearance, "Transmog", () => ApplyPathOfValheimanLook(view, reset: false));
        view.Previous = NewPovCharacterButton(gui, appearance, "Previous", () => { view.Page = Math.Max(0, view.Page - 1); RefreshPathOfValheimanLooks(view); });
        view.Next = NewPovCharacterButton(gui, appearance, "Next", () => { view.Page++; RefreshPathOfValheimanLooks(view); });
        view.Previous.GetComponentInChildren<TMP_Text>(true).text = "<";
        view.Next.GetComponentInChildren<TMP_Text>(true).text = ">";
        for (int i = 0; i < 16; i++)
        {
            int cell = i;
            Button button = NewPovCharacterButton(gui, appearance, "Look " + i, () => PickPathOfValheimanLook(view, cell));
            Image icon = NewPovCharacterRect("Icon", button.transform).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = Vector2.zero; icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(4f, 22f); icon.rectTransform.offsetMax = new Vector2(-4f, -4f);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.alignment = TextAlignmentOptions.Bottom;
            label.fontSizeMin = 9f; label.fontSizeMax = 12f;
            label.rectTransform.SetAsLastSibling();
            TMP_Text current = NewPovCharacterText(button.transform, "Current look", 11f);
            current.alignment = TextAlignmentOptions.TopLeft;
            current.color = new Color(0.65f, 0.9f, 1f);
            current.enableAutoSizing = true; current.fontSizeMin = 8f; current.fontSizeMax = 11f;
            current.rectTransform.anchorMin = new Vector2(0f, 1f); current.rectTransform.anchorMax = Vector2.one;
            current.rectTransform.pivot = new Vector2(0f, 1f);
            current.rectTransform.anchoredPosition = new Vector2(4f, -3f);
            current.rectTransform.sizeDelta = new Vector2(-8f, 17f);
            current.gameObject.SetActive(false);
            Outline selection = button.gameObject.AddComponent<Outline>();
            selection.effectColor = new Color(1f, 0.75f, 0.25f);
            selection.effectDistance = new Vector2(1.5f, -1.5f);
            selection.useGraphicAlpha = false;
            selection.enabled = false;
            view.CurrentMarkers.Add(current);
            view.SelectionOutlines.Add(selection);
            UITooltip hint = button.gameObject.AddComponent<UITooltip>();
            EnsureTooltipPrefab(hint);
            view.ChoiceTooltips.Add(hint);
            view.Choices.Add(button);
        }
        view.Root.gameObject.SetActive(false);
        return view;
    }

    private static void CopyPovCharacterRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta; target.anchoredPosition3D = source.anchoredPosition3D;
        target.localScale = source.localScale; target.localRotation = source.localRotation;
    }

    private static void PlacePovCharacterAfter(Transform anchor, Transform target)
    {
        // Stay with the crafting branch, below native split/confirmation dialogs
        // and hover tooltips. Never compete for the root's last sibling slot.
        int current = target.GetSiblingIndex(), previous = anchor.GetSiblingIndex();
        int wanted = previous + (current > previous ? 1 : 0);
        if (current != wanted) target.SetSiblingIndex(wanted);
    }

    private static RectTransform NewPovCharacterRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    private static TMP_Text NewPovCharacterText(Transform parent, string name, float size)
    {
        RectTransform rect = CreateTextRect(name, parent, out TMP_Text text);
        ApplyDefaultFontAsset(text);
        text.fontSize = size; text.color = new Color(1f, 0.85f, 0.5f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }

    private static Button NewPovCharacterButton(InventoryGui gui, Transform parent, string name, Action click)
    {
        RectTransform rect = NewPovCharacterRect(name, parent);
        rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        ApplyCraftingHeaderButtonStyle(gui, button);
        TMP_Text label = NewPovCharacterText(rect, "Label", 15f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 10f; label.fontSizeMax = 15f;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(3f, 2f); label.rectTransform.offsetMax = new Vector2(-3f, -2f);
        button.onClick.AddListener(() =>
        {
            try { click(); }
            catch (Exception error)
            {
                if (_povCharacterView is { } view) FailPathOfValheimanCharacterTab(view, view.Tab, error);
            }
        });
        return button;
    }

    private static void LayoutPathOfValheimanCharacter(PovCharacterView view)
    {
        float width = view.Size.x, height = view.Size.y;
        for (int i = 0; i < 3; i++)
        {
            SetTopLeftRectLayout((RectTransform)view.Tabs[i].transform, new Vector2(8f + i * (width - 16f) / 3f, -8f), new Vector2((width - 24f) / 3f, 32f));
            SetTopLeftRectLayout(view.Pages[i], new Vector2(8f, -48f), new Vector2(width - 16f, height - 56f));
            if (view.Native[i] is { } panel) panel.Fit(Vector2.zero, view.Pages[i].rect.size);
        }
        width -= 16f; height -= 56f;
        SetTopLeftRectLayout(view.Icon.rectTransform, new Vector2(4f, -4f), new Vector2(52f, 52f));
        SetTopLeftRectLayout(view.Title.rectTransform, new Vector2(64f, -4f), new Vector2(width - 72f, 60f));
        float cell = Mathf.Min((width - 20f) / 4f, (height - 230f) / 4f);
        cell = Mathf.Max(32f, cell);
        for (int i = 0; i < 16; i++) SetTopLeftRectLayout((RectTransform)view.Choices[i].transform,
            new Vector2(4f + i % 4 * (cell + 4f), -72f - i / 4 * (cell + 4f)), new Vector2(cell, cell));
        float bottom = 76f + 4 * (cell + 4f);
        SetTopLeftRectLayout((RectTransform)view.Previous.transform, new Vector2(4f, -bottom), new Vector2(32f, 28f));
        SetTopLeftRectLayout(view.Paging.rectTransform, new Vector2(40f, -bottom), new Vector2(width - 80f, 28f));
        SetTopLeftRectLayout((RectTransform)view.Next.transform, new Vector2(width - 36f, -bottom), new Vector2(32f, 28f));
        SetTopLeftRectLayout(view.RuneIcon.rectTransform, new Vector2(4f, -bottom - 36f), new Vector2(32f, 32f));
        SetTopLeftRectLayout(view.RuneCount.rectTransform, new Vector2(42f, -bottom - 36f), new Vector2(width - 48f, 32f));
        SetTopLeftRectLayout((RectTransform)view.Reset.transform, new Vector2(4f, -height + 38f), new Vector2((width - 12f) / 2f, 32f));
        SetTopLeftRectLayout((RectTransform)view.Apply.transform, new Vector2(width / 2f + 2f, -height + 38f), new Vector2((width - 12f) / 2f, 32f));
    }

    private static void SetPathOfValheimanCharacter(PovCharacterView view, bool active, int tab)
    {
        if (view.Closing || !CanShowPathOfValheimanCharacter(view.Player) || PovCharacterCraftTimer(view.Gui) >= 0f || PovCharacterDragItem(view.Gui) != null) return;
        if (!active) { view.CharacterSelected = false; ClosePathOfValheimanCharacterUi(); return; }
        if (view.Failed[tab]) return;
        if (view.Active && view.Tab == tab) return;
        ClearPathOfValheimanLookPreview();
        if (!view.Active)
        {
            bool enchantWasOpen = _povEnchantOpen!();
            _povCloseEnchant!();
            if (enchantWasOpen) PovSelectCraftTab(view.Gui);
            ClearCraftingQueue();
            CraftingController.ClearHoveredRecipe();
            HideCraftingPinnedTooltips();
            view.OriginalAlpha = view.CraftingGroup.alpha;
            view.OriginalInteractable = view.CraftingGroup.interactable;
            view.OriginalRaycasts = view.CraftingGroup.blocksRaycasts;
        }
        if (view.Native[view.Tab] is { } previous) { previous.Show(false); previous.Closed?.Invoke(); }
        view.CharacterSelected = view.Active = true; view.Tab = tab; view.Language = -1;
        view.Root.gameObject.SetActive(true);
        view.Size = view.Gui.m_crafting.rect.size;
        LayoutPathOfValheimanCharacter(view);
        for (int i = 0; i < 3; i++)
        {
            view.Pages[i].gameObject.SetActive(i == tab);
            view.Tabs[i].interactable = !view.Failed[i] && i != tab;
        }
        try
        {
            if (tab != 0)
            {
                if (view.Native[tab] == null)
                {
                    var factory = tab == 1 ? _povSkillsFactory! : _povStatsFactory!;
                    view.Native[tab] = factory.Create(view.Gui, view.Pages[tab], view.Title.font);
                }
                view.Native[tab]!.Fit(Vector2.zero, view.Pages[tab].rect.size);
                view.Native[tab]!.Show(true);
            }
            else RefreshPathOfValheimanLooks(view);
        }
        catch (Exception error) { FailPathOfValheimanCharacterTab(view, tab, error); }
    }

    private static void OpenPathOfValheimanSkills()
    {
        if (_povSkillsFactory == null || !InventoryGui.IsVisible()) return;
        UpdatePathOfValheimanCharacterUi();
        if (_povCharacterView is { Closing: false } view) SetPathOfValheimanCharacter(view, true, 1);
    }

    private static bool TryGetPathOfValheimanAppearanceTarget(PovCharacterView view, ItemData item, out bool canHide)
    {
        canHide = false;
        Player player = view.Player;
        if (_povLooks == null || !CanShowPathOfValheimanCharacter(player) ||
            !ContainsExactItemReference(player.GetInventory(), item) || !_povLooks.CanChange(item)) return false;
        if (item.m_gridPos.y == 0 && item.m_gridPos.x >= 0 && item.m_gridPos.x < player.GetInventory().GetWidth()) return true;
        foreach (string role in PovAppearanceRoles)
            if (ReferenceEquals(_pov!.Equipped(player, role), item))
            { canHide = true; return true; }
        return false;
    }

    internal static bool TrySelectPathOfValheimanAppearanceItem(InventoryGui gui, InventoryGrid grid, Vector2i pos, InventoryGrid.Modifier mod)
    {
        PovCharacterView? view = _povCharacterView;
        if (view == null || !view.Active || view.Closing || view.Tab != 0 || view.Failed[0] || view.Gui != gui ||
            mod != InventoryGrid.Modifier.Select || PovCharacterDragItem(gui) != null || PovCharacterDragGo(gui) != null ||
            grid != gui.m_playerGrid || grid.GetInventory() != view.Player.GetInventory() || IsFavoriteModifierHeld()) return false;
        try
        {
            ItemData? item = grid.GetInventory().GetItemAt(pos.x, pos.y);
            if (item == null || !TryGetPathOfValheimanAppearanceTarget(view, item, out bool canHide)) return false;
            ClearPathOfValheimanLookPreview();
            view.Target = item; view.TargetCell = pos; view.CanHide = canHide; view.Picked = null; view.Page = 0;
            view.Looks.Clear();
            if (canHide) view.Looks.Add(_povLooks!.Hidden());
            view.Looks.AddRange(_povLooks!.Choices(item));
            RefreshPathOfValheimanLooks(view);
            return true;
        }
        catch (Exception error) { FailPathOfValheimanCharacterTab(view, 0, error); return true; }
    }

    private static bool ValidatePathOfValheimanLookTarget(PovCharacterView view) =>
        view.Target != null && view.Target.m_gridPos == view.TargetCell &&
        ReferenceEquals(view.Player.GetInventory().GetItemAt(view.TargetCell.x, view.TargetCell.y), view.Target) &&
        TryGetPathOfValheimanAppearanceTarget(view, view.Target, out view.CanHide);

    private static void RefreshPathOfValheimanCurrentLook(PovCharacterView view)
    {
        ItemData? target = view.Target;
        // The reviewed PoV look key is only an invalidation signal. Resolve the
        // appearance through the public API; never edit or interpret its value.
        string? key = null;
        target?.m_customData?.TryGetValue("valheiman.equipment.v1.look", out key);
        if (ReferenceEquals(view.CurrentLookTarget, target) && view.CurrentLookKey == key) return;
        view.CurrentLook = target == null ? null : _povLooks!.Current(view.Player, target);
        view.CurrentLookTarget = target;
        view.CurrentLookKey = key;
    }

    private static void RefreshPathOfValheimanLooks(PovCharacterView view)
    {
        if (_povLooks == null) return;
        if (!ValidatePathOfValheimanLookTarget(view))
        { ClearPathOfValheimanLookPreview(); view.Target = view.Picked = null; view.Looks.Clear(); }
        RefreshPathOfValheimanCurrentLook(view);
        view.Title.text = view.Target == null ? PovCharacterText("select_target") : Localization.instance.Localize(view.Target.m_shared.m_name);
        if (view.CurrentLook != null)
        {
            string currentName = ReferenceEquals(view.CurrentLook, _povLooks.Hidden()) ? PovCharacterText("hidden") :
                Localization.instance.Localize(view.CurrentLook.m_shared.m_name);
            view.Title.text += "\n<size=14><color=#A6E6FF>" + string.Format(PovCharacterText("current_look"), currentName) + "</color></size>";
        }
        view.Icon.sprite = view.Target?.GetIcon(); view.Icon.enabled = view.Icon.sprite != null;
        int pages = Math.Max(1, (view.Looks.Count + 15) / 16);
        view.Page = Math.Min(view.Page, pages - 1);
        view.Paging.text = (view.Page + 1) + " / " + pages;
        view.Previous.interactable = view.Page > 0; view.Next.interactable = view.Page + 1 < pages;
        for (int i = 0; i < 16; i++)
        {
            int index = view.Page * 16 + i;
            Button button = view.Choices[i];
            button.gameObject.SetActive(index < view.Looks.Count);
            if (index >= view.Looks.Count) continue;
            ItemData look = view.Looks[index];
            bool hidden = ReferenceEquals(look, _povLooks.Hidden());
            Image icon = button.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = hidden ? null : look.GetIcon(); icon.enabled = icon.sprite != null;
            button.GetComponentInChildren<TMP_Text>(true).text = hidden ? PovCharacterText("hidden") : Localization.instance.Localize(look.m_shared.m_name);
            button.interactable = !hidden || view.CanHide;
            view.ChoiceTooltips[i].m_topic = "";
            view.ChoiceTooltips[i].m_text = _povLooks.IsOpen(look) ? "" : PovCharacterText("locked_preview");
            bool selected = view.Picked != null && _povLooks.SameLook(view.Picked, look);
            bool current = view.CurrentLook != null && _povLooks.SameLook(view.CurrentLook, look);
            view.CurrentMarkers[i].text = PovCharacterText("current_marker");
            view.CurrentMarkers[i].gameObject.SetActive(current);
            view.SelectionOutlines[i].enabled = selected;
            button.image.color = selected ? new Color(1f, 0.7f, 0.3f) : current ? new Color(0.65f, 0.9f, 1f) : Color.white;
        }
        RefreshPathOfValheimanRune(view);
        view.Reset.interactable = view.Target != null;
        // Let the authoritative API explain a rejected click, including noRune.
        view.Apply.interactable = view.Target != null && view.Picked != null;
    }

    private static void RefreshPathOfValheimanRune(PovCharacterView view)
    {
        ObjectDB database = ObjectDB.instance;
        if (database != null && (view.RuneDatabase != database || view.RuneIcon.sprite == null))
        {
            // Looks.RuneIcon in PoV 6.0.7 uses a display-name token as a prefab.
            // Resolve the reviewed prefab for presentation; PoV still owns costs.
            ItemData? rune = database.GetItemPrefab("PT_RuneOfGuise")?.GetComponent<ItemDrop>()?.m_itemData;
            view.RuneIcon.sprite = rune?.GetIcon();
            view.RuneTooltip.m_topic = "";
            view.RuneTooltip.m_text = rune?.m_shared.m_name ?? "$item_pt_runeofguise";
            view.RuneDatabase = database;
        }
        view.RuneIcon.enabled = view.RuneIcon.sprite != null;
        view.RuneCount.text = ": " + _povLooks!.Runes(view.Player);
    }

    private static void PickPathOfValheimanLook(PovCharacterView view, int cell)
    {
        if (!view.Active || view.Tab != 0 || !ValidatePathOfValheimanLookTarget(view)) return;
        int index = view.Page * 16 + cell;
        if (index < 0 || index >= view.Looks.Count) return;
        view.Picked = view.Looks[index];
        StartPathOfValheimanLookPreview(view);
        RefreshPathOfValheimanLooks(view);
    }

    private static void ApplyPathOfValheimanLook(PovCharacterView view, bool reset)
    {
        if (!view.Active || view.Closing || view.Tab != 0 || PovCharacterDragItem(view.Gui) != null || !ValidatePathOfValheimanLookTarget(view)) return;
        if (reset) { ClearPathOfValheimanLookPreview(); _povLooks!.Reset(view.Player, view.Target!); }
        else
        {
            if (view.Picked == null) return;
            string? reason = _povLooks!.WhyNot(view.Player, view.Target!, view.Picked, view.CanHide);
            if (reason != null) { view.Player.Message(MessageHud.MessageType.Center, PovAppearanceRefusal(reason)); return; }
            ClearPathOfValheimanLookPreview();
            reason = _povLooks.Apply(view.Player, new List<(ItemData, ItemData, bool)> { (view.Target!, view.Picked, view.CanHide) });
            if (reason != null)
            {
                view.Player.Message(MessageHud.MessageType.Center, PovAppearanceRefusal(reason));
                StartPathOfValheimanLookPreview(view);
                RefreshPathOfValheimanLooks(view); return;
            }
        }
        view.Picked = null;
        RefreshPathOfValheimanLooks(view);
    }

    private static void FailPathOfValheimanCharacterTab(PovCharacterView view, int tab, Exception error)
    {
        if (!view.Failed[tab]) Log.LogWarning($"PoV character tab {tab} disabled for this GUI: {error.GetBaseException().Message}");
        view.Failed[tab] = true;
        try { view.Native[tab]?.Show(false); view.Native[tab]?.Dispose(); }
        catch (Exception cleanup) { Log.LogWarning("PoV character view cleanup: " + cleanup.GetBaseException().Message); }
        view.Native[tab] = null;
        view.Pages[tab].gameObject.SetActive(false);
        view.Tabs[tab].interactable = false;
        view.CharacterSelected = false;
        ClosePathOfValheimanCharacterUi();
        for (int i = 0; i < 3; i++) if (!view.Failed[i]) { view.Tab = i; break; }
    }

    internal static void SuspendPathOfValheimanCharacterUi()
    {
        ClearPathOfValheimanLookPreview();
        if (_povCharacterView is not { } view) return;
        view.Closing = true;
        view.Toggle.gameObject.SetActive(false);
        // Keep the rendered page during the inventory outro; do not flash crafting.
        // PoV's own GemHand hooks own settlement of any held gem.
        foreach (var panel in view.Native)
        {
            try { panel?.Closed?.Invoke(); }
            catch (Exception error) { Log.LogWarning("PoV character view closing: " + error.GetBaseException().Message); }
        }
    }

    internal static void ClosePathOfValheimanCharacterUi()
    {
        ClearPathOfValheimanLookPreview();
        if (_povCharacterView is not { } view) return;
        if (!view.Active && !view.Closing) { if (view.Toggle != null) view.Toggle.gameObject.SetActive(false); return; }
        foreach (var panel in view.Native)
        {
            try { panel?.Show(false); panel?.Closed?.Invoke(); }
            catch (Exception error) { Log.LogWarning("PoV character view hide: " + error.GetBaseException().Message); }
        }
        if (view.Active && view.CraftingGroup != null)
        {
            view.CraftingGroup.alpha = view.OriginalAlpha;
            view.CraftingGroup.interactable = view.OriginalInteractable;
            view.CraftingGroup.blocksRaycasts = view.OriginalRaycasts;
        }
        view.Active = view.Closing = false; view.Language = -1;
        view.Target = view.Picked = null; view.Looks.Clear();
        view.CurrentLook = view.CurrentLookTarget = null; view.CurrentLookKey = null;
        if (view.Root != null) view.Root.gameObject.SetActive(false);
        if (view.Toggle != null) view.Toggle.gameObject.SetActive(false);
    }

    internal static void ClearPathOfValheimanCharacterUi()
    {
        if (_povCharacterView is not { } view) return;
        ClosePathOfValheimanCharacterUi();
        foreach (var panel in view.Native)
        {
            try { panel?.Dispose(); }
            catch (Exception error) { Log.LogWarning("PoV character view disposal: " + error.GetBaseException().Message); }
        }
        if (view.OwnGroup && view.CraftingGroup != null) UnityEngine.Object.Destroy(view.CraftingGroup);
        if (view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject);
        if (view.Toggle != null) UnityEngine.Object.Destroy(view.Toggle.gameObject);
        _povCharacterView = null;
    }

    private static string PovAppearanceRefusal(string reason) => PovCharacterText(reason switch
    {
        "selectLook" => "select_look",
        "already" => "already",
        "lockedTip" => "locked_look",
        "noRune" => "no_rune",
        _ => "unavailable_look"
    });
}

[HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
internal static class PathOfValheimanAppearanceSelectionPatch
{
    [HarmonyPriority(1000), HarmonyBefore("treextr.pathofvalheiman")]
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, Vector2i pos, InventoryGrid.Modifier mod) =>
        !InventorySlotsPlugin.TrySelectPathOfValheimanAppearanceItem(__instance, grid, pos, mod);
}
