using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pointer on a kit screen: entering a target's face moves the one focus there (the pointer is a pad too), a click
    /// presses it, and the wheel over a scroll region scrolls it; each told to the view, which tells its host. Pooled with
    /// its object and pointed at the target it shows now. Cost a frame: nothing; the engine calls it on pointer events.
    /// </summary>
    public sealed class UiHit : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiHit", typeof(UiHit));   // the engine's calls of it (Core/Guard)

        private UiView _view;
        private int _node = -1;
        private int _clip = -1;

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

        public void OnPointerEnter(PointerEventData data)
        {
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
