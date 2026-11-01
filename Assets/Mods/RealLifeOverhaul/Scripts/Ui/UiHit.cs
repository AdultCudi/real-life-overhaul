using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pointer on a kit screen: entering and leaving a target's face, a click on it, and the wheel over a scroll
    /// region, each told to the view, which tells its host (UiFocusMode decides what a hover does). Pooled with its object
    /// and pointed at the target it shows now. Cost a frame: nothing, the engine calls it on pointer events.
    /// </summary>
    public sealed class UiHit : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IScrollHandler, IPointerDownHandler, IDragHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiHit", typeof(UiHit));   // the engine's calls of it (Core/Guard)

        private UiView _view;
        private int _node = -1;
        private int _clip = -1;

        /// <summary>The target this face shows now, or -1.</summary>
        public int Node => _node;

        /// <summary>The pointer is over this face. A pooled face keeps it across a layout that points it at another target.</summary>
        public bool Inside { get; private set; }

        /// <summary>Points this at a target's face (or at none, with a null view).</summary>
        public void Face(UiView view, int node)
        {
            _view = view;
            _node = view != null ? node : -1;
            _clip = -1;
        }

        /// <summary>Points this at a scroll region, for the wheel.</summary>
        public void Wheel(UiView view, int clip)
        {
            _view = view;
            _clip = clip;
            _node = -1;
        }

        private void OnDisable()
        {
            Inside = false;   // a hidden face never gets its exit, and a stale hover would lift the wrong target
        }

        public void OnPointerEnter(PointerEventData data)
        {
            Inside = true;
            if (FrameGuard.Off || _view == null || _node < 0)
            {
                return;
            }
            try
            {
                _view.PointerEntered?.Invoke(_node);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnPointerExit(PointerEventData data)
        {
            Inside = false;
            if (FrameGuard.Off || _view == null || _node < 0)
            {
                return;
            }
            try
            {
                _view.PointerExited?.Invoke(_node);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (FrameGuard.Off || _view == null || _node < 0 || (data != null && data.button != PointerEventData.InputButton.Left))
            {
                return;
            }
            try
            {
                _view.PointerPressed?.Invoke(_node);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Dragged(data);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Dragged(data);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        // Where across its face the pointer is, for a slider's knob that follows it as the game's Slider is dragged.
        private void Dragged(PointerEventData data)
        {
            if (_view == null || _node < 0 || data == null || data.button != PointerEventData.InputButton.Left || _view.PointerDragged == null)
            {
                return;
            }
            var rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out Vector2 local) && rect.rect.width > 0f)
            {
                _view.PointerDragged(_node, Mathf.Clamp01((local.x - rect.rect.xMin) / rect.rect.width));
            }
        }

        public void OnScroll(PointerEventData data)
        {
            if (FrameGuard.Off || _view == null || _clip < 0 || data == null)
            {
                return;
            }
            try
            {
                _view.Wheeled?.Invoke(_clip, data.scrollDelta.y);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }
    }
}
