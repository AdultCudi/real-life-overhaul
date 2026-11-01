using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where an activity stands, as the game's PlayerActivityState names it.</summary>
    public enum UiActivityState : byte
    {
        NotStarted = 0,   // the panel before it starts: what, how long, Start
        Moving,           // the player walks to it
        Waiting,          // it waits for something (a door to open, a client)
        Started,
        Running,          // the time in the chair
        Finished
    }

    /// <summary>
    /// The game's PlayerActivityUI as data: the headline, its lines (LabelInfo), its buttons (ButtonInfo), the progress bar
    /// and its words, the duration slider, the clock, and the time machine and fast forward with the game's keys for them.
    /// </summary>
    public sealed class UiActivityModel
    {
        public UiActivityState State;
        public string Headline;                            // "Work a shift at Bloom Café"
        public UiIcon Icon = UiIcon.Briefcase;
        public UiInfoRow[] Lines = new UiInfoRow[0];       // what it gives and costs, as the game's labels say
        public bool HasProgress;
        public UiProgressModel Progress;
        public bool HasDuration;                           // the slider before it starts
        public string DurationLabel;                       // "Hours"
        public string DurationValue;                       // "4 hours"
        public float DurationShare;                        // 0 to 1 along the slider
        public string Clock;                               // the game's time now, "14:32"
        public string StateWords;                          // the strip's line: "On your way", "Waiting for the doors to open", "Done"
        public bool CanSkip;                               // the time machine (the game's Special key)
        public string SkipLabel;
        public UiGameKey SkipKey;
        public bool CanFast;                               // fast forward (the game's Secondary key), a toggle
        public bool Fast;
        public string FastLabel;
        public UiGameKey FastKey;
        public bool CanCancel;                             // the way back out while walking to it or before it starts
        public string CancelLabel;
        public string StartLabel;                          // the card's main button: Start, on the game's Interact
        public bool StartDisabled;                         // the game will not start it now (no energy): its line says why
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The activity panel in two faces. Before it starts, a compact decision card centred over the world (the glyph and the
    /// headline in its strip, the lines on a band, the duration's slider between its two steps, Cancel and Start, Start on
    /// the game's Interact as F). While it runs, a slim strip above the status bar at the centre, so the city stays in view:
    /// the glyph on its tile, the headline over the state and the clock, the meter filling, and the time machine and fast
    /// forward as buttons with the game's keys (the game reads them; the mouse presses them). Commands:
    /// <see cref="ButtonCommand"/> (0 Cancel, 1 Start), <see cref="StepCommand"/> (-1 or 1), <see cref="SkipCommand"/>,
    /// <see cref="FastCommand"/> and <see cref="CancelCommand"/>. Game-free. Cost: a layout a change; the strip's meter
    /// moves with the game's progress, laid out again a few times a second at most by its host.
    /// </summary>
    public static class UiActivity
    {
        public const int ButtonCommand = 240, StepCommand = 241, SkipCommand = 242, FastCommand = 243, CancelCommand = 244;
        public const float CardWidth = 560f;      // px: compact, the world round it
        public const float StripWidth = 720f;     // px: the strip, centred over the bar's apps
        public const float StripHeight = 76f;     // px: a tile's row and the meter under it
        public const float StripShort = 64f;      // px: a tile's row alone, while there is no meter (walking there, waiting)
        private const byte CardBody = 1, CardButtons = 2;

        /// <summary>True for the states the strip shows; the card shows the rest.</summary>
        public static bool Strip(UiActivityState state) => state != UiActivityState.NotStarted;

        /// <summary>The card as a screen of its own: centred over the dimmed world above the status bar, with the bar and its prompts.</summary>
        public static void LayCard(UiScene s, UiActivityModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("activity", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float h = CardHeight(m, keys);
            float x = UiDraw.SnapDown((frame.Width - CardWidth) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Card(s, UiAt.Screen, m, x, y, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        public static float CardHeight(UiActivityModel m, IUiKeys keys)
        {
            float h = UiFrame.Strip;
            if (m.Lines.Length > 0)
            {
                h += UiTokens.S2 * 2f + m.Lines.Length * UiTokens.PairRow;
            }
            if (m.HasDuration)
            {
                h += UiTokens.S4 + UiTokens.Control;
            }
            return UiDraw.Snap(h + UiTokens.S4 + UiTokens.Control + UiTokens.S4);
        }

        // The card: its strip, its lines, the duration between its steps, then Cancel and Start at its foot.
        private static void Card(UiScene s, UiAt at, UiActivityModel m, float x, float y, IUiKeys keys)
        {
            float h = CardHeight(m, keys);
            int panel = UiDraw.Box(s, at, x, y, CardWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, CardWidth, m.Icon, m.Headline, false);   // Cancel on Back is its way out: one target for it
            float ix = x + UiTokens.S4, iw = CardWidth - UiTokens.S4 * 2f;
            if (m.Lines.Length > 0)
            {
                float bh = UiTokens.S2 * 2f + m.Lines.Length * UiTokens.PairRow;
                UiDraw.Band(s, on, x, cy, CardWidth, bh);
                float ry = cy + UiTokens.S2;
                for (int i = 0; i < m.Lines.Length; i++)
                {
                    ry += UiDraw.InfoRow(s, on, ix, ry, iw, m.Lines[i].Label, m.Lines[i].Value, false, -1, 0, m.Lines[i].Ink == UiRole.None ? UiRole.Text : m.Lines[i].Ink);
                }
                cy += bh;
            }
            if (m.HasDuration)
            {
                cy += UiTokens.S4;
                Duration(s, on.InRegion(CardBody), m, ix, cy, iw);
                cy += UiTokens.Control;
            }
            cy += UiTokens.S4;
            UiAt buttons = on.InRegion(CardButtons);
            float bw = UiDraw.SnapDown((iw - UiTokens.S3) / 2f);
            int cancel = -1;
            if (m.CanCancel)
            {
                cancel = UiDraw.Button(s, buttons, ix, cy, bw, UiTokens.Control, UiButtonKind.Secondary, m.CancelLabel, UiIcon.None, UiAction.Back, keys, ButtonCommand, 0);
            }
            int start = UiDraw.Button(s, buttons, ix + iw - bw, cy, bw, UiTokens.Control, UiButtonKind.Primary, m.StartLabel, UiIcon.Play, UiAction.Alt, keys, ButtonCommand, 1, m.StartDisabled);
            Link(s, cancel, start);
        }

        // The card's d-pad: the duration's two steps side by side over Cancel and Start; below them only the frame's app row.
        private static void Link(UiScene s, int cancel, int start)
        {
            int less = s.FindNode(StepCommand, -1), more = s.FindNode(StepCommand, 1);
            if (less >= 0 && more >= 0)
            {
                s.Nodes[less].Right = more;
                s.Nodes[more].Left = less;
                s.Nodes[less].Down = cancel >= 0 ? cancel : start;
                s.Nodes[more].Down = start;
                s.Nodes[less].Up = less;
                s.Nodes[more].Up = more;
            }
            if (cancel >= 0)
            {
                s.Nodes[cancel].Right = start;
                s.Nodes[start].Left = cancel;
                s.Nodes[cancel].Up = less >= 0 ? less : cancel;
            }
            s.Nodes[start].Up = more >= 0 ? more : start;
        }

        // The duration: its words and figure, and the slider between the two steps that move it a notch, as the game's own
        // slider moves by its keys.
        private static void Duration(UiScene s, UiAt at, UiActivityModel m, float x, float y, float w)
        {
            float b = UiTokens.ControlSmall, by = y + (UiTokens.Control - b) / 2f;
            float lw = UiDraw.Snap(Math.Min(w * 0.3f, UiTextRules.Width(m.DurationLabel, UiStyle.Body) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw, UiStyle.Body, UiRole.Text2, m.DurationLabel, UiFlags.Label | UiFlags.Truncate);
            float vw = UiDraw.Snap(Math.Min(w * 0.3f, UiTextRules.Width(m.DurationValue, UiStyle.BodyStrong) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x + w - vw, y, UiTokens.Control, vw, UiStyle.BodyStrong, UiRole.Text, m.DurationValue, UiFlags.Right | UiFlags.Truncate);
            float sx = x + lw + UiTokens.S3, sw = w - lw - vw - UiTokens.S3 * 2f;
            int drawn = s.ItemCount;
            int less = UiDraw.IconButton(s, at, sx, by, b, UiIcon.Minus, UiRole.Text, StepCommand, -1);
            if (m.DurationShare <= 0f)
            {
                UiDraw.Dim(s, drawn, less);
            }
            float tx = sx + b + UiTokens.S3, tw = sw - (b + UiTokens.S3) * 2f;
            float share = Math.Max(0f, Math.Min(1f, m.DurationShare));
            int track = UiDraw.Box(s, at, tx, y + (UiTokens.Control - UiTokens.S1) / 2f, tw, UiTokens.S1, UiRole.Track, UiRole.None, UiTokens.S1 / 2f, UiFlags.Middle);
            UiDraw.Box(s, at.In(track), tx, y + (UiTokens.Control - UiTokens.S1) / 2f, Math.Max(UiTokens.S1, tw * share), UiTokens.S1, UiRole.Accent, UiRole.None, UiTokens.S1 / 2f,
                UiFlags.Middle | UiFlags.Share);
            float knob = UiTokens.S4;
            UiDraw.Circle(s, at, tx + (tw - knob) * share, y + (UiTokens.Control - knob) / 2f, knob, knob, UiRole.Text, UiFlags.Middle | UiFlags.Share | UiFlags.Decor);
            drawn = s.ItemCount;
            int more = UiDraw.IconButton(s, at, sx + sw - b, by, b, UiIcon.Plus, UiRole.Text, StepCommand, 1);
            if (m.DurationShare >= 1f)
            {
                UiDraw.Dim(s, drawn, more);
            }
        }

        /// <summary>The strip's place: centred over the status bar's apps, a grid step above the bar.</summary>
        public static void StripPlace(UiFrame frame, UiActivityModel m, out float x, out float y)
        {
            x = UiDraw.SnapDown((frame.Width - StripWidth) / 2f);
            y = frame.BandY - UiTokens.S3 - Height(m);
        }

        /// <summary>The strip's height: with its meter while there is one, its tile's row alone while not.</summary>
        public static float Height(UiActivityModel m) => Metered(m) ? StripHeight : StripShort;

        private static bool Metered(UiActivityModel m) => m.HasProgress || m.State == UiActivityState.Finished;

        /// <summary>
        /// The strip while it runs (and while the player walks to it, waits for it, or it has just finished): the glyph on its
        /// tile, the headline over the state and the clock, the time machine and fast forward with the game's keys, or the way
        /// out while walking, and the meter under them.
        /// </summary>
        public static void DrawStrip(UiScene s, UiAt at, UiActivityModel m, float x, float y)
        {
            float w = StripWidth, h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float pad = UiTokens.S3, tile = UiTokens.IconTile;
            float ty = y + pad;
            int t = UiDraw.Tile(s, on, x + pad, ty, tile, UiRole.None, false);
            UiDraw.Icon(s, on.In(t), x + pad + (tile - UiTokens.Icon) / 2f, ty + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, m.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float right = x + w - pad;
            float bh = UiTokens.ControlSmall, by = ty + (tile - bh) / 2f;
            if (m.State == UiActivityState.Moving || m.State == UiActivityState.Waiting)
            {
                if (m.CanCancel)
                {
                    right -= StripButton(s, on, right, by, m.CancelLabel, default(UiGameKey), false, CancelCommand) + UiTokens.S2;
                }
            }
            else if (m.State != UiActivityState.Finished)
            {
                if (m.CanFast)
                {
                    right -= StripButton(s, on, right, by, m.FastLabel, m.FastKey, m.Fast, FastCommand) + UiTokens.S2;
                }
                if (m.CanSkip)
                {
                    right -= StripButton(s, on, right, by, m.SkipLabel, m.SkipKey, false, SkipCommand) + UiTokens.S2;
                }
            }
            float cx = x + pad + tile + UiTokens.S3;
            float clockW = string.IsNullOrEmpty(m.Clock) ? 0f : UiTextRules.Width(m.Clock, UiStyle.BodyStrong) + UiTokens.S1;
            float tw = right - UiTokens.S2 - cx;
            float nh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
            float block = nh + UiTokens.S1 + sh, hy = ty + (tile - block) / 2f;
            UiDraw.Text(s, on, cx, hy, tw, UiStyle.Title, UiRole.Text, m.Headline, UiFlags.Truncate | UiFlags.Middle);
            float lineW = tw - (clockW > 0f ? clockW + UiTokens.S3 : 0f);
            if (!string.IsNullOrEmpty(m.StateWords))
            {
                UiDraw.Text(s, on, cx, hy + nh + UiTokens.S1, lineW, UiStyle.Small, m.State == UiActivityState.Finished ? UiRole.OkText : UiRole.Text2, m.StateWords,
                    UiFlags.Truncate | UiFlags.Middle);
            }
            if (clockW > 0f)
            {
                UiDraw.Text(s, on, cx + tw - clockW, hy + nh + UiTokens.S1 - (UiTextRules.LineHeight(UiStyle.BodyStrong) - sh) / 2f, clockW, UiStyle.BodyStrong, UiRole.Text, m.Clock,
                    UiFlags.Right | UiFlags.Middle);
            }
            float my = y + h - pad - UiTokens.S2;
            if (Metered(m))
            {
                var bar = new UiProgressModel { Share = m.State == UiActivityState.Finished ? 1f : m.Progress.Share, Tone = m.State == UiActivityState.Finished ? UiRole.Ok : m.Progress.Tone };
                UiDraw.Progress(s, on, x + pad, my, w - pad * 2f, ref bar);
            }
        }

        // A button of the strip from its right edge: the game's key on its cap and its words; on the cyan while it is on.
        private static float StripButton(UiScene s, UiAt at, float right, float y, string label, UiGameKey key, bool on, int command)
        {
            float kw = UiDraw.GameKeyWidth(key);
            float lw = UiTextRules.Width(label, UiStyle.Body) + UiTokens.S1;
            float w = UiDraw.Snap(UiTokens.S2 + (kw > 0f ? kw + UiTokens.S2 : 0f) + lw + UiTokens.S3);
            float x = right - w, h = UiTokens.ControlSmall;
            int face = UiDraw.Box(s, at, x, y, w, h, on ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, on ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S2 + (kw > 0f ? kw + UiTokens.S2 : 0f);
            UiDraw.TextMiddle(s, inside, cx, y, h, lw, UiStyle.Body, on ? UiRole.OnAccent : UiRole.Text, label, UiFlags.Label);
            UiDraw.Node(s, at, x, y, w, h, command, 0, face);
            UiDraw.GameKey(s, inside, x + UiTokens.S2, y + (h - UiTokens.KeyCap) / 2f, key, on);   // after the target: the game, not the button, answers it
            return w;
        }
    }
}
