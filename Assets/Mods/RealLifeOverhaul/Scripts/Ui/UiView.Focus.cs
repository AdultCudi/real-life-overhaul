using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The focus: the main state of a game screen, drawn as an accent ring three pixels outside the target, gliding from
    /// the last target to the next in the focus time, while the target's face lifts to the hover fill (a primary button's
    /// accent brightens). A press tightens the ring for a moment. Pointer, keys and pad share the one focus.
    /// </summary>
    public sealed partial class UiView
    {
        private readonly Image[] _rings = new Image[Layers];
        private int _focus = -1;
        private BoxSlot _face;
        private Vector4 _ringFrom, _ringTo;   // x, y, w, h in px
        private float _ringStart = -1f, _ringLength;
        private int _ringLayer;
        private float _press = -1f;

        /// <summary>The node focus is on, or -1.</summary>
        public int Focus => _focus;

        /// <summary>Whether the ring is still gliding or a press is still showing: the host steps it while true.</summary>
        public bool Moving => _ringStart >= 0f || _press >= 0f;

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

        /// <summary>Moves focus to a node, gliding there (or at once with motion off or <paramref name="glide"/> false).</summary>
        public void SetFocus(int node, bool glide, float now)
        {
            if (_scene == null || node >= _scene.NodeCount)
            {
                node = -1;
            }
            Lift(false);
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
            if (glide && same && length > 0f)
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
        }

        /// <summary>The ring tightens and lets go, as a press lands.</summary>
        public void Press(float now)
        {
            _press = UiTokens.Duration(UiMotionKind.Press, Motion) > 0f ? now : -1f;
        }

        /// <summary>One step of the ring's glide and a press, on the real clock; false once both are still.</summary>
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

        // After a write: the ring and the face found again in the new scene (the same node by its index), the face lifted.
        private void WriteFocus(bool themeChanged)
        {
            _face = null;
            if (_focus >= _scene.NodeCount)
            {
                _focus = UiFocusRules.First(_scene);
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
                Lift(true);
            }
            else
            {
                for (int i = 0; i < Layers; i++)
                {
                    Show(_rings[i], false);
                }
            }
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
                bool on = i == _ringLayer && _focus >= 0;
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

        // The focused target's face lifted to the hover fill (or brightened, on the accent), or put back.
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
            if (_focus < 0 || _scene == null)
            {
                return;
            }
            int item = _scene.Nodes[_focus].Face;
            _face = SlotOf(item) as BoxSlot;
            if (_face == null)
            {
                return;
            }
            switch (_face.Role)
            {
                case UiRole.Accent:
                    _face.Fill.color = Color.Lerp(Colour(UiRole.Accent), Color.white, 0.12f);
                    break;
                case UiRole.Danger:
                case UiRole.DangerSoft:
                case UiRole.RowSel:
                case UiRole.AccentSoft:
                    break;   // already loud: the ring says it
                default:
                    _face.Fill.color = Colour(UiRole.Hover);
                    break;
            }
        }
    }
}
