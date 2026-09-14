using System.Reflection;
using InventorySlots;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

int checks = 0;
void Check(string name, bool result)
{
    if (!result) throw new InvalidOperationException(name);
    checks++;
    Console.WriteLine("PASS " + name);
}
Type patch = typeof(EpicLootCraftDialogVisibilityPatch);
object? Call(string name, params object?[] args) =>
    patch.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);

InventorySlotsPlugin.Loaded = false;
Check("optional dependency absent does not install patch", !(bool)Call("Prepare")!);
InventorySlotsPlugin.Loaded = true;
Check("supported optional dependency installs patch", (bool)Call("Prepare")!);
Check("both dialog targets are selected", ((IEnumerable<MethodBase>)Call("TargetMethods")!).Count() == 2);

foreach (bool initiallyVisible in new[] { false, true })
{
    var dialog = new Component(new GameObject());
    var name = new TMP_Text(new GameObject(initiallyVisible, dialog.transform));
    var icon = new Image(new GameObject(initiallyVisible, dialog.transform));
    var magic = new Image(new GameObject(initiallyVisible, dialog.transform));
    var scroll = new ScrollRect(new GameObject(true, dialog.transform));
    var viewport = new GameObject(true, scroll.transform);
    var description = new TMP_Text(new GameObject(initiallyVisible, viewport.transform));
    var bar = new Scrollbar(new GameObject(initiallyVisible, scroll.transform));
    var background = new Image(bar.gameObject);
    var handle = new Image(new GameObject(true, bar.transform));
    scroll.verticalScrollbar = bar;
    Action listener = () => { };
    bar.onValueChanged = listener;
    var unrelated = new GameObject(false, dialog.transform);
    var template = new TMP_Text(new GameObject());

    Call("Prefix", dialog, name, description, icon, magic);
    string state = initiallyVisible ? "already visible" : "inherited hidden templates";
    Check(state + ": all four cloned elements active", new Graphic[] { name, description, icon, magic }.All(x => x.gameObject.activeSelf));
    Check(state + ": scrollbar and handle render and receive pointer events",
        bar.gameObject.activeSelf && background.enabled && handle.enabled && background.raycastTarget && handle.raycastTarget);
    Check(state + ": text and rarity display policy remain EpicLoot-owned",
        name.text == "original EpicLoot content" && description.text == name.text && !magic.enabled);
    Check(state + ": no new nodes, values or listeners",
        dialog.transform.Children.Count == 5 && bar.value == 0.37f && bar.onValueChanged == listener);
    Check(state + ": dialog lifetime, unrelated UI and source templates unchanged",
        !dialog.gameObject.activeSelf && !unrelated.activeSelf && !template.gameObject.activeSelf);
    Call("Prefix", dialog, name, description, icon, magic);
    Check(state + ": repeated Show remains stable", bar.onValueChanged == listener && dialog.transform.Children.Count == 5);

    // A foreign scroll reference must not reactivate the original recipe bar.
    var externalBar = new Scrollbar(new GameObject());
    scroll.verticalScrollbar = externalBar;
    Call("Prefix", dialog, name, description, icon, magic);
    Check(state + ": external scrollbar is untouched", !externalBar.gameObject.activeSelf);
    Call("Prefix", dialog, template, template, null, null);
    Check(state + ": externally owned field references stay hidden", !template.gameObject.activeSelf);
    Call("Prefix", dialog, null, null, null, null);
    Check(state + ": absent optional elements are tolerated", !dialog.gameObject.activeSelf);
}
Console.WriteLine($"PASS {checks} source-linked UI policy checks. No Unity/game execution.");
