using BepInEx.Configuration;
using UnityEngine;
using Valheim.UI;
using ItemData = ItemDrop.ItemData;

InventorySlots.InventorySlotsPlugin.RunChecks();

namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
    {
        private static int Checks;
        private static void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
            Checks++;
        }
        private static void Equal(Vector2 expected, Vector2 actual, string name) =>
            Check(Math.Abs(expected.x - actual.x) < .001f && Math.Abs(expected.y - actual.y) < .001f,
                $"{name}: expected ({expected.x},{expected.y}), got ({actual.x},{actual.y})");

        public static void RunChecks()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                args.Name.Split(',')[0] == "UnityEngine.ImageConversionModule" ? typeof(ImageConversion).Assembly : null;
            RadialChecks();
            IconChecks();
            SnapChecks();
            Console.WriteLine($"Quick-slot radial/input and panel snapping: {Checks} checks passed (production methods, managed doubles; no Unity/game execution).");
        }

        private static void RadialChecks()
        {
            Player player = Player.m_localPlayer = new();
            Inventory inventory = player.GetInventory();
            for (int i = 0; i < 9; i++) Slots.Add(new() { Id = "quick." + i, Index = i });
            ItemData item = new() { m_gridPos = new(1, 5), m_stack = 3 };
            inventory.m_inventory.Add(item);
            RadialBase radial = new();
            var native = new EmptyElement();
            var main = new List<RadialMenuElement> { native };
            AddQuickSlotRadialGroup(radial, main);
            Check(main.Count == 2 && main[0] == native && main[1] is GroupElement, "Native menu entries retained with one added group");
            AddQuickSlotRadialGroup(radial, main);
            Check(main.Count == 2, "Repeated hook does not duplicate a group");
            var config = ((GroupElement)main[1]).Config;
            Check(config.LocalizedName == "Quick slots", "Group uses localized label");
            foreach (IRadialConfig foreign in new IRadialConfig[] { new OtherConfig(), new HotbarGroupConfig(), config })
            {
                radial.CurrentConfig = foreign;
                var list = new List<RadialMenuElement>();
                AddQuickSlotRadialGroup(radial, list);
                Check(list.Count == 0, "Non-main menus remain untouched");
            }
            radial.CurrentConfig = new ValheimRadialConfig();
            void NoGroup(string name)
            {
                var list = new List<RadialMenuElement>();
                AddQuickSlotRadialGroup(radial, list);
                Check(list.Count == 0, name);
            }
            radial.IsHoverMenu = true; NoGroup("No entry in interactable hover menu"); radial.IsHoverMenu = false;
            IsDedicatedServer = true; NoGroup("No entry on dedicated server"); IsDedicatedServer = false;
            _instance!.isActiveAndEnabled = false; NoGroup("Disabled plugin creates no group");
            config.InitRadialConfig(radial); Check(radial.Closed, "Disabled plugin closes an already captured group");
            _instance.isActiveAndEnabled = true; radial.Closed = false;
            Unlocked = 0; NoGroup("No entry with zero unlocked slots");
            config.InitRadialConfig(radial); Check(radial.Closed, "Locked group closes if opened after unlock loss");
            foreach (int count in new[] { 3, 6, 9 })
            {
                Unlocked = count; radial.CurrentConfig = config; config.InitRadialConfig(radial);
                Check(radial.Elements.Count == count, "Open menu reflects 3/6/9 unlocked slots");
                Check(radial.Elements[0] is EmptyElement && radial.Elements[1] is ItemElement && radial.Elements[2] is EmptyElement,
                    "Empty unlocked slots preserve slot order");
            }
            ItemElement entry = (ItemElement)radial.Elements[1];
            Hud.Radial = true;
            Check(entry.Interact!(), "Primary action works while radial is open");
            Check(player.Uses == 1 && inventory.Changes == 1 && HudNotifications == 1, "One use, inventory notification and HUD activation");
            Check(entry.CloseOnInteract!() && entry.AdvancedCloseOnInteract == null, "Native hotbar close contract");
            Check(entry.SecondaryInteract!() && player.Drops == 1, "Native secondary action retained");
            Check(entry.TryOpenSubRadial!(radial, 1) && player.Submenus == 1, "Native subradial entry retained");
            Check(entry.HoverMenuInteract!(new()) && player.HoverUses == 1, "Native reused hover action retained");

            _quickSlotHotkeys = Enumerable.Range(0, 9).Select(_ => new ConfigEntry<KeyboardShortcut>(new())).ToArray();
            _quickSlotHotkeys[1].Value.Down = true;
            HandleQuickSlotHotkeys(player);
            Check(player.Uses == 1, "Raw keyboard input cannot double-use while radial is open");
            Hud.Radial = false; HandleQuickSlotHotkeys(player);
            Check(player.Uses == 2 && HudNotifications == 2, "Keyboard quickslots still work outside radial");

            void Rejected(string name)
            {
                int uses = player.Uses, changes = inventory.Changes, hud = HudNotifications;
                Check(!entry.Interact!(), name);
                Check(player.Uses == uses && inventory.Changes == changes && HudNotifications == hud, "Rejected action has no use side effects");
            }
            ItemData replacement = new() { m_gridPos = item.m_gridPos };
            inventory.m_inventory.Remove(item); inventory.m_inventory.Add(replacement);
            Rejected("LastUsed callback cannot use a replacement item in the same slot");
            Check(!entry.SecondaryInteract!() && !entry.TryOpenSubRadial!(radial, 1) && !entry.HoverMenuInteract!(new()),
                "Stale secondary actions cannot affect a replacement item");
            inventory.m_inventory.Clear(); inventory.m_inventory.Add(item);
            item.m_gridPos = new(1, 0); Rejected("Moved item cannot be used through its old quickslot"); item.m_gridPos = new(1, 5);
            item.m_stack = 0; Rejected("Consumed stack cannot be reused"); item.m_stack = 3;
            Slots[1].Unlocked = false; Rejected("Newly locked slot cannot execute"); Slots[1].Unlocked = true;
            Slots[1].Allowed = false; Rejected("Live item rule change is respected"); Slots[1].Allowed = true;
            Slots[1].PovCell = "food.1"; Rejected("PoV-owned slots cannot bypass their action policy"); Slots[1].PovCell = null;
            Slots[1].Kind = SlotKind.Other; Rejected("Rebuilt non-quick slot is rejected"); Slots[1].Kind = SlotKind.Quick;
            SlotDefinition previous = Slots[1]; Slots[1] = new() { Id = previous.Id, Index = previous.Index };
            Check(entry.Interact!(), "Equivalent rebuilt definition resolves by stable slot ID");
            Player.m_localPlayer = new(); Rejected("Character switch invalidates retained entry"); Player.m_localPlayer = player;
            player.Inventory = new(); Rejected("Inventory replacement invalidates retained entry"); player.Inventory = inventory;
            player.Dead = true; Rejected("Dead player cannot use entry"); player.Dead = false;
            player.m_isLoading = true; Rejected("Loading player cannot use entry"); player.m_isLoading = false;
            Blocked = true; Rejected("Input dialog owns interaction"); Blocked = false;
            _instance.isActiveAndEnabled = false; Rejected("Disabled plugin cannot execute old callback"); _instance.isActiveAndEnabled = true;
            _instance = null; Rejected("Destroyed plugin cannot execute old callback"); _instance = new();
        }

        private static void IconChecks()
        {
            Sprite icon = QuickSlotsRadialConfig.Instance.Sprite;
            Check(icon.name == "InventorySlots_QuickSlotsRadialIcon", "Group uses its embedded quick-slot artwork");
            Check(ReferenceEquals(icon, GetQuickSlotRadialIcon()) && ImageConversion.Loads == 1,
                "Repeated menu reads do not decode or allocate another texture");
            Check(ImageConversion.ReadOnly, "CPU texture copy is released after loading");
            DestroyQuickSlotRadialIcon();
            Check(icon.Destroyed && icon.texture.Destroyed, "Plugin teardown frees both sprite and texture");
            ImageConversion.Fail = true;
            int warnings = _instance!.Logger.Warnings;
            GetQuickSlotRadialIcon(); GetQuickSlotRadialIcon();
            Check(ImageConversion.Loads == 2 && _quickSlotRadialIcon == null && ImageConversion.LastTexture!.Destroyed,
                "Decode failure frees texture and does not retry on every property read");
            Check(_instance.Logger.Warnings == warnings + 1, "Icon failure is reported once");
            DestroyQuickSlotRadialIcon(); ImageConversion.Fail = false;
            Check(GetQuickSlotRadialIcon().name == icon.name && ImageConversion.Loads == 3,
                "A new plugin lifetime can reload the artwork");
            DestroyQuickSlotRadialIcon();
        }

        private static void SnapChecks()
        {
            Rect inventory = new(0, 0, 800, 400), moving = new(805, 180, 280, 210);
            Equal(new(-10, 10), GetPanelSnapDelta(moving, inventory, null), "Default -5 gap and inventory top snap at fixed distance 10");
            Equal(new(-5, 10), GetPanelSnapDelta(moving, inventory, null, 0), "Zero gap aligns background rectangles");
            Equal(new(-10, 0), GetPanelSnapDelta(new(805, 170, 280, 210), inventory, null), "Exact 10-unit threshold included");
            Equal(Vector2.zero, GetPanelSnapDelta(new(806, 170, 280, 210), inventory, null), "Beyond fixed 10-unit threshold ignored");
            Rect farInventory = new(-1000, -1000, 100, 100), other = new(800, 190, 280, 210);
            Equal(new(-6, 10), GetPanelSnapDelta(new(806, -25, 280, 210), farInventory, other), "Align left and attach below with default overlap");
            Equal(new(0, -10), GetPanelSnapDelta(new(800, 405, 280, 210), farInventory, other), "Attach above with default overlap");
            Equal(new(0, -5), GetPanelSnapDelta(new(800, 305, 280, 100), farInventory, other), "Gap does not offset same top edges");
            Equal(new(0, -10), GetPanelSnapDelta(new(800, 200, 280, 100), farInventory, other), "Gap does not offset same bottom edges");
            Equal(Vector2.zero, GetPanelSnapDelta(new(802, 1000, 280, 210), inventory, other), "Far orthogonal range does not attract");
            Equal(new(2, 0), GetPanelSnapDelta(new(804, 190, 280, 210), inventory, new(806, 190, 280, 210)), "Nearest candidate wins");
            Equal(new(-8, 10), GetPanelSnapDelta(new(800, 180, 280, 210), inventory, null, -8), "Negative gap brings panels into background margins");
            Equal(new(-6, 8), GetPanelSnapDelta(new(806, -20, 280, 210), farInventory, other, -8), "Negative gap below has correct sign");
            Equal(new(0, -8), GetPanelSnapDelta(new(800, 400, 280, 210), farInventory, other, -8), "Negative gap above has correct sign");
            Equal(new(-10, 10), GetPanelSnapDelta(moving, inventory, null, float.NaN), "Non-finite gap falls back to default -5");
            Equal(new(-10, 10), GetPanelSnapDelta(moving, inventory, null, float.PositiveInfinity), "Infinite gap falls back to default -5");
            Equal(new(0, 10), GetPanelSnapDelta(new(790, 180, 280, 210), inventory, null, -999), "Gap is bounded at -10");
            Equal(new(-5, 10), GetPanelSnapDelta(moving, inventory, null, 999), "Gap is bounded at zero");

            InventoryGui gui = InventoryGui.instance = new();
            var space = gui.m_playerGrid.m_gridRoot;
            var quick = new RectTransform { rect = new(824, 194, 252, 182), parent = space };
            var wood = new RectTransform { rect = moving };
            quick.Add("Bkg", wood);
            gui.m_player.parent = space; gui.m_player.rect = inventory;
            InventoryPanels.QuickSlotPanels[1] = quick;
            InventoryPanels.DraggingQuickSlotsPanelOffset = true;
            InventoryPanels.QuickSlotsPanelRuntimeOffset = Vector2.zero;
            Equal(new(-10, 10), SnapDraggedInventoryPanelOffset(Vector2.zero), "Actual background used instead of inset content rect");
            _panelSnapGap!.Value = -8;
            Equal(new(-13, 10), SnapDraggedInventoryPanelOffset(new(-5, 0)), "Live gap setting reaches drag geometry");
            _panelSnapGap = null;
            Equal(new(-10, 10), SnapDraggedInventoryPanelOffset(Vector2.zero), "Unset config falls back to default -5");
            _panelSnapGap = new(PanelSnapDefaultGap);
            space.scale = 1.7f; space.localPosition = new(317, -113);
            Equal(new(-10, 10), SnapDraggedInventoryPanelOffset(Vector2.zero), "UI scale and screen translation preserve snap distance");
            quick.localPosition = new(-10, 10); InventoryPanels.QuickSlotsPanelRuntimeOffset = new(-10, 10);
            Equal(new(-10, 10), SnapDraggedInventoryPanelOffset(Vector2.zero), "Raw drag position avoids accumulated snap drift");
            Equal(new(50, 0), SnapDraggedInventoryPanelOffset(new(50, 0)), "Dragging beyond threshold releases snap");
            _panelSnapping!.Value = Toggle.Off;
            Equal(new(3, 4), SnapDraggedInventoryPanelOffset(new(3, 4)), "Off preserves raw coordinates");
            _panelSnapping.Value = Toggle.On; wood.gameObject.activeSelf = false;
            Equal(new(3, 4), SnapDraggedInventoryPanelOffset(new(3, 4)), "Hidden background is not a snap target");
            wood.gameObject.activeSelf = true; InventoryPanels.QuickSlotPanels.Clear();
            Equal(new(3, 4), SnapDraggedInventoryPanelOffset(new(3, 4)), "Destroyed or missing panel is ignored");
        }
    }
}
