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
    /// and its words, the duration slider, the clock, and the time machine and fast forward with the game's keys for them;
    /// with what the mod knows of a working day where it has it (the place, the person there, the sums under the slider,
    /// the hour and the pay so far).
    /// </summary>
    public sealed class UiActivityModel
    {
        public UiActivityState State;
        public string Headline;                            // "Work a shift at Bloom Café"
        public UiIcon Icon = UiIcon.Briefcase;
        public UiRole IconTint = UiRole.Gold;
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
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;

        // The card's words and side (the side card): a kicker over the title, a paragraph, the place and the person there.
        public string Kicker;                              // "Working day, Monday"
        public string Title;                               // the card's title; the headline when not set
        public string Body;
        public string Place;                               // "Pioneer Game Studio": the side's name, and the strip's line under the headline
        public string[] PlaceLines = new string[0];        // its address
        public string Person, PersonLine;                  // the person met there
        public string DurationFrom, DurationTo;            // the slider's ends: "1 h", "8 h"
        public string Finish, Sum;                         // under the slider: "You finish at 15:00", "$228 before tax"

        // The buttons as the game gives them (Start is its own, greyed as the game greys it): the card's own (its arg the
        // index here, given back with ButtonCommand) and the strip's (CancelCommand with the index).
        public UiSideButton[] Buttons = new UiSideButton[0];
        public UiSideButton[] StripButtons = new UiSideButton[0];
        public bool Bar = true;                            // the card draws the status bar under it; false over the game's own HUD

        // While it runs: the meter's words, "Hour 4 of 6" and "Earned $152"; the game's progress words when not set.
        public string MeterLeft, MeterRight;

        // Laid out: the card it was drawn as, for a press or a drag on the slider to read its share.
        public UiSideCardModel Card;

        // Laid out: the strip's width, as wide as its words, meter, clock and buttons need (StripPlace writes it).
        public float StripW;

        // Appended with the minimap's place (4 Nov 2026): px of the layout, the mod's minimap card the strip keeps clear of; 0 wide for none.
        public float AvoidX, AvoidY, AvoidW, AvoidH;
    }

    /// <summary>
    /// The activity panel in two faces. Before it starts, a side card centred over the dimmed world (UiSideCard: the
    /// emblem and the place at its left, the headline in cyan capitals, the lines on a band, the duration's slider with
    /// its ends and sums, Back at the left, the time machine and Start at the right on the game's keys). While it runs, a
    /// strip above the status bar at the centre, so the city stays in view: the glyph on its tile, the headline in cyan
    /// capitals over the place or the state, the meter with its words, the clock, and the time machine, fast forward and
    /// the activity's own buttons with the game's keys (the game reads them; the mouse presses them). Commands:
    /// <see cref="ButtonCommand"/> (a card button's index), <see cref="StepCommand"/> (the slider, arg 0),
    /// <see cref="SkipCommand"/>, <see cref="FastCommand"/> and <see cref="CancelCommand"/> (a strip button's index).
    /// Game-free. Cost: a layout a change; the strip's meter moves with the game's progress, laid out again a few times a
    /// second at most by its host.
    /// </summary>
    public static class UiActivity
    {
        public const int ButtonCommand = 240, StepCommand = 241, SkipCommand = 242, FastCommand = 243, CancelCommand = 244;   // the duration's slider is StepCommand, arg 0
        public const float CardWidth = UiSideCard.Width;   // px: the side card's
        public const float StripWidth = 1020f;    // px: the strip, centred over the bar's apps
        public const float StripHeight = 72f;     // px: a tile's row with room round it, the meter's words over its bar
        public const float StripShort = 72f;      // px: the same while there is no meter (walking there, waiting): the strip does not jump
        private const float MeterWidth = 208f;    // px: the meter's column at the least while there is room
        private const float MeterLeast = 128f;    // px: the least it keeps before the headline gives way
        private const float StripMargin = 64f;    // px: the strip's least room to the screen's sides

        /// <summary>True for the states the strip shows; the card shows the rest.</summary>
        public static bool Strip(UiActivityState state) => state != UiActivityState.NotStarted;

        /// <summary>The card as a screen of its own: centred over the dimmed world above the status bar, with the bar and its prompts.</summary>
        public static void LayCard(UiScene s, UiActivityModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            UiSideCardModel card = Card(m, keys);
            m.Card = card;
            UiSideCard.Lay(s, card, frame, dark, keys, "activity");
        }

        public static float CardHeight(UiActivityModel m, IUiKeys keys) => UiSideCard.Height(Card(m, keys), keys);

        /// <summary>The slider's share for a point across the screen, by the card laid last.</summary>
        public static float SliderShare(UiActivityModel m, float x) => m != null && m.Card != null ? UiSideCard.TrackShare(m.Card, x) : 0f;

        /// <summary>
        /// The game writes its slider's line as two sentences ("Rest for 24 hours, 0 minutes. Will stop resting at 19:15"): the
        /// first is the value beside the label, the rest a line of its own under the track, so neither is ever drawn over it.
        /// Splits at a line break or a full stop that ends a sentence (a stop and a space, or the Chinese one); a line that is
        /// one sentence is the value alone.
        /// </summary>
        public static string SplitValue(string text, out string note)
        {
            note = null;
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            int cut = -1, skip = 0;
            for (int i = 0; i < text.Length && cut < 0; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r')
                {
                    cut = i;
                    skip = 1;
                }
                else if ((c == '.' || c == '!' || c == '?') && i + 1 < text.Length && text[i + 1] == ' ')
                {
                    cut = i;
                    skip = 2;
                }
                else if ((c == '\u3002' || c == '\uFF01' || c == '\uFF1F') && i + 1 < text.Length)
                {
                    cut = i;
                    skip = 1;
                }
            }
            if (cut < 0)
            {
                return text.Trim();
            }
            string rest = text.Substring(cut + skip).Trim();
            note = rest.Length > 0 ? rest : null;
            return text.Substring(0, cut).Trim();
        }

        // The activity as a side card: its emblem and place at the left, its headline as the title, its lines as rows.
        private static UiSideCardModel Card(UiActivityModel m, IUiKeys keys)
        {
            string value = SplitValue(m.DurationValue, out string note);
            var c = new UiSideCardModel
            {
                Emblem = m.Icon, EmblemTint = m.IconTint == UiRole.None ? UiRole.Gold : m.IconTint, SideName = m.Place, SideLines = m.PlaceLines ?? new string[0],
                Person = m.Person, PersonLine = m.PersonLine, Kicker = m.Kicker, Title = string.IsNullOrEmpty(m.Title) ? m.Headline : m.Title, Body = m.Body,
                Rows = m.Lines ?? new UiInfoRow[0], HasSlider = m.HasDuration, SliderLabel = m.DurationLabel, SliderValue = value, SliderNote = note, SliderShare = m.DurationShare,
                SliderFrom = m.DurationFrom, SliderTo = m.DurationTo, SumLeft = m.Finish, SumRight = m.Sum, Prompts = m.Prompts,
                PromptCount = m.PromptCount, Bar = m.Bar, ButtonCommand = ButtonCommand, SliderCommand = StepCommand,
            };
            c.Buttons = m.Buttons ?? new UiSideButton[0];
            return c;
        }

        /// <summary>The strip's place: centred over the status bar's apps, a grid step above the bar, and off the minimap's card where it stands there.</summary>
        public static void StripPlace(UiFrame frame, UiActivityModel m, out float x, out float y) =>
            StripPlace(frame, m, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH, out x, out y);

        /// <summary>The strip's place for a layout that has its own px: the card to keep clear of is that layout's, not the model's.</summary>
        public static void StripPlace(UiFrame frame, UiActivityModel m, float cardX, float cardY, float cardW, float cardH, out float x, out float y)
        {
            m.StripW = Width(frame, m);
            x = UiDraw.SnapDown((frame.Width - m.StripW) / 2f);
            y = frame.BandY - UiTokens.S3 - Height(m);
            UiHudRules.Clear(ref x, ref y, m.StripW, Height(m), frame.Width, cardX, cardY, cardW, cardH);
        }

        /// <summary>
        /// The strip's width: its least, grown so its headline, the line under it, the meter's words, the clock and every
        /// button stand whole, at most the screen less a margin each side (then the headline gives way first).
        /// </summary>
        public static float Width(UiFrame frame, UiActivityModel m)
        {
            float need = UiTokens.S4 * 2f + UiTokens.IconTile + UiTokens.S3 + HeadNeed(m) + UiTokens.S5 + ButtonsNeed(m) + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Clock))
            {
                need += UiDraw.Snap(UiTextRules.Width(m.Clock, UiStyle.Number) + UiTokens.S1) + UiTokens.S5;
            }
            if (Metered(m))
            {
                need += MeterNeed(m) + UiTokens.S5;
            }
            return UiDraw.Snap(Math.Max(StripWidth, Math.Min(need, frame.Width - StripMargin * 2f)));
        }

        // The headline's column: the wider of the headline in capitals and the line under it.
        private static float HeadNeed(UiActivityModel m)
        {
            bool walking = m.State == UiActivityState.Moving || m.State == UiActivityState.Waiting;
            string under = m.State == UiActivityState.Finished || walking || string.IsNullOrEmpty(m.Place) ? m.StateWords : m.Place;
            string head = string.IsNullOrEmpty(m.Kicker) ? m.Headline : m.Kicker;
            float w = Math.Max(string.IsNullOrEmpty(head) ? 0f : UiTextRules.Width(head, UiStyle.SectionTitle), string.IsNullOrEmpty(under) ? 0f : UiTextRules.Width(under, UiStyle.Small));
            return UiDraw.Snap(w + UiTokens.S1);
        }

        // The meter's column: its words at the left and its figure at the right whole, the figure in at most half of it.
        private static float MeterNeed(UiActivityModel m)
        {
            string label = !string.IsNullOrEmpty(m.MeterLeft) ? m.MeterLeft : m.Progress.Label, value = !string.IsNullOrEmpty(m.MeterRight) ? m.MeterRight : m.Progress.Value;
            float lw = string.IsNullOrEmpty(label) ? 0f : UiTextRules.Width(label, UiStyle.Body) + UiTokens.S1;
            float vw = string.IsNullOrEmpty(value) ? 0f : UiTextRules.Width(value, UiStyle.BodyStrong) + UiTokens.S1;
            return UiDraw.Snap(Math.Max(MeterWidth, Math.Max(lw + UiTokens.S3 + vw, vw * 2f)));
        }

        // The buttons' row as DrawStrip lays them, their gaps included.
        private static float ButtonsNeed(UiActivityModel m)
        {
            bool walking = m.State == UiActivityState.Moving || m.State == UiActivityState.Waiting;
            bool done = m.State == UiActivityState.Finished;
            float sum = 0f;
            if (m.StripButtons != null && m.StripButtons.Length > 0)
            {
                for (int i = 0; i < m.StripButtons.Length; i++)
                {
                    sum += ButtonWidth(m.StripButtons[i].Label, m.StripButtons[i].GameKey) + UiTokens.S2;
                }
            }
            else if (walking && m.CanCancel)
            {
                sum += ButtonWidth(m.CancelLabel, default(UiGameKey)) + UiTokens.S2;
            }
            if (!done && !walking)
            {
                sum += m.CanFast ? ButtonWidth(m.FastLabel, m.FastKey) + UiTokens.S2 : 0f;
                sum += m.CanSkip ? ButtonWidth(m.SkipLabel, m.SkipKey) + UiTokens.S2 : 0f;
            }
            else if (walking && m.State == UiActivityState.Waiting && m.CanSkip && (m.StripButtons == null || m.StripButtons.Length == 0) && !m.CanCancel)
            {
                sum += ButtonWidth(m.SkipLabel, m.SkipKey) + UiTokens.S2;
            }
            return sum;
        }

        private static float ButtonWidth(string label, UiGameKey key)
        {
            float kw = UiDraw.GameKeyWidth(key);
            return UiDraw.Snap(UiTokens.S3 + (kw > 0f ? kw + UiTokens.S2 : 0f) + UiTextRules.Width(label ?? string.Empty, UiStyle.Button) + UiTokens.S1 + UiTokens.S3);
        }

        /// <summary>The strip's height: one height in every state, so it never jumps as the state changes.</summary>
        public static float Height(UiActivityModel m) => Metered(m) ? StripHeight : StripShort;

        private static bool Metered(UiActivityModel m) => m.HasProgress || m.State == UiActivityState.Finished;

        /// <summary>
        /// The strip while it runs (and while the player walks to it, waits for it, or it has just finished): the glyph on its
        /// tile, the headline in capitals over the place or the state, the meter with its words, the clock, then the time
        /// machine, fast forward and the activity's own buttons with the game's keys, or the way out while walking.
        /// </summary>
        public static void DrawStrip(UiScene s, UiAt at, UiActivityModel m, float x, float y)
        {
            float w = m.StripW > 0f ? m.StripW : StripWidth, h = Height(m);
            int firstNode = s.NodeCount;
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float pad = UiTokens.S4, tile = UiTokens.IconTile;
            float ty = y + (h - tile) / 2f;
            int t = UiDraw.Tile(s, on, x + pad, ty, tile, UiRole.None, false);
            UiDraw.Icon(s, on.In(t), x + pad + (tile - UiTokens.Icon) / 2f, ty + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, m.Icon, m.IconTint, UiFlags.Middle | UiFlags.Centre, true);
            float right = x + w - pad;
            float bh = UiTokens.ControlSmall, by = y + (h - bh) / 2f;
            bool walking = m.State == UiActivityState.Moving || m.State == UiActivityState.Waiting;
            bool done = m.State == UiActivityState.Finished;
            // From the right: the activity's own buttons (its Stop), fast forward, then the time machine as the main one.
            if (m.StripButtons != null && m.StripButtons.Length > 0)
            {
                for (int i = m.StripButtons.Length - 1; i >= 0; i--)
                {
                    ref UiSideButton b = ref m.StripButtons[i];
                    right -= StripButton(s, on, right, by, b.Label, b.GameKey, b.Kind == UiButtonKind.Primary, CancelCommand, i, b.Disabled) + UiTokens.S2;
                }
            }
            else if (walking && m.CanCancel)
            {
                right -= StripButton(s, on, right, by, m.CancelLabel, default(UiGameKey), false, CancelCommand, 0, false) + UiTokens.S2;
            }
            if (!done && !walking)
            {
                if (m.CanFast)
                {
                    right -= StripButton(s, on, right, by, m.FastLabel, m.FastKey, m.Fast, FastCommand, 0, false) + UiTokens.S2;
                }
                if (m.CanSkip)
                {
                    right -= StripButton(s, on, right, by, m.SkipLabel, m.SkipKey, true, SkipCommand, 0, false) + UiTokens.S2;
                }
            }
            else if (walking && m.State == UiActivityState.Waiting && m.CanSkip && (m.StripButtons == null || m.StripButtons.Length == 0) && !m.CanCancel)
            {
                right -= StripButton(s, on, right, by, m.SkipLabel, m.SkipKey, true, SkipCommand, 0, false) + UiTokens.S2;
            }
            right -= UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Clock))
            {
                float cw = UiDraw.Snap(UiTextRules.Width(m.Clock, UiStyle.Number) + UiTokens.S1);
                UiDraw.TextMiddle(s, on, right - cw, y, h, cw, UiStyle.Number, UiRole.Text, m.Clock, UiFlags.Right);
                right -= cw + UiTokens.S5;
            }
            float cx = x + pad + tile + UiTokens.S3;
            if (Metered(m))
            {
                // The meter whole where it can be; on a narrow screen the headline keeps half the room left and gives way first.
                float room = right - cx;
                float mw = UiDraw.SnapDown(Math.Max(MeterLeast, Math.Min(MeterNeed(m), room - Math.Min(HeadNeed(m), room / 2f) - UiTokens.S5)));
                var bar = new UiProgressModel
                {
                    Label = !string.IsNullOrEmpty(m.MeterLeft) ? m.MeterLeft : m.Progress.Label, Value = !string.IsNullOrEmpty(m.MeterRight) ? m.MeterRight : m.Progress.Value,
                    Share = done ? 1f : m.Progress.Share, Tone = done ? UiRole.Ok : m.Progress.Tone,
                };
                float mh = UiDraw.ProgressHeight(ref bar);
                UiDraw.Progress(s, on, right - mw, y + UiDraw.SnapDown((h - mh) / 2f), mw, ref bar);
                right -= mw + UiTokens.S5;
            }
            // The headline in the cyan capitals over the place, or the state's words while it has no place or is not running.
            float tw = Math.Max(UiTokens.S8, right - cx);
            string under = done || walking || string.IsNullOrEmpty(m.Place) ? m.StateWords : m.Place;
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionTitle)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float block = string.IsNullOrEmpty(under) ? nh : nh + UiTokens.S1 + sh, hy = y + UiDraw.SnapDown((h - block) / 2f);
            UiDraw.Text(s, on, cx, hy, tw, UiStyle.SectionTitle, UiRole.AccentText, string.IsNullOrEmpty(m.Kicker) ? m.Headline : m.Kicker, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(under))
            {
                UiDraw.Text(s, on, cx, hy + nh + UiTokens.S1, tw, UiStyle.Small, done ? UiRole.OkText : UiRole.Text2, under, UiFlags.Truncate);
            }
            Row(s, firstNode);
        }

        // The strip's buttons left to right as the d-pad's row, its ends going nowhere sideways (only Down leaves for the bar).
        private static void Row(UiScene s, int firstNode)
        {
            int first = -1, last = -1;
            float lastX = float.MinValue;
            // Laid from the right: walk them by their place, left first.
            for (int pass = firstNode; pass < s.NodeCount; pass++)
            {
                int next = -1;
                float nextX = float.MaxValue;
                for (int n = firstNode; n < s.NodeCount; n++)
                {
                    float nx = s.Nodes[n].X;
                    if (nx > lastX && nx < nextX)
                    {
                        next = n;
                        nextX = nx;
                    }
                }
                if (next < 0)
                {
                    break;
                }
                if (last >= 0)
                {
                    s.Nodes[last].Right = next;
                    s.Nodes[next].Left = last;
                }
                else
                {
                    first = next;
                }
                last = next;
                lastX = nextX;
            }
            if (first >= 0)
            {
                s.Nodes[first].Left = first;
                s.Nodes[last].Right = last;
            }
        }

        // A button of the strip from its right edge: the game's key on its cap and its words; on the cyan for the main one
        // and while a toggle is on.
        private static float StripButton(UiScene s, UiAt at, float right, float y, string label, UiGameKey key, bool on, int command, int arg, bool disabled)
        {
            float w = ButtonWidth(label, key);
            UiSideCard.GameButton(s, at, right - w, y, w, UiTokens.ControlSmall, on ? UiButtonKind.Primary : UiButtonKind.Secondary, label, key, command, arg, disabled);
            if (on && command == FastCommand)
            {
                s.Items[s.Nodes[s.NodeCount - 1].Face].Flags |= UiFlags.Selected;   // a toggle that is on reads as chosen
            }
            return w;
        }
    }
}
