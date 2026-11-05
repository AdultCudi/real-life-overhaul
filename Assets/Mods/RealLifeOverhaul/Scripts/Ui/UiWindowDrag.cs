using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A window the player moves by its title strip, as the game's own small windows move (DraggableWindows): the strip is the
    /// only grip, the window is kept whole inside the screen (one larger than the screen is centred), and the place is the
    /// window's own. A layout is made where the screen put it and this moves what it laid, so no screen has code for it.
    /// Game-free. Cost: a pass over the scene's items on a layout of a draggable window; nothing otherwise.
    /// </summary>
    public static class UiWindowDrag
    {
        /// <summary>
        /// The move that keeps a box whole inside an area, as the game's ClampPosition does: the wanted move when the box fits
        /// there, the least that brings it in, and the centring one when it is larger than the area.
        /// </summary>
        public static float Contain(float min, float max, float areaMin, float areaMax, float move)
        {
            min += move;
            max += move;
            if (max - min > areaMax - areaMin)
            {
                return move + (areaMin + areaMax - min - max) * 0.5f;
            }
            return min < areaMin ? move + areaMin - min : max > areaMax ? move + areaMax - max : move;
        }

        private static bool[] _inside = new bool[256];     // per item of the scene: it belongs to the window
        private static bool[] _clipInside = new bool[8];

        /// <summary>
        /// Moves the laid window by (dx, dy), kept inside the area from the top left to (width, bottom). The window is the panel its
        /// strip sits in with everything drawn in it; with no panel, everything but the prompt bar. Gives the move made, which the
        /// clamp may have cut short.
        /// </summary>
        public static void Apply(UiScene s, float dx, float dy, float width, float bottom, out float movedX, out float movedY)
        {
            Box(s, out float x, out float y, out float w, out float h);
            movedX = Contain(x, x + w, 0f, width, dx);
            movedY = Contain(y, y + h, 0f, bottom, dy);
            if (movedX == 0f && movedY == 0f)
            {
                return;
            }
            if (_inside.Length < s.ItemCount)
            {
                _inside = new bool[Math.Max(s.ItemCount, _inside.Length * 2)];
            }
            if (_clipInside.Length < s.ClipCount)
            {
                _clipInside = new bool[Math.Max(s.ClipCount, _clipInside.Length * 2)];
            }
            Array.Clear(_clipInside, 0, s.ClipCount);
            bool panel = s.HandlePanel >= 0 && s.HandlePanel < s.ItemCount;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                bool in_ = panel ? i == s.HandlePanel || (it.Parent >= 0 && it.Parent < i && _inside[it.Parent]) : (it.Flags & UiFlags.Prompt) == 0;
                _inside[i] = in_;
                if (!in_)
                {
                    continue;
                }
                if (it.Clip >= 0)
                {
                    _clipInside[it.Clip] = true;   // what scrolls keeps its place in its region, and the region moves
                    continue;
                }
                it.X += movedX;
                it.Y += movedY;
            }
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                bool in_ = !panel ? true : n.Face >= 0 && n.Face < s.ItemCount ? _inside[n.Face] : false;
                if (!in_)
                {
                    continue;
                }
                if (n.Clip >= 0)
                {
                    _clipInside[n.Clip] = true;
                    continue;
                }
                n.X += movedX;
                n.Y += movedY;
            }
            for (int i = 0; i < s.ClipCount; i++)
            {
                if (_clipInside[i] || !panel)
                {
                    s.Clips[i].X += movedX;
                    s.Clips[i].Y += movedY;
                }
            }
        }

        // The window's box: its panel, or when its strip had none the bounds of what the base layer drew.
        private static void Box(UiScene s, out float x, out float y, out float w, out float h)
        {
            if (s.HandlePanel >= 0 && s.HandlePanel < s.ItemCount && s.Items[s.HandlePanel].Clip < 0)
            {
                ref UiItem p = ref s.Items[s.HandlePanel];
                x = p.X;
                y = p.Y;
                w = p.W;
                h = p.H;
                return;
            }
            float right = 0f, foot = 0f;
            x = y = float.MaxValue;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Layer != 0 || it.Clip >= 0 || (it.Flags & (UiFlags.Prompt | UiFlags.Overflow)) != 0)
                {
                    continue;
                }
                x = Math.Min(x, it.X);
                y = Math.Min(y, it.Y);
                right = Math.Max(right, it.X + it.W);
                foot = Math.Max(foot, it.Y + it.H);
            }
            x = Math.Min(x, right);
            y = Math.Min(y, foot);
            w = right - x;
            h = foot - y;
        }
    }

    /// <summary>
    /// One host's hold on its window: the pointer taking the title strip, dragging it and letting go. The window follows the pointer by
    /// the pointer's own move in px, is cut short by the screen's edge, and where it ends is kept (UiWindowPlaces). Game-free; the host
    /// lays the screen out again after each move. Cost: a few sums on a pointer event; nothing in a frame.
    /// </summary>
    public sealed class UiWindowGrip
    {
        private bool _moving;
        private float _wantX, _wantY, _baseX, _baseY, _grabX, _grabY;

        /// <summary>The window's id in UiWindowPlaces; null for a screen that stays.</summary>
        public string Id;

        /// <summary>The place is kept for the next session (the game keeps this window's).</summary>
        public bool Saved;

        /// <summary>The window is held now.</summary>
        public bool Moving => _moving;

        /// <summary>The move the window has now from where its screen laid it, px of the layout.</summary>
        public float X { get; private set; }
        public float Y { get; private set; }

        /// <summary>Moves what the screen laid by the window's place, kept inside the area above <paramref name="bottom"/>.</summary>
        public void Place(UiScene s, float width, float bottom)
        {
            X = Y = 0f;
            if (Id == null || s.HandleNode < 0)
            {
                return;
            }
            if (!_moving)
            {
                UiWindowPlaces.Get(Id, out _wantX, out _wantY);
            }
            UiWindowDrag.Apply(s, _wantX, _wantY, width, bottom, out float x, out float y);
            X = x;
            Y = y;
        }

        /// <summary>The pointer on the strip at a phase; true when the screen is to be laid out again.</summary>
        public bool Drag(UiDragPoint at)
        {
            switch (at.Phase)
            {
                case UiDrag.Down:
                    _grabX = at.X;
                    _grabY = at.Y;
                    _baseX = X;
                    _baseY = Y;
                    return false;
                case UiDrag.Begin:
                    _moving = true;
                    return false;
                case UiDrag.Move:
                    if (!_moving)
                    {
                        return false;
                    }
                    _wantX = _baseX + at.X - _grabX;
                    _wantY = _baseY + at.Y - _grabY;
                    return true;
                default:
                    End();
                    return false;
            }
        }

        /// <summary>Lets go (released, or the screen hidden): the place it is at is kept.</summary>
        public void End()
        {
            if (!_moving)
            {
                return;
            }
            _moving = false;
            if (Id != null)
            {
                UiWindowPlaces.Set(Id, X, Y, Saved, true);
            }
        }
    }

    /// <summary>
    /// Where the player left each draggable window, as the game keeps its windows (PlayerSettings, by window id): the move from
    /// the place the screen laid it, in px of the layout, so a window the game's screen places by its own rules still follows
    /// them. Saved windows (those with an id the game knows) are kept on this computer and read at the next start, never in the
    /// save and never sent; the others are kept for the session. Game-free: the game file gives Load and Save. Cost: a lookup
    /// on a layout of a draggable window.
    /// </summary>
    public static class UiWindowPlaces
    {
        private struct Place
        {
            public float X, Y;
            public bool Saved;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Place> Moves = new System.Collections.Generic.Dictionary<string, Place>();
        private static bool _read;

        /// <summary>Reads the saved line (null or empty for none); set by the game file.</summary>
        public static Func<string> Load;

        /// <summary>Keeps the saved line; set by the game file.</summary>
        public static Action<string> Save;

        /// <summary>Counts every reset, so a window on show lays itself out again.</summary>
        public static int Version { get; private set; }

        /// <summary>The move kept for a window, or none.</summary>
        public static bool Get(string id, out float dx, out float dy)
        {
            Read();
            if (Moves.TryGetValue(id, out Place p))
            {
                dx = p.X;
                dy = p.Y;
                return true;
            }
            dx = dy = 0f;
            return false;
        }

        /// <summary>Keeps a window's move; a saved one is written out when <paramref name="keep"/> (the drag has ended).</summary>
        public static void Set(string id, float dx, float dy, bool saved, bool keep)
        {
            Read();
            Moves[id] = new Place { X = dx, Y = dy, Saved = saved };
            if (keep && saved)
            {
                Write();
            }
        }

        /// <summary>Every window back where its screen puts it, as the game's reset does.</summary>
        public static void ResetAll()
        {
            Read();
            Moves.Clear();
            Version++;
            Write();
        }

        /// <summary>The saved moves as one line, "id|x|y;id|x|y".</summary>
        public static string Encode()
        {
            var b = new System.Text.StringBuilder();
            foreach (System.Collections.Generic.KeyValuePair<string, Place> p in Moves)
            {
                if (!p.Value.Saved)
                {
                    continue;
                }
                b.Append(b.Length > 0 ? ";" : string.Empty).Append(p.Key).Append('|')
                    .Append(p.Value.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                    .Append(p.Value.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            }
            return b.ToString();
        }

        /// <summary>Takes a line back into the moves; a part that does not read is left out.</summary>
        public static void Decode(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            foreach (string part in line.Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length == 3 && f[0].Length > 0 && float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y) && !float.IsNaN(x) && !float.IsNaN(y))
                {
                    Moves[f[0]] = new Place { X = x, Y = y, Saved = true };
                }
            }
        }

        /// <summary>Forgets what is kept and read, as a new session starts (the offline checks).</summary>
        public static void Forget()
        {
            Moves.Clear();
            _read = false;
        }

        private static void Read()
        {
            if (_read)
            {
                return;
            }
            _read = true;
            Decode(Load?.Invoke());
        }

        private static void Write()
        {
            Save?.Invoke(Encode());
        }
    }
}
