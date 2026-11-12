using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What one mouse press did, told to the host for its trace (UiHost.PressTrace).</summary>
    internal struct UiPressReport
    {
        public int RayNode;    // the target the engine's raycast named at the press, or -1
        public int HereNode;   // the target the scene itself has under the pointer at the press, or -1
        public int DownNode;   // the one the press then began on (the position's, else the raycast's)
        public int Pressed;    // the node pressed on the up, or -1
        public string Why;     // what the up decided
    }

    /// <summary>The mouse press and drag on the view, kept by the target they began on (UiPointer, UiPress).</summary>
    public sealed partial class UiView
    {
        private readonly UiPress _pressed = new UiPress();
        private int _rayNode = -1, _hereNode = -1, _downNode = -1;

        // Called with every press's report, after the press itself: nothing unless the host wants the trace.
        internal System.Action<UiPressReport> PointerReport;

        // Where the pointer is for a press: the mouse's own place while its button is down (the engine's pointer data can name the
        // place of an earlier frame, and a click then lands on what was under the pointer before), else the engine's.
        private static Vector2 Here(PointerEventData data)
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed ? mouse.position.ReadValue() : data.position;
        }

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
                _rayNode = under != null && under.TryGetComponent(out UiHit hit) ? hit.Node : -1;
                _hereNode = ToLayout(Here(data), data.pressEventCamera, out Vector2 at) ? UiPress.At(_scene, at.x, at.y) : -1;
                _downNode = _hereNode >= 0 ? _hereNode : _rayNode;
                _pressed.Down(_scene, _downNode);
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
            int node = -1;
            string why;
            if (!ToLayout(Here(data), data.pressEventCamera, out Vector2 px))
            {
                _pressed.Cancel();
                why = "the pointer is off the screen";
            }
            else
            {
                node = _pressed.Up(_scene, px.x, px.y);
                why = _pressed.Why;
            }
            if (node >= 0)
            {
                PointerPressed?.Invoke(node);
            }
            PointerReport?.Invoke(new UiPressReport { RayNode = _rayNode, HereNode = _hereNode, DownNode = _downNode, Pressed = node, Why = why });
        }
    }
}
