using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;

namespace ContainerUiDiagnostics;

[BepInPlugin("sighsorry.ContainerUiDiagnostics", "Container UI Diagnostics", "1.0.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private const float SampleInterval = 2f;
    private FieldInfo _currentContainer = null!;
    private FieldInfo _gridElements = null!;
    private FieldInfo _gridWidth = null!;
    private FieldInfo _gridHeight = null!;
    private FieldInfo _containerRevision = null!;
    private InventoryGui? _gui;
    private Container? _lastContainer;
    private string? _lastSnapshot;
    private float _nextSample;
    private int _failures;
    private bool _samplingStarted;

    private void Awake()
    {
        // BepInEx can run before the client's graphics device exists. A Null
        // graphics device here does not identify a dedicated server. Sampling
        // waits for a local player and checks the running ZNet role instead.
        try
        {
            // Private in original Valheim 1.0.12. Cache lookups; never write these fields.
            _currentContainer = RequireField<InventoryGui>("m_currentContainer", typeof(Container));
            _gridElements = RequireField<InventoryGrid>("m_elements", typeof(List<InventoryElement>));
            _gridWidth = RequireField<InventoryGrid>("m_width", typeof(int));
            _gridHeight = RequireField<InventoryGrid>("m_height", typeof(int));
            _containerRevision = RequireField<Container>("m_lastRevision", typeof(uint));
            Logger.LogInfo("Standalone client container UI diagnostics active. Changed snapshots are logged at most once every 2 seconds. No server installation is required.");
        }
        catch (Exception error)
        {
            Logger.LogError($"Container UI diagnostics disabled: {error.GetType().Name}: {error.Message}");
            enabled = false;
        }
    }

    private static FieldInfo RequireField<T>(string name, Type expectedType)
    {
        FieldInfo? field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != expectedType)
            throw new MissingFieldException(typeof(T).FullName, name);
        return field;
    }

    private void LateUpdate()
    {
        if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
        if (Time.unscaledTime < _nextSample) return;
        _nextSample = Time.unscaledTime + SampleInterval;
        Player? player = Player.m_localPlayer;
        InventoryGui? gui = InventoryGui.instance;
        if (player == null || gui == null) return;
        if (_gui != gui)
        {
            _gui = gui;
            _lastContainer = null;
            _lastSnapshot = null;
            _samplingStarted = false;
        }
        if (!_samplingStarted)
        {
            _samplingStarted = true;
            Logger.LogInfo("Container UI diagnostics sampling ready: local player and inventory GUI detected.");
        }
        if (gui.m_container == null) return;
        try
        {
            Container? container = _currentContainer.GetValue(gui) as Container;
            if (container == null && !gui.m_container.gameObject.activeInHierarchy)
            {
                _lastSnapshot = null;
                return;
            }
            InventoryGrid? grid = gui.ContainerGrid;
            if (grid == null)
            {
                WriteChanged("Container UI diagnostic: visible container panel has no ContainerGrid.");
                return;
            }
            Inventory? displayedInventory = grid.GetInventory();
            string source = container != null ? "opened" : "unresolved";
            // Preview identification uses the displayed Inventory reference, not an InventorySlots dependency.
            if (container == null && displayedInventory != null)
            {
                GameObject? hover = player.GetHoverObject();
                Container? hovered = hover != null ? hover.GetComponentInParent<Container>() : null;
                if (hovered != null && ReferenceEquals(hovered.GetInventory(), displayedInventory))
                {
                    container = hovered;
                    source = "hovered";
                }
                else if (_lastContainer != null && ReferenceEquals(_lastContainer.GetInventory(), displayedInventory))
                {
                    container = _lastContainer;
                    source = "last-displayed";
                }
            }
            if (container != null) _lastContainer = container;
            string snapshot = Capture(gui, grid, container, source, displayedInventory);
            WriteChanged(snapshot);
            _failures = 0;
        }
        catch (Exception error)
        {
            Logger.LogWarning($"Container UI diagnostic probe failed: {error.GetType().Name}: {error.Message}");
            if (++_failures >= 3)
            {
                Logger.LogWarning("Container UI diagnostics disabled after repeated probe failures.");
                enabled = false;
            }
        }
    }

    private string Capture(InventoryGui gui, InventoryGrid grid, Container? container, string source, Inventory? displayedInventory)
    {
        List<InventoryElement> elements = (List<InventoryElement>)_gridElements.GetValue(grid);
        int active = 0, used = 0, icons = 0, culled = 0;
        Image? sampleIcon = null;
        foreach (InventoryElement element in elements)
        {
            if (element == null) continue;
            if (element.gameObject.activeInHierarchy) active++;
            if (element.m_used)
            {
                used++;
                if (sampleIcon == null) sampleIcon = element.m_icon;
            }
            if (element.m_icon != null && element.m_icon.enabled && element.m_icon.gameObject.activeInHierarchy)
            {
                icons++;
                if (element.m_icon.canvasRenderer.cull) culled++;
            }
        }
        var text = new StringBuilder(1536);
        Animator? animator = gui.GetComponent<Animator>();
        text.Append($"Container UI diagnostic: source={source} chest={(container != null ? container.name : "unknown")} inputVisible={InventoryGui.IsVisible()} animatorVisible={animator != null && animator.GetBool("visible")}");
        if (container != null)
        {
            text.Append($" owner={container.IsOwner()} inUse={container.IsInUse()} loadedRevision={_containerRevision.GetValue(container)}");
            AppendInventory(text, "containerInventory", container.GetInventory());
            text.Append($" gridInventoryMatches={ReferenceEquals(displayedInventory, container.GetInventory())}");
        }
        AppendInventory(text, "displayedInventory", displayedInventory);
        text.Append($" cachedGrid={_gridWidth.GetValue(grid)}x{_gridHeight.GetValue(grid)} elements={elements.Count} active={active} used={used} icons={icons} culled={culled}");
        text.Append($" templateActive={grid.m_elementPrefab != null && grid.m_elementPrefab.activeSelf}");
        AppendRect(text, "panel", gui.m_container);
        AppendRect(text, "grid", grid.transform as RectTransform);
        AppendRect(text, "root", grid.m_gridRoot);
        if (elements.Count > 0 && elements[0] != null)
        {
            AppendRect(text, "firstCell", elements[0].transform as RectTransform);
            AppendGraphic(text, "firstCellImage", elements[0].GetComponent<Image>());
        }
        AppendGraphic(text, "firstUsedIcon", sampleIcon);
        ScrollRect? scroll = grid.GetComponent<ScrollRect>();
        if (scroll != null)
        {
            // normalizedPosition's getter updates ScrollRect bounds internally.
            // Inspect existing transforms instead so the probe does not refresh its layout caches.
            text.Append($" scrollEnabled={scroll.enabled} scrollContentMatches={scroll.content == grid.m_gridRoot}");
            AppendRect(text, "scrollViewport", scroll.viewport);
        }
        foreach (CanvasGroup group in grid.GetComponentsInParent<CanvasGroup>(includeInactive: true))
            text.Append($" group[{group.name}]={group.alpha:F2}/ignoreParent:{group.ignoreParentGroups}");
        foreach (RectMask2D mask in grid.GetComponentsInParent<RectMask2D>(includeInactive: true))
            text.Append($" mask[{mask.name}]={mask.isActiveAndEnabled}/{mask.rectTransform.rect}");
        foreach (Canvas canvas in grid.GetComponentsInParent<Canvas>(includeInactive: true))
            text.Append($" canvas[{canvas.name}]={canvas.enabled}/override:{canvas.overrideSorting}/order:{canvas.sortingOrder}");
        return text.ToString();
    }

    private void WriteChanged(string snapshot)
    {
        if (string.Equals(snapshot, _lastSnapshot, StringComparison.Ordinal)) return;
        _lastSnapshot = snapshot;
        Logger.LogInfo(snapshot);
    }

    private static void AppendInventory(StringBuilder text, string label, Inventory? inventory)
    {
        if (inventory == null) { text.Append($" {label}=null"); return; }
        text.Append($" {label}={inventory.GetWidth()}x{inventory.GetHeight()}/items:{inventory.NrOfItems()}/weight:{inventory.GetTotalWeight():F1}");
    }

    private static void AppendRect(StringBuilder text, string label, RectTransform? rect)
    {
        if (rect == null) { text.Append($" {label}=null"); return; }
        text.Append($" {label}[{rect.name}]={rect.gameObject.activeSelf}/{rect.gameObject.activeInHierarchy}");
        text.Append($" size=({rect.rect.width:F1},{rect.rect.height:F1}) pos=({rect.anchoredPosition.x:F1},{rect.anchoredPosition.y:F1})");
        text.Append($" world=({rect.position.x:F1},{rect.position.y:F1}) pivot={rect.pivot} scale={rect.lossyScale} sibling={rect.GetSiblingIndex()}");
    }

    private static void AppendGraphic(StringBuilder text, string label, Graphic? graphic)
    {
        if (graphic == null) { text.Append($" {label}=null"); return; }
        text.Append($" {label}={graphic.enabled}/{graphic.gameObject.activeInHierarchy}/alpha:{graphic.color.a:F2}/rendererAlpha:{graphic.canvasRenderer.GetAlpha():F2}/cull:{graphic.canvasRenderer.cull}");
    }
}
