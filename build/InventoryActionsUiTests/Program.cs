using InventoryActions;
using UnityEngine;
using UnityEngine.UI;
using P = InventoryActions.InventoryActionsPlugin;

int assertions = 0;
void Check(bool ok, string why) { ++assertions; if (!ok) throw new Exception(why); }
Button NewButton(Transform parent, string name, float x)
{
    var go = new GameObject(name); go.transform.SetParent(parent, false);
    go.transform.localPosition = new Vector3(x, 0, 0); return go.AddComponent<Button>();
}
InventoryGui NewGui(bool stretch = false)
{
    var parent = new GameObject("container").transform;
    var gui = new InventoryGui { m_currentContainer = new Container(), m_takeAllButton = NewButton(parent, "take", 0), m_stackAllButton = NewButton(parent, "stack", 150) };
    if (stretch) ((RectTransform)gui.m_takeAllButton.transform).anchorMax = new Vector2(1, 0);
    return gui;
}
void Stable(InventoryGui gui)
{
    Transform.Writes = 0; int permissions = P.PermissionChecks, captions = P.Captions, tooltips = P.Tooltips, controller = P.ControllerRegistrations;
    for (int i = 0; i < 20; ++i) P.Refresh(gui);
    Check(Transform.Writes == 0, "stable refresh writes no geometry");
    Check(P.PermissionChecks == permissions + 20, "ownership remains live");
    Check(P.Captions == captions + 60 && P.Tooltips == tooltips + 60 && P.ControllerRegistrations == controller + 20, "captions/tooltips/controller remain live");
}
void Relayout(InventoryGui gui, string why)
{
    Transform.Writes = 0; P.Refresh(gui); Check(Transform.Writes > 0, why); Stable(gui);
}

var gui = NewGui();
var nativeTake = (RectTransform)gui.m_takeAllButton!.transform;
var nativeOriginal = new RectTransformSnapshot(nativeTake);
P.Refresh(gui);
Check(nativeTake.sizeDelta.x == 58, "pair split once");
Stable(gui);
nativeTake.localPosition = new Vector3(50, 30, 0); Relayout(gui, "external geometry change");
var oldStore = P.Runtime.ContainerStoreAllButton!;
oldStore.gameObject.Destroyed = true;
// Unity destroyed objects compare null; drop the runtime ref to model that boundary here.
P.Runtime.ContainerStoreAllButton = null;
Relayout(gui, "destroyed owned control recreated");
Check(P.Runtime.ContainerStoreAllButton != oldStore, "fresh store control");
var renamed = P.Runtime.ContainerStoreAllButton!;
renamed.gameObject.name = "renamed-by-other-mod";
Relayout(gui, "renamed control replacement laid out in the same refresh");
Check(P.Runtime.ContainerStoreAllButton != renamed, "resolve actual control before cache match");
P.Runtime.ContainerSortButton!.transform.localScale = new Vector3(2, 2, 2);
Relayout(gui, "external scale change");
P.Runtime.ContainerSortButton!.transform.SetSiblingIndex(0);
Relayout(gui, "external sibling change");
P.Runtime.ContainerRestockButton!.transform.SetParent(new GameObject("other-parent").transform, false);
Relayout(gui, "owned control reparented");
gui.m_currentContainer!.Allowed = false; P.Refresh(gui);
Check(nativeOriginal.Matches(nativeTake), "owner loss restores native geometry");
Check(P.Runtime.ContainerButtonLayout == null && !P.Runtime.ContainerSortButton!.gameObject.activeSelf, "owner loss invalidates and hides");
gui.m_currentContainer.Allowed = true; Relayout(gui, "owner regain lays out");
P.Release(); Check(nativeOriginal.Matches(nativeTake), "hide restores native geometry");
Relayout(gui, "reopen lays out"); Check(nativeTake.sizeDelta.x == 58, "no cumulative shrink");
var oldNative = nativeTake;
gui.m_takeAllButton = NewButton(oldNative.parent!, "replacement", 0);
Relayout(gui, "native control replacement");
Check(nativeOriginal.Matches(oldNative), "old native restored on replacement");
var second = NewGui(); P.Refresh(second);
P.Destroy(gui); Stable(second);
Check(P.Runtime.ContainerButtonLayoutGui == second, "old GUI destruction does not release new GUI");
P.Destroy(second); Check(P.Runtime.ContainerButtonLayout == null && P.Runtime.TakeAllButtonOriginal == null, "current GUI teardown releases snapshots");
var stretched = NewGui(true); P.Refresh(stretched); Stable(stretched);
var parentRect = (RectTransform)stretched.m_takeAllButton!.transform.parent!;
parentRect.sizeDelta = new Vector2(500, 40); Relayout(stretched, "resolved bounds change invalidates stretched layout");
stretched.m_currentContainer = null; P.Refresh(stretched);
Check(P.Runtime.ContainerButtonLayout == null && !P.Runtime.ContainerRestockButton!.gameObject.activeSelf, "missing container restores/hides");
P.Release();

