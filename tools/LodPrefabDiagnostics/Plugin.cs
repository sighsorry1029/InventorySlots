using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace LodPrefabDiagnostics;

[BepInPlugin(PluginId, "LOD Prefab Diagnostics", "1.0.0")]
public sealed class Plugin : BaseUnityPlugin
{
    internal const string PluginId = "sighsorry.LodPrefabDiagnostics";
    private const float ScanInterval = 2f;
    private const int MaxDetailsPerScan = 20;
    private static Plugin? _instance;
    private readonly Harmony _harmony = new(PluginId);
    private readonly Dictionary<int, Attachment> _attachments = new();
    private readonly Dictionary<string, WeakReference<Renderer>> _reported = new();
    private readonly Queue<string> _recentAttachments = new();
    private int _pendingWarnings;
    private int _failures;
    private float _nextScan;
    private float _nextPrune;
    private float _nextSummary;
    private bool _initialScan;
    private bool _listening;
    private bool _observationFailed;
    private bool _warningSummaryWritten;
    private string _lastSummary = "";

    private sealed class Attachment
    {
        internal readonly WeakReference<GameObject> Root;
        internal readonly string Description;

        internal Attachment(GameObject root, string description)
        {
            Root = new WeakReference<GameObject>(root);
            Description = description;
        }
    }

    private void Awake()
    {
        _instance = this;
        Application.logMessageReceivedThreaded += OnUnityLog;
        _listening = true;
        try
        {
            // Validate the original private overloads before installing either hook.
            _ = ItemAttachmentPatch.TargetMethod();
            _ = ArmorAttachmentPatch.TargetMethod();
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogWarning("LOD diagnostics ready (client only). Warning-triggered scans identify duplicate renderer ownership and observed attachment prefabs. Remove this diagnostic DLL after collecting a log.");
        }
        catch (Exception error)
        {
            _harmony.UnpatchSelf();
            Logger.LogWarning($"LOD diagnostics: attachment observation unavailable ({error.GetType().Name}: {error.Message}). Scene scans remain active; item prefab attribution may be unknown.");
        }
    }

    private void OnUnityLog(string message, string stackTrace, LogType type)
    {
        // This callback can run off the Unity thread. Do not inspect any Unity object here.
        if (type == LogType.Warning && message.StartsWith("Renderer '", StringComparison.Ordinal) &&
            message.Contains(" is registered with more than one LODGroup "))
            Interlocked.Increment(ref _pendingWarnings);
    }

    private void LateUpdate()
    {
        if (ZNet.instance != null && ZNet.instance.IsDedicated())
        {
            Stop();
            enabled = false;
            return;
        }
        if (Time.unscaledTime < _nextScan) return;
        bool initial = !_initialScan && ObjectDB.instance != null;
        if (!initial && Volatile.Read(ref _pendingWarnings) == 0) return;
        _nextScan = Time.unscaledTime + ScanInterval;
        if (initial) _initialScan = true;
        int warnings = Interlocked.Exchange(ref _pendingWarnings, 0);
        try
        {
            Scan(warnings, initial);
            _failures = 0;
        }
        catch (Exception error)
        {
            Logger.LogWarning($"LOD diagnostic scan failed: {error.GetType().Name}: {error.Message}");
            if (++_failures >= 3)
            {
                Logger.LogWarning("LOD diagnostics stopped after three consecutive scan failures.");
                Stop();
                enabled = false;
            }
        }
    }

