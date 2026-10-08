#if INVENTORY_SLOTS
using System;
using UnityEngine;
using Plugin = InventorySlots.InventorySlotsPlugin;

internal static class QuickSlotHudTests
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
    private static void Equal(Vector3 actual, Vector3 expected, string message) =>
        Check(Math.Abs(actual.x - expected.x) < .001 && Math.Abs(actual.y - expected.y) < .001, message);

    public static void Run()
    {
        var hud = new RectTransform { Size = new(1920, 1080) };
        var canvas = new RectTransform { Size = new(1920, 1080) };
        var player = new RectTransform { parent = canvas, Size = new(560, 280), Anchor = new(0, 1), Pivot = new(0, 1), anchoredPosition = new(40, -40) };
        var gridRoot = new RectTransform { parent = player, Size = new(560, 280), Anchor = new(0, 1), Pivot = new(0, 1) };
        var grid = new InventoryGrid { transform = gridRoot, m_gridRoot = gridRoot };
        var gui = new InventoryGui { m_player = player };
        InventoryGui.instance = gui;
        Plugin.Awake(gui);
        Plugin.SavedHud(new(64, -520, 0));

        Vector3 first = Plugin.HudPosition(hud, grid, 1, true, out float spacing);
        Equal(first, new(-440, -52, 0), "First hidden frame derives the panel position instead of the bottom-edge fallback");
        Check(spacing == 70, "Following uses grid spacing");
        Check(gui.Animator!.State.IsName("inventory_hidden") && !gui.Animator.Visible, "Initialization must not open or animate inventory");

        player.anchoredPosition = new(40, 350);
        Equal(Plugin.HudPosition(hud, grid, 1, true, out _), first, "Hidden native slide is removed before first Tab");
        gui.Animator.Visible = true;
        gui.Animator.State = new("inventory_show", .5f);
        player.anchoredPosition = new(40, 140);
        Equal(Plugin.HudPosition(hud, grid, 1, true, out _), first, "Interrupted Tab does not change the target");
        player.anchoredPosition = new(40, -40);
        gui.Animator.State = new("inventory_show", 1);
        Equal(Plugin.HudPosition(hud, grid, 1, true, out _), first, "Opening Tab does not jump from the first HUD position");

        gui.Animator.Visible = false;
        player.anchoredPosition = new(40, 350);
        canvas.Scale = new(.8f, .8f);
        hud.Scale = new(1.25f, 1.25f);
        Equal(Plugin.HudPosition(hud, grid, 1, true, out _), new(-281.6f, -33.28f, 0), "Different inventory and HUD canvas scales preserve the shown pose");
        Check(player.anchoredPosition == new Vector2(40, 350), "Projection never changes the inventory transform");

        Plugin.SavedHud(new(-300, -100, 0), 60);
        Equal(Plugin.HudPosition(hud, grid, 3, false, out spacing), new(-300, -100, 0), "Off preserves a valid saved position despite a different panel position");
        Check(spacing == 60, "Off preserves saved spacing");
        Plugin.SavedHud(new(64, -520, 0));
        Equal(Plugin.HudPosition(hud, grid, 3, false, out _), new(64, -322, 0), "Off repairs a clipped three-row HUD without following the panel");

        hud.Size = new(500, 300);
        var small = Plugin.HudPosition(hud, grid, 3, false, out _);
        Equal(small, new(32, 68, 0), "Changing resolution rechecks the entire HUD bounds");
        Equal(Plugin.HudPosition(hud, grid, 3, false, out _), small, "Repeated bounds validation is stable");
        hud.Pivot = new(0, 1);
        Equal(Plugin.HudPosition(hud, grid, 3, false, out _), new(32, -8, 0), "A replaced root pivot uses parent-local bounds");

        hud.Size = new(1920, 1080);
        hud.Pivot = new(.5f, .5f);
        Plugin.SavedHud(new(64, -520, 0));
        Equal(Plugin.HudPosition(hud, null, 1, true, out _), new(64, -462, 0), "Missing inventory geometry still gets a visible fallback");
        gridRoot.Size = new(0, 280);
        Equal(Plugin.HudPosition(hud, grid, 1, true, out _), new(64, -462, 0), "Unready layout does not replace the fallback");
        gridRoot.Size = new(560, 280);
        Equal(Plugin.HudPosition(hud, grid, 1, true, out _), new(-281.6f, -33.28f, 0), "Late layout readiness is retried without Tab");

        var viewport = new Rect(-960, -540, 1920, 1080);
        Equal(Plugin.ClampHud(viewport, new(-5000, 5000, 0), 70, 1), new(-952, 532, 0), "Old offscreen animation coordinates recover at top and left");
        Equal(Plugin.ClampHud(viewport, new(5000, -5000, 0), 70, 3), new(742, -322, 0), "All rows and columns fit at bottom and right");
        Equal(Plugin.ClampHud(new Rect(0, 0, 100, 100), new(500, -500, 0), 70, 3), new(8, 92, 0), "Oversized HUD uses a deterministic visible top-left");
        Equal(Plugin.ClampHud(viewport, new(5000, -5000, 0), 1, 3), new(926, -506, 0), "Minimum 24-pixel cells are included in bounds");

        var nextPlayer = new RectTransform { parent = canvas, Size = new(560, 280), Anchor = new(0, 1), Pivot = new(0, 1), anchoredPosition = new(100, -100) };
        var nextGui = new InventoryGui { m_player = nextPlayer };
        InventoryGui.instance = nextGui;
        Plugin.Awake(nextGui);
        Plugin.Destroy(gui);
        gridRoot.parent = nextPlayer;
        Equal(Plugin.HudPosition(hud, grid, 1, true, out _), new(-243.2f, -71.68f, 0), "GUI recreation cannot reuse the previous player's shown pose");
        Console.WriteLine($"Quick slot HUD layout: {_checks} checks passed.");
    }
}
#endif
