using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    private static IEnumerable<T> GetInventoryButtonCaptionTexts<T>(Button button) where T : Component
    {
        HashSet<Transform> hintRoots = GetInventoryButtonHintRoots(button);
        foreach (T text in button.GetComponentsInChildren<T>(true))
        {
            bool isHint = false;
            foreach (Transform hintRoot in hintRoots)
                if (text.transform.IsChildOf(hintRoot))
                {
                    isHint = true;
                    break;
                }
            if (!isHint) yield return text;
        }
    }

    private static HashSet<Transform> GetInventoryButtonHintRoots(Button button)
    {
        var roots = new HashSet<Transform>();
        InventoryButtonHintRoots? retained = button.GetComponent<InventoryButtonHintRoots>();
        if (retained != null)
            foreach (GameObject root in retained.Roots) Add(root);
        foreach (UIGamePad shortcut in button.GetComponentsInChildren<UIGamePad>(true))
            Add(shortcut.m_hint);
        foreach (UIInputHint hint in button.GetComponentsInChildren<UIInputHint>(true))
        {
            Add(hint.m_gamepadHint);
            Add(hint.m_mouseKeyboardHint);
            Add(hint.m_gamepadMouseHint);
            if (hint.m_inputLayoutSettings != null)
                foreach (UIInputHint.InputLayoutElement layout in hint.m_inputLayoutSettings)
                    if (layout != null) Add(layout.m_hintObject);
        }
        return roots;

        void Add(GameObject? root)
        {
            // A cloned controller can retain a reference outside this button.
            // Neither that object nor the button/caption root belongs to its hint UI.
            if (root != null && root != button.gameObject && root.transform.IsChildOf(button.transform))
                roots.Add(root.transform);
        }
    }

    private static void RemoveClonedInventoryButtonHints(Button button)
    {
        HashSet<Transform> roots = GetInventoryButtonHintRoots(button);
        if (roots.Count > 0)
        {
            InventoryButtonHintRoots retained = button.GetComponent<InventoryButtonHintRoots>() ??
                button.gameObject.AddComponent<InventoryButtonHintRoots>();
            retained.Roots.Clear();
            foreach (Transform root in roots) retained.Roots.Add(root.gameObject);
        }

        foreach (UIGamePad shortcut in button.GetComponentsInChildren<UIGamePad>(true))
        {
            shortcut.m_hint = null;
            shortcut.enabled = false;
            Object.Destroy(shortcut);
        }
        foreach (UIInputHint hint in button.GetComponentsInChildren<UIInputHint>(true))
        {
            // Input/group events can still invoke a disabled component until
            // deferred OnDestroy unsubscribes it. Remove its targets first and
            // replace the list so even a shared source list stays untouched.
            hint.m_gamepadHint = null;
            hint.m_mouseKeyboardHint = null;
            hint.m_gamepadMouseHint = null;
            hint.m_inputLayoutSettings = new List<UIInputHint.InputLayoutElement>();
            hint.enabled = false;
            Object.Destroy(hint);
        }
        foreach (Transform root in roots) root.gameObject.SetActive(false);
    }
}

// Keep the ownership information after native hint controllers are removed.
// The GameObjects remain available for other cloned component references.
internal sealed class InventoryButtonHintRoots : MonoBehaviour
{
    public List<GameObject> Roots = new List<GameObject>();
}
