using System;
using System.Reflection;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPriority : Attribute { public HarmonyPriority(int priority) { } }
    public static class Priority { public const int First = 800; public const int Last = 0; }
    public static class AccessTools
    {
        public delegate TValue FieldRef<T, TValue>(T instance);
        public static FieldRef<T, TValue> FieldRefAccess<T, TValue>(string name)
        {
            FieldInfo field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
            return instance => (TValue)field.GetValue(instance)!;
        }
        public static MethodInfo DeclaredMethod(Type type, string name) =>
            type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        public static TDelegate MethodDelegate<TDelegate>(MethodInfo method) where TDelegate : Delegate =>
            method.CreateDelegate<TDelegate>();
    }
}
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
#if INVENTORY_SLOTS
namespace InventorySlots { public sealed partial class InventorySlotsPlugin { internal static bool UsesVanillaContainerProtocol = true; } }
#else
namespace InventoryActions { public sealed partial class InventoryActionsPlugin { internal static bool UsesVanillaContainerProtocol = true; } }
#endif

public readonly record struct ZDOID(long Value) { public bool IsNone() => Value == 0; }
public sealed class ZDO
{
    public ZDOID m_uid;
    public long Owner = 77;
    public ushort OwnerRevision = 8;
    public uint DataRevision = 30;
    public int SerializedItems = 50;
    public long GetOwner() => Owner;
}
public sealed class ZNetView
{
    public ZDO Data = new();
    public bool Valid = true;
    public int Requests;
    public long RequestOwner;
    public long RequestPlayer;
    public Action? OnRequest;
    public bool IsValid() => Valid;
    public bool IsOwner() => Data.Owner == ZNet.Session;
    public ZDO GetZDO() => Data;
    public void InvokeRPC(long target, string method, params object[] args)
    {
        if (method != "RPC_RequestStack") throw new Exception("Unexpected RPC: " + method);
        Requests++;
        RequestOwner = target;
        RequestPlayer = (long)args[0];
        OnRequest?.Invoke();
    }
}
public sealed class Player
{
    public static Player m_localPlayer = null!;
    public long ID = 123;
    public long GetPlayerID() => ID;
}
public sealed class ZNet
{
    public static ZNet instance = null!;
    public static long Session = 42;
    public static long GetUID() => Session;
    public void Shutdown(bool save = true) => StopAll();
    public void ShutdownWithoutSave(bool suspending) => StopAll(suspending);
    private void StopAll(bool suspending = false) { }
}
public sealed class Inventory { public int Items; }
public sealed class Container
{
    private ZNetView m_nview;
    private uint m_lastRevision;
    private bool m_loading;
    public bool InUse;
    public bool FailLoad;
    public int LoadCount;
    public Action? DuringLoad;
    public Inventory? Inventory = new();
    public ZNetView View => m_nview;
    public Container(long id)
    {
        m_nview = new ZNetView { Data = new ZDO { m_uid = new ZDOID(id) } };
    }
    public Inventory GetInventory() => Inventory!;
    public void StackAll() { }
    public void SetLoadedRevision(uint revision) => m_lastRevision = revision;
    public void SetLoading(bool loading) => m_loading = loading;
    private bool Load()
    {
        if (m_lastRevision == m_nview.Data.DataRevision || InUse) return false;
        m_lastRevision = m_nview.Data.DataRevision;
        m_loading = true;
        LoadCount++;
        if (FailLoad) throw new Exception("Malformed inventory");
        if (Inventory != null) Inventory.Items = m_nview.Data.SerializedItems;
        DuringLoad?.Invoke();
        m_loading = false;
        return true;
    }
}