    private void Scan(int warnings, bool initial)
    {
        Prune();
        var membership = new LodMembership();
        var renderers = new Dictionary<int, Renderer>();
        var groups = new Dictionary<int, LODGroup>();
        foreach (LODGroup group in Resources.FindObjectsOfTypeAll<LODGroup>())
        {
            if (group == null || !group.gameObject.scene.IsValid() || !group.gameObject.scene.isLoaded) continue;
            int groupId = group.GetInstanceID();
            groups[groupId] = group;
            LOD[] levels = group.GetLODs();
            for (int level = 0; level < levels.Length; level++)
            {
                foreach (Renderer renderer in levels[level].renderers ?? Array.Empty<Renderer>())
                {
                    if (renderer == null) continue;
                    int rendererId = renderer.GetInstanceID();
                    renderers[rendererId] = renderer;
                    membership.Add(rendererId, groupId, level);
                }
            }
        }

        int duplicates = 0, written = 0, deferred = 0;
        foreach (var entry in membership.Renderers)
        {
            if (entry.Value.Count < 2) continue;
            duplicates++;
            Renderer renderer = renderers[entry.Key];
            int[] owners = entry.Value.Keys.OrderBy(id => id).ToArray();
            string fingerprint = entry.Key + ":" + string.Join(",", owners);
            if (_reported.TryGetValue(fingerprint, out var previous) &&
                previous.TryGetTarget(out Renderer old) && old != null && ReferenceEquals(old, renderer)) continue;
            if (written >= MaxDetailsPerScan) { deferred++; continue; }
            var text = new StringBuilder();
            text.AppendLine($"LOD duplicate: rendererId={entry.Key} type={renderer.GetType().Name} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy}");
            text.AppendLine($"  rendererPath={PathOf(renderer.transform)} scene={renderer.gameObject.scene.name}");
            Mesh? mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null) text.AppendLine($"  mesh={mesh.name} meshId={mesh.GetInstanceID()}");
            text.AppendLine("  " + DescribeSource(renderer.transform));
            foreach (int ownerId in owners)
            {
                LODGroup owner = groups[ownerId];
                text.AppendLine($"  groupId={ownerId} levels=[{string.Join(",", entry.Value[ownerId])}] enabled={owner.enabled} active={owner.gameObject.activeInHierarchy} path={PathOf(owner.transform)}");
            }
            Logger.LogWarning(text.ToString().TrimEnd());
            _reported[fingerprint] = new WeakReference<Renderer>(renderer);
            written++;
        }

