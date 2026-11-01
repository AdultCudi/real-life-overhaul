using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The focus, drawn as an accent ring three pixels outside the target while the keys or a pad lead, gliding from the
    /// last target to the next in the focus time, the target's face lifted to the hover fill. While the pointer leads
    /// there is no ring: the target under the pointer gets that same still fill instead, so hovering never moves anything.
    /// A press tightens the ring for a moment. One highlight a list: while the ring stands on another row of a list, that
    /// list's selected row keeps its accent bar and lets its fill go quiet, so the two never read as two selections.
    /// </summary>
    public sealed partial class UiView
    {
        private readonly Image[] _rings = new Image[Layers];
        private readonly List<BoxSlot> _quiet = new List<BoxSlot>(4);
        private int _focus = -1;
        private int _hover = -1;
        private bool _ring = true;
        private BoxSlot _face;
        private Vector4 _ringFrom, _ringTo;   // x, y, w, h in px
        private float _ringStart = -1f, _ringLength;
        private int _ringLayer;
        private float _press = -1f;

        /// <summary>The node focus is on, or -1.</summary>
        public int Focus => _focus;

        /// <summary>Whether the ring is still gliding or a press is still showing: the host steps it while true.</summary>
        public bool Moving => _ringStart >= 0f || _press >= 0f;

        /// <summary>The ring shows only while the keys or a pad lead (UiFocusMode).</summary>
        public bool Ring
        {
            get => _ring;
            set
            {
                if (_ring == value)
                {
                    return;
                }
                _ring = value;
                _ringStart = _press = -1f;   // a ring that comes back appears where the focus is, it never glides in from somewhere old
                Lift(false);
                Quiet(false);
                if (_scene != null && _focus >= 0)
                {
                    _ringTo = _ringFrom = RingBox(_focus);
                    _ringLayer = _scene.Nodes[_focus].Layer;
                }
                PlaceRing(_ringTo);
                Lift(true);
                Quiet(true);
            }
        }

        /// <summary>The target under the pointer, lifted while the pointer leads, or -1 for none.</summary>
        public void SetHover(int node)
        {
            if (_scene == null || node >= _scene.NodeCount)
            {
                node = -1;
            }
            if (node == _hover)
            {
                return;
            }
            Lift(false);
            _hover = node;
            Lift(true);
        }

        /// <summary>The target the resting pointer is over after a write (its pooled face may show another target now), or -1.</summary>
        public int PointerNode()
        {
            if (_scene == null)
            {
                return -1;
            }
            for (int i = 0; i < _scene.NodeCount; i++)
            {
                int face = _scene.Nodes[i].Face;
                if (SlotOf(face) is BoxSlot b && b.Hit != null && b.Hit.Inside && b.Hit.Node == i && b.Rect.gameObject.activeInHierarchy)
                {
                    return i;
                }
            }
            return -1;
        }

        private void BuildFocus()
        {
            for (int i = 0; i < Layers; i++)
            {
                RectTransform r = NewRect(_layers[i].Rect, "Focus");
                _rings[i] = r.gameObject.AddComponent<Image>();
                _rings[i].raycastTarget = false;
                _rings[i].type = Image.Type.Sliced;
                Show(_rings[i], false);
            }
        }

        /// <summary>Moves focus to a node, gliding there (or at once with motion off, <paramref name="glide"/> false, or no ring shown).</summary>
        public void SetFocus(int node, bool glide, float now)
        {
            if (_scene == null || node >= _scene.NodeCount)
            {
                node = -1;
            }
            Lift(false);
            Quiet(false);
            int from = _focus;
            _focus = node;
            if (node < 0)
            {
                for (int i = 0; i < Layers; i++)
                {
                    Show(_rings[i], false);
                }
                return;
            }
            Vector4 target = RingBox(node);
            int layer = _scene.Nodes[node].Layer;
            bool same = from >= 0 && from < _scene.NodeCount && _scene.Nodes[from].Layer == layer && _rings[layer].gameObject.activeSelf;
            float length = UiTokens.Duration(UiMotionKind.Focus, Motion);
            _ringTo = target;
            _ringLayer = layer;
            if (glide && same && _ring && length > 0f)
            {
                _ringStart = now;
                _ringLength = length;
            }
            else
            {
                _ringFrom = target;
                _ringStart = -1f;
                PlaceRing(target);
            }
            Lift(true);
            Quiet(true);
        }

        /// <summary>The ring tightens and lets go, as a press lands.</summary>
        public void Press(float now)
        {
            _press = _ring && UiTokens.Duration(UiMotionKind.Press, Motion) > 0f ? now : -1f;
        }

        /// <summary>One step of the ring's glide and a press, on the real clock. False once both are still.</summary>
        public bool StepFocus(float now)
        {
            if (_focus < 0)
            {
                _ringStart = _press = -1f;
                return false;
            }
            Vector4 box = _ringTo;
            if (_ringStart >= 0f)
            {
                float t = Mathf.Clamp01((now - _ringStart) / _ringLength);
                float e = UiMotionRules.Ease(t);
                box = Vector4.LerpUnclamped(_ringFrom, _ringTo, e);
                if (t >= 1f)
                {
                    _ringStart = -1f;
                    _ringFrom = _ringTo;
                }
            }
            if (_press >= 0f)
            {
                float length = UiTokens.Duration(UiMotionKind.Press, Motion) * 2f;
                float t = Mathf.Clamp01((now - _press) / length);
                float squeeze = (1f - Mathf.Abs(t * 2f - 1f)) * UiTokens.FocusGap;
                box = new Vector4(box.x + squeeze, box.y + squeeze, box.z - squeeze * 2f, box.w - squeeze * 2f);
                if (t >= 1f)
                {
                    _press = -1f;
                }
            }
            PlaceRing(box);
            return Moving;
        }

        // After a write: the ring and the lifted face found again in the new scene (the same node by its index).
        private void WriteFocus(bool themeChanged)
        {
            _face = null;
            _quiet.Clear();   // the slots were bound afresh: nothing is quieted or lifted in them now
            if (_focus >= _scene.NodeCount)
            {
                _focus = UiFocusRules.First(_scene);
            }
            if (_hover >= _scene.NodeCount)
            {
                _hover = -1;
            }
            for (int i = 0; i < Layers; i++)
            {
                if (_rings[i].transform.GetSiblingIndex() != _rings[i].transform.parent.childCount - 1)
                {
                    _rings[i].transform.SetAsLastSibling();
                }
                _rings[i].color = Colour(UiRole.Focus);
            }
            if (_focus >= 0)
            {
                _ringTo = RingBox(_focus);
                if (_ringStart < 0f)
                {
                    _ringFrom = _ringTo;
                }
                _ringLayer = _scene.Nodes[_focus].Layer;
                PlaceRing(_ringStart >= 0f ? _ringFrom : _ringTo);
            }
            else
            {
                for (int i = 0; i < Layers; i++)
                {
                    Show(_rings[i], false);
                }
            }
            Lift(true);
            Quiet(true);
        }

        private Vector4 RingBox(int node)
        {
            _scene.NodeBox(node, out float x, out float y, out float w, out float h);
            float out1 = UiTokens.FocusGap + UiTokens.FocusStroke;
            return new Vector4(x - out1, y - out1, w + out1 * 2f, h + out1 * 2f);
        }

        private void PlaceRing(Vector4 box)
        {
            for (int i = 0; i < Layers; i++)
            {
                bool on = _ring && i == _ringLayer && _focus >= 0 && _scene != null && _focus < _scene.NodeCount;
                Show(_rings[i], on);
                if (!on)
                {
                    continue;
                }
                int face = _scene.Nodes[_focus].Face;
                float radius = face >= 0 ? (_scene.Items[face].Flags & UiFlags.Circle) != 0 ? _scene.Items[face].H / 2f : _scene.Items[face].Radius : UiTokens.RadiusLarge;
                Sprite ring = UiSprites.Ring(radius);
                if (_rings[i].sprite != ring)
                {
                    _rings[i].sprite = ring;
                }
                Place((RectTransform)_rings[i].transform, box.x, box.y, box.z, box.w);
            }
        }

        // The lifted target's face (the focus with the ring, the hover without it) at the hover fill, or put back.
        private void Lift(bool on)
        {
            if (!on)
            {
                if (_face != null && _face.Rect != null)
                {
                    _face.Fill.color = _face.Role == UiRole.None ? new Color(0f, 0f, 0f, 0f) : Colour(_face.Role, _face.Alpha);
                }
                _face = null;
                return;
            }
            int node = _ring ? _focus : _hover;
            if (node < 0 || _scene == null || node >= _scene.NodeCount || _scene.Nodes[node].Disabled)
            {
                return;
            }
            _face = SlotOf(_scene.Nodes[node].Face) as BoxSlot;
            if (_face == null)
            {
                return;
            }
            switch (_face.Role)
            {
                case UiRole.Accent:
                    _face.Fill.color = Color.Lerp(Colour(UiRole.Accent), Colour(UiRole.Text), 0.12f);
                    break;
                case UiRole.Danger:
                case UiRole.DangerSoft:
                case UiRole.RowSel:
                case UiRole.AccentSoft:
                    break;   // already marked: the ring says it, and a hover over the selection changes nothing
                case UiRole.None:
                    _face.Fill.color = Colour(UiRole.Hover);
                    break;
                default:
                    // A face with a fill of its own (a button, a card) steps a little towards the words: a quiet change
                    // that never matches the selection's fill, where one fixed hover colour vanished on a field.
                    _face.Fill.color = Color.Lerp(Colour(_face.Role, _face.Alpha), Colour(UiRole.Text), 0.045f);
                    break;
            }
        }

        // While the ring stands on another row of a list, the list's selected rows let their fill go quiet (their accent
        // bar stays to say what is open), so one list never shows two highlights.
        private void Quiet(bool on)
        {
            if (!on)
            {
                for (int i = 0; i < _quiet.Count; i++)
                {
                    BoxSlot b = _quiet[i];
                    if (b.Rect != null)
                    {
                        b.Fill.color = b.Role == UiRole.None ? new Color(0f, 0f, 0f, 0f) : Colour(b.Role, b.Alpha);
                    }
                }
                _quiet.Clear();
                return;
            }
            if (!_ring || _scene == null || _focus < 0 || _focus >= _scene.NodeCount)
            {
                return;
            }
            short list = _scene.Nodes[_focus].List;
            if (list < 0)
            {
                return;
            }
            for (int i = 0; i < _scene.NodeCount; i++)
            {
                if (i == _focus || _scene.Nodes[i].List != list)
                {
                    continue;
                }
                int face = _scene.Nodes[i].Face;
                if (face >= 0 && (_scene.Items[face].Flags & UiFlags.Selected) != 0 && SlotOf(face) is BoxSlot b)
                {
                    b.Fill.color = Colour(UiRole.Row);
                    _quiet.Add(b);
                }
            }
        }
    }
}
