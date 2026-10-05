using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static partial class Program
{
    private static void GuideKeyFrame(params KeyCode[] keys)
    {
        Time.frameCount++;
        Input.Down.Clear();
        foreach (KeyCode key in keys) Input.Down.Add(key);
        Plugin.TestGuideHotkey();
    }

    private static void CheckFeatureGuideHotkey()
    {
        foreach (bool inventoryOpen in new[] { false, true })
        {
            Plugin.TestReset(); InventoryGui.Visible = inventoryOpen; ZInput.Exclusive = false;
            Input.Held.Add(KeyCode.W);
            GuideKeyFrame(KeyCode.F6);
            Check(Plugin.TestGuideShown && Plugin.TestGuideCollapsed && Plugin.TestGuideToggles == 1,
                "First press collapses guide during movement, independently of inventory: " + inventoryOpen);
            Check(Player.m_localPlayer.Messages.Count == 0, "Collapsing does not show a hidden-guide message");
            Check(Plugin.TestGuideState == "Collapsed", "Keyboard writes collapsed state to the shared config entry");
            Plugin.TestGuideHotkey();
            Check(Plugin.TestGuideShown && Plugin.TestGuideCollapsed && Plugin.TestGuideToggles == 1, "Repeated same-frame polls do not cycle twice");
            Input.Held.Add(KeyCode.F6); GuideKeyFrame();
            Check(Plugin.TestGuideCollapsed && Plugin.TestGuideToggles == 1, "Holding guide key does not repeat");
            GuideKeyFrame(KeyCode.F6);
            Check(!Plugin.TestGuideShown && Plugin.TestGuideToggles == 2, "Second press hides guide");
            Check(Player.m_localPlayer.Messages.Count == 1 && Player.m_localPlayer.Messages[0].Contains("F6"),
                "Hiding guide tells player how to restore it");
            Check(Plugin.TestGuideState == "Hidden", "Keyboard writes hidden state to the shared config entry");
            Plugin.TestGuideReady = false; // Hidden HUD must not be required to turn it back on.
            GuideKeyFrame(KeyCode.F6);
            Check(Plugin.TestGuideShown && !Plugin.TestGuideCollapsed && Plugin.TestGuideToggles == 3 && Player.m_localPlayer.Messages.Count == 1,
                "Third press restores expanded guide without active UI");
            Check(Plugin.TestGuideState == "Expanded", "Keyboard writes expanded state to the shared config entry");
        }

        Plugin.TestReset(); Plugin.TestGuideKey(KeyCode.F7, KeyCode.LeftAlt);
        GuideKeyFrame(KeyCode.F6); Check(!Plugin.TestGuideCollapsed, "Changed binding disables the old key immediately");
        GuideKeyFrame(KeyCode.F7); Check(!Plugin.TestGuideCollapsed, "Configured modifier is required");
        Input.Held.Add(KeyCode.RightAlt); GuideKeyFrame(KeyCode.F7);
        Check(Plugin.TestGuideCollapsed, "Remapped shortcut supports Alt pairing");
        GuideKeyFrame(KeyCode.F7);
        Check(!Plugin.TestGuideShown && Player.m_localPlayer.Messages[0].Contains("F7"), "Recovery message uses the live binding");
        Plugin.TestGuideKey(KeyCode.None); GuideKeyFrame(KeyCode.F7, KeyCode.F6);
        Check(!Plugin.TestGuideShown, "None disables the visibility shortcut");

        Action[] blockers =
        {
            () => Plugin.TestDedicated = true,
            () => Plugin.TestPluginActive(false),
            () => Player.m_localPlayer = null!,
            () => Player.m_localPlayer.Loading = true,
            () => Player.m_localPlayer.Teleporting = true,
            () => Plugin.TestBlocked = true, // Existing chat/console/config/menu guard.
            () => Plugin.TestRulesPinned = true,
            () => Plugin.TestRulesClosedFrame = Time.frameCount + 1,
            () => UnifiedPopup.Visible = true,
            () => PlayerCustomizaton.Visible = true,
            () => Hud.PieceSelection = true,
            () => Hud.Radial = true,
            () => Plugin.TestClosing = true,
            () => InventoryGui.instance.m_splitDialog = new Component(),
            () => InventoryGui.instance.m_variantDialog = new Component(),
            () => InventoryGui.instance.IsSkillsPanelOpen = true,
            () => InventoryGui.instance.IsTextPanelOpen = true,
            () => InventoryGui.instance.IsTrophisPanelOpen = true,
            () => InventoryGui.instance.IsAchievementsPanelOpen = true,
            Plugin.TestSetTrashDialog,
            () => SelectGuideInput(new TMPro.TMP_InputField(), false),
            () => SelectGuideInput(new UnityEngine.UI.InputField(), false),
            () => SelectGuideInput(new TMPro.TMP_InputField(), true),
            TapItemMenu
        };
        for (int i = 0; i < blockers.Length; i++)
        {
            Plugin.TestReset(); blockers[i](); GuideKeyFrame(KeyCode.F6);
            Check(Plugin.TestGuideShown && !Plugin.TestGuideCollapsed && Plugin.TestGuideToggles == 0, "Guide key ignored during blocked state " + i);
            Input.Down.Clear(); Time.frameCount++; Plugin.TestGuideHotkey();
            Check(Plugin.TestGuideShown && !Plugin.TestGuideCollapsed, "Blocked input never queues a delayed toggle " + i);
        }

        Plugin.TestReset(); ZInput.Exclusive = false;
        const string guide = "<b>Quick guide</b>\nBody";
        string text = Plugin.TestGuideHeader(guide);
        Check(text.Contains("[F6] Collapse") && text.IndexOf("[F6] Collapse", StringComparison.Ordinal) < text.IndexOf("Quick guide", StringComparison.Ordinal) && text.EndsWith("\nBody"),
            "Shortcut precedes the title in narrow/collapsed headers without changing the body");
        GuideKeyFrame(KeyCode.F6);
        Check(Plugin.TestGuideHeader(guide).Contains("[F6] Hide"), "Collapsed header advertises the next action");
        Plugin.TestGuideKey(KeyCode.F7, KeyCode.LeftControl);
        text = Plugin.TestGuideHeader(guide);
        Check(text.Contains("LeftControl + F7") && !text.Contains("F6"), "Header follows live shortcut changes");
        Plugin.TestGuideKey(KeyCode.None);
        Check(Plugin.TestGuideHeader(guide) == guide, "Disabled shortcut does not advertise a key");
        ZInput.Exclusive = true;
        Check(Plugin.TestGuideHeader(guide) == guide, "Controller header leaves the chord to its own toggle label");
        InventoryGui.Visible = false; Plugin.TestEnable(false);
        text = Plugin.TestGuideHeader(guide);
        Check(text.Contains("Open inventory →") && text.Contains("JoyLTrigger") && text.Contains("JoyRStick") && text.Contains("Hide") &&
            !text.Contains("F6") && text.EndsWith("\nBody"),
            "Closed controller inventory explains how to reach the chord with keyboard shortcut unset and optional hotkeys off");
        Plugin.TestGuideKey(KeyCode.F7);
        Check(!Plugin.TestGuideHeader(guide).Contains("F7"), "Controller world hint does not switch to the keyboard shortcut");
        ZInput.Exclusive = false;
        Check(Plugin.TestGuideHeader(guide).Contains("[F7] Hide") && !Plugin.TestGuideHeader(guide).Contains("Open inventory"),
            "Returning to keyboard restores the configured key without the inventory condition");
        ZInput.Exclusive = true; InventoryGui.Visible = true;
        Check(Plugin.TestGuideHeader(guide) == guide, "Opening inventory removes the world hint so the active chord is shown once");
        Plugin.TestReset(); InventoryGui.Visible = false;
        text = Plugin.TestGuideHeader(guide);
        Check(text.Contains("Open inventory →") && text.Contains("Collapse") && !text.Contains("F6"),
            "Expanded controller world guide advertises the next action through the inventory");

        Plugin.TestReset();
        Plugin.TestGuideTriangle();
        Check(Plugin.TestGuideShown && Plugin.TestGuideCollapsed, "Triangle collapses expanded guide");
        Plugin.TestGuideTriangle();
        Check(Plugin.TestGuideShown && !Plugin.TestGuideCollapsed, "Triangle expands collapsed guide without hiding");
        Plugin.TestShowGuide(false); Plugin.TestGuideTriangle();
        Check(!Plugin.TestGuideShown, "Inactive triangle cannot restore a hidden guide");

        Plugin.TestReset(); Plugin.TestGuideKey(KeyCode.None);
        foreach (string configured in new[] { "Hidden", "Collapsed", "Expanded" })
        {
            int refreshes = Plugin.TestGuideToggles;
            Plugin.TestSetGuideConfig(configured);
            Check(Plugin.TestGuideShown == (configured != "Hidden") && Plugin.TestGuideCollapsed == (configured == "Collapsed"),
                "Direct config change controls guide with shortcut unset: " + configured);
            Check(Plugin.TestGuideToggles == refreshes + 1, "Direct config change refreshes guide text immediately");
            Plugin.TestSetGuideConfig(configured);
            Check(Plugin.TestGuideToggles == refreshes + 1, "Unchanged config does not rebuild guide text");
        }
        Plugin.TestSetGuideConfig("Collapsed"); Plugin.TestGuideKey(KeyCode.F6);
        GuideKeyFrame(KeyCode.F6);
        Check(Plugin.TestGuideState == "Hidden", "Keyboard cycle follows a directly configured state");
        Plugin.TestSetGuideConfig("Expanded"); Plugin.TestGuideTriangle();
        Check(Plugin.TestGuideState == "Collapsed", "Triangle writes the same config state");

        Plugin.TestReset();
#if INVENTORY_SLOTS
        Plugin.TestLoadGuideYaml("inventory:\n  featureGuideHidden: true\n  featureGuideCollapsed: true\n  quickSlotsHudElementSpace: 81\nplayers:\n  example:\n    favoriteSlots:\n      - x: 3\n        y: 1\n        prefab: Wood\n");
#else
        Plugin.TestLoadGuideYaml("featureGuideHidden: true\nfeatureGuideCollapsed: true\nplayers:\n  example:\n    favoriteSlots:\n      - x: 3\n        y: 1\n        prefab: Wood\n");
#endif
        Check(Plugin.TestGuideShown && !Plugin.TestGuideCollapsed, "Old YAML guide fields are not migrated into config");
        string originalYaml = Plugin.TestGuideYaml;
        GuideKeyFrame(KeyCode.F6); GuideKeyFrame(KeyCode.F6);
        Check(Plugin.TestGuideYaml == originalYaml, "Guide controls never write the client-state document");
        Plugin.TestReloadClientState();
        Check(!Plugin.TestGuideShown, "Client-state reload cannot override configured guide state");
        Check(Plugin.TestSaveClientState(), "Remaining client state still saves successfully");
        Check(!Plugin.TestGuideYaml.Contains("featureGuide"), "Client-state serializer no longer writes guide preferences");
#if INVENTORY_SLOTS
        var state = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build().Deserialize<InventorySlots.InventorySlotsClientState>(Plugin.TestGuideYaml);
        Check(state.Inventory.QuickSlotsHudElementSpace == 81 && state.Players["example"].FavoriteSlots[0].Prefab == "Wood" &&
            state.Players["example"].FavoriteSlots[0].X == 3, "Removing guide fields preserves existing layout and favorite item memory");
#else
        Plugin.TestLoadFavorites("example");
        Check(Plugin.TestFavorite(3, 1) == "Wood", "Removing guide fields preserves favorite item memory");
#endif

        using (FileStream locked = File.Open(Plugin.TestGuideFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            int warnings = Plugin.TestStateWarnings.Count;
            GuideKeyFrame(KeyCode.F6);
            Check(Plugin.TestGuideState == "Expanded" && Plugin.TestStateWarnings.Count == warnings,
                "A locked client-state file cannot interfere with guide config changes");
        }
    }

    private static void SelectGuideInput(Component input, bool childSelected)
    {
        GameObject field = new(); field.Components[input.GetType()] = input;
        GameObject selected = field;
        if (childSelected) { selected = new(); selected.transform.parent = field.transform; }
        EventSystem.current!.currentSelectedGameObject = selected;
    }
}
