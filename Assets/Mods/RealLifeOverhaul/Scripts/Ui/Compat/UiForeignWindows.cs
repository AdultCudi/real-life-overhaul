using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Another mod's own canvas or window, opened while a screen of the kit is up. The kit's canvases stand just over the screen
    /// they cover, so a window of a mod's at the same order or below would be hidden by them, and the kit's keys (Escape above
    /// all) would be taken before the window's. While a kit screen shows, this looks four times a second at the canvases that
    /// hold graphics (Unity's own registry of them, read through its private dictionary once, copied into a buffer kept: no
    /// scene search, nothing allocated). A canvas that was not up when the first kit screen opened, is not the kit's or the
    /// game's own, takes the mouse and is active now is a foreign window: it is lifted over the kit's canvases (put back as it
    /// was when it closes), the cursor is freed, and every kit screen holds its keys while it stands and takes the mouse, so
    /// Escape and the rest are its own first. The canvas the kit draws its own screens on is never one (UiForeignRules.Foreign).
    /// Nothing at all while no kit screen shows. Game side.
    /// </summary>
    public static class UiForeignWindows
    {
        private const float Every = 0.25f;   // seconds between looks: a window takes longer than that to be read and answered

        private static readonly List<Canvas> Open = new List<Canvas>(4);          // the foreign windows up
        private static readonly List<bool> WasOverride = new List<bool>(4);       // ...and how each was sorted before it was lifted
        private static readonly List<int> WasOrder = new List<int>(4);
        private static readonly HashSet<int> Before = new HashSet<int>();         // the canvases up when the first kit screen opened
        private static readonly Dictionary<int, bool> Judged = new Dictionary<int, bool>();   // canvases judged once: another mod's or not
        private static IDictionary _registry;
        private static object[] _buffer = new object[64];
        private static bool _bound, _unavailable;
        private static int _shown;
        private static float _next;
        private static bool _cursorFreed;

        /// <summary>True while a window of another mod's that opened over the kit is up and takes the mouse: every kit screen holds its keys.</summary>
        public static bool Up { get; private set; }

        /// <summary>True when Unity's list of canvases could be read, so a window opened over the kit is seen.</summary>
        public static bool Watching => !_unavailable && Bind();

        /// <summary>The first window holding the keys, by its object's name, for the press line (Developer diagnostics); null when none is.</summary>
        public static string Named
        {
            get
            {
                for (int i = 0; i < Open.Count; i++)
                {
                    if (Open[i] != null && TakesMouse(Open[i]))
                    {
                        return Open[i].name;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// The kit now draws a screen on this canvas (a host bound to it): what was judged of it is forgotten, and it is put back
        /// if it was lifted, so the canvas the kit stands on never holds the kit's keys, however early it was first seen.
        /// </summary>
        public static void Forget(Canvas c)
        {
            if (c == null)
            {
                return;
            }
            Judged.Remove(c.GetInstanceID());
            int i = Open.IndexOf(c);
            if (i >= 0)
            {
                Drop(i);
                Settle();
            }
        }

        /// <summary>A kit screen began or stopped showing; the first one takes the picture of what is up, the last puts everything back.</summary>
        public static void Shown(bool shown)
        {
            if (shown)
            {
                if (_shown++ == 0)
                {
                    Snapshot();
                }
                return;
            }
            if (_shown > 0 && --_shown == 0)
            {
                Release();
            }
        }

        /// <summary>A look, from a shown screen's frame: cheap unless it is time, then a pass over the canvases.</summary>
        public static void Poll()
        {
            if (_shown == 0 || _unavailable || Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + Every;
            try
            {
                Look();
            }
            catch (Exception e)
            {
                _unavailable = true;   // not as this Unity has it: another mod's windows are left as they were
                UiDirector.Logger?.Warn("UI: another mod's windows over the kit are not watched (" + e.GetType().Name + ").");
                Release();
            }
        }

        /// <summary>Everything put back: the kit stopped, the city went.</summary>
        public static void Release()
        {
            for (int i = 0; i < Open.Count; i++)
            {
                if (Open[i] != null)
                {
                    Open[i].overrideSorting = WasOverride[i];
                    Open[i].sortingOrder = WasOrder[i];
                }
            }
            Open.Clear();
            WasOverride.Clear();
            WasOrder.Clear();
            Judged.Clear();
            Before.Clear();
            Up = false;
            _cursorFreed = false;
            _shown = 0;
        }

        // The canvases standing now are the background: a window that was there before the kit opened is not one that opened over it.
        private static void Snapshot()
        {
            Before.Clear();
            Up = false;
            if (_unavailable || !Bind())
            {
                return;
            }
            try
            {
                int n = Copy();
                for (int i = 0; i < n; i++)
                {
                    if (Eligible(_buffer[i] as Canvas, out Canvas c))
                    {
                        Before.Add(c.GetInstanceID());   // every canvas up now is the background, the game's and the mods': none of it opened over the kit
                    }
                }
            }
            catch (Exception)
            {
                _unavailable = true;
            }
        }

        private static void Look()
        {
            if (!Bind())
            {
                return;
            }
            int n = Copy();
            for (int i = 0; i < n; i++)
            {
                if (!Eligible(_buffer[i] as Canvas, out Canvas c) || Before.Contains(c.GetInstanceID()) || Open.Contains(c) || !Foreign(c))
                {
                    continue;
                }
                if (UiForeignRules.IsWindow(false, false, false, true, TakesMouse(c)))
                {
                    Rise(c);
                }
            }
            for (int i = Open.Count - 1; i >= 0; i--)
            {
                if (Open[i] == null || !Open[i].isActiveAndEnabled)
                {
                    Drop(i);
                }
            }
            Settle();
        }

        // A window put back as it was sorted before it was lifted, and no longer counted.
        private static void Drop(int i)
        {
            if (Open[i] != null)
            {
                Open[i].overrideSorting = WasOverride[i];
                Open[i].sortingOrder = WasOrder[i];
            }
            Open.RemoveAt(i);
            WasOverride.RemoveAt(i);
            WasOrder.RemoveAt(i);
        }

        private static void Settle()
        {
            Up = false;
            for (int i = 0; i < Open.Count && !Up; i++)
            {
                // One faded out or letting clicks through, but left switched on, stays lifted (so a fade out is seen whole) and
                // holds no keys: an unseen window would leave every kit screen deaf.
                Up = Open[i] != null && TakesMouse(Open[i]);
            }
            if (!Up)
            {
                _cursorFreed = false;
            }
        }

        // The mouse stops at it as at a window: its raycaster on, its own group (if any) seen and blocking.
        private static bool TakesMouse(Canvas c)
        {
            GraphicRaycaster ray = c.GetComponent<GraphicRaycaster>();
            CanvasGroup group = c.GetComponent<CanvasGroup>();
            bool grouped = group != null && group.enabled;
            return UiForeignRules.TakesMouse(ray != null && ray.enabled, grouped, grouped ? group.alpha : 1f, !grouped || group.blocksRaycasts);
        }

        // A canvas another mod opened over the kit: over the kit's canvases from now, the mouse free, said once.
        private static void Rise(Canvas c)
        {
            Open.Add(c);
            WasOverride.Add(c.overrideSorting);
            WasOrder.Add(c.sortingOrder);
            int top = UiGameHost.TopOrder();
            int order = UiForeignRules.Lifted(c.sortingOrder, top);
            if (!c.overrideSorting || order != c.sortingOrder)
            {
                c.overrideSorting = true;
                c.sortingOrder = order;
            }
            if (!_cursorFreed && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _cursorFreed = true;
            }
            string owner = UiForeignGame.OwnerOf(c.transform) ?? c.gameObject.name;
            UiForeign.Note("windows", UiPieceKind.Canvas, owner, c.gameObject.name, "it is lifted over the kit, takes the mouse and the keys first, and the kit waits until it closes");
            Up = true;
        }

        // A canvas that sorts by itself (a root or an override), active, with graphics, not the kit's and not under the game's own interface.
        private static bool Eligible(Canvas candidate, out Canvas c)
        {
            c = candidate;
            if (c == null || !c.isActiveAndEnabled || !(c.isRootCanvas || c.overrideSorting) || c.renderMode == RenderMode.WorldSpace)
            {
                return false;
            }
            Transform t = c.transform;
            if (t.name.StartsWith(UiHostRules.ModPrefix, StringComparison.Ordinal))
            {
                return false;   // the kit's own, or the mod's
            }
            return true;
        }

        // Another mod's (UiForeignRules.Foreign), asked once a canvas: the search for a mod's script under one inside the game's
        // interface is made only for a canvas the kit does not draw on.
        private static bool Foreign(Canvas c)
        {
            int id = c.GetInstanceID();
            if (Judged.TryGetValue(id, out bool foreign))
            {
                return foreign;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            bool inGame = uis != null && c.transform.IsChildOf(uis.transform);
            bool ground = KitGround(c.transform);
            foreign = UiForeignRules.Foreign(ground, inGame, !ground && inGame && UiForeignGame.OwnerOf(c.transform) != null);
            Judged[id] = foreign;
            return foreign;
        }

        // A canvas of the kit's stands straight on it, as UiGameHost and UiOverCanvas put theirs (switched off between showings too).
        private static bool KitGround(Transform canvas)
        {
            for (int i = 0; i < canvas.childCount; i++)
            {
                Transform child = canvas.GetChild(i);
                if (child.name.StartsWith(UiHostRules.ModPrefix, StringComparison.Ordinal) && child.GetComponent<Canvas>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        // Unity's registry of the canvases that hold graphics: a dictionary keyed by canvas, read through the private field once.
        private static bool Bind()
        {
            if (_bound)
            {
                return _registry != null;
            }
            _bound = true;
            try
            {
                Type type = typeof(GraphicRegistry);
                object instance = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
                _registry = instance != null ? type.GetField("m_Graphics", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance) as IDictionary : null;
            }
            catch (Exception)
            {
                _registry = null;
            }
            if (_registry == null)
            {
                _unavailable = true;
                UiDirector.Logger?.Warn("UI: another mod's windows over the kit are not watched (Unity's list of canvases is not as expected).");
            }
            return _registry != null;
        }

        // The registry's canvases copied into the buffer kept; returns how many.
        private static int Copy()
        {
            int count = _registry.Count;
            if (_buffer.Length < count)
            {
                _buffer = new object[count * 2];
            }
            _registry.Keys.CopyTo(_buffer, 0);
            return count;
        }
    }
}
