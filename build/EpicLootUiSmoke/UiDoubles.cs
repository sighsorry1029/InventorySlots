// Managed hierarchy doubles for the source-linked patch. These do not emulate
// Unity rendering, EpicLoot enchanting or Harmony detours; see README.md.
using System.Reflection;

namespace UnityEngine
{
    public class GameObject
    {
        public readonly Transform transform;
        public bool activeSelf;
        internal readonly List<Component> Components = new();
        public GameObject(bool active = false, Transform? parent = null)
        {
            activeSelf = active;
            transform = new Transform(this, parent);
            parent?.Children.Add(transform);
        }
        public void SetActive(bool active) => activeSelf = active;
    }
    public class Transform
    {
        public readonly GameObject gameObject;
        public readonly Transform? parent;
        internal readonly List<Transform> Children = new();
        internal Transform(GameObject obj, Transform? parent) { gameObject = obj; this.parent = parent; }
        public bool IsChildOf(Transform root) => this == root || (parent?.IsChildOf(root) ?? false);
    }
    public class Component
    {
        public readonly GameObject gameObject;
        public Transform transform => gameObject.transform;
        public Component(GameObject obj) { gameObject = obj; obj.Components.Add(this); }
        public T? GetComponentInParent<T>(bool includeInactive) where T : Component
        {
            for (Transform? node = transform; node != null; node = node.parent)
            {
                T? found = node.gameObject.Components.OfType<T>().FirstOrDefault();
                if (found != null) return found;
            }
            return null;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            IEnumerable<T> Walk(Transform node) => node.gameObject.Components.OfType<T>()
                .Concat(node.Children.SelectMany(Walk));
            return Walk(transform).ToArray();
        }
    }
}
namespace UnityEngine.UI
{
    public class Graphic(UnityEngine.GameObject obj) : UnityEngine.Component(obj)
    {
        public bool enabled;
        public bool raycastTarget;
    }
    public class Image(UnityEngine.GameObject obj) : Graphic(obj) { }
    public class Scrollbar(UnityEngine.GameObject obj) : UnityEngine.Component(obj)
    {
        public float value = 0.37f;
        public Action? onValueChanged;
    }
    public class ScrollRect(UnityEngine.GameObject obj) : UnityEngine.Component(obj)
    {
        public Scrollbar? verticalScrollbar;
    }
}
namespace TMPro
{
    public class TMP_Text(UnityEngine.GameObject obj) : UnityEngine.UI.Graphic(obj)
    {
        public string text = "original EpicLoot content";
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { }
    public static class AccessTools
    {
        public static Type? TypeByName(string name) => typeof(AccessTools).Assembly.GetType(name);
        public static FieldInfo? Field(Type type, string name) => type.GetField(name);
        public static MethodInfo? Method(Type type, string name) => type.GetMethod(name);
    }
}
namespace InventorySlots
{
    internal static class InventorySlotsPlugin
    {
        internal static bool Loaded = true;
        internal static bool IsEpicLootLoadedForPatches() => Loaded;
    }
}
namespace EpicLoot.Crafting
{
    public class CraftSuccessDialog(UnityEngine.GameObject obj) : UnityEngine.Component(obj)
    {
        public TMPro.TMP_Text NameText = null!, Description = null!;
        public UnityEngine.UI.Image Icon = null!, MagicBG = null!;
        public void Show(object item) { }
    }
    public class AugmentChoiceDialog(UnityEngine.GameObject obj) : UnityEngine.Component(obj)
    {
        public TMPro.TMP_Text NameText = null!, Description = null!;
        public UnityEngine.UI.Image Icon = null!, MagicBG = null!;
        public void Show(object item, int effectIndex, Action<object, int, object> callback) { }
    }
}
