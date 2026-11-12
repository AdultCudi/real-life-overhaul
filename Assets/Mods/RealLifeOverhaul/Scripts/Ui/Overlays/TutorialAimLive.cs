using System.Collections.Generic;
using Tutorial;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The tutorial's arrow aimed at the kit's own button. The game points an arrow at one of its own buttons by the button's
    /// scene path and sets it at the button's centre; where the kit draws a screen it hides the game's buttons (alpha 0, still
    /// there), so the arrow stood where the hidden button is. Four times a second, while the tutorial has an arrow at a hidden
    /// button, the kit looks in the views that stand for the node that says the same words as the button and keeps the arrow on
    /// it each frame, after the game moved it; where no node says them the arrow is put away until one does, so it never points
    /// at nothing. The arrows come back exactly as found when the switch goes off. Cost: nothing without an arrow; with one, a
    /// walk of the standing views' items four times a second and one position write a frame.
    /// </summary>
    internal static class TutorialAimLive
    {
        private const float Every = 0.25f;   // s of real time between looks at the arrows and the views
        private const int Most = 4;          // arrows a quest entry points at once

        private static readonly Transform[] Arrows = new Transform[Most];
        private static readonly Vector3[] At = new Vector3[Most];
        private static readonly bool[] Aimed = new bool[Most];
        private static readonly bool[] Put = new bool[Most];   // the arrow's own object put away by this, to be given back
        private static float _due;
        private static int _count;

        /// <summary>How many arrows are aimed at the kit's buttons now, for the tests.</summary>
        public static int Aiming
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    n += Aimed[i] ? 1 : 0;
                }
                return n;
            }
        }

        /// <summary>Looks again at the tutorial's arrows, four times a second.</summary>
        public static void Tick(float now)
        {
            if (now < _due)
            {
                return;
            }
            _due = now + Every;
            var list = GameContract.TutorialPointers.Field?.GetValue(null) as List<TutorialPointer>;
            if (list == null || list.Count == 0 || GameContract.TutorialTarget.Field == null)
            {
                Release();
                return;
            }
            int n = 0;
            for (int i = 0; i < list.Count && n < Most; i++)
            {
                TutorialPointer pointer = list[i];
                var data = pointer != null ? pointer.data as TutorialPointerDataUiElement : null;
                var target = data != null ? GameContract.TutorialTarget.Field.GetValue(data) as RectTransform : null;
                Transform arrow = pointer != null && pointer.transform.childCount > 0 ? pointer.transform.GetChild(0) : null;
                if (target == null || arrow == null || !target.gameObject.activeInHierarchy || !Hidden(target))
                {
                    continue;   // the game's button is shown as it is, or there is none: the arrow is the game's own
                }
                if (arrow != Arrows[n])
                {
                    Give(n);
                }
                Arrows[n] = arrow;
                bool found = Find(UiWidgets.Label(target), out Vector2 screen);
                Aimed[n] = found;
                if (found)
                {
                    At[n] = new Vector3(screen.x, screen.y, arrow.position.z);
                    if (Put[n] && !arrow.gameObject.activeSelf)
                    {
                        arrow.gameObject.SetActive(true);
                        Put[n] = false;
                    }
                }
                else if (arrow.gameObject.activeSelf)
                {
                    arrow.gameObject.SetActive(false);   // no node says what the button says: the arrow waits rather than point at nothing
                    Put[n] = true;
                }
                n++;
            }
            for (int i = n; i < _count; i++)
            {
                Give(i);
            }
            _count = n;
        }

        /// <summary>A frame: each arrow aimed at a kit button put on it, after the game's own move of it.</summary>
        public static void Late()
        {
            for (int i = 0; i < _count; i++)
            {
                Transform arrow = Arrows[i];
                if (Aimed[i] && arrow != null && arrow.gameObject.activeInHierarchy)
                {
                    arrow.position = At[i];
                }
            }
        }

        /// <summary>Every arrow back as the game had it.</summary>
        public static void Release()
        {
            for (int i = 0; i < _count; i++)
            {
                Give(i);
            }
            _count = 0;
        }

        private static void Give(int i)
        {
            if (Put[i] && Arrows[i] != null)
            {
                Arrows[i].gameObject.SetActive(true);
            }
            Put[i] = false;
            Aimed[i] = false;
            Arrows[i] = null;
        }

        // The game's button is one the kit hides: a group over it that is clear and cannot be pressed (UiConceal).
        private static bool Hidden(RectTransform target)
        {
            for (Transform t = target; t != null; t = t.parent)
            {
                CanvasGroup g = t.GetComponent<CanvasGroup>();
                if (g != null && g.alpha <= 0.01f && !g.interactable)
                {
                    return true;
                }
            }
            return false;
        }

        // The centre, on the screen, of the target of a standing view that says these words; false when none does.
        private static bool Find(string label, out Vector2 screen)
        {
            screen = default(Vector2);
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }
            label = label.Trim();
            List<UiView> views = UiPointer.Views;
            for (int v = views.Count - 1; v >= 0; v--)
            {
                UiView view = views[v];
                UiScene scene = view != null ? view.Scene : null;
                if (scene == null || !view.Root.gameObject.activeInHierarchy)
                {
                    continue;
                }
                for (int i = 0; i < scene.ItemCount; i++)
                {
                    UiItem item = scene.Items[i];
                    if (item.Kind != UiKind.Text || item.Node < 0 || item.Clip >= 0 || !string.Equals(item.Text, label, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    UiNode node = scene.Nodes[item.Node];
                    if (node.Disabled || node.Clip >= 0)
                    {
                        continue;
                    }
                    Vector3 world = view.Root.TransformPoint(new Vector3((node.X + node.W / 2f) * view.Scale, -(node.Y + node.H / 2f) * view.Scale, 0f));
                    screen = new Vector2(world.x, world.y);
                    return true;
                }
            }
            return false;
        }
    }
}
