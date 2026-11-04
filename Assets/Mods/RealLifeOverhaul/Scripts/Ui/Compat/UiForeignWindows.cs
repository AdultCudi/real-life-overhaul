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
    /// was when it closes), the cursor is freed, and every kit screen holds its keys while it stands, so Escape and the rest are
    /// its own first. Nothing at all while no kit screen shows. Game side.
    /// </summary>
    public static class UiForeignWindows
    {
        private const float Every = 0.25f;   // seconds between looks: a window takes longer than that to be read and answered

        private static readonly List<Canvas> Open = new List<Canvas>(4);          // the foreign windows up
        private static readonly List<bool> WasOverride = new List<bool>(4);       // ...and how each was sorted before it was lifted
        private static readonly List<int> WasOrder = new List<int>(4);
        private static readonly HashSet<int> Before = new HashSet<int>();         // the canvases up when the first kit screen opened
        private static readonly Dictionary<int, bool> Judged = new Dictionary<int, bool>();   // canvases inside the game's interface, judged once: another mod's or not
        private static IDictionary _registry;
        private static object[] _buffer = new object[64];
        private static bool _bound, _unavailable;
        private static int _shown;
        private static float _next;
        private static bool _cursorFreed;

        /// <summary>True while a window of another mod's that opened over the kit is up: every kit screen holds its keys.</summary>
        public static bool Up { get; private set; }

        /// <summary>True when Unity's list of canvases could be read, so a window opened over the kit is seen.</summary>
        public static bool Watching => !_unavailable && Bind();

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
                bool takesMouse = c.GetComponent<GraphicRaycaster>() != null;
                if (UiForeignRules.IsWindow(false, false, false, true, takesMouse))
                {
                    Rise(c);
                }
            }
            for (int i = Open.Count - 1; i >= 0; i--)
            {
                if (Open[i] == null || !Open[i].isActiveAndEnabled)
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
            }
            Up = Open.Count > 0;
            if (!Up)
            {
                _cursorFreed = false;
            }
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

        // Not the game's own: outside its interface, or inside it with a script or a listener of another mod's on it (asked once a canvas).
        private static bool Foreign(Canvas c)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || !c.transform.IsChildOf(uis.transform))
            {
                return true;
            }
            int id = c.GetInstanceID();
            if (!Judged.TryGetValue(id, out bool foreign))
            {
                foreign = UiForeignGame.OwnerOf(c.transform) != null;
                Judged[id] = foreign;
            }
            return foreign;
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
