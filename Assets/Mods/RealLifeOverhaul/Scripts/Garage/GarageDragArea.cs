using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The part of the screen the shop's list leaves free: dragging turns the turntable view, the wheel moves it in and
    /// out. Nothing per frame: it costs only the engine's pointer events.
    /// </summary>
    public sealed class GarageDragArea : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "GarageDragArea", typeof(GarageDragArea));   // the engine's calls of it

        public Action<Vector2> Dragged;
        public Action<float> Scrolled;

        public void OnDrag(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Dragged?.Invoke(data.delta);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnScroll(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Scrolled?.Invoke(data.scrollDelta.y);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }
}
