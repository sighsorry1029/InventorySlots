using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    private enum InventorySlideButton { Trash, Restock, Exclude }
    private static readonly InventoryButtonSlide?[] InventoryButtonSlides = new InventoryButtonSlide?[3];

    private static RectTransform EnsureInventoryButtonSlide(InventoryGui gui, Vector3 origin, int rows,
        InventorySlideButton kind = InventorySlideButton.Trash, Transform? parent = null)
    {
        parent ??= gui.m_playerGrid.m_gridRoot;
        InventoryButtonSlide? slide = InventoryButtonSlides[(int)kind];
        if (slide == null || slide.Owner != gui || slide.transform.parent != parent)
        {
            if (slide != null) Object.Destroy(slide.gameObject);
            GameObject root = new(ModName + "_ButtonSlide_" + kind, typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = Color.clear;
            slide = root.AddComponent<InventoryButtonSlide>();
            slide.Initialize(gui, kind);
            InventoryButtonSlides[(int)kind] = slide;
        }
        slide.Layout(origin, rows);
        return slide.Content;
    }

    private static bool IsPointerOverSlide(InventorySlideButton kind)
    {
        InventoryButtonSlide? slide = InventoryButtonSlides[(int)kind];
        return slide != null && slide.PointerOver && slide.IsRaycastLocationValid(Input.mousePosition, slide.Camera!);
    }

    // Each button owns a clip, progress and hover deadline in the player-grid
    // render phase. Its masked content moves without moving its neighbors.
    private sealed class InventoryButtonSlide : MonoBehaviour, ICanvasRaycastFilter
    {
        private const float Peek = 7f;
        private const float Duration = 0.15f;
        private const float CloseDelay = 0.3f;
        internal InventoryGui Owner = null!;
        internal RectTransform Content = null!;
        internal bool PointerOver { get; private set; }
        internal Camera? Camera => _camera;
        private InventorySlideButton _kind;
        private RectTransform _clip = null!;
        private CanvasGroup _input = null!;
        private Animator? _animator;
        private Camera? _camera;
        private EventSystem? _eventSystem;
        private PointerEventData? _pointer;
        private readonly List<RaycastResult> _hits = new();
        private float _progress, _size, _lastInside = float.NegativeInfinity;
        private bool _wasOpen;
        private bool _hasButtons;

        internal void Initialize(InventoryGui gui, InventorySlideButton kind)
        {
            Owner = gui;
            _kind = kind;
            _animator = gui.GetComponent<Animator>();
            _camera = gui.GetComponentInParent<Canvas>()?.worldCamera;
            _clip = (RectTransform)transform;
            _clip.anchorMin = _clip.anchorMax = _clip.pivot = new Vector2(0, 1);
            _input = gameObject.AddComponent<CanvasGroup>();
            GameObject content = new("Buttons", typeof(RectTransform));
            Content = (RectTransform)content.transform;
            Content.SetParent(transform, false);
            Content.anchorMin = Content.anchorMax = Content.pivot = new Vector2(0, 1);
            Content.sizeDelta = Vector2.zero;
        }

        internal void Layout(Vector3 origin, int rows)
        {
            InventoryGrid grid = Owner.m_playerGrid;
            float spacing = Mathf.Max(1, grid.m_elementSpace);
            _size = Mathf.Clamp(spacing * 0.72f, 42f, 58f);
            int columns = Mathf.Max(1, grid.m_inventory != null ? grid.m_inventory.GetWidth() : 8);
            _hasButtons = ButtonEnabled;
            int column = _kind == InventorySlideButton.Trash ? 0 : GetItemRuleColumnsFromRight(_kind == InventorySlideButton.Restock);
            Vector3 first = origin + CalculateInventoryBottomButtonPosition(columns, rows, spacing, _size, column);
            _clip.localPosition = first;
            _clip.sizeDelta = new Vector2(_size, _size);
            ApplyOffset();
        }

        private bool ButtonEnabled => _kind == InventorySlideButton.Trash
            ? IsInventoryTrashButtonEnabled() : IsItemRuleButtonEnabled(_kind == InventorySlideButton.Restock);

        private InventoryButtonMode Mode => (_kind == InventorySlideButton.Trash ? _trashButtonMode :
            _kind == InventorySlideButton.Restock ? _restockButtonMode : _autoPickupButtonMode)?.Value ?? InventoryButtonMode.Auto;

        private float VisibleHeight => Mathf.Lerp(Peek, _size, _progress);

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return _hasButtons && RectTransformUtility.ScreenPointToLocalPointInRectangle(_clip, screenPoint, eventCamera, out Vector2 point) &&
                point.x >= 0 && point.x <= _clip.rect.width && point.y <= 0 && point.y >= -VisibleHeight;
        }

        private void ApplyOffset()
        {
            // Content coordinates stay identical to grid coordinates except for
            // this button's slide. Row/scale changes therefore cannot add drift.
            Content.localPosition = -_clip.localPosition + Vector3.up * (_size - VisibleHeight);
        }

        private bool HitVisibleStrip()
        {
            if (!IsRaycastLocationValid(Input.mousePosition, _camera!)) return false;
            EventSystem current = EventSystem.current;
            if (current == null) return false;
            if (_eventSystem != current)
            {
                _eventSystem = current;
                _pointer = new PointerEventData(current);
            }
            _pointer!.Reset();
            _pointer.position = Input.mousePosition;
            _hits.Clear();
            current.RaycastAll(_pointer, _hits);
            // A container or modal drawn above the strip wins the hit test.
            return _hits.Count > 0 && _hits[0].gameObject.transform.IsChildOf(transform);
        }

        private void LateUpdate()
        {
            if (Owner == null || _instance == null) return;
            bool open = InventoryGui.IsVisible() && (_animator == null || _animator.GetBool("visible"));
            bool available = _instance.isActiveAndEnabled && Player.m_localPlayer != null && !Player.m_localPlayer.m_isLoading && CanShowItemRules(Owner);
            // The setting may be turned off without another layout call.
            _hasButtons = ButtonEnabled;
            _input.blocksRaycasts = open && available && _hasButtons;
            if (!open || !available)
            {
                PointerOver = false;
                _wasOpen = false;
                // Freeze the slide while the parent plays its closing animation.
                return;
            }
            if (!_wasOpen)
            {
                _progress = 0;
                _lastInside = float.NegativeInfinity;
                ApplyOffset();
            }
            _wasOpen = true;
            bool controller = ZInput.IsExclusiveGamepadActive();
            PointerOver = !controller && HitVisibleStrip();
            bool editing = _kind != InventorySlideButton.Trash && _itemRuleEditor != null &&
                _itemRuleEditor.IsPopupOpenFor(_kind == InventorySlideButton.Restock);
            // Controller focus reveals only its own button. Ignore the parked
            // mouse cursor and its hover grace period while using the gamepad.
            bool expand = editing || (controller ? IsControllerInventoryButtonFocused(_kind) : PointerOver);
            if (controller) _lastInside = float.NegativeInfinity;
            else if (expand) _lastInside = Time.unscaledTime;
            float target = expand || !controller && Time.unscaledTime - _lastInside < CloseDelay ? 1 : 0;
            _progress = Mode == InventoryButtonMode.On ? 1 : Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime / Duration);
            ApplyOffset();
            if (editing) _itemRuleEditor!.PositionPopup();
        }

        private void OnDisable()
        {
            PointerOver = false;
            _wasOpen = false;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(InventoryButtonSlides[(int)_kind], this)) InventoryButtonSlides[(int)_kind] = null;
        }
    }
}
