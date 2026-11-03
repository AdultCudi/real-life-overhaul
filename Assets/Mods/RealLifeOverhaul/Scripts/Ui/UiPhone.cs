using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// One tile on the phone's screen: an app (its glyph in its own colour, or another mod's own picture), or a folder
    /// (the glyphs of its first four apps in a two by two). Its name goes under it and its badge on its corner.
    /// </summary>
    public struct UiPhoneApp
    {
        public UiIcon Icon;
        public UiRole Tint;          // None for the icon's own colour (UiIconTints.Of)
        public string Label;
        public int Badge;
        public int Picture;          // another mod's own picture (UiPictures), drawn instead of the glyph; 0 or less for none
        public bool Folder;          // a folder: Minis are its first apps' glyphs
        public UiIcon[] Minis;
        public bool HasPicture => Picture > 0;
    }

    /// <summary>What the phone shows: its name, the time, a page of tiles, what comes next, the radio and the pages.</summary>
    public sealed class UiPhoneModel
    {
        public string Name = "BizPhone";
        public string Time;
        public string Folder;                        // the open folder's name in the head, with a way back; null on the home screen
        public bool Collapsed;                       // folded down to the screen's edge: the head's chevron opens it again
        public UiPhoneApp[] Apps = new UiPhoneApp[0];
        public int AppCount = -1;                    // the tiles of this page in Apps; -1 for all of Apps
        public string NextTitle;                     // "Up next": the day's next things under the apps
        public UiInfoRow[] Next = new UiInfoRow[0];  // each a time and what it is
        public int NextCount = -1;                   // -1 for all of Next
        public string NextEmpty;                     // what fills this place and how, when nothing is coming
        public int Page;
        public int Pages = 1;
        public int OtherPagesBadge;                  // the numbers waiting on the pages not shown, on the next arrow
        public bool Radio;                           // the game's radio row is up (the radio plays)
        public string RadioTitle;                    // the station
        public string RadioTrack;                    // the song, or the game's word that songs are loading
        public float RadioVolume;                    // 0 to 1, the game's own slider
        public bool RadioCanSkip;
        public bool Driver;                          // the game's private driver button is up
        public string DriverLabel;
        public float DeviceHeight;                   // px: the phone's height on a frame that is not the phone's own; 0 for the game's
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;

        public int Tiles => AppCount < 0 ? Apps.Length : Math.Min(AppCount, Apps.Length);
        public int NextRows => NextCount < 0 ? Next.Length : Math.Min(NextCount, Next.Length);
    }

    /// <summary>
    /// The phone, as a phone: Big Ambitions' phone held at the screen's bottom right edge, restyled in the kit's tokens
    /// (design/DIRECTION_CS2.md, section 4.7; the owner, 2 Nov: "Biz phone does not look like a phone at all"). A device
    /// body with a thin bezel and rounded corners, a phone's status line (the time, the camera, signal and battery), the
    /// phone's name in cyan capitals with its fold-down chevron, the apps as rounded phone tiles with their names under
    /// them, what comes next filling the room under the apps, the game's radio row and private driver button when the game
    /// shows them, and a nav bar fixed at the screen's foot with the page arrows and dots and the home indicator under
    /// it, so the next page is always in one place (the owner, 1 Nov: "The next button must be at the bottom"). Inside a
    /// folder the head names it with a back chevron. On a frame of kind Phone the frame is the device (the game's phone);
    /// any other frame stands the device at its bottom right above the status bar. Commands: <see cref="AppCommand"/> (a
    /// tile's index on the page), <see cref="PageCommand"/> (-1 back, +1 on), <see cref="ControlCommand"/> (the head's
    /// and the radio's buttons, by <see cref="Collapse"/> and its neighbours). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPhone
    {
        public const int AppCommand = 1, PageCommand = 2, ControlCommand = 3;
        public const int Collapse = 0, FolderBack = 1, RadioSkip = 2, RadioQuieter = 3, RadioLouder = 4, DriverButton = 5;   // ControlCommand's arguments

        public const float Width = 328f;          // px: the game's phone with its frame (Smartphone/Container, 628 canvas units, and the frame's 21), on the grid
        public const float HeightShort = 564f;    // px: the game's phone without the radio row (1,050 units and the frame's 61), on the grid
        public const float HeightTall = 648f;     // px: with the radio row (1,230 units and the frame's 61), on the grid
        public const float ScreenMargin = 16f;    // px: the least the phone keeps clear of the screen's top, whatever stretches the game's phone
        public const int Across = 3;              // tiles a row: a phone's columns, each name whole under its tile

        // The device's own shapes, the one place the kit draws a phone rather than a panel: a body rounder than any panel
        // and an app tile rounded as a phone's app icons are. Every gap inside is a token step.
        private const float Bezel = 8f;           // px: the body's thin rim round the screen
        private const float BodyCorner = 32f;     // px
        private const float ScreenCorner = 24f;   // px: the body's corner less its rim
        private const float TileCorner = 16f;     // px: an app's tile
        private const float Tile = 56f;           // px: an app's tile
        private const float Glyph = 32f;          // px: the glyph on it
        private const float StatusH = 28f;        // px: the phone's status line
        private const float HeadH = 40f;          // px: the phone's name and its chevron
        private const float NavH = 48f;           // px: the nav bar with the page arrows
        private const float IndicatorH = 16f;     // px: the home indicator's strip under it
        private const float IndicatorW = 96f;     // px
        private const float RadioH = 76f;         // px: the radio's two lines and its buttons
        private const float DriverH = 36f;        // px: the private driver's button and its gap
        private const int MostRows = 4;
        private const int NextShown = 2;          // rows of what comes next the apps always leave room for

        /// <summary>
        /// The phone's height in px: the game's own phone, taller with the radio row, never taller than the screen. It
        /// never follows the game's phone's height, which other mods stretch (one grows it to show forty buttons at once):
        /// their buttons are tiles on the kit's pages instead.
        /// </summary>
        public static float DeviceHeight(bool radio, float screenHeight)
        {
            float wanted = radio ? HeightTall : HeightShort;
            float most = UiDraw.SnapDown(screenHeight - ScreenMargin);
            return most > 0f && wanted > most ? most : wanted;
        }

        /// <summary>
        /// The kit phone's raise in the game's container, screen px, so its foot clears the HUD's bar (<paramref name="lift"/>,
        /// 0 for no bar): <paramref name="footNow"/> its foot now, <paramref name="raisedNow"/> the raise it has, and
        /// <paramref name="travel"/> how far the game's fold has taken the container from its open place (below is under 0).
        /// Measured where the container stands open, the raise is the same at every point of the fold, so the phone folds
        /// with the game's. Measured where it stood now, the raise grew by all of the fold and held the phone up.
        /// </summary>
        public static float Raise(float lift, float footNow, float raisedNow, float travel)
        {
            // The owner, 3 Nov 2026: "The phone is not closing".
            if (!(lift > 0f))
            {
                return 0f;
            }
            float footOpen = footNow - raisedNow - Math.Min(0f, travel);
            return Math.Max(0f, lift - footOpen);
        }

        /// <summary>A label's height under its tile: two lines, so a long name wraps whole rather than being cut.</summary>
        private static float LabelH => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small) * 2f);

        private static float RowH => Tile + UiTokens.S2 + LabelH + UiTokens.S1;   // the name clear of the focus ring round the tile

        private static float SectionHead => UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));

        // The room for the tiles: the screen less its status line, head, the gap under it, the nav bar, the radio row and
        // what comes next. The private driver's button takes what comes next's room, never a row of apps, so no app moves
        // to another page when the game shows it.
        private static float GridRoom(float deviceH, bool radio)
        {
            float screen = deviceH - Bezel * 2f;
            float nextMin = SectionHead + NextShown * UiTokens.PairRow;
            return screen - StatusH - HeadH - UiTokens.S3 - NavH - IndicatorH - (radio ? RadioH : 0f) - nextMin;
        }

        /// <summary>The tiles a page holds on a phone of this height: whole rows, as many as leave what comes next its room.</summary>
        public static int PerPage(float deviceH, bool radio)
        {
            int rows = (int)Math.Floor(GridRoom(deviceH, radio) / RowH + 0.001f);
            rows = Math.Max(1, Math.Min(MostRows, rows));
            return rows * Across;
        }

        /// <summary>The device's height on this frame: the frame itself on the game's phone, else the model's or the game's own.</summary>
        public static float HeightOn(UiFrame frame, UiPhoneModel m)
        {
            if (frame.Kind == UiFrameKind.Phone)
            {
                return frame.Height;
            }
            return m.DeviceHeight > 0f ? Math.Min(m.DeviceHeight, frame.BandY - UiTokens.S4) : DeviceHeight(m.Radio, frame.BandY - UiTokens.S4);
        }

        /// <summary>The height of the phone's top that shows while it is folded down: its rim, status line and head.</summary>
        public static float FoldedHeight => Bezel + StatusH + HeadH + UiTokens.S2;

        public static void Lay(UiScene s, UiPhoneModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("phone", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            bool own = frame.Kind == UiFrameKind.Phone;
            // Over the world (a preview, a check), a folded phone is its top standing on the status bar, as the game lowers
            // its phone to the screen's edge; on the game's phone the game's container does the lowering.
            bool peek = m.Collapsed && !own;
            float h = peek ? FoldedHeight : HeightOn(frame, m);
            float w = own ? frame.Width : Width;
            float x = own ? 0f : frame.Width - Width - UiTokens.S4, y = own ? 0f : frame.BandY - h - (peek ? 0f : UiTokens.S4);
            int body = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.StatusBar, UiRole.PanelEdge, peek ? -BodyCorner : BodyCorner, UiFlags.Panel | UiFlags.Chrome);
            UiAt at = UiAt.Screen.In(body);
            float sx = x + Bezel, sy = y + Bezel, sw = w - Bezel * 2f, sh = h - Bezel * (peek ? 1f : 2f);
            int screen = UiDraw.Box(s, at, sx, sy, sw, sh, UiRole.Panel, UiRole.None, peek ? -ScreenCorner : ScreenCorner, UiFlags.Chrome);
            UiAt on = at.In(screen);
            int firstNode = s.NodeCount;
            StatusLine(s, on, m, sx, sy, sw);
            float ty = Head(s, on, m, sx, sy + StatusH, sw);
            int first = -1;
            if (!peek)
            {
                float footY = sy + sh - IndicatorH - NavH;
                float rowsBottom = footY - (m.Radio ? RadioH : 0f) - (m.Driver ? DriverH : 0f);
                int perPage = PerPage(h, m.Radio);
                float gridTop = ty + UiTokens.S3;
                float cell = UiDraw.SnapDown((sw - UiTokens.S3 * 2f) / Across);
                float gx = sx + UiDraw.SnapDown((sw - cell * Across) / 2f);
                int tiles = Math.Min(m.Tiles, perPage);
                for (int i = 0; i < tiles; i++)
                {
                    float cx = gx + (i % Across) * cell, cy = gridTop + (i / Across) * RowH;
                    int node = App(s, on, ref m.Apps[i], i, cx, cy, cell);
                    first = first < 0 ? node : first;
                }
                int rows = Math.Max(1, (perPage + Across - 1) / Across);
                float ny = gridTop + rows * RowH;
                Next(s, on, m, sx + UiTokens.S4, ny, sw - UiTokens.S4 * 2f, rowsBottom - ny);
                float ry = rowsBottom;
                if (m.Driver)
                {
                    ry = DriverRow(s, on, m, sx + UiTokens.S4, ry, sw - UiTokens.S4 * 2f);
                }
                if (m.Radio)
                {
                    RadioRow(s, on, m, sx + UiTokens.S4, ry, sw - UiTokens.S4 * 2f);
                }
                Nav(s, on, m, sx, footY, sw, keys);
                // The home indicator, a phone's own mark under its nav bar.
                UiDraw.Box(s, on, sx + UiDraw.SnapDown((sw - IndicatorW) / 2f), sy + sh - IndicatorH + UiTokens.S2 - UiTokens.S1 / 2f, IndicatorW, UiTokens.S1, UiRole.Track,
                    UiRole.None, UiTokens.S1 / 2f, UiFlags.Circle | UiFlags.Decor);
            }
            Links(s, firstNode, s.NodeCount);
            if (first >= 0)
            {
                s.FocusFirst = first;   // the apps, never the chevron: a pad's first press would fold the phone away
            }
            if (!own)
            {
                for (int i = 0; i < m.PromptCount; i++)
                {
                    // Only the keys that do something now: no pages to turn on one page, nothing to move to on a folded phone.
                    UiAction act = m.Prompts[i].Action;
                    if ((act == UiAction.TabPrev || act == UiAction.TabNext) && (peek || m.Pages <= 1) || act == UiAction.Move && peek)
                    {
                        continue;
                    }
                    s.AddPrompt(act, m.Prompts[i].Label);
                }
                UiDraw.PromptBar(s, frame, keys);
            }
        }

        // The d-pad's ways, as a phone's are: left and right along the row and nowhere past its ends; up and down to the
        // row above or below, onto its target nearest across. The head, the rows of tiles, the driver, the radio's two
        // lines and the nav bar are the rows, read from where the targets stand. A target resting (dimmed) is left out.
        private static void Links(UiScene s, int from, int to)
        {
            const float Same = UiTokens.S2;   // px: two targets' middles this close in height stand on one row
            for (int i = from; i < to; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                float cx = n.X + n.W / 2f, cy = n.Y + n.H / 2f;
                int left = i, right = i, up = i, down = i;
                float bestLeft = float.MaxValue, bestRight = float.MaxValue, rowUp = float.MaxValue, rowDown = float.MaxValue, acrossUp = float.MaxValue, acrossDown = float.MaxValue;
                for (int j = from; j < to; j++)
                {
                    if (j == i || s.Nodes[j].Disabled)
                    {
                        continue;
                    }
                    ref UiNode o = ref s.Nodes[j];
                    float ox = o.X + o.W / 2f, oy = o.Y + o.H / 2f, dy = oy - cy, dx = ox - cx;
                    if (Math.Abs(dy) < Same)
                    {
                        if (dx < 0f && -dx < bestLeft)
                        {
                            bestLeft = -dx;
                            left = j;
                        }
                        else if (dx > 0f && dx < bestRight)
                        {
                            bestRight = dx;
                            right = j;
                        }
                        continue;
                    }
                    if (dy < 0f && (-dy < rowUp - Same || Math.Abs(-dy - rowUp) < Same && Math.Abs(dx) < acrossUp))
                    {
                        rowUp = Math.Min(rowUp, -dy);
                        acrossUp = Math.Abs(dx);
                        up = j;
                    }
                    else if (dy > 0f && (dy < rowDown - Same || Math.Abs(dy - rowDown) < Same && Math.Abs(dx) < acrossDown))
                    {
                        rowDown = Math.Min(rowDown, dy);
                        acrossDown = Math.Abs(dx);
                        down = j;
                    }
                }
                n.Left = left;
                n.Right = right;
                n.Up = up;
                n.Down = down;
            }
        }

        // The phone's status line: the time at the left, the camera in the middle, the signal and the battery at the right.
        private static void StatusLine(UiScene s, UiAt at, UiPhoneModel m, float x, float y, float w)
        {
            float inset = UiTokens.S5;
            if (!string.IsNullOrEmpty(m.Time))
            {
                UiDraw.TextMiddle(s, at, x + inset, y, StatusH, UiDraw.SnapDown(w / 3f), UiStyle.SmallStrong, UiRole.Text, m.Time, UiFlags.None);
            }
            float dot = UiTokens.S2;
            UiDraw.Circle(s, at, x + UiDraw.SnapDown((w - dot) / 2f), y + (StatusH - dot) / 2f, dot, dot, UiRole.StatusBar, UiFlags.Decor | UiFlags.Middle);
            // Signal: four bars rising; battery: a body and its fill. Shapes of the phone itself, not icons with a meaning.
            float right = x + w - inset;
            float bw = UiTokens.S5 + UiTokens.S1, bh = UiTokens.S3 - UiTokens.S1 / 2f;
            float by = y + (StatusH - bh) / 2f;
            int battery = UiDraw.Box(s, at, right - bw, by, bw, bh, UiRole.None, UiRole.Text2, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Middle);
            UiDraw.Box(s, at.In(battery), right - bw + 2f, by + 2f, (bw - 4f) * 0.7f, bh - 4f, UiRole.Text2, UiRole.None, 1f, UiFlags.Decor | UiFlags.Middle);
            const float barW = 3f, gap = 2f;
            float tallest = UiTokens.S3;
            float bars = right - bw - UiTokens.S2 - (barW * 4f + gap * 3f);
            for (int i = 0; i < 4; i++)
            {
                float bar = tallest * (i + 1) / 4f;
                UiDraw.Box(s, at, bars + i * (barW + gap), y + (StatusH + tallest) / 2f - bar, barW, bar, UiRole.Text2, UiRole.None, 1f, UiFlags.Decor | UiFlags.Middle);
            }
        }

        // The phone's name in cyan capitals and its fold-down chevron, or the open folder's name with its back chevron.
        // Returns the y under its hairline.
        private static float Head(UiScene s, UiAt at, UiPhoneModel m, float x, float y, float w)
        {
            float b = UiTokens.ControlSmall;
            float side = UiTokens.S3 + b;
            bool folder = !string.IsNullOrEmpty(m.Folder);
            if (folder)
            {
                UiDraw.IconButton(s, at, x + UiTokens.S3, y + (HeadH - b) / 2f, b, UiIcon.Back, UiRole.Text, ControlCommand, FolderBack, false, UiAction.Back, false, true);
            }
            string title = folder ? m.Folder : m.Name;
            UiDraw.TextMiddle(s, at, x + side, y, HeadH, w - side * 2f, UiStyle.SectionTitle, UiRole.AccentText, title, UiFlags.Centre | UiFlags.Truncate | UiFlags.Chrome);
            UiDraw.IconButton(s, at, x + w - UiTokens.S3 - b, y + (HeadH - b) / 2f, b, m.Collapsed ? UiIcon.Up : UiIcon.Down, UiRole.Text2, ControlCommand, Collapse, false,
                UiAction.Count, false, true);
            UiDraw.Box(s, at, x + UiTokens.S4, y + HeadH, w - UiTokens.S4 * 2f, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            return y + HeadH;
        }

        // A tile: an app's glyph on its rounded slate tile (or another mod's picture, or a folder's four glyphs), its name
        // under it, a badge on its corner for what waits. Returns its target.
        private static int App(UiScene s, UiAt at, ref UiPhoneApp app, int index, float x, float y, float w)
        {
            float tx = x + UiDraw.SnapDown((w - Tile) / 2f);
            int face = UiDraw.Box(s, at, tx, y, Tile, Tile, UiRole.Tile, UiRole.None, TileCorner);
            UiAt inside = at.In(face);
            if (app.Folder)
            {
                Minis(s, inside, app.Minis, tx, y);
            }
            else if (app.HasPicture)
            {
                float p = Glyph + UiTokens.S2;
                UiDraw.Picture(s, inside, tx + (Tile - p) / 2f, y + (Tile - p) / 2f, p, app.Picture);
            }
            else
            {
                UiDraw.Icon(s, inside, tx + (Tile - Glyph) / 2f, y + (Tile - Glyph) / 2f, Glyph, app.Icon, app.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            if (app.Badge > 0)
            {
                UiDraw.Badge(s, inside, tx + Tile + UiTokens.S2, y - UiTokens.S1, app.Badge, true);   // its number below the focus ring's stroke
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Overflow;
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Overflow;
            }
            UiDraw.Text(s, at, x, y + Tile + UiTokens.S2, w, UiStyle.Small, UiRole.Text, app.Label, UiFlags.Centre | UiFlags.Wrap | UiFlags.Label, 2);
            return UiDraw.Node(s, at, tx, y, Tile, Tile, AppCommand, index, face);
        }

        // A folder's tile: its first four apps' glyphs, two by two, each in its own colour, as a phone shows a folder. Set
        // low enough that a badge on the tile's corner never covers one.
        private static void Minis(UiScene s, UiAt at, UiIcon[] minis, float x, float y)
        {
            float cell = UiTokens.S4, pad = UiTokens.S1;
            float from = (Tile - cell * 2f - pad) / 2f;
            for (int k = 0; minis != null && k < minis.Length && k < 4; k++)
            {
                if (minis[k] == UiIcon.None)
                {
                    continue;
                }
                float mx = x + from + (k % 2) * (cell + pad), my = y + UiTokens.S4 + (k / 2) * (cell + pad);
                UiDraw.Icon(s, at, mx, my, cell, minis[k], UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
        }

        // What comes next under the apps: a time and what it is, as many as the room holds; a sentence when nothing is.
        private static void Next(UiScene s, UiAt at, UiPhoneModel m, float x, float y, float w, float room)
        {
            int rows = m.NextRows;
            if (string.IsNullOrEmpty(m.NextTitle) || room < SectionHead + UiTokens.PairRow)
            {
                return;
            }
            UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float ty = y + UiTokens.S2;
            UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text, m.NextTitle, UiFlags.Label | UiFlags.Truncate);
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            if (rows == 0)
            {
                if (!string.IsNullOrEmpty(m.NextEmpty))
                {
                    int lines = Math.Max(1, (int)((y + room - ty) / UiTextRules.LineHeight(UiStyle.Body)));
                    UiDraw.Text(s, at, x, ty, w, UiStyle.Body, UiRole.Text2, m.NextEmpty, UiFlags.Wrap | UiFlags.Label, Math.Min(3, lines));
                }
                return;
            }
            // The when column as wide as the widest word of the rows the room can show ("16:00", "Tomorrow"), never more than
            // two fifths: a row out of sight never widens it.
            float tw = UiTextRules.Width("88:88", UiStyle.BodyStrong);
            int shown = Math.Min(rows, (int)((y + room - ty) / UiTokens.PairRow));
            for (int i = 0; i < shown; i++)
            {
                tw = Math.Max(tw, UiTextRules.Width(m.Next[i].Label, UiStyle.BodyStrong));
            }
            tw = Math.Min(UiDraw.Snap(tw + UiTokens.S3), UiDraw.SnapDown(w * 0.4f));
            float vw = w - tw, line = UiTextRules.LineHeight(UiStyle.Body), bottom = y + room;
            for (int i = 0; i < rows; i++)
            {
                // A long event takes a second line, cut at a word only past it, so it never stops mid phrase on one line.
                string what = m.Next[i].Value;
                bool two = !UiTextRules.Fits(what, UiStyle.Body, vw);
                float rh = two ? UiDraw.Snap(line * 2f + UiTokens.S1) : UiTokens.PairRow;
                if (ty + rh > bottom)
                {
                    break;
                }
                // Past two lines it is cut at a word, shorter until the two lines hold it (words wrap, so twice the width is not enough).
                for (float most = vw * 2f; two && most > vw && UiTextRules.Lines(what, UiStyle.Body, vw, 3, out float _) > 2; most -= UiTokens.S4)
                {
                    what = UiTextRules.Cut(m.Next[i].Value, UiStyle.Body, most);
                }
                UiDraw.TextMiddle(s, at, x, ty, UiTokens.PairRow, tw, UiStyle.BodyStrong, UiRole.Text, m.Next[i].Label, UiFlags.Truncate);
                if (two)
                {
                    UiDraw.Text(s, at, x + tw, ty + (UiTokens.PairRow - line) / 2f, vw, UiStyle.Body, UiRole.Text, what, UiFlags.Wrap | UiFlags.Middle, 2);
                }
                else
                {
                    UiDraw.TextMiddle(s, at, x + tw, ty, UiTokens.PairRow, vw, UiStyle.Body, UiRole.Text, what, UiFlags.Truncate);
                }
                ty += rh;
            }
        }

        // The game's private driver button, as a row button with the car: it presses the game's own.
        private static float DriverRow(UiScene s, UiAt at, UiPhoneModel m, float x, float y, float w)
        {
            float bh = UiTokens.ControlSmall;
            int face = UiDraw.Box(s, at, x, y, w, bh, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, x + UiTokens.S3, y + (bh - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Key, UiRole.None, UiFlags.Middle, true);   // the car's key: the car glyph is the showroom's
            float tx = x + UiTokens.S3 + UiTokens.Icon + UiTokens.S2;
            UiDraw.TextMiddle(s, inside, tx, y, bh, w - (tx - x) - UiTokens.S3, UiStyle.BodyStrong, UiRole.Text, m.DriverLabel, UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, bh, ControlCommand, DriverButton, face);
            return y + DriverH;
        }

        // The game's radio: the station and the song, the skip, and the volume between a quieter and a louder button.
        private static void RadioRow(UiScene s, UiAt at, UiPhoneModel m, float x, float y, float w)
        {
            float b = UiTokens.ControlSmall;
            UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float ly = y + UiTokens.S3;
            UiDraw.Icon(s, at, x, ly + (b - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.MusicNotes, UiRole.None, UiFlags.Middle, true);
            float tx = x + UiTokens.Icon + UiTokens.S2, tw = w - (tx - x) - b - UiTokens.S2;
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong));
            UiDraw.Text(s, at, tx, ly, tw, UiStyle.SmallStrong, UiRole.Text, m.RadioTitle, UiFlags.Truncate);
            UiDraw.Text(s, at, tx, ly + lh, tw, UiStyle.Small, UiRole.Text2, m.RadioTrack, UiFlags.Truncate);
            int skip = UiDraw.IconButton(s, at, x + w - b, ly, b, UiIcon.FastForward, UiRole.None, ControlCommand, RadioSkip);
            if (!m.RadioCanSkip)
            {
                Rest(s, skip);
            }
            float small = UiTokens.MinTarget;
            float vy = ly + b + UiTokens.S1;
            UiDraw.IconButton(s, at, x, vy, small, UiIcon.Minus, UiRole.Text2, ControlCommand, RadioQuieter, false, UiAction.Count, false, true);
            UiDraw.IconButton(s, at, x + w - small, vy, small, UiIcon.Plus, UiRole.Text2, ControlCommand, RadioLouder, false, UiAction.Count, false, true);
            float mx = x + small + UiTokens.S2, mw = w - (small + UiTokens.S2) * 2f;
            UiDraw.Meter(s, at, mx, vy + (small - UiTokens.S1) / 2f, mw, UiTokens.S1, (int)Math.Round(Math.Max(0f, Math.Min(1f, m.RadioVolume)) * 100f), UiRole.Accent);
        }

        // A button with nothing to do now: it stays in its place, so nothing moves, but dims and takes no press.
        private static void Rest(UiScene s, int node)
        {
            s.Nodes[node].Disabled = true;
            ref UiItem glyph = ref s.Items[s.ItemCount - 1];
            glyph.Flags |= UiFlags.Disabled;
            glyph.Alpha = 0.4f;
        }

        // The nav bar fixed at the screen's foot: back a page, the pages as dots, on a page; the same place on every page
        // and in every folder. Pages wrap, so the arrows dim only when there is one page.
        private static void Nav(UiScene s, UiAt at, UiPhoneModel m, float x, float y, float w, IUiKeys keys)
        {
            UiDraw.Box(s, at, x + UiTokens.S4, y, w - UiTokens.S4 * 2f, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float b = UiTokens.Control;
            float by = y + (NavH - b) / 2f;
            bool one = m.Pages <= 1;
            int back = UiDraw.IconButton(s, at, x + UiTokens.S3, by, b, UiIcon.Back, UiRole.Text2, PageCommand, -1, false, UiAction.TabPrev, false, true);
            if (one)
            {
                Rest(s, back);
            }
            float onX = x + w - UiTokens.S3 - b;
            int on = UiDraw.IconButton(s, at, onX, by, b, UiIcon.Next, UiRole.Text2, PageCommand, 1, false, UiAction.TabNext, false, true);
            if (one)
            {
                Rest(s, on);
            }
            // While the keys lead, each arrow shows its key beside it, inside the bar; with the mouse they are gone.
            float inner = onX - UiTokens.S1;
            if (!s.Pointer && !one && keys != null)
            {
                float capY = y + (NavH - UiTokens.KeyCap) / 2f;
                UiDraw.KeyCap(s, at, x + UiTokens.S3 + b + UiTokens.S1, capY, UiAction.TabPrev, keys, UiRole.PromptCap, UiRole.PromptInk);
                float cap = UiDraw.KeyCapWidth(UiAction.TabNext, keys);
                inner -= cap;
                UiDraw.KeyCap(s, at, inner, capY, UiAction.TabNext, keys, UiRole.PromptCap, UiRole.PromptInk);
                inner -= UiTokens.S1;
            }
            if (!one && m.OtherPagesBadge > 0)
            {
                UiDraw.Badge(s, at, inner, y + (NavH - UiTokens.Badge) / 2f, m.OtherPagesBadge, true);   // beside the arrow, never over it
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Middle;   // centred on the bar's height, as its arrows are
            }
            int pages = Math.Max(1, m.Pages);
            float dot = UiTokens.S2, gap = UiTokens.S2;
            float total = pages * dot + (pages - 1) * gap;
            float dx = x + UiDraw.SnapDown((w - total) / 2f);
            for (int i = 0; i < pages; i++)
            {
                UiDraw.Circle(s, at, dx + i * (dot + gap), y + (NavH - dot) / 2f, dot, dot, i == m.Page ? UiRole.Accent : UiRole.Track, UiFlags.Middle);
            }
        }
    }
}
