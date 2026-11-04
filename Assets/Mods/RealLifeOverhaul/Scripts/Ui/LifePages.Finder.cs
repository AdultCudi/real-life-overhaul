using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One tool of the viewfinder's strip: its icon, the word for its state, whether it is on, and which tool it is (a number the camera knows).</summary>
    public struct UiFinderTool
    {
        public UiIcon Icon;
        public string Label;
        public bool On;
        public int Id;
    }

    /// <summary>
    /// The viewfinder's chrome: the modes, the place and the weather, the strip of the zoom, the exposure and the tools, the
    /// filters and the frames, the shutter, the newest photo and the way into the Gallery. The camera fills it when something
    /// on it changes. Game-free.
    /// </summary>
    public sealed class UiFinderModel
    {
        public string Title;
        public string[] Modes = new string[0];
        public int Mode;
        public string Place;
        public string When;
        public string Count;
        public string[] Filters = new string[0];
        public int Filter;
        public int FilterFirst;            // the first filter chip shown: the strip pages when they do not all fit
        public string[] Frames = new string[0];
        public int Frame;
        public string FilterTitle, FrameTitle;
        public string Toast;
        public string Open;
        public string Latest;              // the newest photo's picture key, or null
        public string Newest, OpenGallery; // "Newest photo", "Open Gallery"
        public string Shoot;               // "Take a photo"
        public string Zoom;                // "2.4x"; null for the selfie, which does not zoom
        public string[] Lenses = new string[0];
        public int Lens = -1;
        public bool ZoomOut = true, ZoomIn = true;
        public string Exposure;            // "+0.5"; null where the camera has none
        public bool ExposureDown = true, ExposureUp = true;
        public UiFinderTool[] Tools = new UiFinderTool[0];
        public string Countdown;           // the second the timer is at; null when it is not counting
        public bool Clean;                 // the chrome is put away but for the way back
        public string CleanBack;
        public string Move, Select, Back;

        /// <summary>Takes what another model holds, field by field: the host's screen keeps its own model, and its paging.</summary>
        public void Take(UiFinderModel m)
        {
            Title = m.Title;
            Modes = m.Modes;
            Mode = m.Mode;
            Place = m.Place;
            When = m.When;
            Count = m.Count;
            Filters = m.Filters;
            Filter = m.Filter;
            Frames = m.Frames;
            Frame = m.Frame;
            FilterTitle = m.FilterTitle;
            FrameTitle = m.FrameTitle;
            Toast = m.Toast;
            Open = m.Open;
            Latest = m.Latest;
            Newest = m.Newest;
            OpenGallery = m.OpenGallery;
            Shoot = m.Shoot;
            Zoom = m.Zoom;
            Lenses = m.Lenses;
            Lens = m.Lens;
            ZoomOut = m.ZoomOut;
            ZoomIn = m.ZoomIn;
            Exposure = m.Exposure;
            ExposureDown = m.ExposureDown;
            ExposureUp = m.ExposureUp;
            Tools = m.Tools;
            Countdown = m.Countdown;
            Clean = m.Clean;
            CleanBack = m.CleanBack;
            Move = m.Move;
            Select = m.Select;
            Back = m.Back;
        }
    }

    public static partial class LifePages
    {
        public const int VZoomOut = 57, VZoomIn = 58, VLens = 59, VExposureDown = 60, VExposureUp = 61, VTool = 62, VFlip = 63, VFilterPage = 64, VClean = 65;
        private const float FinderBar = 56f, FinderFoot = 88f, FinderThumb = 56f;
        private static readonly UiButtonKind[] ToolKinds = { UiButtonKind.Chip, UiButtonKind.Primary };

        /// <summary>The viewfinder chrome over the picture. The picture itself is the camera.</summary>
        public static void Finder(UiScene s, UiFinderModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("viewfinder", frame.Width, frame.Height, dark);
            s.Floats = true;
            s.Pointer = frame.Pointer;
            if (m.Clean)
            {
                Clean(s, m, frame, keys);
                return;
            }
            float y0 = UiTokens.S4;
            int top = UiDraw.Box(s, UiAt.Screen, UiTokens.S4, y0, frame.Width - UiTokens.S8, FinderBar, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = UiAt.Screen.In(top);
            float x = UiTokens.S4 + UiTokens.S3;
            float cx = x;
            if (!string.IsNullOrEmpty(m.Title))
            {
                float iw = UiTokens.Icon;
                UiDraw.Icon(s, on, cx, y0 + (FinderBar - iw) / 2f, iw, UiIcon.Camera, UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
                float tw = UiDraw.Snap(Math.Min(160f, UiTextRules.Width(m.Title, UiStyle.SectionHeader) + UiTokens.S2));
                UiDraw.TextMiddle(s, on, cx + iw + UiTokens.S2, y0, FinderBar, tw, UiStyle.SectionHeader, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Truncate);
                cx += iw + UiTokens.S2 + tw + UiTokens.S3;
            }
            for (int i = 0; i < m.Modes.Length; i++)
            {
                cx = Choice(s, on, cx, y0 + UiTokens.S4, m.Modes[i], i == m.Mode, VMode, i, frame.Width * 0.4f);
            }
            float close = UiTokens.Control;
            UiDraw.IconButton(s, on, frame.Width - UiTokens.S4 - UiTokens.S3 - close, y0 + (FinderBar - close) / 2f, close, UiIcon.Close, UiRole.Text, VClose, 0, false, UiAction.Back);
            float countW = string.IsNullOrEmpty(m.Count) ? 0f : UiDraw.Snap(Math.Min(280f, UiTextRules.Width(m.Count, UiStyle.Small) + UiTokens.S2));
            float right = frame.Width - UiTokens.S4 - UiTokens.S3 - close - UiTokens.S3 - countW;
            if (countW > 0f)
            {
                UiDraw.TextMiddle(s, on, right, y0, FinderBar, countW, UiStyle.Small, UiRole.Text2, m.Count, UiFlags.Right | UiFlags.Truncate);
            }
            float midL = cx + UiTokens.S4;
            float midW = right - UiTokens.S4 - midL;
            if (midW >= UiTokens.S16 * 2f)
            {
                string line = string.IsNullOrEmpty(m.When) ? m.Place : string.IsNullOrEmpty(m.Place) ? m.When : m.Place + "  " + m.When;
                UiDraw.TextMiddle(s, on, midL, y0, FinderBar, midW, UiStyle.Body, UiRole.Text, line, UiFlags.Centre | UiFlags.Truncate);
            }

            float fy = frame.BandY - FinderFoot - UiFrame.PromptLane;   // the keys' prompts stand in the lane between its foot and the bar
            Foot(s, m, frame, keys, fy);
            float stripTop = Strip(s, m, frame, keys, fy);

            if (!string.IsNullOrEmpty(m.Countdown))
            {
                float ch = UiDraw.Snap(UiTextRules.Width("0", UiStyle.Display) * 3f);
                float cw = Math.Max(ch, UiDraw.Snap(UiTextRules.Width(m.Countdown, UiStyle.Display) + UiTokens.S8));
                float dx = UiDraw.SnapDown((frame.Width - cw) / 2f), dy = UiDraw.SnapDown((frame.Height - ch) / 2f);
                int disc = UiDraw.Box(s, UiAt.Screen, dx, dy, cw, ch, UiRole.Panel, UiRole.None, ch / 2f, UiFlags.Circle | UiFlags.Decor);
                UiDraw.TextMiddle(s, UiAt.Screen.In(disc), dx, dy, ch, cw, UiStyle.Display, UiRole.Text, m.Countdown, UiFlags.Centre | UiFlags.Decor);
            }
            if (!string.IsNullOrEmpty(m.Toast))
            {
                float tw = UiDraw.Snap(Math.Min(520f, UiTextRules.Width(m.Toast, UiStyle.Body) + UiTokens.S8));
                float openW = string.IsNullOrEmpty(m.Open) ? 0f : UiDraw.Snap(Math.Min(160f, UiTextRules.Width(m.Open, UiStyle.Small) + UiTokens.S4));
                float tx = UiDraw.SnapDown((frame.Width - tw - openW) / 2f);
                float ty = stripTop - UiTokens.Control - UiTokens.S3;
                int toast = UiDraw.Box(s, UiAt.Screen, tx, ty, tw + openW, UiTokens.Control, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.TextMiddle(s, UiAt.Screen.In(toast), tx, ty, UiTokens.Control, tw, UiStyle.Body, UiRole.Text, m.Toast, UiFlags.Centre | UiFlags.Truncate);
                if (openW > 0f)
                {
                    UiDraw.TextMiddle(s, UiAt.Screen.In(toast), tx + tw, ty, UiTokens.Control, openW, UiStyle.Small, UiRole.AccentText, m.Open, UiFlags.Centre | UiFlags.Truncate);
                    UiDraw.Node(s, UiAt.Screen, tx + tw, ty, openW, UiTokens.Control, VOpen, 0, toast);
                }
            }
            Bar(s, frame, keys, m.Move, m.Select, m.Back, null);
        }

        // The chrome put away: a small button at the foot that brings it back, and the prompts.
        private static void Clean(UiScene s, UiFinderModel m, UiFrame frame, IUiKeys keys)
        {
            string back = string.IsNullOrEmpty(m.CleanBack) ? "..." : m.CleanBack;
            float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, back, UiIcon.FrameCorners, UiAction.Count, keys);
            float by = frame.BandY - UiTokens.Control - UiTokens.S4;
            UiDraw.Button(s, UiAt.Screen, UiDraw.SnapDown((frame.Width - bw) / 2f), by, bw, UiTokens.Control, UiButtonKind.Secondary, back, UiIcon.FrameCorners, UiAction.Count, keys, VClean, 0);
            Bar(s, frame, keys, m.Move, m.Select, m.Back, null);
        }

        // The bottom bar: the newest photo and the way to the Gallery, the filters, the shutter, the frames, the other camera.
        private static void Foot(UiScene s, UiFinderModel m, UiFrame frame, IUiKeys keys, float fy)
        {
            int bottom = UiDraw.Box(s, UiAt.Screen, UiTokens.S4, fy, frame.Width - UiTokens.S8, FinderFoot, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
            UiAt low = UiAt.Screen.In(bottom);
            float shutter = 64f;
            float sx = UiDraw.SnapDown((frame.Width - shutter) / 2f);
            UiDraw.IconButton(s, low, sx, fy + (FinderFoot - shutter) / 2f, shutter, UiIcon.Camera, UiRole.Text, VShutter, 0, false, UiAction.Count, true);
            float left = UiTokens.S4 + UiTokens.S3;
            float edge = frame.Width - UiTokens.S4 - UiTokens.S3;

            // Left: the newest photo, and with room its words.
            float ty = fy + (FinderFoot - FinderThumb) / 2f;
            bool wide = frame.Width >= 1600f;
            float wordsW = 0f;
            if (!string.IsNullOrEmpty(m.Latest))
            {
                int tile = UiDraw.Box(s, low, left, ty, FinderThumb, FinderThumb, UiRole.Tile, UiRole.None, UiTokens.RadiusMedium);
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre | UiFlags.Cover, X = left, Y = ty, W = FinderThumb, H = FinderThumb, Text = m.Latest, Value = 1f,
                    Clip = low.Clip, Node = -1, Layer = low.Layer, Order = low.Order, Parent = tile, Alpha = 1f
                };
                s.Add(ref item);
                UiDraw.Node(s, low, left, ty, FinderThumb, FinderThumb, VOpen, 1, tile, false, -1, 0, UiAction.Option);
                if (wide && !string.IsNullOrEmpty(m.OpenGallery))
                {
                    wordsW = UiDraw.Snap(Math.Min(180f, Math.Max(UiTextRules.Width(m.Newest ?? string.Empty, UiStyle.Small), UiTextRules.Width(m.OpenGallery, UiStyle.Small)) + UiTokens.S2));
                    float wx = left + FinderThumb + UiTokens.S3;
                    if (!string.IsNullOrEmpty(m.Newest))
                    {
                        UiDraw.TextMiddle(s, low, wx, ty, FinderThumb / 2f, wordsW, UiStyle.Small, UiRole.Muted, m.Newest, UiFlags.Truncate);
                    }
                    UiDraw.TextMiddle(s, low, wx, ty + FinderThumb / 2f, FinderThumb / 2f, wordsW, UiStyle.Small, UiRole.Text, m.OpenGallery, UiFlags.Truncate);
                }
                left += FinderThumb + (wordsW > 0f ? UiTokens.S3 + wordsW : 0f) + UiTokens.S6;
            }

            // Right: the other camera, and with room the shutter's words and key.
            float flip = UiTokens.Control;
            UiDraw.IconButton(s, low, edge - flip, fy + (FinderFoot - flip) / 2f, flip, UiIcon.FlipCamera, UiRole.Text, VFlip, 0);
            float right = edge - flip - UiTokens.S4;
            if (wide && !string.IsNullOrEmpty(m.Shoot))
            {
                float tw = UiDraw.ButtonWidth(UiButtonKind.Quiet, m.Shoot, UiIcon.None, UiAction.Alt, keys);
                if (right - tw > sx + shutter + UiTokens.S16 * 3f)
                {
                    UiDraw.Button(s, low, right - tw, fy + (FinderFoot - UiTokens.Control) / 2f, tw, UiTokens.Control, UiButtonKind.Quiet, m.Shoot, UiIcon.None, UiAction.Alt, keys, VShutter, 1);
                    right -= tw + UiTokens.S4;
                }
            }
            PlaceChoices(s, low, m.Filters, m.Filter, VFilter, left, fy + 40f, sx - UiTokens.S6, m.FilterTitle, fy + UiTokens.S2, m);
            PlaceChoices(s, low, m.Frames, m.Frame, VFrame, sx + shutter + UiTokens.S6, fy + 40f, right, m.FrameTitle, fy + UiTokens.S2, null);
        }

        // The strip above the foot: zoom, lenses, exposure and the tools. A row, or two when the screen is narrow. Returns its top.
        private static float Strip(UiScene s, UiFinderModel m, UiFrame frame, IUiKeys keys, float fy)
        {
            const float h = 32f;   // UiTokens.ControlSmall
            float gap = UiTokens.S2, group = UiTokens.S4;
            int tools = m.Tools != null ? m.Tools.Length : 0;
            // Each piece's width, in order: zoom, lenses, exposure, tools.
            float zoomW = string.IsNullOrEmpty(m.Zoom) ? 0f : h + gap + UiDraw.Snap(Math.Max(UiTextRules.Width(m.Zoom, UiStyle.BodyStrong), 40f) + UiTokens.S2) + gap + h;
            float lensW = 0f;
            var lens = new float[m.Lenses.Length];
            for (int i = 0; zoomW > 0f && i < m.Lenses.Length; i++)
            {
                lens[i] = UiDraw.Snap(Math.Max(UiTokens.MinTarget + UiTokens.S4, UiTextRules.Width(m.Lenses[i], UiStyle.Button) + UiTokens.S6));
                lensW += (i > 0 ? gap : 0f) + lens[i];
            }
            float expW = string.IsNullOrEmpty(m.Exposure) ? 0f : UiTokens.Icon + gap + h + gap + UiDraw.Snap(Math.Max(UiTextRules.Width(m.Exposure, UiStyle.BodyStrong), 36f) + UiTokens.S2) + gap + h;
            var toolW = new float[tools];
            for (int i = 0; i < tools; i++)
            {
                toolW[i] = UiDraw.ButtonWidth(UiButtonKind.Chip, m.Tools[i].Label, m.Tools[i].Icon, UiAction.Count, keys);
            }
            float avail = frame.Width - UiTokens.S8 - UiTokens.S6;
            int pieces = 0;
            float total = 0f;
            total += Count(zoomW, ref pieces) + Count(lensW, ref pieces) + Count(expW, ref pieces);
            for (int i = 0; i < tools; i++)
            {
                total += Count(toolW[i], ref pieces);
            }
            if (pieces == 0)
            {
                return fy;
            }
            total += (pieces - 1) * group;
            int rows = total <= avail ? 1 : 2;
            float panelW = UiDraw.Snap(Math.Min(frame.Width - UiTokens.S8, (rows == 1 ? total : avail) + UiTokens.S6));
            float panelH = rows * h + (rows - 1) * gap + UiTokens.S4;
            float py = fy - UiTokens.S3 - panelH;
            float px = UiDraw.SnapDown((frame.Width - panelW) / 2f);
            int panel = UiDraw.Box(s, UiAt.Screen, px, py, panelW, panelH, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
            UiAt in_ = UiAt.Screen.In(panel);
            float x = px + UiTokens.S3, y = py + UiTokens.S2;
            float limit = px + panelW - UiTokens.S3;
            if (zoomW > 0f)
            {
                Piece(ref x, ref y, zoomW, limit, group, h, px + UiTokens.S3, gap);
                int from = s.ItemCount;
                int d = UiDraw.IconButton(s, in_, x, y, h, UiIcon.Minus, UiRole.Text, VZoomOut, 0);
                if (!m.ZoomOut)
                {
                    UiDraw.Dim(s, from, d);
                }
                float vw = zoomW - 2f * h - 2f * gap;
                UiDraw.TextMiddle(s, in_, x + h + gap, y, h, vw, UiStyle.BodyStrong, UiRole.Text, m.Zoom, UiFlags.Centre | UiFlags.Truncate);
                from = s.ItemCount;
                int u = UiDraw.IconButton(s, in_, x + h + gap + vw + gap, y, h, UiIcon.Plus, UiRole.Text, VZoomIn, 0);
                if (!m.ZoomIn)
                {
                    UiDraw.Dim(s, from, u);
                }
                x += zoomW + group;
            }
            if (lensW > 0f)
            {
                Piece(ref x, ref y, lensW, limit, group, h, px + UiTokens.S3, gap);
                float lx = x;
                for (int i = 0; i < m.Lenses.Length; i++)
                {
                    ChoiceAt(s, in_, lx, y, lens[i], h, m.Lenses[i], i == m.Lens, VLens, i);
                    lx += lens[i] + gap;
                }
                x += lensW + group;
            }
            if (expW > 0f)
            {
                Piece(ref x, ref y, expW, limit, group, h, px + UiTokens.S3, gap);
                UiDraw.Icon(s, in_, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Exposure, UiRole.Text2, UiFlags.Middle | UiFlags.Decor);
                float ex = x + UiTokens.Icon + gap;
                int from = s.ItemCount;
                int d = UiDraw.IconButton(s, in_, ex, y, h, UiIcon.Minus, UiRole.Text, VExposureDown, 0);
                if (!m.ExposureDown)
                {
                    UiDraw.Dim(s, from, d);
                }
                float vw = expW - UiTokens.Icon - 2f * h - 3f * gap;
                UiDraw.TextMiddle(s, in_, ex + h + gap, y, h, vw, UiStyle.BodyStrong, UiRole.Text, m.Exposure, UiFlags.Centre | UiFlags.Truncate);
                from = s.ItemCount;
                int u = UiDraw.IconButton(s, in_, ex + h + gap + vw + gap, y, h, UiIcon.Plus, UiRole.Text, VExposureUp, 0);
                if (!m.ExposureUp)
                {
                    UiDraw.Dim(s, from, u);
                }
                x += expW + group;
            }
            for (int i = 0; i < tools; i++)
            {
                Piece(ref x, ref y, toolW[i], limit, group, h, px + UiTokens.S3, gap);
                UiFinderTool t = m.Tools[i];
                ToolAt(s, in_, x, y, Math.Min(toolW[i], limit - x), h, t, keys);
                x += toolW[i] + group;
            }
            return py;
        }

        // A tool: a chip with its icon and the word for its state; on, the chosen fill as the other choices have, not a primary button.
        private static void ToolAt(UiScene s, UiAt at, float x, float y, float w, float h, UiFinderTool t, IUiKeys keys)
        {
            if (!t.On)
            {
                UiDraw.Button(s, at, x, y, w, h, UiButtonKind.Chip, t.Label, t.Icon, UiAction.Count, keys, VTool, t.Id);
                return;
            }
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Accent, UiRole.None, UiTokens.RadiusMedium, UiFlags.Selected);
            UiAt inside = at.In(face);
            float iw = UiTokens.Icon, lx = x + UiTokens.S4;
            UiDraw.Icon(s, inside, lx, y + (h - iw) / 2f, iw, t.Icon, UiRole.OnAccent, UiFlags.Middle | UiFlags.Decor);
            float room = Math.Max(UiTokens.S4, w - UiTokens.S4 * 2f - iw - UiTokens.S2);
            UiDraw.TextMiddle(s, inside, lx + iw + UiTokens.S2, y, h, room, UiStyle.Button, UiRole.OnAccent, t.Label, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, h, VTool, t.Id, face);
        }

        private static float Count(float width, ref int pieces)
        {
            if (width <= 0f)
            {
                return 0f;
            }
            pieces++;
            return width;
        }

        // Moves to the next row when a piece would pass the panel's edge.
        private static void Piece(ref float x, ref float y, float width, float limit, float group, float h, float home, float gap)
        {
            if (x + width > limit + 0.5f && x > home + 0.5f)
            {
                x = home;
                y += h + gap;
            }
        }

        // Filter or frame chips in the room between two edges, one row, under the group's small title; paged with arrows when they do not all fit.
        private static void PlaceChoices(UiScene s, UiAt at, string[] labels, int chosen, int command, float x, float y, float right, string title, float titleY, UiFinderModel pager)
        {
            if (labels == null || labels.Length == 0 || right - x < UiTokens.S16)
            {
                return;
            }
            float h = UiTokens.Chip;
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.Text(s, at, x, titleY, Math.Min(160f, right - x), UiStyle.SectionHeader, UiRole.Muted, title, UiFlags.Label | UiFlags.Truncate);
            }
            var widths = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                widths[i] = UiDraw.Snap(Math.Min(160f, UiTextRules.Width(labels[i], UiStyle.Button) + UiTokens.S8));
            }
            // How many chips from a first fit; a pager keeps the chosen one in view and shows arrows when some are left out.
            float room = right - x;
            int first = pager != null ? Math.Max(0, Math.Min(pager.FilterFirst, labels.Length - 1)) : 0;
            int fit = Fit(widths, first, room);
            bool all = pager == null || (first == 0 && fit == labels.Length);
            if (!all)
            {
                float arrow = UiTokens.Chip;
                room = right - x - 2f * (arrow + UiTokens.S2);
                if (chosen >= 0 && chosen < first)
                {
                    first = chosen;
                }
                fit = Fit(widths, first, room);
                while (chosen >= first + fit && first < labels.Length - 1)
                {
                    first++;
                    fit = Fit(widths, first, room);
                }
                pager.FilterFirst = first;
                float cy = y;
                UiDraw.IconButton(s, at, x, cy, arrow, UiIcon.Back, UiRole.Text, VFilterPage, -1);
                float cx = x + arrow + UiTokens.S2;
                for (int i = first; i < first + fit; i++)
                {
                    ChoiceAt(s, at, cx, cy, widths[i], h, labels[i], i == chosen, command, i);
                    cx += widths[i] + UiTokens.S2;
                }
                UiDraw.IconButton(s, at, right - arrow, cy, arrow, UiIcon.Next, UiRole.Text, VFilterPage, 1);
                return;
            }
            float ax = x;
            float ay = y;
            for (int i = 0; i < labels.Length; i++)
            {
                ChoiceAt(s, at, ax, ay, widths[i], h, labels[i], i == chosen, command, i);
                ax += widths[i] + UiTokens.S2;
            }
        }

        // How many chips from <first> fit a room.
        private static int Fit(float[] widths, int first, float room)
        {
            float used = 0f;
            int n = 0;
            for (int i = first; i < widths.Length; i++)
            {
                float next = used + (n > 0 ? UiTokens.S2 : 0f) + widths[i];
                if (next > room && n > 0)
                {
                    break;
                }
                used = next;
                n++;
            }
            return Math.Max(1, n);
        }
    }
}
