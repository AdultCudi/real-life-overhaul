using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Draws a laid-out scene with the engine's own interface objects: each item is bound, in the scene's order, to a
    /// pooled object of its kind under its layer or its scroll region, and only what changed is set, so laying a screen
    /// out again after a press costs a few property writes and no allocation once the pools have grown. Colours come
    /// from the theme by role, sizes from the tokens through the scene: the view itself decides nothing about the look.
    /// Its parts: this file (the root, the layers, the scroll regions, a write), UiView.Slots.cs (each kind's object),
    /// UiView.Focus.cs (the focus ring and a target's face) and UiView.Motion.cs (arrivals, counts and fills). No engine
    /// type of the game: the same view renders the offline previews. Cost: nothing a frame; a write per change.
    /// </summary>
    public sealed partial class UiView
    {
        private const int Layers = 3;

        private readonly RectTransform _root;
        private readonly Container[] _layers = new Container[Layers];
        private readonly List<ClipView> _clips = new List<ClipView>(4);
        private Slot[] _itemSlots = new Slot[256];   // the object each item of the last write was bound to
        private UiScene _scene;
        private bool _dark;

        /// <summary>Canvas units in a px: 2 on the game's 3840 x 2160 canvas.</summary>
        public float Scale = UiTokens.CanvasPerPx;

        /// <summary>The Motion setting the view animates by.</summary>
        public UiMotionSetting Motion = UiMotionSetting.Full;

        /// <summary>Told when the pointer enters a target (its node), leaves it and presses it. Set by the host.</summary>
        public System.Action<int> PointerEntered, PointerExited, PointerPressed;

        /// <summary>Told when the wheel turns over a scroll region (its clip, the notches).</summary>
        public System.Action<int, float> Wheeled;

        /// <summary>Told when the pointer goes down on a target or drags from it: its node, the phase and the place in px of the layout.</summary>
        public System.Action<int, UiDragPoint> PointerDragged;

        /// <summary>A place on the screen as px of the layout, the root's top left its origin; false when it is off the root's plane.</summary>
        public bool ToLayout(Vector2 screen, Camera camera, out Vector2 px)
        {
            px = Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, camera, out Vector2 local))
            {
                return false;
            }
            px = new Vector2(local.x / Scale, -local.y / Scale);
            return true;
        }

        /// <summary>The root: anchored at its parent's top left, sized to the scene.</summary>
        public RectTransform Root => _root;

        public UiView(Transform parent, string name)
        {
            _root = NewRect(parent, name);
            for (int i = 0; i < Layers; i++)
            {
                _layers[i] = new Container(NewRect(_root, "Layer" + i));
                Stretch(_layers[i].Rect);
            }
            BuildFocus();
        }

        /// <summary>The scene last written.</summary>
        public UiScene Scene => _scene;

        /// <summary>
        /// Draws a scene in a theme; <paramref name="arriving"/> plays the arrival (rows rising in turn, figures counting
        /// up, bars filling) as on a screen's first showing or a new group's list.
        /// </summary>
        public void Write(UiScene s, bool arriving)
        {
            // A tween in flight holds the place its object had when it began. Its pooled object is bound to another item
            // now, and a tween left running pulled that item to the old place (beta 3: the Team's first row inside the
            // Games table, a figure's words in the next panel). Every tween lands before the objects are bound again.
            UiTweens.Instance?.StopAll(_root);
            _scene = s;
            bool themeChanged = s.Dark != _dark;
            _dark = s.Dark;
            var rootSize = new Vector2(s.Width * Scale, s.Height * Scale);
            if (_root.sizeDelta != rootSize)
            {
                _root.sizeDelta = rootSize;
            }
            for (int i = 0; i < Layers; i++)
            {
                _layers[i].Begin();
            }
            for (int i = 0; i < _clips.Count; i++)
            {
                _clips[i].Content.Begin();
                _clips[i].Placed = false;
            }
            if (_itemSlots.Length < s.ItemCount)
            {
                _itemSlots = new Slot[Mathf.NextPowerOfTwo(s.ItemCount)];
            }
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                Container into;
                if (it.Clip >= 0)
                {
                    ClipView clip = Clip(it.Clip, it.Layer);
                    if (!clip.Placed)
                    {
                        clip.Placed = true;
                        _layers[it.Layer].Order.Add(clip.Viewport);
                    }
                    into = clip.Content;
                }
                else
                {
                    into = _layers[Mathf.Min(it.Layer, Layers - 1)];
                }
                Slot slot = into.Take(it.Kind, this);
                Bind(slot, ref it, i);
                into.Order.Add(slot.Rect);
                _itemSlots[i] = slot;
            }
            for (int i = 0; i < _clips.Count; i++)
            {
                ClipView c = _clips[i];
                c.Content.End();
                bool live = i < s.ClipCount;
                Show(c.Viewport, live && c.Placed);
                if (live)
                {
                    ref UiClip uc = ref s.Clips[i];
                    Place(c.Viewport, uc.X, uc.Y, uc.W, uc.H);
                    var moved = new Vector2(0f, uc.Offset * Scale);
                    var room = new Vector2(uc.W * Scale, Mathf.Max(uc.H, uc.ContentHeight) * Scale);
                    if (c.Content.Rect.anchoredPosition != moved)
                    {
                        c.Content.Rect.anchoredPosition = moved;
                    }
                    if (c.Content.Rect.sizeDelta != room)
                    {
                        c.Content.Rect.sizeDelta = room;
                    }
                }
            }
            for (int i = 0; i < Layers; i++)
            {
                _layers[i].End();
            }
            WriteFocus(themeChanged);
            if (arriving)
            {
                Arrive();
            }
        }

        /// <summary>The object an item of the last write was bound to, for the host's pointer and the motion.</summary>
        internal Slot SlotOf(int item) => item >= 0 && item < _itemSlots.Length ? _itemSlots[item] : null;

        // ------------------------------------------------------------------------------------------------- scroll regions

        private sealed class ClipView
        {
            public RectTransform Viewport;
            public Container Content;
            public bool Placed;
            public int Index;
        }

        private ClipView Clip(int index, int layer)
        {
            while (_clips.Count <= index)
            {
                var c = new ClipView { Index = _clips.Count };
                c.Viewport = NewRect(_layers[Mathf.Min(layer, Layers - 1)].Rect, "Clip" + c.Index);
                c.Viewport.gameObject.AddComponent<RectMask2D>();
                var catcher = c.Viewport.gameObject.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);   // takes the wheel anywhere over the region
                c.Viewport.gameObject.AddComponent<UiHit>().Wheel(this, c.Index);
                c.Content = new Container(NewRect(c.Viewport, "Content"));
                _clips.Add(c);
            }
            ClipView found = _clips[index];
            Transform want = _layers[Mathf.Min(layer, Layers - 1)].Rect;
            if (found.Viewport.parent != want)
            {
                found.Viewport.SetParent(want, false);
            }
            return found;
        }

        // ------------------------------------------------------------------------------------------------- containers

        /// <summary>A parent of drawn objects: a layer or a scroll region's content, with its own pools so nothing changes parent.</summary>
        private sealed class Container
        {
            public readonly RectTransform Rect;
            public readonly List<Transform> Order = new List<Transform>(64);
            private readonly List<Slot>[] _pools = new List<Slot>[(int)UiKind.Picture + 1];
            private readonly int[] _used = new int[(int)UiKind.Picture + 1];

            public Container(RectTransform rect)
            {
                Rect = rect;
            }

            public void Begin()
            {
                Order.Clear();
                for (int i = 0; i < _used.Length; i++)
                {
                    _used[i] = 0;
                }
            }

            public Slot Take(UiKind kind, UiView view)
            {
                int k = (int)kind;
                List<Slot> pool = _pools[k] ?? (_pools[k] = new List<Slot>(16));
                Slot s;
                if (_used[k] < pool.Count)
                {
                    s = pool[_used[k]];
                }
                else
                {
                    s = view.Make(kind, Rect);
                    pool.Add(s);
                }
                _used[k]++;
                Show(s.Rect, true);
                return s;
            }

            // The objects not used this time hidden, and the used ones in the scene's drawing order.
            public void End()
            {
                for (int k = 0; k < _pools.Length; k++)
                {
                    List<Slot> pool = _pools[k];
                    for (int i = _used[k]; pool != null && i < pool.Count; i++)
                    {
                        Show(pool[i].Rect, false);
                        pool[i].Node = -1;
                    }
                }
                for (int i = 0; i < Order.Count; i++)
                {
                    if (Order[i].GetSiblingIndex() != i)
                    {
                        Order[i].SetSiblingIndex(i);
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- helpers

        internal static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            return r;
        }

        internal static void Stretch(RectTransform r)
        {
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        internal void Place(RectTransform r, float x, float y, float w, float h)
        {
            var pos = new Vector2(x * Scale, -y * Scale);
            var size = new Vector2(Mathf.Max(0f, w * Scale), Mathf.Max(0f, h * Scale));
            if (r.anchoredPosition != pos)
            {
                r.anchoredPosition = pos;
            }
            if (r.sizeDelta != size)
            {
                r.sizeDelta = size;
            }
        }

        internal static void Show(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on)
            {
                c.gameObject.SetActive(on);
            }
        }

        /// <summary>A role's colour in the view's theme, at an item's alpha.</summary>
        internal Color Colour(UiRole role, float alpha = 1f)
        {
            UiColour c = UiColour.Of(role, _dark);
            return new Color(c.R, c.G, c.B, c.A * alpha);
        }

        internal static Color ToColor(UiColour c, float alpha = 1f) => new Color(c.R, c.G, c.B, c.A * alpha);
    }
}
