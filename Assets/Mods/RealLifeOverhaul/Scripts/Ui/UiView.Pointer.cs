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

        /// <summary>
        /// Asked once a drag begins, after the host has told its screen: true when something already took the drag (a slider, a board's shift, a table's column, the
        /// map, a window's grip), so no scroll region follows it. Set by the host; null takes none.
        /// </summary>
        public System.Func<bool> DragTaken;

        private Vector2 _downPx;
        private int _panClip = -1, _panStuck;
        private float _panStart, _panFrom, _panStep = UiTokens.S16;

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
                _downPx = at;
                _panClip = -1;
                _downNode = _hereNode >= 0 ? _hereNode : _rayNode;
                _pressed.Down(_scene, _downNode);
            }
            else if (phase == UiDrag.Begin && PointerDragged != null)
            {
                _pressed.Dragged();   // a screen that takes drags reads this as a drag, not a click
            }
            int target = _pressed.Current(_scene);
            if (target >= 0 && PointerDragged != null)
            {
                ToLayout(data.position, data.pressEventCamera, out Vector2 px);
                PointerDragged(target, new UiDragPoint { Phase = phase, X = px.x, Y = px.y });
            }
            if (phase != UiDrag.Down)
            {
                Pan(data, phase);
            }
        }

        // A scroll region follows the pointer as the game's ScrollRect does: a press that moved past the engine's drag threshold (the engine tells it as a begin) and
        // that nothing else took becomes the drag of the innermost region under the press that can move that way; the content follows the pointer from where
        // the drag began and stops at its ends, and the press is no click. The offset lives in the screen, so the region is moved through the wheel's own way
        // (Wheeled), by as many notches as the pointer's distance is, the notch's length learned from what the last one moved. Nothing runs without a drag.
        private void Pan(PointerEventData data, UiDrag phase)
        {
            if (phase == UiDrag.Begin)
            {
                _panClip = -1;
                if (Wheeled == null || (DragTaken != null && DragTaken()) || !ToLayout(data.position, data.pressEventCamera, out Vector2 now))
                {
                    return;
                }
                float dx = now.x - _downPx.x, dy = now.y - _downPx.y;
                if (Mathf.Abs(dx) > Mathf.Abs(dy))
                {
                    return;   // a sideways drag: the regions scroll up and down only
                }
                int clip = UiPanRules.ClipAt(_scene, _downPx.x, _downPx.y, dy);
                if (clip < 0)
                {
                    return;
                }
                _panClip = clip;
                _panStart = _scene.Clips[clip].Offset;
                _panFrom = now.y;
                _panStep = UiTokens.S16;
                _panStuck = 0;
                _pressed.Dragged();   // the control under the press is not pressed when the drag ends
            }
            else if (phase == UiDrag.Move && _panClip >= 0)
            {
                if (_panClip >= _scene.ClipCount || !ToLayout(data.position, data.pressEventCamera, out Vector2 now))
                {
                    _panClip = -1;
                    return;
                }
                float want = UiPanRules.Want(_panStart, now.y - _panFrom, _scene.Clips[_panClip].ContentHeight, _scene.Clips[_panClip].H);
                float before = _scene.Clips[_panClip].Offset, change = want - before;
                if (Mathf.Abs(change) < 0.5f)
                {
                    return;
                }
                float notches = UiPanRules.Notches(change, _panStep);
                Wheeled?.Invoke(_panClip, notches);
                if (_panClip >= _scene.ClipCount)
                {
                    _panClip = -1;
                    return;
                }
                float moved = _scene.Clips[_panClip].Offset - before;
                if (Mathf.Abs(moved) > 0.01f)
                {
                    _panStep = UiPanRules.Step(moved, notches, _panStep);
                    _panStuck = 0;
                }
                else if (++_panStuck > 3)
                {
                    _panClip = -1;   // the screen moved nothing for this region: leave it
                }
            }
            else if (phase == UiDrag.End)
            {
                _panClip = -1;
            }
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
