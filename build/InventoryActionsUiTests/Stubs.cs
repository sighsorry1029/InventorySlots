using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine
{
    public class Object { public bool Destroyed; }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
    }
    public class MonoBehaviour : Component { }
    public class Sprite { }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => new(0, 0);
        public static Vector2 one => new(1, 1);
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    { public static Quaternion identity => new(0, 0, 0, 1); }
    public readonly record struct Rect(float width, float height);
    public readonly record struct Color(float r, float g, float b);
    public static class Mathf
    {
        public static int Clamp(int n, int min, int max) => Math.Clamp(n, min, max);
        public static float Clamp(float n, float min, float max) => Math.Clamp(n, min, max);
        public static float Max(float a, float b) => Math.Max(a, b);
    }
    public class GameObject : Object
    {
        public string name;
        public bool activeSelf = true;
        public RectTransform transform;
        private readonly Dictionary<Type, Component> _components = new();
        public GameObject(string name) { this.name = name; transform = new RectTransform { gameObject = this }; }
        public T? GetComponent<T>() where T : Component => _components.TryGetValue(typeof(T), out var c) ? (T)c : null;
        public T AddComponent<T>() where T : Component, new()
        { var c = new T { gameObject = this }; _components[typeof(T)] = c; return c; }
        public void SetActive(bool active) => activeSelf = active;
    }
    public class Transform : Component
    {
        public static int Writes;
        public Transform? parent;
        public readonly List<Transform> Children = new();
        public int childCount => Children.Count;
        private Vector3 _position, _scale = Vector3.one;
        private Quaternion _rotation = Quaternion.identity;
        public Vector3 localPosition { get => _position; set { ++Writes; _position = value; } }
        public Vector3 localScale { get => _scale; set { ++Writes; _scale = value; } }
        public Quaternion localRotation { get => _rotation; set { ++Writes; _rotation = value; } }
        public void SetParent(Transform? p, bool worldPositionStays)
        { ++Writes; parent?.Children.Remove(this); parent = p; p?.Children.Add(this); }
        public int GetSiblingIndex() => parent?.Children.IndexOf(this) ?? 0;
        public void SetSiblingIndex(int index)
        { ++Writes; if (parent == null) return; parent.Children.Remove(this); parent.Children.Insert(Math.Clamp(index, 0, parent.Children.Count), this); }
        public void SetAsLastSibling() => SetSiblingIndex(parent!.childCount - 1);
    }
    public class RectTransform : Transform
    {
        private Vector2 _min, _max, _pivot, _size = new(120, 30), _position, _offsetMin, _offsetMax;
        public Vector2 anchorMin { get => _min; set { ++Writes; _min = value; } }
        public Vector2 anchorMax { get => _max; set { ++Writes; _max = value; } }
        public Vector2 pivot { get => _pivot; set { ++Writes; _pivot = value; } }
        public Vector2 sizeDelta { get => _size; set { ++Writes; _size = value; } }
        public Vector2 anchoredPosition { get => _position; set { ++Writes; _position = value; } }
        public Vector2 offsetMin { get => _offsetMin; set { ++Writes; _offsetMin = value; } }
        public Vector2 offsetMax { get => _offsetMax; set { ++Writes; _offsetMax = value; } }
        public Rect rect => new(_size.x + (_max.x - _min.x) * (parent is RectTransform p ? p.rect.width : 0),
            _size.y + (_max.y - _min.y) * (parent is RectTransform q ? q.rect.height : 0));
    }
}
namespace UnityEngine.UI
{
    public class Button : Component { public bool interactable; }
    public class Image : Component { public Color color; }
}
namespace TMPro { public class TMP_Text { } }
namespace HarmonyLib
{
    public static class Harmony
    {
        public static readonly HashSet<string> Patched = new();
        private static readonly object Info = new();
        public static object? GetPatchInfo(MethodInfo method) => Patched.Contains(method.Name) ? Info : null;
    }
    public static class AccessTools
    {
        public delegate ref F FieldRef<T, F>(T obj);
        public static MethodInfo DeclaredMethod(Type type, string name, Type[] args) => type.GetMethod(name, args)!;
        public static FieldRef<T, F> FieldRefAccess<T, F>(string name) =>
            (FieldRef<T, F>)(object)(FieldRef<InventoryGrid, int>)((InventoryGrid grid) => ref grid.Width);
    }
}
public readonly record struct Vector2i(int x, int y);
public class ItemDrop { public class ItemData { } }
public class Inventory { public int Width = 8; }
public class Player { public static Player m_localPlayer = new(); public Inventory Inventory = new(); }
public class Container { public bool Allowed = true; }
public class InventoryGui
{
    public Container? m_currentContainer;
    public Button? m_takeAllButton, m_stackAllButton;
}
public class InventoryElement : Component { }
public class InventoryGrid
{
    public int Width = 8, Lookups;
    public Inventory m_inventory = Player.m_localPlayer.Inventory;
    public readonly List<InventoryElement> m_elements = new();
    public Vector2i GetButtonPos(GameObject go)
    {
        ++Lookups;
        int index = m_elements.FindIndex(e => e.gameObject == go);
        if (HarmonyLib.Harmony.Patched.Contains("GetButtonPos")) return new Vector2i(0, 0);
        return GetPositionFromIndex(index);
    }
    public Vector2i GetPositionFromIndex(int index) => HarmonyLib.Harmony.Patched.Contains("GetPositionFromIndex")
        ? new Vector2i(0, 0) : new Vector2i(index % Width, index / Width);
}
namespace InventoryActions
{
    public sealed partial class InventoryActionsPlugin
    {
        internal static InventoryActionRuntimeState Runtime = new();
        internal static int PermissionChecks, Captions, Tooltips, ControllerRegistrations, AllowedRows = 100;
        internal static readonly HashSet<InventoryElement> Shown = new();
        private const float SortButtonOutsideGap = 1f;
        private static Color FavoriteBorderColor => new(1, 1, 0);
        private static bool CanMutateContainerDirectly(Container container, bool allowLocalWithoutZNetView)
        { ++PermissionChecks; return container.Allowed; }
        private static string LocalizeUi(string token, string fallback) => fallback;
        private static void StoreAllToCurrentContainer(Player player) { }
        private static void RestockFromCurrentContainer(Player player) { }
        private static void SortCurrentContainer(Player player) { }
        private static Button EnsureActionButton(RectTransform panel, Button template, string name, string label, Action action)
        {
            ++Captions;
            var existing = panel.Children.FirstOrDefault(t => t.gameObject.name == name && !t.gameObject.Destroyed)?.gameObject.GetComponent<Button>();
            if (existing != null) return existing;
            var go = new GameObject(name); go.transform.SetParent(panel, false); return go.AddComponent<Button>();
        }
        private static void SetTooltip(Button? button, string topic, string text) => ++Tooltips;
        private static void RegisterControllerSortButton(InventoryGui gui, bool container, Button? button) => ++ControllerRegistrations;
        private static bool IsUnityNull(UnityEngine.Object value) => value.Destroyed || value is Component c && c.gameObject.Destroyed;
        private static Inventory GetPlayerInventory(Player player) => player.Inventory;
        private static void EnsureFavoritesLoaded(Player player) { }
        private static bool CanFavoriteCell(Inventory inventory, Vector2i pos) => pos.x >= 0 && pos.y >= 0 && pos.y < AllowedRows;
        private static void HideFavoriteBorder(InventoryElement element) => Shown.Remove(element);
        private static RectTransform EnsureFavoriteBorder(InventoryElement element, InventoryGridElementMarker marker)
        {
            Shown.Add(element);
            if (marker.FavoriteBorder == null)
            {
                marker.FavoriteBorder = new GameObject("border").transform;
                marker.FavoriteBorder.SetParent(element.transform, false);
            }
            return marker.FavoriteBorder;
        }
        internal static void Refresh(InventoryGui gui) => UpdateContainerActionButtons(gui);
        internal static void Release() => ReleaseContainerActionButtonLayout();
        internal static void Destroy(InventoryGui gui) => ReleaseContainerActionButtonLayout(gui);
        internal static void Refresh(InventoryGrid grid) => UpdateFavoriteBorders(grid, Player.m_localPlayer);
    }
}
