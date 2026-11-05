using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Map25;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The street names on the 2.5D city map: a pool of 24 labels in the game's own lettering, put along the streets nearest the middle of
    /// the picture (the game's own names are painted on the real roads, which the picture covers). Turned to run along their street as the
    /// camera sees it and kept upright, capitals, a dark halo so they read over any block; the same name is not written twice close
    /// together. Placed only when the picture is drawn again. Cost a frame: nothing; a draw: the anchors (a few hundred) projected once and
    /// 24 labels moved, none made after the first.
    /// </summary>
    internal sealed class NavMapNames
    {
        private const int Pool = 24;
        private const float Size = 17f;               // points on a 1080 line screen
        private const float Apart = 0.14f;            // of the picture's width: two labels of one name are at least this far apart
        private const float Margin = 0.04f;           // of the picture's edge that no label is put on

        private readonly TMP_Text[] _label = new TMP_Text[Pool];
        private readonly string[] _shown = new string[Pool];
        private readonly int[] _pick = new int[Pool];
        private readonly float[] _score = new float[Pool], _at = new float[Pool * 2];
        private Map25Names _names;
        private int _used;

        /// <summary>The names to write (after every road has its name); null takes them all away.</summary>
        public void Set(Map25Names names)
        {
            _names = names;
        }

        /// <summary>Puts the labels where <paramref name="view"/> shows the streets, in screen pixels of the layer <paramref name="parent"/> stands on.</summary>
        public void Place(RectTransform parent, Map25View view, float width, float height, float unitsPerPixel, float groundY)
        {
            if (_names == null || _names.Count == 0)
            {
                Hide();
                return;
            }
            int chosen = 0;
            for (int i = 0; i < _names.Count; i++)
            {
                if (!view.Project(_names.X[i], groundY, _names.Z[i], out float u, out float v) || u < Margin || u > 1f - Margin || v < Margin || v > 1f - Margin)
                {
                    continue;
                }
                float score = (u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f);
                int k = chosen < Pool ? chosen : Pool - 1;
                if (chosen >= Pool && score >= _score[k])
                {
                    continue;
                }
                while (k > 0 && _score[k - 1] > score)
                {
                    _score[k] = _score[k - 1];
                    _pick[k] = _pick[k - 1];
                    _at[k * 2] = _at[(k - 1) * 2];
                    _at[k * 2 + 1] = _at[(k - 1) * 2 + 1];
                    k--;
                }
                _score[k] = score;
                _pick[k] = i;
                _at[k * 2] = u;
                _at[k * 2 + 1] = v;
                chosen = chosen < Pool ? chosen + 1 : Pool;
            }
            int used = 0;
            for (int k = 0; k < chosen; k++)
            {
                if (Twin(k, used))
                {
                    continue;
                }
                int i = _pick[k];
                TMP_Text label = Label(parent, used, unitsPerPixel);
                if (label == null)
                {
                    break;
                }
                string name = _names.Name[i];
                if (!ReferenceEquals(_shown[used], name))
                {
                    _shown[used] = name;
                    label.text = name.ToUpperInvariant();
                }
                float u = _at[k * 2], v = _at[k * 2 + 1];
                float angle = 0f;
                if (view.Project(_names.X[i] - _names.DirX[i] * 14f, groundY, _names.Z[i] - _names.DirZ[i] * 14f, out float u0, out float v0)
                    && view.Project(_names.X[i] + _names.DirX[i] * 14f, groundY, _names.Z[i] + _names.DirZ[i] * 14f, out float u1, out float v1))
                {
                    angle = Mathf.Atan2((v1 - v0) * height, (u1 - u0) * width) * Mathf.Rad2Deg;
                    angle = angle > 90f ? angle - 180f : angle <= -90f ? angle + 180f : angle;
                }
                var rect = label.rectTransform;
                rect.position = new Vector3(u * width, v * height, 0f);
                rect.localEulerAngles = new Vector3(0f, 0f, angle);
                if (!label.gameObject.activeSelf)
                {
                    label.gameObject.SetActive(true);
                }
                // _pick keeps the kept ones in front for the next twin test
                _pick[used] = i;
                _at[used * 2] = u;
                _at[used * 2 + 1] = v;
                used++;
            }
            for (int k = used; k < _used; k++)
            {
                if (_label[k] != null && _label[k].gameObject.activeSelf)
                {
                    _label[k].gameObject.SetActive(false);
                }
            }
            _used = used;
        }

        // True when a label of the same name is already kept within the apart distance (the kept ones are the first of the arrays).
        private bool Twin(int candidate, int kept)
        {
            string name = _names.Name[_pick[candidate]];
            float u = _at[candidate * 2], v = _at[candidate * 2 + 1];
            for (int k = 0; k < kept; k++)
            {
                if (ReferenceEquals(_names.Name[_pick[k]], name))
                {
                    float du = _at[k * 2] - u, dv = _at[k * 2 + 1] - v;
                    if (du * du + dv * dv < Apart * Apart)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private TMP_Text Label(RectTransform parent, int index, float unitsPerPixel)
        {
            if (_label[index] != null)
            {
                return _label[index];
            }
            var made = new GameObject("Street", typeof(RectTransform));
            made.SetActive(false);
            var rect = (RectTransform)made.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f * unitsPerPixel, 60f * unitsPerPixel);
            var text = made.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GameFont.Asset;
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = Size * Screen.height / 1080f * unitsPerPixel;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = new Color32(181, 198, 212, 255);
            text.outlineWidth = 0.3f;
            text.outlineColor = new Color32(21, 32, 43, 255);
            text.raycastTarget = false;
            _label[index] = text;
            return text;
        }

        public void Hide()
        {
            for (int k = 0; k < _used; k++)
            {
                if (_label[k] != null && _label[k].gameObject.activeSelf)
                {
                    _label[k].gameObject.SetActive(false);
                }
            }
            _used = 0;
        }
    }
}
