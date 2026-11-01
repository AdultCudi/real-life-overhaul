using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A key of the game's own (Interact, Secondary, Special, Confirm): its short name as the game writes it, or a pad's glyph.</summary>
    public struct UiGameKey
    {
        public string Name;      // "F", "Enter"; null when a glyph shows
        public UiIcon Glyph;     // a pad's button, or None

        public bool Shown => Glyph != UiIcon.None || !string.IsNullOrEmpty(Name);
    }

    /// <summary>A bar that fills: the game's ProgressBar and the mod's GameBar, with its words and its figure.</summary>
    public struct UiProgressModel
    {
        public string Label;     // "Cooking"; null for the bar alone
        public string Value;     // "64%", "12 min left"
        public float Share;      // 0 to 1
        public UiRole Tone;      // None for the accent: a bar that says good or bad passes Ok, Warn or Danger
    }

    /// <summary>A timing gauge: a zone to land in and the marker moving across it (a trade's timing, the Leisure gauge).</summary>
    public struct UiTimingModel
    {
        public string Label;     // "Hit the mark"
        public string Value;     // "2 of 3"
        public float ZoneFrom, ZoneTo;   // 0 to 1: the good zone
        public float Marker;     // 0 to 1: where the marker is now
        public bool Hit;         // the last press landed in the zone
    }

    /// <summary>
    /// The small parts the HUD's pieces share (w0-hudkit): the game's own key on a cap (the HUD never owns a key: the game
    /// reads its keys and the kit shows which), a meter with its words and figure, and a timing gauge with its zone and
    /// marker. Game-free. Cost: a few boxes each.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>A timing gauge's track.</summary>
        public const float GaugeTrack = 16f;

        /// <summary>
        /// The game's key for an action on its cap (a word or a pad's glyph), as its world box prints "[F]": drawn as a box and
        /// its words, not as the kit's key cap, because the game answers this key, not the kit. Returns its width.
        /// </summary>
        public static float GameKey(UiScene s, UiAt at, float x, float y, UiGameKey key, bool onAccent = false)
        {
            if (!key.Shown)
            {
                return 0f;
            }
            float h = UiTokens.KeyCap;
            float w = key.Glyph != UiIcon.None ? h : Math.Max(h, Snap(UiTextRules.Width(key.Name, UiStyle.KeyCap) + UiTokens.S2 * 2f));
            // On the cyan the cap is the dark ink faint and its key the dark ink, as a primary button's cap reads.
            int face = Box(s, at, x, y, w, h, onAccent ? UiRole.OnAccent : UiRole.Track, UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle);
            if (onAccent)
            {
                s.Items[face].Alpha = 0.16f;
            }
            UiAt inside = at.In(face);
            UiRole ink = onAccent ? UiRole.OnAccent : UiRole.Text;
            if (key.Glyph != UiIcon.None)
            {
                Icon(s, inside, x + UiTokens.S1, y + UiTokens.S1, h - UiTokens.S2, key.Glyph, ink, UiFlags.Middle | UiFlags.Centre);
            }
            else
            {
                TextMiddle(s, inside, x, y, h, w, UiStyle.KeyCap, ink, key.Name, UiFlags.Centre);
            }
            return w;
        }

        /// <summary>A game key's width before it is drawn.</summary>
        public static float GameKeyWidth(UiGameKey key) =>
            !key.Shown ? 0f : key.Glyph != UiIcon.None ? UiTokens.KeyCap : Math.Max(UiTokens.KeyCap, Snap(UiTextRules.Width(key.Name, UiStyle.KeyCap) + UiTokens.S2 * 2f));

        /// <summary>A progress meter's height: its words over its bar, or the bar alone.</summary>
        public static float ProgressHeight(ref UiProgressModel m) => (string.IsNullOrEmpty(m.Label) && string.IsNullOrEmpty(m.Value) ? 0f : UiTokens.PairRow + UiTokens.S1) + UiTokens.S2;

        /// <summary>
        /// A meter: its words at the left and its figure at the right on a pair row, the bar under them filled to its share in
        /// its tone (the accent unless it says good or bad), as the game's progress bars read. Returns its height.
        /// </summary>
        public static float Progress(UiScene s, UiAt at, float x, float y, float w, ref UiProgressModel m)
        {
            float by = y;
            if (!string.IsNullOrEmpty(m.Label) || !string.IsNullOrEmpty(m.Value))
            {
                float vw = string.IsNullOrEmpty(m.Value) ? 0f : Math.Min(w * 0.5f, UiTextRules.Width(m.Value, UiStyle.BodyStrong) + UiTokens.S1);
                if (!string.IsNullOrEmpty(m.Label))
                {
                    TextMiddle(s, at, x, y, UiTokens.PairRow, w - vw - UiTokens.S3, UiStyle.Body, UiRole.Text2, m.Label, UiFlags.Truncate);
                }
                if (vw > 0f)
                {
                    TextMiddle(s, at, x + w - vw, y, UiTokens.PairRow, vw, UiStyle.BodyStrong, UiRole.Text, m.Value, UiFlags.Right | UiFlags.Truncate);
                }
                by = y + UiTokens.PairRow + UiTokens.S1;
            }
            float share = Math.Max(0f, Math.Min(1f, m.Share));
            int track = Box(s, at, x, by, w, UiTokens.S2, UiRole.Track, UiRole.None, UiTokens.S1, UiFlags.Middle);
            if (share > 0f)
            {
                Box(s, at.In(track), x, by, Math.Max(UiTokens.S2, w * share), UiTokens.S2, m.Tone == UiRole.None ? UiRole.Accent : m.Tone, UiRole.None, UiTokens.S1,
                    UiFlags.Middle | UiFlags.Share);
            }
            return by + UiTokens.S2 - y;
        }

        /// <summary>A timing gauge's height: its words over its track.</summary>
        public const float TimingHeight = UiTokens.PairRow + UiTokens.S1 + GaugeTrack;

        /// <summary>
        /// A timing gauge: its words and its figure on a pair row, the track under them with the good zone marked in the good
        /// tone and the marker standing across it where it is now, taller than the track so it reads as it moves. A press that
        /// landed shows the zone full; the marker never changes colour. Returns its height.
        /// </summary>
        public static float Timing(UiScene s, UiAt at, float x, float y, float w, ref UiTimingModel m)
        {
            float vw = string.IsNullOrEmpty(m.Value) ? 0f : Math.Min(w * 0.5f, UiTextRules.Width(m.Value, UiStyle.BodyStrong) + UiTokens.S1);
            TextMiddle(s, at, x, y, UiTokens.PairRow, w - vw - UiTokens.S3, UiStyle.Body, UiRole.Text2, m.Label, UiFlags.Truncate);
            if (vw > 0f)
            {
                TextMiddle(s, at, x + w - vw, y, UiTokens.PairRow, vw, UiStyle.BodyStrong, UiRole.Text, m.Value, UiFlags.Right | UiFlags.Truncate);
            }
            float ty = y + UiTokens.PairRow + UiTokens.S1;
            float inner = UiTokens.S1;   // the marker's room above and below the track's fill
            int track = Box(s, at, x, ty, w, GaugeTrack, UiRole.Track, UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle);
            UiAt on = at.In(track);
            float from = Math.Max(0f, Math.Min(1f, m.ZoneFrom)), to = Math.Max(from, Math.Min(1f, m.ZoneTo));
            Box(s, on, x + w * from, ty + inner, Math.Max(UiTokens.S1, w * (to - from)), GaugeTrack - inner * 2f, m.Hit ? UiRole.Ok : UiRole.OkSoft, UiRole.None, UiTokens.RadiusSmall,
                UiFlags.Middle | UiFlags.Share);
            float mx = x + w * Math.Max(0f, Math.Min(1f, m.Marker)) - UiTokens.S1 / 2f;
            mx = Math.Max(x, Math.Min(mx, x + w - UiTokens.S1));
            Box(s, on, mx, ty, UiTokens.S1, GaugeTrack, UiRole.Text, UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle | UiFlags.Share | UiFlags.Decor);
            return TimingHeight;
        }
    }
}
