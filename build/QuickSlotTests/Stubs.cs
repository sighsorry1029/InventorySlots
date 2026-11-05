using UnityEngine;
using BepInEx.Configuration;
using Valheim.UI;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name, Type[] args) { } }
}
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T>(T value) { public T Value = value; }
    public sealed class KeyboardShortcut
    {
        public bool Down;
        public int[] Modifiers = [];
        public bool IsKeyDown() => Down;
    }
}
namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public HideFlags hideFlags;
        public bool Destroyed;
        public static void Destroy(Object target) => target.Destroyed = true;
        public static T Instantiate<T>(T original) where T : Object => (T)Activator.CreateInstance(original.GetType())!;
    }
    public enum HideFlags { HideAndDontSave }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public enum TextureFormat { RGBA32 }
    public enum SpriteMeshType { FullRect }
    public class Texture2D(int width, int height, TextureFormat format, bool mipChain) : Object
    {
        public int width = width, height = height;
        public TextureFormat format = format;
        public bool mipChain = mipChain;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
    }
    public static class ImageConversion
    {
        public static int Loads;
        public static bool Fail, ReadOnly;
        public static Texture2D? LastTexture;
        public static bool LoadImage(Texture2D texture, byte[] data, bool markNonReadable)
        {
            Loads++; LastTexture = texture; ReadOnly = markNonReadable;
            return !Fail && data.Length > 8 && data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71;
        }
    }
    public class Sprite : Object
    {
        public Texture2D texture = null!;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType) => new() { texture = texture };
    }
    public struct Vector2(float x, float y)
    {
        public float x = x, y = y;
        public static Vector2 zero => new(0, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    }
    public struct Vector3(float x, float y, float z = 0)
    {
        public float x = x, y = y, z = z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Rect(float x, float y, float width, float height)
    {
        public float x = x, y = y, width = width, height = height;
        public float xMin => x; public float xMax => x + width;
        public float yMin => y; public float yMax => y + height;
        public Vector2 position { get => new(x, y); set { x = value.x; y = value.y; } }
        public static Rect MinMaxRect(float left, float bottom, float right, float top) => new(left, bottom, right - left, top - bottom);
    }
    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float n) => Math.Abs(n);
        public static int Clamp(int n, int a, int b) => Math.Clamp(n, a, b);
        public static float Clamp(float n, float a, float b) => Math.Clamp(n, a, b);
    }
    public class GameObject
    {
        public bool activeSelf = true;
        internal Transform? Owner;
        public bool activeInHierarchy => activeSelf && (Owner?.parent?.gameObject.activeInHierarchy ?? true);
    }
    public class Transform : Object
    {
        public Transform? parent;
        public GameObject gameObject = new();
        public Vector3 localPosition;
        public float scale = 1;
        public readonly Dictionary<string, Transform> Children = new();
        public Transform() { gameObject.Owner = this; }
        public Transform? Find(string name) => Children.GetValueOrDefault(name);
        public void Add(string name, Transform child) { Children[name] = child; child.parent = this; }
        public Vector3 TransformPoint(Vector3 p)
        {
            var local = new Vector3(p.x * scale + localPosition.x, p.y * scale + localPosition.y);
            return parent?.TransformPoint(local) ?? local;
        }
        public Vector3 InverseTransformPoint(Vector3 p)
        {
            var local = parent?.InverseTransformPoint(p) ?? p;
            return new((local.x - localPosition.x) / scale, (local.y - localPosition.y) / scale);
        }
    }
    public class RectTransform : Transform
    {
        public Rect rect;
        public void GetWorldCorners(Vector3[] points)
        {
            points[0] = TransformPoint(new(rect.xMin, rect.yMin));
            points[1] = TransformPoint(new(rect.xMin, rect.yMax));
            points[2] = TransformPoint(new(rect.xMax, rect.yMax));
            points[3] = TransformPoint(new(rect.xMax, rect.yMin));
        }
    }
}
public struct Vector2i(int x, int y) { public int x = x, y = y; }
public class ItemDrop
{
    public class ItemData { public int m_stack = 1; public Vector2i m_gridPos; }
}
public class Inventory
{
    public List<ItemDrop.ItemData> m_inventory = [];
    public int Changes;
    public ItemDrop.ItemData? GetItemAt(int x, int y) => m_inventory.FirstOrDefault(i => i.m_gridPos.x == x && i.m_gridPos.y == y);
    public void Changed() => Changes++;
}
public class Humanoid { public Inventory Inventory = new(); public Inventory GetInventory() => Inventory; }
public class Player : Humanoid
{
    public static Player? m_localPlayer;
    public bool m_isLoading, Dead;
    public int Uses, Drops, HoverUses, Submenus;
    public bool IsDead() => Dead;
    public void UseItem(Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui) => Uses++;
}
public static class Hud { public static bool Radial; public static bool InRadial() => Radial; }
public class InventoryGrid
{
    public RectTransform m_gridRoot = new();
    public int GetInstanceID() => 1;
}
public class InventoryGui
{
    public static InventoryGui? instance;
    public InventoryGrid m_playerGrid = new();
    public RectTransform m_player = new();
}
namespace Valheim.UI
{
    public interface IRadialConfig { string LocalizedName { get; } Sprite Sprite { get; } void InitRadialConfig(RadialBase radial); }
    public class ValheimRadialConfig : IRadialConfig
    {
        public string LocalizedName => "Native"; public Sprite Sprite => new();
        public void InitRadialConfig(RadialBase radial) { }
    }
    public class HotbarGroupConfig : OtherConfig { }
    public class OtherConfig : IRadialConfig
    {
        public string LocalizedName => "Other"; public Sprite Sprite => new();
        public void InitRadialConfig(RadialBase radial) { }
    }
    public class RadialMenuElement : UnityEngine.Object
    {
        public Func<bool>? Interact, SecondaryInteract, CloseOnInteract;
        public Func<RadialBase, int, bool>? TryOpenSubRadial;
        public object? AdvancedCloseOnInteract;
    }
    public class GroupElement : RadialMenuElement
    {
        public IRadialConfig Config = null!;
        public void Init(IRadialConfig config, IRadialConfig parent, RadialBase radial) => Config = config;
    }
    public class EmptyElement : RadialMenuElement { public void Init() { } }
    public class ItemElement : RadialMenuElement
    {
        public ItemDrop.ItemData Item = null!;
        public Func<GameObject, bool>? HoverMenuInteract;
        public void Init(ItemDrop.ItemData item)
        {
            Item = item;
            SecondaryInteract = () => { Player.m_localPlayer!.Drops++; return true; };
            TryOpenSubRadial = (_, _) => { Player.m_localPlayer!.Submenus++; return true; };
            HoverMenuInteract = _ => { Player.m_localPlayer!.HoverUses++; return true; };
        }
    }
    public class RadialBase
    {
        public bool IsHoverMenu, Closed;
        public IRadialConfig CurrentConfig = new ValheimRadialConfig();
        public List<RadialMenuElement> Elements = [];
        public void ConstructRadial(List<RadialMenuElement> elements) => Elements = elements;
        public void QueuedClose() => Closed = true;
    }
    public class RadialDataSO
    {
        public GroupElement GroupElement = new(); public ItemElement ItemElement = new();
        public EmptyElement EmptyElement = new(); public HotbarGroupConfig HotbarGroupConfig = new();
    }
    public static class RadialData { public static RadialDataSO SO = new(); }
}
namespace InventorySlots
{
    public enum Toggle { Off, On }
    public enum ControllerHotkeyAction { Off }
    public enum SlotKind { Quick, Other }
    public class SlotDefinition
    {
        public string Id = ""; public string? PovCell; public SlotKind Kind = SlotKind.Quick;
        public int Index; public bool Unlocked = true, Allowed = true;
        public bool Accepts(ItemDrop.ItemData item) => Allowed;
    }
    public sealed partial class InventorySlotsPlugin
    {
        private sealed class TestLogger { public int Warnings; public void LogWarning(string message) => Warnings++; }
        private sealed class Instance { public bool isActiveAndEnabled = true; public TestLogger Logger = new(); }
        private static Instance? _instance = new();
        private static bool IsDedicatedServer, Blocked;
        private static int Unlocked = 3, HudNotifications;
        private static readonly List<SlotDefinition> Slots = [];
        private static string LocalizeUi(string key, string fallback) => fallback;
        private static bool ShouldBlockGlobalHotkeys(Player player) => Blocked;
        private static int GetUnlockedQuickSlotCount(Player player) => Unlocked;
        private static bool TryGetQuickSlotDefinition(int index, out SlotDefinition? slot) { slot = Slots.FirstOrDefault(s => s.Index == index); return slot != null; }
        private static bool TryGetSlotById(string id, out SlotDefinition? slot) { slot = Slots.FirstOrDefault(s => s.Id == id); return slot != null; }
        private static bool TryGetSlotGridPos(Inventory inventory, SlotDefinition slot, out Vector2i pos) { pos = new(slot.Index, 5); return true; }
        private static bool IsQuickSlotUnlocked(Player player, SlotDefinition slot) => slot.Unlocked && slot.Index < Unlocked;
        private static void NotifyQuickSlotHudUse(Player player, SlotDefinition slot) => HudNotifications++;
        private sealed class Yaml { public object? QuickSlots = null; }
        private static readonly Yaml _yamlConfig = new();
        private static bool ItemMatchesSlotItems(ItemDrop.ItemData? item, object items) => true;
        private static ConfigEntry<KeyboardShortcut>[]? _quickSlotHotkeys;
        private static ConfigEntry<KeyboardShortcut>? _hotbarSwitchKey = null;
        private static ConfigEntry<ControllerHotkeyAction>? _controllerHotbarSwitchButton = null;
        private static bool IsControllerHotkeyDown(ConfigEntry<ControllerHotkeyAction>? entry) => false;
        private static bool IsShortcutDownAllowingAltPair(KeyboardShortcut shortcut) => shortcut.Down;
        private static int HotbarRowsToSwitch = 1;
        private static int GetUsableRegularRows(Player player) => 4;
        private static bool TryGetSlotAtGridPos(Inventory inventory, Vector2i pos, out SlotDefinition? slot) { slot = null; return false; }
        private sealed class Panels
        {
            public Dictionary<int, RectTransform> CustomSlotPanels = new(), QuickSlotPanels = new();
            public bool DraggingQuickSlotsPanelOffset;
            public Vector2 QuickSlotsPanelRuntimeOffset, EquipmentSlotsPanelRuntimeOffset = Vector2.zero;
        }
        private static readonly Panels InventoryPanels = new();
        private static ConfigEntry<float>? _panelSnapGap = new(PanelSnapDefaultGap);
        private static ConfigEntry<Toggle>? _panelSnapping = new(Toggle.On);
    }
}
