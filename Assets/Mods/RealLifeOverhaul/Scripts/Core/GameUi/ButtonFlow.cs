using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The layout of a row of buttons of the apps' white box (InfoBox.ButtonRow), in place of the game's own row's
    /// HorizontalLayoutGroup. The game's row has one button of each kind (positive, negative, additional) and a page
    /// asked for a kind by its place in the row, so a second button of a kind took the first one's place: the Campus
    /// app's "Enrol on a student loan" was written over by "Close", and some forty other rows of the apps lost a button
    /// the same way. And the game's row is one line, so buttons that did not fit were squeezed until their words were
    /// cut. Here a row shows every button its page writes, in the order it writes them: a kind's own button first, a
    /// copy of it (<see cref="Take"/>) for every further one, and the copies go when the row is written again.
    /// Where they stand is ButtonRules': lines from the box's left edge to its right, the game's gap between buttons
    /// and lines, as many lines as the words need.
    ///
    /// Cost: nothing per frame. The engine asks a layout only when something under it changed (a page written on a
    /// click): then one measure of each button's words and arithmetic over the buttons that show. Lists are kept and
    /// used again.
    /// </summary>
    public sealed class ButtonFlow : LayoutGroup
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "ButtonFlow", typeof(ButtonFlow));   // the game's calls of it (Core/Guard)

        private const string CopyMark = " (more)";   // the name of a copy of one of the row's own buttons

        private struct Shown
        {
            public RectTransform Button;
            public int Kind;
            public string Key;
        }

        private readonly List<Shown> _shown = new List<Shown>(4);                   // the buttons written on this page, in order
        private readonly List<GameObject> _copies = new List<GameObject>(2);        // the copies made for this page
        private readonly List<RectTransform> _order = new List<RectTransform>(4);   // the buttons laid out, in order
        private float[] _needs = new float[4], _x = new float[4], _w = new float[4];
        private int[] _line = new int[4];
        private int _originals, _lines;

        /// <summary>The row is written again: the copies of the last page go, the row's own buttons are hidden.</summary>
        public void Begin()
        {
            for (int i = 0; i < _copies.Count; i++)
            {
                GameObject copy = _copies[i];
                if (copy != null && copy.transform.parent == transform)
                {
                    copy.SetActive(false);
                    copy.transform.SetParent(null, false);
                    Destroy(copy);
                }
            }
            _copies.Clear();
            _shown.Clear();
            _originals = transform.childCount;
            for (int i = 0; i < _originals; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
            SetDirty();
        }

        /// <summary>
        /// The button a page writes with this kind and key: the one already written with both on this page (written
        /// again), else the row's own button of the kind if this page has not used it, else a new copy of it. A row with
        /// fewer kinds uses the last it has.
        /// </summary>
        public Transform Take(int kind, string key)
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i].Kind == kind && _shown[i].Key == key && _shown[i].Button != null)
                {
                    return _shown[i].Button;
                }
            }
            int originals = _originals > 0 && _originals <= transform.childCount ? _originals : transform.childCount - _copies.Count;
            if (originals <= 0)
            {
                return null;
            }
            var own = (RectTransform)transform.GetChild(Mathf.Clamp(kind, 0, originals - 1));
            bool used = false;
            for (int i = 0; i < _shown.Count && !used; i++)
            {
                used = _shown[i].Button == own;
            }
            RectTransform button = own;
            if (used)
            {
                GameObject copy = Instantiate(own.gameObject, transform, false);
                copy.name = own.name + CopyMark;
                for (int i = 0; i < copy.transform.childCount; i++)
                {
                    copy.transform.GetChild(i).localScale = Vector3.one;   // (a copy of the button under the pointer is not taken at its hover size)
                }
                _copies.Add(copy);
                button = (RectTransform)copy.transform;
            }
            _shown.Add(new Shown { Button = button, Kind = kind, Key = key });
            SetDirty();
            return button;
        }

        public override void CalculateLayoutInputHorizontal()
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                base.CalculateLayoutInputHorizontal();   // the buttons that show (rectChildren)
                Order();
                SetLayoutInputForAxis(0f, 0f, 1f, 0);   // as wide as the box makes it: the lines fit whatever width it is
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public override void CalculateLayoutInputVertical()
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Place();
                float height = ButtonRules.RowHeight(_lines);
                SetLayoutInputForAxis(height, height, -1f, 1);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public override void SetLayoutHorizontal()
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Place();
                for (int i = 0; i < _order.Count; i++)
                {
                    SetChildAlongAxis(_order[i], 0, _x[i], _w[i]);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        public override void SetLayoutVertical()
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                float height = rectTransform.rect.height;
                for (int i = 0; i < _order.Count; i++)
                {
                    SetChildAlongAxis(_order[i], 1, ButtonRules.Top(height, _lines, _line[i]), ButtonRules.Height);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        // The buttons that show, in the order the page wrote them (any other shown by another hand after them), and the
        // width each one's words need.
        private void Order()
        {
            _order.Clear();
            for (int i = 0; i < _shown.Count; i++)
            {
                RectTransform button = _shown[i].Button;
                if (button != null && rectChildren.Contains(button) && !_order.Contains(button))
                {
                    _order.Add(button);
                }
            }
            for (int i = 0; i < rectChildren.Count; i++)
            {
                if (!_order.Contains(rectChildren[i]))
                {
                    _order.Add(rectChildren[i]);
                }
            }
            if (_needs.Length < _order.Count)
            {
                int size = _order.Count * 2;
                _needs = new float[size];
                _x = new float[size];
                _w = new float[size];
                _line = new int[size];
            }
            for (int i = 0; i < _order.Count; i++)
            {
                var words = _order[i].GetComponentInChildren<TMP_Text>(true);
                _needs[i] = ButtonRules.Need(words != null && !string.IsNullOrEmpty(words.text) ? words.preferredWidth : 0f);
            }
        }

        // The lines for the width the row has now.
        private void Place()
        {
            _lines = ButtonRules.Place(rectTransform.rect.width - padding.horizontal, _needs, _order.Count, _line, _x, _w);
            for (int i = 0; i < _order.Count; i++)
            {
                _x[i] += padding.left;
            }
        }
    }
}
