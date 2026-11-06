using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The mouse going down, up and dragging on a kit screen, taken once at the view's root and handed to the view. The engine
    /// clicks only the object the pointer went down on, and the kit's faces are pooled: a layout between down and up hands that
    /// object to another target (the click went to the target shown before, or was dropped when the object was hidden). The root is
    /// never pooled, so the press is told by the view from what the target is (UiPress). Cost a frame: nothing, the engine calls
    /// it on pointer events.
    /// </summary>
    public sealed class UiPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiPointer", typeof(UiPointer));   // the engine's calls of it (Core/Guard)

        private static readonly System.Collections.Generic.List<UiView> Standing = new System.Collections.Generic.List<UiView>(16);

        private UiView _view;

        /// <summary>Every view that stands: the tutorial's arrow finds the kit's own button behind the game's by looking at what they say. Cost: a list the engine's create and destroy keep.</summary>
        public static System.Collections.Generic.List<UiView> Views => Standing;

        /// <summary>Points this at the view whose root it stands on.</summary>
        public void Bind(UiView view)
        {
            _view = view;
            if (view != null && !Standing.Contains(view))
            {
                Standing.Add(view);
            }
        }

        private void OnDestroy()
        {
            Standing.Remove(_view);
        }



        public void OnPointerDown(PointerEventData data)
        {
            if (FrameGuard.Off || _view == null || data == null || data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            try
            {
                _view.PointerPhase(data, UiDrag.Down);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (FrameGuard.Off || _view == null || data == null || data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            try
            {
                _view.PointerUp(data);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (FrameGuard.Off || _view == null || data == null || data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            try
            {
                _view.PointerPhase(data, UiDrag.Begin);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (FrameGuard.Off || _view == null || data == null || data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            try
            {
                _view.PointerPhase(data, UiDrag.Move);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (FrameGuard.Off || _view == null || data == null || data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            try
            {
                _view.PointerPhase(data, UiDrag.End);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }
    }
}