var grid = new InventoryGrid { Width = 3 }; // Inventory width remains 8.
for (int i = 0; i < 9; ++i) grid.m_elements.Add(new GameObject($"cell{i}").AddComponent<InventoryElement>());
P.Runtime.FavoriteSlots.Add(new Vector2i(1, 1)); P.Refresh(grid);
Check(grid.Lookups == 0, "unpatched refresh skips native scans");
Check(P.Shown.SetEquals(new[] { grid.m_elements[4] }), "uses actual grid width");
grid.m_elements[4].gameObject.SetActive(false); P.Refresh(grid); Check(P.Shown.Count == 0, "hidden cell hides border");
grid.m_elements[4].gameObject.SetActive(true); P.AllowedRows = 1; P.Refresh(grid); Check(P.Shown.Count == 0, "live special-row eligibility");
P.AllowedRows = 3; P.Refresh(grid); Check(P.Shown.Contains(grid.m_elements[4]), "live eligibility restored");
P.Runtime.FavoriteSlots.Clear(); P.Runtime.FavoriteSlots.Add(new Vector2i(0, 0));
HarmonyLib.Harmony.Patched.Add("GetButtonPos"); P.Refresh(grid);
Check(grid.Lookups == 9 && P.Shown.Count == 9, "late button mapper patch uses fallback");
HarmonyLib.Harmony.Patched.Clear(); HarmonyLib.Harmony.Patched.Add("GetPositionFromIndex"); P.Refresh(grid);
Check(grid.Lookups == 18 && P.Shown.Count == 9, "index mapper patch uses fallback");
HarmonyLib.Harmony.Patched.Clear(); P.Refresh(grid);
Check(P.Shown.SetEquals(new[] { grid.m_elements[0] }), "unpatched native semantics restored");
grid.m_elements.Reverse(); P.Refresh(grid);
Check(P.Shown.SetEquals(new[] { grid.m_elements[0] }), "list reorder uses current index");
grid.Width = 4; P.Runtime.FavoriteSlots.Clear(); P.Runtime.FavoriteSlots.Add(new Vector2i(0, 2)); P.Refresh(grid);
Check(P.Shown.SetEquals(new[] { grid.m_elements[8] }), "live width change");
grid.m_elements[8].gameObject.Destroyed = true; P.Shown.Clear(); P.Refresh(grid); Check(P.Shown.Count == 0, "destroyed elements skipped");
grid.m_elements[8] = new GameObject("newcell").AddComponent<InventoryElement>(); P.Refresh(grid);
Check(P.Shown.Contains(grid.m_elements[8]), "replacement cell receives favorite border");
grid.m_elements.Add(new GameObject("appendedcell").AddComponent<InventoryElement>());
P.Runtime.FavoriteSlots.Add(new Vector2i(1, 2)); P.Refresh(grid);
Check(P.Shown.Contains(grid.m_elements[9]), "appended element uses live list mapping");
Console.WriteLine($"Passed {assertions} InventoryActions UI checks.");
