using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        private static readonly List<Component> PendingDestruction = new();
        public bool DestroyRequested { get; private set; }
        public bool Destroyed { get; private set; }

        public static void Destroy(Object value)
        {
            if (value is not Component component)
                throw new InvalidOperationException("Hint cleanup must preserve every GameObject.");
            value.DestroyRequested = true;
            PendingDestruction.Add(component);
        }

        public static void FinishFrame()
        {
            foreach (Component component in PendingDestruction)
            {
                component.Destroyed = true;
                component.gameObject.Components.Remove(component);
            }
            PendingDestruction.Clear();
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; } = null!;
        public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component =>
            gameObject.GetComponentsInChildren<T>(includeInactive);
    }

    public class MonoBehaviour : Component
    {
        public bool enabled = true;
    }

    public class Transform : Component
    {
        internal readonly List<Transform> Children = new();
        public Transform? parent { get; private set; }
        public void SetParent(Transform? value)
        {
            parent?.Children.Remove(this);
            parent = value;
            parent?.Children.Add(this);
        }

        public bool IsChildOf(Transform ancestor)
        {
            for (Transform? candidate = this; candidate != null; candidate = candidate.parent)
                if (candidate == ancestor) return true;
            return false;
        }
    }

    public class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public string name;
        public Transform transform { get; }
        public bool activeSelf { get; private set; } = true;
        public bool activeInHierarchy => activeSelf && (transform.parent?.gameObject.activeInHierarchy ?? true);
        public int SetActiveCalls { get; private set; }

        public GameObject(string name)
        {
            this.name = name;
            transform = AddComponent<Transform>();
        }

        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));
        public Component AddComponent(Type type)
        {
            var component = (Component)Activator.CreateInstance(type, nonPublic: true)!;
            component.gameObject = this;
            Components.Add(component);
            return component;
        }

        public T? GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            var result = new List<T>();
            Visit(this);
            return result.ToArray();

            void Visit(GameObject current)
            {
                if (!includeInactive && !current.activeInHierarchy) return;
                result.AddRange(current.Components.OfType<T>());
                foreach (Transform child in current.transform.Children) Visit(child.gameObject);
            }
        }

        public void SetActive(bool active)
        {
            SetActiveCalls++;
            activeSelf = active;
        }
    }
}

namespace UnityEngine.UI
{
    public class Text : UnityEngine.Component
    {
        public string text = "";
    }

    public class Button : UnityEngine.MonoBehaviour
    {
        public readonly ButtonClickedEvent onClick = new();
        public class ButtonClickedEvent
        {
            private event Action? Clicked;
            public void AddListener(Action action) => Clicked += action;
            public void Invoke() => Clicked?.Invoke();
        }
    }
}

namespace TMPro
{
    public class TMP_Text : UnityEngine.Component
    {
        public string text = "";
    }
}

public class UIGamePad : UnityEngine.MonoBehaviour
{
    public UnityEngine.GameObject? m_hint;
}

public class UIInputHint : UnityEngine.MonoBehaviour
{
    public class InputLayoutElement
    {
        public UnityEngine.GameObject? m_hintObject;
    }

    public UnityEngine.GameObject? m_gamepadHint;
    public UnityEngine.GameObject? m_mouseKeyboardHint;
    public UnityEngine.GameObject? m_gamepadMouseHint;
    public List<InputLayoutElement> m_inputLayoutSettings = new();

    // Native layout/group subscriptions ignore Behaviour.enabled and are only
    // removed by OnDestroy. Simulate delivery during deferred destruction.
    public void DeliverInputChanged()
    {
        if (Destroyed) return;
        m_gamepadHint?.SetActive(true);
        m_mouseKeyboardHint?.SetActive(true);
        m_gamepadMouseHint?.SetActive(true);
        foreach (InputLayoutElement layout in m_inputLayoutSettings)
            layout.m_hintObject!.SetActive(true);
    }
}
