using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A review of a place: who wrote it, its stars (in halves), when, and a line of what they said.</summary>
    public struct MapReview
    {
        public string Name;       // "Maria R."
        public float Hue;
        public int Halves;        // 2 to 10
        public string When;       // "2 days ago"
        public string Text;
    }

    /// <summary>A line of a place card's overview or business tab: its label and value, and whether the value is a link.</summary>
    public struct MapPlaceRow
    {
        public UiIcon Icon;
        public string Label;
        public string Value;
        public bool Link;         // the value is cyan and pressed (View the unit next door)
        public UiRole Ink;        // the value's colour when it says a state, else None
    }

    /// <summary>
    /// What a place's card shows: its picture's place (the type's glyph on a slate tile, until a picture source exists), its name, its
    /// rating, kind and price tier, its state chips, three actions (Directions, Save, Set destination), and three tabs: Overview (its
    /// address, its hours, a vacant unit next door, its busy hours, what people say), Reviews and Business. The mod fills it from the
    /// building, the Flare city's ratings and the simulation's curves; the kit only draws it.
    /// </summary>
    public sealed class MapPlaceModel
    {
        public string Title;                         // the strip's: "Place card"
        public UiIcon Icon = UiIcon.Store;
        public UiIcon Glyph = UiIcon.Store;          // the type's glyph on the hero tile
        public UiRole Tint = UiRole.CatRetail;       // the type's colour on it
        public string Picture;                       // a picture the game holds, or null
        public string Name;
        public int StarHalves;                       // 0 when the Flare city has not rated it
        public string Rating, Count, Kind, Price;    // "4.3", "(212)", "Fast food", "$$"; each null when not known
        public MapStateChip[] Chips = new MapStateChip[0];
        public string DirectionsLabel, SaveLabel, SavedLabel, DestinationLabel;
        public bool Saved;                           // the place is in the player's list
        public bool RouteShown;                      // Directions is chosen: the route strip is up
        public bool CanDestination;
        public string[] Tabs = new string[0];
        public int Tab;
        public MapPlaceRow[] Rows = new MapPlaceRow[0];
        public string BusyTitle, BusyNote;           // "Busy times", "Busier than usual" or "A typical day"
        public bool BusyUp;                          // the note is the busier-than-usual one
        public int[] Busy = new int[0];              // 0 to 100 an hour, from BusyFrom
        public int BusyFrom, BusyNow = -1;           // the first hour drawn; the index of the hour now, or -1
        public string PeopleTitle, PeopleAll;        // "What people say", "All 212"
        public MapReview[] Reviews = new MapReview[0];
        public string ReviewsEmpty;                  // the sentence when no one has reviewed it
        public string BusinessTitle;
        public MapPlaceRow[] Business = new MapPlaceRow[0];
        public float Offset;                         // the reviews' and the business tab's scroll
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The place card, docked where the filters stand (the dock swaps pages: the card has Back, which is the strip's close): the picture's place, the
    /// name, the rating line, the state chips, three actions and the three tabs, then the tab's body. The overview keeps only the sections that fit
    /// whole in the room it has, so a short screen drops the reviews first; the Reviews and Business tabs scroll. Commands: the 5020 block,
    /// <see cref="TabCommand"/>, <see cref="DirectionsCommand"/>, <see cref="SaveCommand"/>, <see cref="DestinationCommand"/>,
    /// <see cref="RentCommand"/>, <see cref="AllCommand"/> and <see cref="ReviewCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiMapPlace
    {
        public const int TabCommand = 5020, DirectionsCommand = 5021, SaveCommand = 5022, DestinationCommand = 5023, RentCommand = 5024, AllCommand = 5025, ReviewCommand = 5026;
        public const float Width = UiMapFilters.Width;
        private const float Hero = 80f;           // the picture's tile
        private const float BusyBars = 72f;       // the tallest bar
        private const float Avatar = 32f;

        /// <summary>The card in a frame as a screen of its own over the map (the preview's and the check's).</summary>
        public static void Lay(UiScene s, MapPlaceModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("place card", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, UiAt.Screen, m, frame, UiTokens.S2, UiTokens.S4, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The card at a top left; returns the x it ends at.</summary>
        public static float Draw(UiScene s, UiAt at, MapPlaceModel m, UiFrame frame, float x, float y, IUiKeys keys)
        {
            float h = Math.Min(UiMapFilters.PanelHeight(frame), Needed(m));
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, m.Icon, m.Title, true) + UiTokens.S3;
            // The picture's place: the type's glyph in its colour on a slate tile, as the owner asked until a picture source exists.
            int hero = UiDraw.Box(s, on, ix, cy, iw, Hero, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            if (!string.IsNullOrEmpty(m.Picture))
            {
                UiDraw.PictureFace(s, on.In(hero), ix + (iw - Hero) / 2f, cy + UiTokens.S2, Hero - UiTokens.S4, m.Picture);
            }
            else
            {
                UiDraw.Icon(s, on.In(hero), ix + (iw - UiTokens.S12) / 2f, cy + (Hero - UiTokens.S12) / 2f, UiTokens.S12, m.Glyph, m.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            cy += Hero + UiTokens.S3;
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero));
            UiDraw.TextMiddle(s, on, ix, cy, nh, iw, UiStyle.Hero, UiRole.Text, m.Name, UiFlags.Truncate);
            cy += nh + UiTokens.S1;
            cy += RatingLine(s, on, m, ix, cy, iw) + UiTokens.S2;
            if (m.Chips.Length > 0)
            {
                float cx = ix;
                for (int i = 0; i < m.Chips.Length; i++)
                {
                    cx += UiDraw.Chip(s, on, cx, cy, m.Chips[i].Label, m.Chips[i].Tone, ix + iw - cx) + UiTokens.S2;
                }
                cy += UiTokens.Badge + UiTokens.S1 + UiTokens.S3;
            }
            Actions(s, on, m, ix, cy, iw, keys);
            cy += UiTokens.Control * 2f + UiTokens.S2 + UiTokens.S3;
            if (m.Tabs.Length > 0)
            {
                UiDraw.Tabs(s, on, ix, cy, iw, m.Tabs, m.Tab, null, TabCommand);
                cy += UiTokens.Control + UiTokens.S2;
            }
            float bottom = y + h - UiTokens.S4;
            if (m.Tab == 0)
            {
                Overview(s, on, m, ix, cy, iw, bottom);
            }
            else if (m.Tab == 1)
            {
                Reviews(s, on, m, x, cy, bottom);
            }
            else
            {
                Business(s, on, m, x, cy, bottom);
            }
            return x + Width;
        }

        // The stars, the rating's figure, how many rated it, the kind and the price tier: only what is known.
        private static float RatingLine(UiScene s, UiAt at, MapPlaceModel m, float x, float y, float w)
        {
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            float cx = x;
            if (m.StarHalves > 0 && !string.IsNullOrEmpty(m.Rating))
            {
                float rw = UiDraw.Snap(UiTextRules.Width(m.Rating, UiStyle.BodyStrong) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, cx, y, lh, rw, UiStyle.BodyStrong, UiRole.Text, m.Rating, UiFlags.Truncate);
                cx += rw + UiTokens.S1;
                cx += UiDraw.Stars(s, at, cx, y + (lh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.StarHalves) + UiTokens.S2;
            }
            string rest = Join(m.StarHalves > 0 ? m.Count : null, m.Kind, m.Price);
            if (!string.IsNullOrEmpty(rest))
            {
                UiDraw.TextMiddle(s, at, cx, y, lh, x + w - cx, UiStyle.Body, UiRole.Text2, rest, UiFlags.Truncate);
            }
            return lh;
        }

        private static string Join(string a, string b, string c)
        {
            string text = string.Empty;
            foreach (string part in new[] { a, b, c })
            {
                if (!string.IsNullOrEmpty(part))
                {
                    text += (text.Length > 0 ? "  " + "·" + "  " : string.Empty) + part;
                }
            }
            return text;
        }

        // Directions and Save side by side, then Set destination across: the card's one main action, wide enough for its words in any language.
        private static void Actions(UiScene s, UiAt at, MapPlaceModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.Control, gap = UiTokens.S2, half = UiDraw.SnapDown((w - gap) / 2f);
            string save = m.Saved ? m.SavedLabel : m.SaveLabel;
            UiDraw.Button(s, at, x, y, half, h, m.RouteShown ? UiButtonKind.Chip : UiButtonKind.Secondary, m.DirectionsLabel, UiIcon.Navigation, UiAction.Count, null, DirectionsCommand, 0);
            UiDraw.Button(s, at, x + half + gap, y, w - half - gap, h, m.Saved ? UiButtonKind.Chip : UiButtonKind.Secondary, save, UiIcon.Bookmark, UiAction.Count, null, SaveCommand, 0);
            UiDraw.Button(s, at, x, y + h + gap, w, h, UiButtonKind.Primary, m.DestinationLabel, UiIcon.None, UiAction.Alt, keys, DestinationCommand, 0, !m.CanDestination);
        }

        /// <summary>The height the card needs: its head, the actions and tabs, and the overview whole, so a tab's body never moves the card.</summary>
        public static float Needed(MapPlaceModel m)
        {
            float iw = Width - UiTokens.S4 * 2f;
            float need = UiFrame.Strip + UiTokens.S3 + Hero + UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2
                         + (m.Chips.Length > 0 ? UiTokens.Badge + UiTokens.S1 + UiTokens.S3 : 0f) + UiTokens.Control * 2f + UiTokens.S2 + UiTokens.S3
                         + (m.Tabs.Length > 0 ? UiTokens.Control + UiTokens.S2 : 0f) + RowsHeight(m) + BusyHeight(m);
            if (m.Reviews.Length > 0)
            {
                need += UiTokens.S3 + UiTokens.PairRow + UiTokens.S1;
                for (int i = 0; i < m.Reviews.Length && i < 2; i++)
                {
                    need += ReviewHeight(m.Reviews[i], iw);
                }
            }
            return UiDraw.Snap(need + UiTokens.S4);
        }

        // ------------------------------------------------------------------------------------------------- the overview

        private static float RowsHeight(MapPlaceModel m) => m.Rows.Length * UiTokens.PairRow;
        private static float BusyHeight(MapPlaceModel m) => m.Busy.Length == 0 ? 0f : UiTokens.S3 + UiTokens.PairRow + UiTokens.S1 + BusyBars + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
        private static float ReviewHeight(MapReview r, float w) => Math.Max(Avatar, UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body))) + UiTokens.S1
            + UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, w - Avatar - UiTokens.S3, 2)) + UiTokens.S3;

        private static void Overview(UiScene s, UiAt at, MapPlaceModel m, float ix, float y, float iw, float bottom)
        {
            float cy = y;
            for (int i = 0; i < m.Rows.Length; i++)
            {
                MapPlaceRow r = m.Rows[i];
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, r.Label, r.Value, r.Link, r.Link ? RentCommand : -1, i, r.Ink == UiRole.None ? UiRole.Text : r.Ink);
            }
            if (m.Busy.Length > 0 && cy + BusyHeight(m) <= bottom)
            {
                cy = Busy(s, at, m, ix, cy + UiTokens.S3, iw);
            }
            int shown = 0;
            float used = cy;
            for (int i = 0; i < m.Reviews.Length && i < 2; i++)
            {
                used += ReviewHeight(m.Reviews[i], iw);
            }
            if (m.Reviews.Length > 0 && used + UiTokens.S3 + UiTokens.PairRow <= bottom)
            {
                cy += UiTokens.S3;
                UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw - UiTokens.S12 * 2f, UiStyle.SectionHeader, UiRole.Text, m.PeopleTitle, UiFlags.Label | UiFlags.Truncate);
                if (!string.IsNullOrEmpty(m.PeopleAll))
                {
                    float aw = UiDraw.Snap(UiTextRules.Width(m.PeopleAll, UiStyle.SmallStrong) + UiTokens.S3 * 2f);
                    int link = UiDraw.Box(s, at, ix + iw - aw, cy, aw, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                    UiDraw.TextMiddle(s, at.In(link), ix + iw - aw, cy, UiTokens.PairRow, aw - UiTokens.S3, UiStyle.SmallStrong, UiRole.AccentText, m.PeopleAll, UiFlags.Label | UiFlags.Right);
                    UiDraw.Node(s, at, ix + iw - aw, cy, aw, UiTokens.PairRow, AllCommand, 0, link);
                }
                cy += UiTokens.PairRow + UiTokens.S1;
                for (int i = 0; i < m.Reviews.Length && i < 2; i++)
                {
                    float rh = ReviewHeight(m.Reviews[i], iw);
                    if (cy + rh - UiTokens.S3 > bottom)
                    {
                        break;
                    }
                    Review(s, at, ref m.Reviews[i], ix, cy, iw);
                    cy += rh;
                    shown++;
                }
            }
        }

        private static float Busy(UiScene s, UiAt at, MapPlaceModel m, float x, float y, float w)
        {
            float cy = y;
            float nw = string.IsNullOrEmpty(m.BusyNote) ? 0f : UiDraw.Snap(UiTextRules.Width(m.BusyNote, UiStyle.SmallStrong) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, w - nw - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text, m.BusyTitle, UiFlags.Label | UiFlags.Truncate);
            if (nw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - nw, cy, UiTokens.PairRow, nw, UiStyle.SmallStrong, m.BusyUp ? UiRole.WarnText : UiRole.Muted, m.BusyNote, UiFlags.Right | UiFlags.Truncate);
            }
            cy += UiTokens.PairRow + UiTokens.S1;
            int n = m.Busy.Length;
            float gap = UiTokens.S1;
            float bw = UiDraw.SnapDown((w - (n - 1) * gap) / n);
            float total = n * bw + (n - 1) * gap;
            float bx = x + (w - total) / 2f;
            for (int i = 0; i < n; i++)
            {
                int v = Math.Max(0, Math.Min(100, m.Busy[i]));
                float bh = Math.Max(UiTokens.S1, UiDraw.Snap(BusyBars * v / 100f));
                UiDraw.Box(s, at, bx + i * (bw + gap), cy + BusyBars - bh, bw, bh, i == m.BusyNow ? UiRole.Accent : UiRole.Track, UiRole.None, UiTokens.RadiusSmall, i == m.BusyNow ? UiFlags.Selected : UiFlags.None);
            }
            cy += BusyBars + UiTokens.S1;
            for (int i = 0; i < n; i++)
            {
                int hour = (m.BusyFrom + i) % 24;
                if (hour % 3 == 0)
                {
                    string label = hour.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                    UiDraw.TextMiddle(s, at, bx + i * (bw + gap) - UiTokens.S2, cy, UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)), bw + UiTokens.S4, UiStyle.Small, UiRole.Muted, label, UiFlags.Centre);
                }
            }
            return cy + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
        }

        private static void Review(UiScene s, UiAt at, ref MapReview r, float x, float y, float w)
        {
            UiDraw.Avatar(s, at, x, y, Avatar, r.Name, r.Hue);
            float tx = x + Avatar + UiTokens.S3, tw = w - Avatar - UiTokens.S3;
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            float when = string.IsNullOrEmpty(r.When) ? 0f : UiDraw.Snap(UiTextRules.Width(r.When, UiStyle.Small) + UiTokens.S1);
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Name, UiStyle.BodyStrong) + UiTokens.S1), tw - when - UiTokens.IconSmall * 5f - UiTokens.S4);
            UiDraw.TextMiddle(s, at, tx, y, lh, nameW, UiStyle.BodyStrong, UiRole.Text, r.Name, UiFlags.Truncate);
            float sx = tx + nameW + UiTokens.S2;
            sx += UiDraw.Stars(s, at, sx, y + (lh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, r.Halves) + UiTokens.S2;
            if (when > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - when, y, lh, when, UiStyle.Small, UiRole.Muted, r.When, UiFlags.Right | UiFlags.Truncate);
            }
            if (!string.IsNullOrEmpty(r.Text))
            {
                UiDraw.Text(s, at, tx, y + lh + UiTokens.S1, tw, UiStyle.Body, UiRole.Text2, r.Text, UiFlags.Wrap | UiFlags.Truncate, 2);
            }
        }

        // ------------------------------------------------------------------------------------------------- reviews and business

        private static void Reviews(UiScene s, UiAt at, MapPlaceModel m, float px, float y, float bottom)
        {
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            if (m.Reviews.Length == 0)
            {
                UiDraw.Text(s, at, ix, y + UiTokens.S2, iw, UiStyle.Body, UiRole.Text2, m.ReviewsEmpty, UiFlags.Wrap | UiFlags.Label, Math.Max(1, UiTextRules.Lines(m.ReviewsEmpty, UiStyle.Body, iw, 3, out float widest)));
                return;
            }
            float content = 0f;
            for (int i = 0; i < m.Reviews.Length; i++)
            {
                content += ReviewHeight(m.Reviews[i], iw - UiTokens.S4);
            }
            float view = Math.Max(UiTokens.S12, bottom - y);
            float offset = Math.Max(0f, Math.Min(m.Offset, Math.Max(0f, content - view)));
            m.Offset = offset;
            short clip = s.AddClip(px, y, Width, Math.Min(view, content), offset);
            s.Clips[clip].ContentHeight = content;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float cy = 0f;
            for (int i = 0; i < m.Reviews.Length; i++)
            {
                float rh = ReviewHeight(m.Reviews[i], iw - UiTokens.S4);
                int row = UiDraw.Box(s, inClip, UiTokens.S4, cy, iw - UiTokens.S4, rh - UiTokens.S3, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                Review(s, inClip.In(row), ref m.Reviews[i], UiTokens.S4, cy, iw - UiTokens.S4);
                UiDraw.Node(s, inClip, UiTokens.S4, cy, iw - UiTokens.S4, rh - UiTokens.S3, ReviewCommand, i, row);
                cy += rh;
            }
            if (content > view + 0.5f)
            {
                UiDraw.ScrollThumb(s, at, px + Width - UiTokens.S1, y, Math.Min(view, content), offset, content);
            }
        }

        private static void Business(UiScene s, UiAt at, MapPlaceModel m, float px, float y, float bottom)
        {
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            float cy = y;
            if (!string.IsNullOrEmpty(m.BusinessTitle))
            {
                UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.BusinessTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            for (int i = 0; i < m.Business.Length && cy + UiTokens.PairRow <= bottom + 0.5f; i++)
            {
                MapPlaceRow r = m.Business[i];
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, r.Label, r.Value, false, -1, 0, r.Ink == UiRole.None ? UiRole.Text : r.Ink);
            }
        }
    }
}
