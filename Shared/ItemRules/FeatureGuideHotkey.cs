using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static ConfigEntry<KeyboardShortcut> _featureGuideToggleKey = null!;
    private static int _featureGuideVisibilityFrame = -1;
    private static bool _featureGuideSavePending;
    private static float _featureGuideSaveRetryAt;
    private const string FeatureGuideToggleKeyDescription =
        "Press to cycle the feature guide: Expanded -> Collapsed -> Hidden -> Expanded. Works during gameplay with the inventory open or closed. Remembers the state locally. Ignored while typing or using menus/dialogs. None unsets this shortcut. Configuration Manager is optional. Not synced with server.";

    private static void HandleFeatureGuideToggleHotkey(Player player)
    {
        if (_featureGuideToggleKey == null ||
            _featureGuideVisibilityFrame == Time.frameCount) return;

        KeyboardShortcut shortcut = _featureGuideToggleKey.Value;
        if (shortcut.MainKey == KeyCode.None) return;
        // KeyboardShortcut.IsDown also rejects unrelated held keys (e.g. W).
        bool pressed = shortcut.MainKey is KeyCode.LeftAlt or KeyCode.RightAlt
            ? Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt)
            : Input.GetKeyDown(shortcut.MainKey);
        if (!pressed || !AreShortcutModifiersHeldAllowingAltPair(shortcut) || IsFeatureGuideHotkeyBlocked(player)) return;

        CycleFeatureGuideState(player, controller: false);
    }

    private static void CycleFeatureGuideState(Player player, bool controller)
    {
        if (_featureGuideVisibilityFrame == Time.frameCount) return;
        _featureGuideVisibilityFrame = Time.frameCount;
        bool visible = IsFeatureGuideVisible();
        bool hide = visible && IsFeatureGuideCollapsed();
        SetFeatureGuideState(visible: !hide, collapsed: visible && !hide);
        if (hide)
        {
            player.Message(MessageHud.MessageType.TopLeft,
                (controller
                    ? LocalizeUi("$" + ModName.ToLowerInvariant() + "_feature_guide_hidden_controller",
                        "{mod}: Guide hidden. Open the inventory and press {key} to show it again.")
                    : LocalizeUi("$" + ModName.ToLowerInvariant() + "_feature_guide_hidden",
                        "{mod}: Guide hidden. Press {key} to show it again."))
                    .Replace("{mod}", ModName).Replace("{key}", controller
                        ? GetControllerFeatureGuideToggleDisplay() : GetFeatureGuideToggleKeyDisplay()), 0, null);
        }
    }

    private static void ToggleFeatureGuideCollapsed()
    {
        if (IsFeatureGuideVisible() && CanInteractWithFeatureGuideToggle())
            SetFeatureGuideState(visible: true, collapsed: !IsFeatureGuideCollapsed());
    }

    private static void SaveFeatureGuideState()
    {
        _featureGuideSavePending = !SaveClientState();
        _featureGuideSaveRetryAt = Time.unscaledTime + 5f;
    }

    private static void RetryFeatureGuideStateSave(bool flush = false)
    {
        if (_featureGuideSavePending && (flush || Time.unscaledTime >= _featureGuideSaveRetryAt))
            SaveFeatureGuideState();
    }

    private static bool IsFeatureGuideHotkeyBlocked(Player player)
    {
        // Do not depend on an active guide object: this must also restore a hidden guide.
        if (_instance == null || !_instance.isActiveAndEnabled || IsDedicatedServer ||
            player == null || FavoriteMemoryAccess.IsLoading(player) || player.IsTeleporting() ||
            ShouldBlockGlobalHotkeys(player) || IsItemRuleInputBlocked() || IsControllerItemMenuOpen() ||
            UnifiedPopup.WasVisibleThisFrame() || PlayerCustomizaton.IsBarberGuiVisible() ||
            Hud.IsPieceSelectionVisible() || Hud.InRadial()) return true;

        InventoryGui gui = InventoryGui.instance;
        if (gui != null && InventoryGui.IsVisible() &&
            (IsInventoryPanelClosing(gui) ||
             (gui.m_splitDialog != null && gui.m_splitDialog.gameObject.activeInHierarchy) ||
             (gui.m_variantDialog != null && gui.m_variantDialog.gameObject.activeInHierarchy) ||
             gui.IsSkillsPanelOpen || gui.IsTextPanelOpen || gui.IsTrophisPanelOpen || gui.IsAchievementsPanelOpen)) return true;
#if INVENTORY_SLOTS
        if (_inventoryTrashConfirmDialog != null && _inventoryTrashConfirmDialog.activeInHierarchy) return true;
#else
        if (Runtime.TrashConfirmDialog != null && Runtime.TrashConfirmDialog.activeInHierarchy) return true;
#endif
        GameObject? selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected != null && (selected.GetComponentInParent<TMP_InputField>() != null ||
                                    selected.GetComponentInParent<InputField>() != null);
    }

    private static string GetFeatureGuideToggleKeyDisplay()
    {
        if (_featureGuideToggleKey == null || _featureGuideToggleKey.Value.MainKey == KeyCode.None) return "";
#if INVENTORY_SLOTS
        return _featureGuideToggleKey.Value.GetDisplayText();
#else
        return GetShortcutDisplayText(_featureGuideToggleKey.Value);
#endif
    }

    private static string AddFeatureGuideToggleHint(string guide)
    {
        // Controller mode already displays its chord on the header's toggle control.
        if (ShowControllerFeatureGuideToggle()) return guide;
        string hint;
        if (IsInventoryControllerActive())
        {
            hint = LocalizeUi("$" + ModName.ToLowerInvariant() + "_feature_guide_open_inventory_hint",
                "Open inventory → {action}")
                .Replace("{action}", GetFeatureGuideCycleHint(GetControllerFeatureGuideToggleDisplay()));
        }
        else
        {
            string key = GetFeatureGuideToggleKeyDisplay();
            if (string.IsNullOrEmpty(key)) return guide;
            hint = GetFeatureGuideCycleHint(key);
        }
        // Keep the key before the title so narrow/collapsed headers truncate the title first.
        return "<color=#FFA94D>" + hint + "</color>  " + guide;
    }

    private static string GetFeatureGuideCycleHint(string key) => (IsFeatureGuideCollapsed()
            ? LocalizeUi("$" + ModName.ToLowerInvariant() + "_feature_guide_toggle_hint", "[{key}] Hide")
            : LocalizeUi("$" + ModName.ToLowerInvariant() + "_feature_guide_collapse_hint", "[{key}] Collapse"))
            .Replace("{key}", key);
}
