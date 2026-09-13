using System.Reflection;

// The production adapter and requester gate are linked into this test project.
// These stand-ins supply engine/plugin state; no Unity or network session is simulated.
public sealed class Container
{
    public bool m_checkGuardStone = true;
    public bool Access = true;
    public readonly TestTransform transform = new();
}
public sealed class TestTransform { public readonly object position = new(); }
public sealed class Piece { public long Creator; public long GetCreator() => Creator; }
public sealed class PrivateArea
{
    public bool Enabled = true;
    public bool Inside = true;
    public bool Managed;
    public readonly Piece Piece = new();
    public readonly List<KeyValuePair<long, string>> Permitted = new();
    public T? GetComponent<T>() where T : class => Piece as T;
}

namespace BepInEx.Bootstrap
{
    public sealed class TestPluginInfo { public object? Instance; }
    public static class Chainloader
    {
        public static readonly Dictionary<string, TestPluginInfo> PluginInfos = new();
    }
}

namespace STUWard
{
    public static class WardAccessApi
    {
        public static bool Handled;
        public static bool Allowed;
        public static bool Throw;
        public static bool TryCheckContainerAccess(Container container, long playerId, out bool allowed)
        {
            if (Throw) throw new InvalidOperationException("provider unavailable");
            allowed = Allowed;
            return Handled;
        }
        public static bool IsManagedWard(PrivateArea area) => area.Managed;
    }
}

namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
    {
        public static readonly List<PrivateArea> TestWards = new();
        private static readonly FieldInfo ContainerAreaWards = typeof(InventorySlotsPlugin).GetField(nameof(TestWards))!;
        private static bool CheckContainerAreaAccess(Container container, long player) => container.Access;
        private static bool IsContainerAreaWardEnabled(PrivateArea ward) => ward.Enabled;
        private static bool IsInsideContainerAreaWard(PrivateArea ward, object position, float radius) => ward.Inside;
        private static List<KeyValuePair<long, string>> ContainerAreaWardPlayers(PrivateArea ward) => ward.Permitted;
        public static bool TestAccess(Container container) => HasContainerAreaRequesterAccess(20, container);
        public static readonly TestLog Log = new();
    }
    public sealed class TestLog { public void LogWarning(string message) { } }
}

internal static class WardInteropTests
{
    internal static void Run()
    {
        void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
        var wards = InventorySlots.InventorySlotsPlugin.TestWards;
        var container = new Container();
        var managed = new PrivateArea { Managed = true };
        wards.Add(managed);
        Check(!InventorySlots.InventorySlotsPlugin.TestAccess(container), "Absent STUWard must retain explicit-permit protection");
        managed.Permitted.Add(new(20, "requester"));
        Check(InventorySlots.InventorySlotsPlugin.TestAccess(container), "Absent STUWard must retain explicit permits");
        managed.Permitted.Clear();

        BepInEx.Bootstrap.Chainloader.PluginInfos.Add("sighsorry.STUWard", new() { Instance = new TestPlugin() });
        STUWard.WardAccessApi.Handled = true;
        STUWard.WardAccessApi.Allowed = true;
        Check(InventorySlots.InventorySlotsPlugin.TestAccess(container), "Delegated managed trust must not require a second explicit permit");
        container.Access = false;
        Check(!InventorySlots.InventorySlotsPlugin.TestAccess(container), "Container privacy/other CheckAccess denial must remain");
        container.Access = true;

        var vanilla = new PrivateArea();
        vanilla.Permitted.Add(new(20, "requester"));
        wards.Insert(0, vanilla);
        STUWard.WardAccessApi.Allowed = false;
        Check(!InventorySlots.InventorySlotsPlugin.TestAccess(container), "Vanilla permit must not bypass a managed deny");
        STUWard.WardAccessApi.Handled = false;
        Check(InventorySlots.InventorySlotsPlugin.TestAccess(container), "Vanilla overlapping wards must retain any-permit policy");
        wards.Clear();
        wards.Add(new PrivateArea());
        Check(!InventorySlots.InventorySlotsPlugin.TestAccess(container), "Unmanaged wards must not inherit group trust");
        wards[0].Enabled = false;
        Check(InventorySlots.InventorySlotsPlugin.TestAccess(container), "Disabled wards must remain ignored");
        STUWard.WardAccessApi.Throw = true;
        Check(!InventorySlots.InventorySlotsPlugin.TestAccess(container), "A failing recognized API must fail closed");
        STUWard.WardAccessApi.Throw = false;
        wards.Clear();
    }
    private sealed class TestPlugin { }
}
