using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static class Program
{
    private static int Main()
    {
        Action[] tests =
        {
            CaptionFilteringUsesHintOwnership,
            CleanupPreservesCaptionsAndClickListeners,
            DeferredEventsCannotRestoreHints,
            OutsideReferencesAndButtonRootArePreserved,
            RetainedOwnershipSurvivesControllerRemoval,
            EmptyAndNullHintReferencesAreSafe
        };
        try
        {
            foreach (Action test in tests)
            {
                test();
                Object.FinishFrame();
            }
            Console.WriteLine($"{typeof(Plugin).Name}: {tests.Length} button caption tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CaptionFilteringUsesHintOwnership()
    {
        var fixture = new Fixture();
        AssertCaptions(fixture);
        Assert(fixture.Roots.Any(root => !root.activeSelf), "Fixture must include inactive native hints.");
        Assert(fixture.Roots.Any(root => root.activeSelf), "Fixture must include active native hints.");
        Assert(fixture.HintTexts.All(text => text.text == "Native keyboard/controller hint"),
            "Enumeration must not change hint text.");
    }

    private static void CleanupPreservesCaptionsAndClickListeners()
    {
        var fixture = new Fixture();
        int clicked = 0;
        var captionVisibility = fixture.Captions.ToDictionary(text => text, text => text.gameObject.activeSelf);
        fixture.Button.onClick.AddListener(() => clicked++);
        Plugin.RemoveHints(fixture.Button);
        Assert(fixture.Roots.All(root => !root.activeSelf && !root.DestroyRequested),
            "Owned hint roots must be hidden and preserved.");
        Assert(fixture.Button.enabled && !fixture.Button.DestroyRequested && fixture.Button.gameObject.activeSelf,
            "Native Button must remain enabled and alive.");
        Assert(fixture.Captions.All(text => text.gameObject.activeSelf == captionVisibility[text] && !text.DestroyRequested),
            "Main captions must preserve their visibility and remain alive.");
        foreach (TMP_Text text in Plugin.CaptionTexts<TMP_Text>(fixture.Button)) text.text = "Custom caption";
        foreach (Text text in Plugin.CaptionTexts<Text>(fixture.Button)) text.text = "Legacy caption";
        Assert(fixture.HintTexts.All(text => text.text == "Native keyboard/controller hint"),
            "Caption update must not rewrite native hint text.");
        Assert(fixture.LegacyHint.text == "Mouse key", "Legacy hint text must remain unchanged.");
        fixture.Button.onClick.Invoke();
        Assert(clicked == 1, "Cleanup must preserve native Button listeners.");
        AssertCaptions(fixture);
    }

    private static void DeferredEventsCannotRestoreHints()
    {
        var fixture = new Fixture();
        List<UIInputHint.InputLayoutElement> originalSettings = fixture.InputHint.m_inputLayoutSettings;
        int originalCount = originalSettings.Count;
        Plugin.RemoveHints(fixture.Button);
        Assert(!fixture.InputHint.enabled && fixture.InputHint.DestroyRequested && !fixture.InputHint.Destroyed,
            "UIInputHint must be disabled while destruction is deferred.");
        Assert(!fixture.Shortcut.enabled && fixture.Shortcut.DestroyRequested && fixture.Shortcut.m_hint == null,
            "UIGamePad must be disabled, detached, and scheduled for destruction.");
        Assert(fixture.InputHint.m_gamepadHint == null && fixture.InputHint.m_mouseKeyboardHint == null &&
               fixture.InputHint.m_gamepadMouseHint == null && fixture.InputHint.m_inputLayoutSettings.Count == 0,
            "All event-driven hint targets must be detached before OnDestroy.");
        Assert(originalSettings.Count == originalCount && !ReferenceEquals(originalSettings, fixture.InputHint.m_inputLayoutSettings),
            "Detaching a cloned controller must not clear a source/shared layout list.");
        fixture.InputHint.DeliverInputChanged();
        Assert(fixture.Roots.All(root => !root.activeSelf), "A pending native event must not reactivate hidden hints.");
    }

    private static void OutsideReferencesAndButtonRootArePreserved()
    {
        GameObject owner = new("Inventory");
        GameObject buttonObject = Child(owner, "Button");
        Button button = buttonObject.AddComponent<Button>();
        TMP_Text rootCaption = buttonObject.AddComponent<TMP_Text>();
        TMP_Text childCaption = Child(buttonObject, "GamepadHint").AddComponent<TMP_Text>();
        GameObject external = Child(owner, "Sibling hint");
        UIInputHint externalController = external.AddComponent<UIInputHint>();
        external.SetActive(false);
        int externalCalls = external.SetActiveCalls;
        int ownerCalls = owner.SetActiveCalls;
        int buttonCalls = buttonObject.SetActiveCalls;
        UIGamePad shortcut = buttonObject.AddComponent<UIGamePad>();
        shortcut.m_hint = buttonObject;
        UIGamePad childShortcut = childCaption.gameObject.AddComponent<UIGamePad>();
        childShortcut.m_hint = external;
        UIInputHint input = buttonObject.AddComponent<UIInputHint>();
        input.m_gamepadHint = external;
        input.m_mouseKeyboardHint = owner;
        input.m_gamepadMouseHint = buttonObject;
        input.m_inputLayoutSettings.Add(new() { m_hintObject = external });
        input.m_inputLayoutSettings.Add(new() { m_hintObject = buttonObject });
        input.m_inputLayoutSettings.Add(new() { m_hintObject = owner });
        AssertSame(Plugin.CaptionTexts<TMP_Text>(button), new[] { rootCaption, childCaption },
            "The button root and unrelated external hint roots cannot exclude captions.");
        Plugin.RemoveHints(button);
        input.DeliverInputChanged();
        Assert(external.SetActiveCalls == externalCalls && owner.SetActiveCalls == ownerCalls &&
               buttonObject.SetActiveCalls == buttonCalls, "Cleanup/events must not touch external objects or button root.");
        Assert(externalController.enabled && !externalController.DestroyRequested,
            "An external hint controller must remain untouched.");
        Assert(childShortcut.DestroyRequested, "Inactive/nested native controllers must also be removed.");
        Object.FinishFrame();
        AssertSame(Plugin.CaptionTexts<TMP_Text>(button), new[] { rootCaption, childCaption },
            "Root and ordinary captions must survive cleanup.");
    }

    private static void RetainedOwnershipSurvivesControllerRemoval()
    {
        var fixture = new Fixture();
        Plugin.RemoveHints(fixture.Button);
        Object.FinishFrame();
        Assert(fixture.Button.GetComponentsInChildren<UIGamePad>(true).Length == 0 &&
               fixture.Button.GetComponentsInChildren<UIInputHint>(true).Length == 0,
            "Native controllers must actually be absent for this regression test.");
        AssertCaptions(fixture);
        // A later layout or localization pass still needs the same ownership
        // even if another component temporarily activates the preserved roots.
        foreach (GameObject root in fixture.Roots) root.SetActive(true);
        AssertCaptions(fixture);
        Plugin.RemoveHints(fixture.Button);
        Assert(fixture.Roots.All(root => !root.activeSelf), "Repeated cleanup must use retained ownership.");
        AssertCaptions(fixture);
        GameObject movedOutside = fixture.Roots[0];
        movedOutside.transform.SetParent(null);
        movedOutside.SetActive(true);
        int activationCalls = movedOutside.SetActiveCalls;
        Plugin.RemoveHints(fixture.Button);
        Assert(movedOutside.SetActiveCalls == activationCalls,
            "Retained roots that leave this button subtree must no longer be touched.");
    }

    private static void EmptyAndNullHintReferencesAreSafe()
    {
        GameObject root = new("Button");
        Button button = root.AddComponent<Button>();
        TMP_Text caption = Child(root, "label").AddComponent<TMP_Text>();
        root.AddComponent<UIGamePad>();
        UIInputHint input = root.AddComponent<UIInputHint>();
        input.m_inputLayoutSettings.Add(null!);
        input.m_inputLayoutSettings.Add(new());
        AssertSame(Plugin.CaptionTexts<TMP_Text>(button), new[] { caption }, "Missing references cannot hide captions.");
        input.m_inputLayoutSettings = null!;
        Plugin.RemoveHints(button);
        input.DeliverInputChanged();
        Object.FinishFrame();
        Plugin.RemoveHints(button);
        AssertSame(Plugin.CaptionTexts<TMP_Text>(button), new[] { caption }, "Empty cleanup must preserve captions.");
    }

    private static void AssertCaptions(Fixture fixture)
    {
        AssertSame(Plugin.CaptionTexts<TMP_Text>(fixture.Button), fixture.Captions,
            "Only owned hint descendants may be excluded from TMP captions.");
        AssertSame(Plugin.CaptionTexts<Text>(fixture.Button), new[] { fixture.LegacyCaption },
            "Legacy caption filtering must use the same native ownership.");
    }

    private static void AssertSame<T>(IEnumerable<T> actual, IEnumerable<T> expected, string message)
    {
        T[] values = actual.ToArray();
        T[] wanted = expected.ToArray();
        Assert(values.Length == wanted.Length && new HashSet<T>(values).SetEquals(wanted), message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static GameObject Child(GameObject parent, string name)
    {
        GameObject result = new(name);
        result.transform.SetParent(parent.transform);
        return result;
    }

    private sealed class Fixture
    {
        public readonly Button Button;
        public readonly UIGamePad Shortcut;
        public readonly UIInputHint InputHint;
        public readonly List<TMP_Text> Captions = new();
        public readonly List<GameObject> Roots = new();
        public readonly List<TMP_Text> HintTexts = new();
        public readonly Text LegacyCaption;
        public readonly Text LegacyHint;

        public Fixture()
        {
            GameObject root = new("Custom inventory action");
            Button = root.AddComponent<Button>();
            Captions.Add(root.AddComponent<TMP_Text>());
            Captions.Add(Child(root, "label").AddComponent<TMP_Text>());
            Captions.Add(Child(root, "GamepadHint").AddComponent<TMP_Text>());
            Captions.Add(Child(root, "Key").AddComponent<TMP_Text>());
            GameObject inactiveCaption = Child(root, "Inactive caption");
            inactiveCaption.SetActive(false);
            Captions.Add(inactiveCaption.AddComponent<TMP_Text>());
            LegacyCaption = Child(root, "Legacy label").AddComponent<Text>();
            Shortcut = root.AddComponent<UIGamePad>();
            Shortcut.m_hint = HintRoot("Arbitrary controller glyph", false);
            InputHint = Child(root, "Native input controller").AddComponent<UIInputHint>();
            InputHint.gameObject.SetActive(false);
            InputHint.m_gamepadHint = HintRoot("Arbitrary primary input", true);
            InputHint.m_mouseKeyboardHint = HintRoot("Arbitrary keyboard input", false);
            InputHint.m_gamepadMouseHint = HintRoot("Arbitrary pointer input", true);
            InputHint.m_inputLayoutSettings.Add(new() { m_hintObject = HintRoot("Arbitrary layout", false) });
            // Duplicate native references must remain harmless.
            InputHint.m_inputLayoutSettings.Add(new() { m_hintObject = Shortcut.m_hint });
            LegacyHint = Child(InputHint.m_mouseKeyboardHint, "Key").AddComponent<Text>();
            LegacyHint.text = "Mouse key";

            GameObject HintRoot(string name, bool active)
            {
                GameObject hint = Child(root, name);
                hint.SetActive(active);
                Roots.Add(hint);
                TMP_Text ownText = hint.AddComponent<TMP_Text>();
                TMP_Text label = Child(Child(hint, "Nested decoration"), "label").AddComponent<TMP_Text>();
                TMP_Text key = Child(hint, "Key").AddComponent<TMP_Text>();
                foreach (TMP_Text text in new[] { ownText, label, key })
                {
                    text.text = "Native keyboard/controller hint";
                    HintTexts.Add(text);
                }
                return hint;
            }
        }
    }
}
