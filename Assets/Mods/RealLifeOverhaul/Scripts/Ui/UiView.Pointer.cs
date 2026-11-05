using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The mouse press and drag on the view, kept by the target they began on (UiPointer, UiPress).</summary>
    public sealed partial class UiView
    {
        private readonly UiPress _pressed = new UiPress();

        /// <summary>The pointer went down, drags or let go at the root (UiPointer): the target is the one the press began on, found again in the scene as it is now.</summary>
        internal void PointerPhase(PointerEventData data, UiDrag phase)
        {
            if (_scene == null)
            {
                return;
            }
            if (phase == UiDrag.Down)
            {
                GameObject under = data.pointerPressRaycast.gameObject != null ? data.pointerPressRaycast.gameObject : data.pointerCurrentRaycast.gameObject;
                int node = under != null && under.TryGetComponent(out UiHit hit) ? hit.Node : -1;
                _pressed.Down(_scene, node);
            }
            else if (phase == UiDrag.Begin && PointerDragged != null)
            {
                _pressed.Dragged();   // a screen that takes drags reads this as a drag, not a click
            }
            int target = _pressed.Current(_scene);
            if (target < 0 || PointerDragged == null)
            {
                return;
            }
            ToLayout(data.position, data.pressEventCamera, out Vector2 px);
            PointerDragged(target, new UiDragPoint { Phase = phase, X = px.x, Y = px.y });
        }

        /// <summary>The pointer came up: the press the pointer went down on is told, once, if the pointer is still over that target.</summary>
        internal void PointerUp(PointerEventData data)
        {
            if (_scene == null || !_pressed.Armed)
            {
                return;
            }
            if (!ToLayout(data.position, data.pressEventCamera, out Vector2 px))
            {
                _pressed.Cancel();
                return;
            }
            int node = _pressed.Up(_scene, px.x, px.y);
            if (node >= 0)
            {
                PointerPressed?.Invoke(node);
            }
        }
    }
}
