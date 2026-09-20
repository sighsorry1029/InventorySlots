using System;
using System.Collections.Generic;
using System.Reflection;

namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T> { public T Value; public ConfigEntry(T value) => Value = value; }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type type, string name) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPriority : Attribute { public HarmonyPriority(int value) { } }
    public static class Priority { public const int First = 800; }
    public static class AccessTools
    {
        // The dispatcher only reads these accessors; reflection returns the
        // actual stub field, so selection/drag state are not duplicated here.
        public delegate TValue FieldRef<T, TValue>(T value);
        public static FieldRef<T, TValue> FieldRefAccess<T, TValue>(string name)
        {
            FieldInfo field = typeof(T).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
            return value => (TValue)field.GetValue(value)!;
        }
        public static Type? TypeByName(string name) => Type.GetType(name);
    }
}
namespace UnityEngine
{
    public class Object { public static void Destroy(Object value) { } }
    public sealed class Transform
    {
        public Transform? parent;
        public bool IsChildOf(Transform root) => this == root || (parent?.IsChildOf(root) ?? false);
    }
    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        public readonly Transform transform = new();
        public readonly Dictionary<Type, object> Components = new();
        public T? GetComponent<T>() where T : class => Components.TryGetValue(typeof(T), out object? value) ? (T)value : null;
        public void SetActive(bool value) => activeInHierarchy = value;
    }
    public class Component : Object
    {
        public GameObject gameObject = new();
        public Transform transform => gameObject.transform;
        public bool enabled = true;
        public bool isActiveAndEnabled = true;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.Empty<T>();
    }
    public static class Time { public static int frameCount; }
}
namespace UnityEngine.UI
{
    public class Button : UnityEngine.Component { }
    public class InputField : UnityEngine.Component { }
}
namespace TMPro { public class TMP_InputField : UnityEngine.Component { } }
namespace UnityEngine.EventSystems
{
    public sealed class EventSystem
    {
        public static EventSystem? current;
        public UnityEngine.GameObject? currentSelectedGameObject;
    }
}
namespace UnityEngine.InputSystem.UI
{
    public sealed class InputSystemUIInputModule
    {
        private void ProcessNavigation() { }
    }
}

public readonly record struct Vector2i(int x, int y);
public sealed class Inventory { public int Width = 8; public int Height = 4; }
public class Humanoid { public Inventory Inventory = new(); public Inventory GetInventory() => Inventory; }
public sealed class Player : Humanoid
{
    public static Player m_localPlayer = null!;
    public bool Loading;
    public bool Teleporting;
    public bool IsTeleporting() => Teleporting;
}
public sealed class UIGroupHandler { public bool IsActive; }
public sealed class InventoryGrid : UnityEngine.Component
{
    public Vector2i m_selected;
    public UIGroupHandler m_uiGroup = new();
    public Inventory? Inventory;
    public Inventory GetInventory() => Inventory!;
    private void UpdateGamepad() { }
}
public sealed class InventoryGui : UnityEngine.Component
{
    public static InventoryGui instance = null!;
    public static bool Visible;
    public InventoryGrid m_playerGrid = new();
    public InventoryGrid ContainerGrid = new();
    public UnityEngine.GameObject? m_dragGo;
    public UnityEngine.Component? m_splitDialog;
    public UnityEngine.Component? m_variantDialog;
    public bool IsSkillsPanelOpen, IsTextPanelOpen, IsTrophisPanelOpen, IsAchievementsPanelOpen;
    public static bool IsVisible() => Visible;
    private void Update() { }
    private void UpdateGamepad() { }
}
public sealed class UIGamePad : UnityEngine.Component
{
    public UnityEngine.GameObject? m_hint;
    public bool ButtonPressed() => true;
}
public sealed class ZInput
{
    public static ZInput instance = new();
    public static bool Exclusive = true;
    public static readonly HashSet<string> Held = new();
    public static readonly HashSet<string> Down = new();
    public static readonly List<string> ResetCalls = new();
    public static bool IsExclusiveGamepadActive() => Exclusive;
    public string GetBoundKeyString(string action, bool gamepad) => action;
    public static bool GetButton(string action) => Held.Contains(action);
    public static bool GetButtonDown(string action) => Down.Contains(action);
    public static void ResetButtonStatus(string action)
    {
        Held.Remove(action); Down.Remove(action); ResetCalls.Add(action);
    }
    public static void Press(params string[] actions)
    {
        foreach (string action in actions) { Held.Add(action); Down.Add(action); }
    }
}