        string summary = $"loadedGroups={groups.Count} duplicateRenderers={duplicates} deferredDetails={deferred}";
        if (initial || (warnings > 0 && !_warningSummaryWritten) || written > 0 || summary != _lastSummary || Time.unscaledTime >= _nextSummary)
        {
            if (warnings > 0) _warningSummaryWritten = true;
            Logger.LogWarning($"LOD scan: {summary} newDetails={written} warningsSincePreviousScan={warnings}");
            if (warnings > 0 && duplicates == 0)
            {
                Logger.LogWarning("LOD scan found no duplicate at scan time. The reported object may already have been changed/destroyed or belong to a prefab asset outside a loaded scene; the cause is not confirmed.");
                foreach (string recent in _recentAttachments)
                    Logger.LogWarning("LOD recent attachment (context only, not proof): " + recent);
            }
            _lastSummary = summary;
            _nextSummary = Time.unscaledTime + 30f;
        }
        // Bound diagnostic bookkeeping during long sessions or object churn.
        if (_reported.Count > 4096) _reported.Clear();
    }

    private string DescribeSource(Transform renderer)
    {
        for (Transform node = renderer; node != null; node = node.parent)
        {
            if (_attachments.TryGetValue(node.gameObject.GetInstanceID(), out Attachment attachment) &&
                attachment.Root.TryGetTarget(out GameObject root) && root != null && root == node.gameObject)
                return "source=attach-observed " + attachment.Description + " attachmentPath=" + PathOf(node);
        }
        // Network prefab is a containing entity, not necessarily the nested mesh's asset owner.
        for (Transform node = renderer; node != null; node = node.parent)
        {
            ZNetView view = node.GetComponent<ZNetView>();
            if (view == null) continue;
            ZDO zdo = view.GetZDO();
            if (zdo == null) continue;
            int hash = zdo.GetPrefab();
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
            return $"source=containing-network-prefab prefab={prefab?.name ?? "unknown"} hash={hash} entityPath={PathOf(node)} (nested item source unconfirmed)";
        }
        return "source=unknown rootPath=" + PathOf(renderer.root);
    }

    private void Observe(GameObject root, VisEquipment equipment, int itemHash, int variant, int quality, string method)
    {
        if (root == null || !enabled || (ZNet.instance != null && ZNet.instance.IsDedicated())) return;
        if (Time.unscaledTime >= _nextPrune) Prune();
        GameObject? prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(itemHash) : null;
        string description = $"prefab={prefab?.name ?? "unknown"} hash={itemHash} variant={variant} quality={quality} method={method} equipmentPath={PathOf(equipment.transform)}";
        if (_attachments.Count >= 4096 && !_attachments.ContainsKey(root.GetInstanceID()))
        {
            // Bounded fallback: keep current observations even in very large worlds.
            _attachments.Clear();
            Logger.LogWarning("LOD attachment history reached its limit; old mappings cleared. Unmapped objects will report a containing prefab or unknown source.");
        }
        _attachments[root.GetInstanceID()] = new Attachment(root, description);
        _recentAttachments.Enqueue($"time={Time.unscaledTime:F1} {description}");
        while (_recentAttachments.Count > 8) _recentAttachments.Dequeue();
    }

    private static void ObserveSafely(GameObject root, VisEquipment equipment, int itemHash, int variant, int quality, string method)
    {
        Plugin? instance = _instance;
        if (instance == null) return;
        try { instance.Observe(root, equipment, itemHash, variant, quality, method); }
        catch (Exception error)
        {
            // A failed probe must never break the original equipment attachment.
            if (instance._observationFailed) return;
            instance._observationFailed = true;
            instance.Logger.LogWarning($"LOD attachment observation failed: {error.GetType().Name}: {error.Message}");
        }
    }

    private void Prune()
    {
        foreach (int key in _attachments.Where(pair => !pair.Value.Root.TryGetTarget(out GameObject root) || root == null).Select(pair => pair.Key).ToArray())
            _attachments.Remove(key);
        foreach (string key in _reported.Where(pair => !pair.Value.TryGetTarget(out Renderer renderer) || renderer == null).Select(pair => pair.Key).ToArray())
            _reported.Remove(key);
        _nextPrune = Time.unscaledTime + 10f;
    }

    private static string PathOf(Transform node)
    {
        var parts = new List<string>();
        for (Transform current = node; current != null; current = current.parent)
            parts.Add(current.name + "[" + current.GetSiblingIndex() + "]");
        parts.Reverse();
        return string.Join("/", parts);
    }

    private void OnDestroy() => Stop();

    private void Stop()
    {
        if (_listening) Application.logMessageReceivedThreaded -= OnUnityLog;
        _listening = false;
        if (_instance == this) _instance = null;
        _harmony.UnpatchSelf();
        _attachments.Clear();
        _reported.Clear();
        _recentAttachments.Clear();
    }

    private static MethodInfo RequireAttachment(string name, Type returnType, params Type[] parameters)
    {
        MethodInfo? method = typeof(VisEquipment).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, parameters, null);
        if (method == null || method.ReturnType != returnType) throw new MissingMethodException(typeof(VisEquipment).FullName, name);
        return method;
    }

    [HarmonyPatch]
    private static class ItemAttachmentPatch
    {
        internal static MethodBase TargetMethod() => RequireAttachment("AttachItem", typeof(GameObject), typeof(int), typeof(int), typeof(Transform), typeof(bool), typeof(bool), typeof(int));

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(VisEquipment __instance, int itemHash, int variant, int quality, GameObject __result) =>
            ObserveSafely(__result, __instance, itemHash, variant, quality, "AttachItem");
    }

    [HarmonyPatch]
    private static class ArmorAttachmentPatch
    {
        internal static MethodBase TargetMethod() => RequireAttachment("AttachArmor", typeof(List<GameObject>), typeof(int), typeof(int), typeof(int));

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(VisEquipment __instance, int itemHash, int variant, int quality, List<GameObject> __result)
        {
            if (__result == null) return;
            foreach (GameObject root in __result) ObserveSafely(root, __instance, itemHash, variant, quality, "AttachArmor");
        }
    }
}
