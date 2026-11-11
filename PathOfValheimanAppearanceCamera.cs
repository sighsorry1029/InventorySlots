using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // Own only an orbit session, never the player's transform/eye, native zoom,
    // camera transform or FOV. The original camera still computes its pose each
    // frame; ending this override therefore restores the current gameplay view.
    private sealed class PovAppearanceCamera
    {
        internal PovCharacterView View = null!;
        internal GameCamera Camera = null!;
        internal float Yaw, Pitch = 5f;
        internal bool Dragging, HadItemDrag;
        internal int InputFrame = -1;
        internal readonly Vector3[] Corners = new Vector3[4];
        internal readonly List<RaycastResult> Hits = new();
        internal EventSystem? Events;
        internal PointerEventData? Pointer;
    }

    private static PovAppearanceCamera? _povAppearanceCamera;
    private static PovCharacterView? _povAppearanceCameraFailedView;

    private static void InstallPathOfValheimanAppearanceCamera(Harmony harmony)
    {
        // Original 1.0.21: both this method and m_camera are private. Harmony
        // binds the exact signature and injects the field, without direct access.
        MethodInfo method = AccessTools.DeclaredMethod(typeof(GameCamera), "GetCameraPosition",
            new[] { typeof(float), typeof(Vector3).MakeByRefType(), typeof(Quaternion).MakeByRefType() });
        FieldInfo camera = AccessTools.DeclaredField(typeof(GameCamera), "m_camera");
        if (method == null || method.ReturnType != typeof(void) || method.IsStatic ||
            camera == null || camera.IsStatic || camera.FieldType != typeof(Camera))
            throw new MissingMethodException("GameCamera", "GetCameraPosition / m_camera");
        harmony.Patch(method, postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(ApplyPathOfValheimanAppearanceCamera)));
    }

    private static void ResetPathOfValheimanAppearanceCamera() => _povAppearanceCamera = null;

    private static void ValidatePathOfValheimanAppearanceCamera()
    {
        // Attached/death/freefly branches bypass GetCameraPosition entirely.
        if (_povAppearanceCamera is { } orbit &&
            (orbit.View != _povCharacterView || !CanUsePathOfValheimanAppearanceCamera(orbit.View, orbit.Camera)))
            ResetPathOfValheimanAppearanceCamera();
    }

    private static bool CanUsePathOfValheimanAppearanceCamera(PovCharacterView view, GameCamera camera) =>
        !IsDedicatedServer && _instance != null && _instance.isActiveAndEnabled &&
        view.Active && !view.Closing && view.Tab == 0 && !view.Failed[0] &&
        view.Gui != null && view.Gui == InventoryGui.instance && view.Player != null && view.Player == Player.m_localPlayer &&
        view.Root != null && view.Root.gameObject.activeInHierarchy && InventoryGui.IsVisible() && !IsInventoryPanelClosing(view.Gui) &&
        Application.isFocused && Screen.width > 0 && Screen.height > 0 &&
        camera != null && camera == GameCamera.instance && !GameCamera.InFreeFly() &&
        !view.Player.IsDead() && !view.Player.IsTeleporting() && !view.Player.InIntro() && !view.Player.InCutscene() &&
        !view.Player.IsAttached();

    private static void ApplyPathOfValheimanAppearanceCamera(GameCamera __instance, Camera ___m_camera,
        bool __runOriginal, ref Vector3 __1, ref Quaternion __2)
    {
        PovCharacterView? view = _povCharacterView;
        try
        {
            if (!__runOriginal || view == null || view == _povAppearanceCameraFailedView || ___m_camera == null ||
                !CanUsePathOfValheimanAppearanceCamera(view, __instance))
            {
                ResetPathOfValheimanAppearanceCamera();
                return;
            }
            if (_povAppearanceCamera is not { } orbit || orbit.View != view || orbit.Camera != __instance)
                _povAppearanceCamera = orbit = new PovAppearanceCamera
                { View = view, Camera = __instance, Yaw = view.Player.transform.eulerAngles.y + 180f };

            Rect area = GetPathOfValheimanAppearanceArea(orbit);
            UpdatePathOfValheimanAppearanceCameraInput(orbit, area);
            Vector3 center = view.Player.GetCenterPoint();
            CalculatePathOfValheimanAppearancePose(center, view.Player.GetHeight(), ___m_camera.fieldOfView,
                ___m_camera.aspect, area, orbit.Yaw, orbit.Pitch, out Vector3 position, out Quaternion rotation);

            // Do not call CollideRay2 again: its snow avoidance mutates shared
            // interpolation state. This cast only constrains our temporary pose.
            float radius = Mathf.Max(__instance.m_raycastWidth, ___m_camera.nearClipPlane + 0.05f);
            if (Physics.CheckSphere(center, radius, __instance.m_blockCameraMask, QueryTriggerInteraction.Ignore)) return;
            Vector3 offset = position - center;
            if (Physics.SphereCast(center, radius, offset.normalized, out RaycastHit hit, offset.magnitude,
                __instance.m_blockCameraMask, QueryTriggerInteraction.Ignore))
                position = center + offset.normalized * Mathf.Max(0f, hit.distance - 0.05f);
            // If a wall leaves no useful viewing distance, keep the native pose.
            if ((position - center).sqrMagnitude < 0.25f) return;
            __1 = position;
            __2 = rotation;
        }
        catch (Exception error)
        {
            // Only this optional presentation feature fails; preserve the native
            // out parameters and keep appearance selection/application usable.
            _povAppearanceCameraFailedView = view;
            ResetPathOfValheimanAppearanceCamera();
            Log.LogWarning("PoV appearance camera unavailable for this GUI: " + error.GetBaseException().Message);
        }
    }

    private static Rect GetPathOfValheimanAppearanceArea(PovAppearanceCamera orbit)
    {
        float left = Screen.width * 0.04f, right = Screen.width * 0.96f;
        Include(orbit.View.Gui.m_player);
        InventoryGrid grid = orbit.View.Gui.m_playerGrid;
        if (grid != null)
        {
            int id = grid.GetInstanceID();
            if (InventoryPanels.CustomSlotPanels.TryGetValue(id, out RectTransform equipment)) Include(equipment);
            if (InventoryPanels.QuickSlotPanels.TryGetValue(id, out RectTransform quick)) Include(quick);
        }
        Include(orbit.View.Background);
        // Custom panel placement can cover the center entirely. Keep a stable
        // screen-center view rather than unbounded zoom/offset in that case.
        if (right - left < Screen.width * 0.18f)
        { left = Screen.width * 0.35f; right = Screen.width * 0.65f; }
        return Rect.MinMaxRect(left / Screen.width, 0.08f, right / Screen.width, 0.88f);

        void Include(RectTransform panel)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            Camera? uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            panel.GetWorldCorners(orbit.Corners);
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                float x = RectTransformUtility.WorldToScreenPoint(uiCamera, orbit.Corners[i]).x;
                min = Mathf.Min(min, x); max = Mathf.Max(max, x);
            }
            if ((min + max) * 0.5f < Screen.width * 0.5f) left = Mathf.Max(left, max + 12f);
            else right = Mathf.Min(right, min - 12f);
        }
    }

    private static void UpdatePathOfValheimanAppearanceCameraInput(PovAppearanceCamera orbit, Rect area)
    {
        if (orbit.InputFrame == Time.frameCount) return;
        bool wasClear = orbit.InputFrame == Time.frameCount - 1 && !orbit.HadItemDrag;
        // InventoryGui can drop/transfer an item during Update, before this
        // LateUpdate hook. Require a preceding clear frame as well as a new press.
        orbit.HadItemDrag = PovCharacterDragItem(orbit.View.Gui) != null || PovCharacterDragGo(orbit.View.Gui) != null;
        orbit.InputFrame = Time.frameCount;
        bool held = ZInput.GetKey(KeyCode.Mouse0);
        if (!held) { orbit.Dragging = false; return; }
        bool down = ZInput.GetKeyDown(KeyCode.Mouse0);
        if (!down && !orbit.Dragging) return;
        Vector2 mouse = Input.mousePosition;
        InventoryGui gui = orbit.View.Gui;
        bool blocked = orbit.HadItemDrag || down && !wasClear || ShouldBlockGlobalHotkeys(orbit.View.Player) ||
            UnifiedPopup.IsVisible() || Hud.InRadial() ||
            gui.m_splitDialog != null && gui.m_splitDialog.gameObject.activeInHierarchy ||
            gui.m_variantDialog != null && gui.m_variantDialog.gameObject.activeInHierarchy ||
            gui.IsSkillsPanelOpen || gui.IsTextPanelOpen || gui.IsTrophisPanelOpen || gui.IsAchievementsPanelOpen ||
            _inventoryTrashConfirmDialog != null && _inventoryTrashConfirmDialog.activeInHierarchy ||
            ZInput.IsExclusiveGamepadActive() || !area.Contains(new Vector2(mouse.x / Screen.width, mouse.y / Screen.height));
        if (blocked || IsPathOfValheimanAppearancePointerOverUi(orbit, mouse))
        { orbit.Dragging = false; return; }
        if (down) { orbit.Dragging = true; return; } // Discard pre-click mouse motion.
        Vector2 delta = ZInput.GetMouseDelta() * (PlayerController.m_mouseSens * 2f);
        if (PlayerController.m_invertMouse) delta.y = -delta.y;
        orbit.Yaw = Mathf.Repeat(orbit.Yaw + delta.x, 360f);
        orbit.Pitch = Mathf.Clamp(orbit.Pitch - delta.y, -20f, 35f);
    }

    private static bool IsPathOfValheimanAppearancePointerOverUi(PovAppearanceCamera orbit, Vector2 mouse)
    {
        EventSystem events = EventSystem.current;
        if (events == null) return true;
        if (orbit.Events != events)
        {
            orbit.Events = events;
            orbit.Pointer = new PointerEventData(events);
        }
        orbit.Pointer!.Reset();
        orbit.Pointer.position = mouse;
        orbit.Hits.Clear();
        events.RaycastAll(orbit.Pointer, orbit.Hits);
        bool hit = false;
        GameObject? outsideDrop = orbit.View.Gui.m_dropButton != null ? orbit.View.Gui.m_dropButton.gameObject : null;
        Hud hud = Hud.instance;
        GameObject? hoverName = hud != null && hud.m_hoverName != null ? hud.m_hoverName.gameObject : null;
        foreach (RaycastResult result in orbit.Hits)
        {
            // Native outside-inventory drop surface also covers empty world space.
            // No item is held here; leave its drop handler intact. The native
            // display-only HoverName also retains a raycast rect when its text is
            // empty. Ignore just these objects in our orbit check, not their
            // children or other overlapping UI, without changing UI raycasting.
            if (result.gameObject != null && result.gameObject != outsideDrop && result.gameObject != hoverName)
            { hit = true; break; }
        }
        orbit.Hits.Clear();
        return hit;
    }

    private static void CalculatePathOfValheimanAppearancePose(Vector3 center, float height, float fov, float aspect,
        Rect area, float yaw, float pitch, out Vector3 position, out Quaternion rotation)
    {
        height = Mathf.Max(1f, height);
        float vertical = Mathf.Tan(Mathf.Clamp(fov, 15f, 120f) * Mathf.Deg2Rad * 0.5f);
        float horizontal = vertical * Mathf.Max(0.5f, aspect);
        // Fit the full body with head/foot margins and room for held equipment.
        float distance = Mathf.Max((height + 0.4f) / (2f * vertical * area.height),
            1.4f / (2f * horizontal * area.width));
        distance = Mathf.Max(2f, distance);
        rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector2 screenCenter = area.center;
        Vector3 shift = new Vector3((screenCenter.x - 0.5f) * 2f * distance * horizontal,
            (screenCenter.y - 0.5f) * 2f * distance * vertical, distance);
        position = center - rotation * shift;
    }
}
