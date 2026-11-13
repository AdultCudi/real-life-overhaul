using Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Leads a new player to the row an arrow of the tutorial aims at. The game hides such an arrow when its row is outside
    /// the bounds of its own scroll view (TutorialPointerHideConditionIfUiNotVisibleInScrollView), judged on the list the kit
    /// hides, so the game's list is scrolled to the row (as the game does for its map filters) and the kit's own list is
    /// scrolled the way its wheel does until the row stands in view. The kit's list moves once for each arrow: a player who
    /// scrolls away afterwards is left alone. Cost: nothing without an arrow at a hidden row; with one, a few looks four times
    /// a second until the row shows, and never more than a hundred steps for one row.
    /// </summary>
    internal static class TutorialAimScroll
    {
        private const int StepsPerLook = 6;    // wheel notches given in one look
        private const int MostSteps = 120;     // notches given for one row, however the list answers

        private static RectTransform _key;
        private static string _keyLabel;
        private static int _spent;
        private static bool _done;
        private static int _flips;

        /// <summary>The row stands where the arrow can show: the game's list is scrolled to it, and the game's own conditions then allow the arrow.</summary>
        public static bool Reveal(TutorialPointerData data, RectTransform target)
        {
            ScrollRect scroll = ScrollOf(target);
            if (scroll != null && !Inside(scroll, target))
            {
                Show(scroll, target);
            }
            return data.ShouldBeEnabled();
        }

        /// <summary>The kit's row was found and drawn in view: it is not led to again.</summary>
        public static void Settled(RectTransform target, string label)
        {
            Begin(target, label);
            _done = true;
        }

        /// <summary>Forgets the row being led to (the arrow went).</summary>
        public static void Reset()
        {
            _key = null;
            _keyLabel = null;
        }

        /// <summary>
        /// Scrolls the kit's list toward the row that says this; true when the row is in a standing view's scene in view after it.
        /// Only a row of a scrolling game list is led to.
        /// </summary>
        public static bool Lead(RectTransform target, string label)
        {
            ScrollRect game = ScrollOf(target);
            if (string.IsNullOrWhiteSpace(label) || game == null)
            {
                return false;
            }
            Begin(target, label);
            if (_done || _spent >= MostSteps)
            {
                return false;
            }
            string words = label.Trim();
            float place = Place(game, target);
            var views = UiPointer.Views;
            for (int v = views.Count - 1; v >= 0; v--)
            {
                UiView view = views[v];
                UiScene scene = view != null ? view.Scene : null;
                if (scene == null || view.Wheeled == null || !view.Root.gameObject.activeInHierarchy)
                {
                    continue;
                }
                int clip = Choose(scene, words, place, out int notches);
                if (clip < 0)
                {
                    continue;
                }
                for (int step = 0; step < StepsPerLook && _spent < MostSteps; step++)
                {
                    float before = scene.Clips[clip].Offset;
                    view.Wheeled(clip, notches);
                    _spent++;
                    if (clip >= scene.ClipCount)
                    {
                        break;
                    }
                    if (Mathf.Abs(scene.Clips[clip].Offset - before) < 0.01f)
                    {
                        notches = -notches;   // the end of the list, or a list that does not scroll: look the other way once, then stop
                        if (++_flips > 1)
                        {
                            _spent = MostSteps;
                            break;
                        }
                    }
                    else if (Standing(scene, words))
                    {
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        private static void Begin(RectTransform target, string label)
        {
            if (target != _key || label != _keyLabel)
            {
                _key = target;
                _keyLabel = label;
                _spent = 0;
                _flips = 0;
                _done = false;
            }
        }

        // The clip to give notches to and their sign (a positive notch scrolls up): the clip holding the row when the scene has
        // it out of view, else the scene's main list, toward where the row stands in the game's list.
        private static int Choose(UiScene s, string label, float place, out int notches)
        {
            notches = -1;
            for (int i = 0; i < s.ItemCount; i++)
            {
                UiItem item = s.Items[i];
                if (item.Kind != UiKind.Text || item.Clip < 0 || !string.Equals(item.Text, label, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                s.ScreenBox(i, out _, out float y, out _, out float h);
                ref UiClip c = ref s.Clips[item.Clip];
                if (y + h / 2f < c.Y)
                {
                    notches = 1;
                    return item.Clip;
                }
                if (y + h / 2f > c.Y + c.H)
                {
                    return item.Clip;
                }
            }
            int best = -1;
            float most = 0f;
            for (int i = 0; i < s.ClipCount; i++)
            {
                ref UiClip c = ref s.Clips[i];
                float room = c.ContentHeight - c.H;
                if (room <= 1f)
                {
                    continue;
                }
                if (c.List >= 0)
                {
                    room += 1000000f;   // a list of rows is what the game's list shows
                }
                if (room > most)
                {
                    most = room;
                    best = i;
                }
            }
            if (best >= 0 && place >= 0f)
            {
                ref UiClip c = ref s.Clips[best];
                notches = place * c.ContentHeight > c.Offset + c.H / 2f ? -1 : 1;
            }
            return best;
        }

        // The row is in the scene, in the view of its clip, and can be pressed.
        private static bool Standing(UiScene s, string label)
        {
            for (int i = 0; i < s.ItemCount; i++)
            {
                UiItem item = s.Items[i];
                if (item.Kind != UiKind.Text || item.Node < 0 || !string.Equals(item.Text, label, System.StringComparison.OrdinalIgnoreCase) || s.Nodes[item.Node].Disabled)
                {
                    continue;
                }
                s.NodeBox(item.Node, out _, out float y, out _, out float h);
                short clip = s.Nodes[item.Node].Clip;
                if (clip < 0 || (y + h / 2f >= s.Clips[clip].Y && y + h / 2f <= s.Clips[clip].Y + s.Clips[clip].H))
                {
                    return true;
                }
            }
            return false;
        }

        // The game's scroll view the row sits in: the nearest above it (a walk of the parents, so a switched off view is found too).
        private static ScrollRect ScrollOf(RectTransform target)
        {
            for (Transform t = target != null ? target.parent : null; t != null; t = t.parent)
            {
                ScrollRect scroll = t.GetComponent<ScrollRect>();
                if (scroll != null && scroll.content != null)
                {
                    return scroll;
                }
            }
            return null;
        }

        // The game's own test: the row inside its scroll view's rect.
        private static bool Inside(ScrollRect scroll, RectTransform target)
        {
            var view = (RectTransform)scroll.transform;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view, target);
            return view.rect.Contains(bounds.min) && view.rect.Contains(bounds.max);
        }

        // How far down its content the row stands, 0 at the top to 1 at the foot.
        private static float Place(ScrollRect scroll, RectTransform target)
        {
            RectTransform content = scroll.content;
            if (content.rect.height <= 0f)
            {
                return -1f;
            }
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, target);
            return Mathf.Clamp01((content.rect.yMax - bounds.center.y) / content.rect.height);
        }

        // The row's centre to the view's centre, as the game's own scroll to a map filter does it.
        private static void Show(ScrollRect scroll, RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            float room = scroll.content.rect.height - view.rect.height;
            if (room <= 0f)
            {
                return;
            }
            float by = RectTransformUtility.CalculateRelativeRectTransformBounds(view, target).center.y - view.rect.center.y;
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + by / room);
        }
    }
}
