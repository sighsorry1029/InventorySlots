using System;
using UnityEngine;
#if INVENTORY_SLOTS
using Guide = InventorySlots.InventorySlotsPlugin;
#else
using Guide = InventoryActions.InventoryActionsPlugin;
#endif

internal static class Program
{
    private static int _checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        _checks++;
    }
    private static void Equal(Rect a, Rect b, string name) => Check(
        Math.Abs(a.xMin - b.xMin) < .001 && Math.Abs(a.yMin - b.yMin) < .001 &&
        Math.Abs(a.width - b.width) < .001 && Math.Abs(a.height - b.height) < .001, name);
    private static RectTransform Panel(RectTransform parent, Vector2 size, Vector2 anchor, Vector2 position) =>
        new() { parent = parent, Size = size, Anchor = anchor, Pivot = anchor, anchoredPosition = position };

    private sealed class Fixture
    {
        public readonly RectTransform Hud = new() { Size = new(1920, 1080) };
        public readonly RectTransform Canvas = new() { Size = new(1920, 1080) };
        public readonly InventoryGui Gui = new();
        public readonly RectTransform Quick;
        public Fixture()
        {
            Gui.m_player = Panel(Canvas, new(550, 400), new(0, 1), new(40, -40));
            Gui.m_info = Panel(Canvas, new(550, 130), new(1, 1), new(-40, -40));
            Gui.m_crafting = Panel(Canvas, new(550, 750), new(1, 1), new(-40, -200));
            Quick = Panel(Canvas, new(200, 180), new(0, 1), new(620, -680));
#if INVENTORY_SLOTS
            var equipment = Panel(Gui.m_player, new(190, 350), new(0, 1), new(560, 0));
            var rail = Panel(Gui.m_crafting, new(35, 500), new(0, 1), new(-40, 0));
            Guide.ExtraPanels(equipment, Quick, rail);
#endif
            // Move a native stat panel under Player, as InventorySlots does.
            Gui.m_armor = Panel(Gui.m_player, new(60, 80), new(0, 1), new(560, 0));
            Gui.m_repairPanel = Panel(Gui.m_crafting, new(30, 80), new(0, 1), new(-32, -100));
            InventoryGui.instance = Gui;
            Guide.Awake(Gui);
        }
        public void Slide(float progress)
        {
            Gui.m_player!.anchoredPosition = new(40, -40 + 390 * progress);
            Gui.m_info!.anchoredPosition = new(-40, -40 + 390 * progress);
            Gui.m_crafting!.anchoredPosition = new(-40 + 670 * progress, -200);
            Quick.anchoredPosition = new(620, -680 - 2000 * progress);
        }
        public void Settled()
        {
            Slide(0);
            Gui.Animator!.Visible = true;
            Gui.Animator.State = new("inventory_show", 1);
        }
    }

    private static void Main()
    {
        Check(Guide.Settled(null), "No animator permits live anchor capture");
        var animation = new Animator { Visible = true, State = new("inventory_show", 1) };
        Check(Guide.Settled(animation), "Completed native show permits anchor capture");
        animation.Visible = false;
        Check(!Guide.Settled(animation), "Closing before animator evaluation forbids anchor capture");
        animation.Visible = true;
        animation.State = new("inventory_hidden", 3);
        Check(!Guide.Settled(animation), "Rapid reopen forbids anchor capture");
        animation.State = new("inventory_show", .99f);
        Check(!Guide.Settled(animation), "Partial native slide forbids anchor capture");
        animation.State = new("inventory_show", 1);
        animation.Transition = true;
        Check(!Guide.Settled(animation), "Transition forbids anchor capture");
        animation.isActiveAndEnabled = false;
        Check(Guide.Settled(animation), "Disabled animator permits current pose");
        animation.isActiveAndEnabled = true;
        animation.runtimeAnimatorController = null;
        Check(Guide.Settled(animation), "No controller permits current pose");

        var f = new Fixture();
        Rect initial = Guide.Area(f.Hud);
        Check(initial.width > 160 && initial.yMax == 500, "Initial hidden inventory uses prefab shown pose");
        foreach (string state in new[] { "inventory_hidden", "inventory_show", "inventory_hide" })
        {
            f.Gui.Animator!.Visible = state == "inventory_show";
            for (int i = 0; i <= 10; i++)
            {
                f.Gui.Animator.State = new(state, i / 11f);
                f.Slide(i / 10f);
                Equal(initial, Guide.Area(f.Hud), state + " frame " + i);
            }
        }
        // Show has changed the bool but the previous hidden state still runs.
        f.Gui.Animator!.Visible = true;
        f.Gui.Animator.State = new("inventory_hidden", 3);
        Equal(initial, Guide.Area(f.Hud), "Rapid reopen before animator evaluation");
        f.Gui.Animator.State = new("inventory_show", 2);
        f.Gui.Animator.Visible = false;
        Equal(initial, Guide.Area(f.Hud), "Hide before animator evaluation");
        f.Gui.Animator.Visible = true;
        f.Gui.Animator.Transition = true;
        Equal(initial, Guide.Area(f.Hud), "Transition must not record intermediate pose");
        f.Gui.Animator.Transition = false;
        f.Settled();
        Equal(initial, Guide.Area(f.Hud), "Completed show");
        Check(f.Gui.Lookups == 1, "Animator lookup is cached");

        // The only measured values kept across Tab are the three slide axes.
        f.Gui.m_crafting!.anchoredPosition = new(-100, -200);
        Rect moved = Guide.Area(f.Hud);
        Check(moved.xMax < initial.xMax, "Settled layout edits update the reserved edge");
        f.Gui.Animator.Visible = false;
        f.Slide(1);
        Equal(moved, Guide.Area(f.Hud), "Close retains the edited settled pose");
        f.Gui.m_crafting.Size = new(600, 750);
        Check(Guide.Area(f.Hud).xMax < moved.xMax, "Live panel widths are not cached");

        // Compare hidden animated and settled positions after resolution/scale
        // changes. This checks coordinate conversion, not a copy of the algorithm.
        f = new Fixture();
        f.Canvas.Size = f.Hud.Size = new(2560, 1440);
        f.Canvas.Scale = new(.9f, .9f);
        f.Hud.Scale = new(1.1f, 1.1f);
        Rect resized = Guide.Area(f.Hud);
        f.Slide(.75f);
        Equal(resized, Guide.Area(f.Hud), "Live anchors and parent transforms while animating");
        f.Settled();
        Equal(resized, Guide.Area(f.Hud), "Same layout after resize finishes opening");
        f.Gui.Animator!.Visible = false;
        f.Canvas.Rotation = .04f;
        Rect rotated = Guide.Area(f.Hud);
        f.Slide(.5f);
        // The independent quick-slot motion is vertical in inventory space;
        // rotation intentionally moves it horizontally in HUD space as well.
        f.Quick.anchoredPosition = new(620, -680);
        Equal(rotated, Guide.Area(f.Hud), "Native descendant correction under rotated ancestors");

        // Different HUD and inventory owners cannot inherit a previous pose.
        var old = f.Gui;
        f = new Fixture();
        Guide.Destroy(old);
        f.Slide(1);
        Equal(initial, Guide.Area(f.Hud), "Old GUI destruction does not clear new owner's cache");
        Guide.Destroy(f.Gui);
        Guide.ClearFeatureGuideLayout();
        f = new Fixture();
        Equal(initial, Guide.Area(f.Hud), "GUI recreation resets all captured positions");

        foreach (int mode in new[] { 0, 1, 2 })
        {
            f = new Fixture();
            if (mode == 0) f.Gui.Animator = null;
            if (mode == 1) f.Gui.Animator!.isActiveAndEnabled = false;
            if (mode == 2) f.Gui.Animator!.runtimeAnimatorController = null;
            Guide.Awake(f.Gui);
            f.Gui.m_info!.anchoredPosition = new(-40, -90);
            f.Gui.m_player!.anchoredPosition = new(40, -90);
            Check(Guide.Area(f.Hud).yMax == 450, "Unanimated UI honors current layout " + mode);
        }
        f = new Fixture();
        f.Gui.m_player = f.Gui.m_info = f.Gui.m_crafting = null;
        Check(Guide.Area(f.Hud).width >= 0, "Missing optional panels are safe");
#if INVENTORY_SLOTS
        QuickSlotHudTests.Run();
#endif
        Console.WriteLine($"Guide placement: {_checks} checks passed ({typeof(Guide).Namespace}).");
    }
}
