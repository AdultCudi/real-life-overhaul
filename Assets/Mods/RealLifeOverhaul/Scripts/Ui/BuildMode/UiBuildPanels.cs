using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Build mode's panels on the kit, after Cities: Skylines II's build tools and the approved mockup (construction/build_mode): the views as an icon rail and a panel
    /// at the left with the view's legend and what the district wants (meters), the tool panel at the right with what is picked and the one or two things to press, the
    /// hint line at the top centre, "Choose a building" over the tool row with its category tabs, favourites, search, the designs as cards and the chosen one's detail,
    /// and the tour's tip. The tool row stands on the status bar (UiToolRow). Draws only; every press comes back as a command (BuildFace gives it to BuildMode's own
    /// calls). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiBuildPanels
    {
        public const int ViewCommand = 7700, RowCommand = 7701, LeaveCommand = 7702, TabCommand = 7703, SearchCommand = 7704, FavouritesCommand = 7705, CardCommand = 7706,
            StarCommand = 7707, PlaceCommand = 7708, BackCommand = 7709, TourNextCommand = 7710, TourSkipCommand = 7711;

        /// <summary>Every command the panels answer, for the layout checks.</summary>
        public static readonly int[] Commands =
        {
            UiToolRow.ToolCommand, UiToolRow.ChoiceCommand, ViewCommand, RowCommand, LeaveCommand, TabCommand, SearchCommand, FavouritesCommand, CardCommand, StarCommand,
            PlaceCommand, BackCommand, TourNextCommand, TourSkipCommand
        };

        public const float Rail = 104f;          // px: the views' menu, two icons across, as the city map's
        public const float Cell = 44f;           // px: a view's icon
        public const float PanelWidth = 400f;    // px: the left panel, as an info view's
        public const float SheetWidth = 360f;    // px: the tool panel, as the game's little windows
        public const float CardWidth = 152f;     // px: a design's card
        public const float CardHeight = 164f;
        public const float DetailWidth = 320f;   // px: the chosen design's detail
        private const float PictureHeight = 84f;
        private const float KeyRow = 28f;
        private const float DemandRow = UiTokens.TableRow;
        public const int CardRows = 2;           // rows of cards in view; the wheel moves them
        private static float Lh(UiStyle style) => UiDraw.Snap(UiTextRules.LineHeight(style));

        private const byte RailRegion = 1, PanelRegion = 2, SheetRegion = 3, CatalogueRegion = 4, TourRegion = 5;

        /// <summary>The panels as one scene over the map: pieces, the keys' prompts and the status bar with the tool row on it.</summary>
        public static void Lay(UiScene s, UiBuildModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("build mode", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            LeftBox(m, frame, out float lx, out float ly, out float lw, out float lh);
            Left(s, UiAt.Screen, m, lx, ly);
            float right = frame.Width - UiTokens.S4;
            Leave(s, UiAt.Screen, m, right, keys);
            float sheetTop = UiTokens.S4 + UiTokens.Control + UiTokens.S2;
            if (m.Catalogue == null && (m.LineCount > 0 || !string.IsNullOrEmpty(m.SheetTitle)))
            {
                Sheet(s, UiAt.Screen.InRegion(SheetRegion), m, right - SheetWidth, sheetTop, keys);
            }
            Hint(s, UiAt.Screen, m, frame, lx + lw + UiTokens.S4, right - SheetWidth - UiTokens.S4);
            if (m.Catalogue != null)
            {
                CatalogueBox(m.Catalogue, frame, lx + lw, out float cx, out float cy, out float cw, out float ch);
                Catalogue(s, UiAt.Screen.InRegion(CatalogueRegion), m.Catalogue, cx, cy, cw, ch, keys);
            }
            if (m.Tour != null)
            {
                Tour(s, UiAt.Screen.InRegion(TourRegion), m, frame, lx, ly, lw, lh, right - SheetWidth);
            }
            UiDraw.PromptBar(s, frame, keys);
            if (m.Tools != null)
            {
                UiToolRow.Draw(s, UiAt.Screen, m.Tools, frame);   // on the bar, after it
            }
        }

        // ------------------------------------------------------------------ the left panel: views, legend, demand

        /// <summary>The left panel with its rail as a box of the layout's px: what the HUD's pieces and the objectives keep clear of.</summary>
        public static void LeftBox(UiBuildModel m, UiFrame frame, out float x, out float y, out float w, out float h)
        {
            x = UiTokens.S2;
            y = UiTokens.S4;
            w = Rail + UiTokens.S2 + PanelWidth + UiTokens.S4;
            h = Math.Max(LeftHeight(m), RailHeight(m));
        }

        private static float RailHeight(UiBuildModel m)
        {
            int rows = (m.ViewCount + 1) / 2;
            return m.ViewCount == 0 ? 0f : rows * Cell + (rows - 1) * UiTokens.S1 + UiTokens.S2 * 2f;
        }

        private static float TipHeight(UiBuildModel m) =>
            string.IsNullOrEmpty(m.ViewTip) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.ViewTip, UiStyle.Small, PanelWidth - UiTokens.S4 * 2f, 2));

        private static float LeftHeight(UiBuildModel m)
        {
            float h = UiFrame.Strip + UiTokens.S3 + UiTokens.PairRow + TipHeight(m) + UiTokens.S3;
            if (m.KeyCount > 0)
            {
                h += m.KeyCount * KeyRow + UiTokens.S2 + (string.IsNullOrEmpty(m.LegendMore) ? 0f : UiTokens.PairRow);
            }
            if (m.DemandCount > 0)
            {
                h += UiTokens.S3 + UiTokens.S2 + UiTokens.PairRow + m.DemandCount * DemandRow + UiTokens.S2;
            }
            return UiDraw.Snap(h);
        }

        private static void Left(UiScene s, UiAt at, UiBuildModel m, float x, float y)
        {
            if (m.ViewCount > 0)
            {
                Views(s, at.InRegion(RailRegion), m, x, y);
            }
            float px = x + Rail + UiTokens.S2, h = LeftHeight(m);
            UiAt column = at.InRegion(PanelRegion);
            int panel = UiDraw.Box(s, column, px, y, PanelWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = column.In(panel);
            float cy = UiDraw.TitleStrip(s, on, px, y, PanelWidth, UiIcon.Building, m.Title, false);
            if (!string.IsNullOrEmpty(m.District))
            {
                float dw = Math.Min(UiTextRules.Width(m.District, UiStyle.Small) + UiTokens.S1, PanelWidth * 0.3f);
                UiDraw.TextMiddle(s, on, px + PanelWidth - UiTokens.S3 - dw, y, UiFrame.Strip, dw, UiStyle.Small, UiRole.Text2, m.District, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
            float ix = px + UiTokens.S4, iw = PanelWidth - UiTokens.S4 * 2f;
            cy += UiTokens.S3;
            UiDraw.TextMiddle(s, on, ix, cy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.ViewNames[Math.Max(0, Math.Min(m.ViewCount - 1, m.View))], UiFlags.Truncate);
            cy += UiTokens.PairRow;
            if (TipHeight(m) > 0f)
            {
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.Small, UiRole.Text2, m.ViewTip, UiFlags.Wrap | UiFlags.Truncate, 2);
                cy += TipHeight(m);
            }
            cy += UiTokens.S3;
            for (int i = 0; i < m.KeyCount; i++)
            {
                Key(s, on, ref m.Keys[i], ix, cy, iw);
                cy += KeyRow;
            }
            if (m.KeyCount > 0)
            {
                cy += UiTokens.S2;
                if (!string.IsNullOrEmpty(m.LegendMore))
                {
                    UiDraw.TextMiddle(s, on, ix, cy, UiTokens.PairRow, iw, UiStyle.Small, UiRole.Muted, m.LegendMore, UiFlags.Truncate);
                    cy += UiTokens.PairRow;
                }
            }
            if (m.DemandCount > 0)
            {
                cy += UiTokens.S3;
                float bh = UiTokens.S2 + UiTokens.PairRow + m.DemandCount * DemandRow + UiTokens.S2;
                UiDraw.Band(s, on, px, cy, PanelWidth, bh);
                float ry = cy + UiTokens.S2;
                UiDraw.TextMiddle(s, on, ix, ry, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.DemandTitle, UiFlags.Label | UiFlags.Truncate);
                ry += UiTokens.PairRow;
                for (int i = 0; i < m.DemandCount; i++)
                {
                    Demand(s, on, ref m.Demand[i], ix, ry, iw);
                    ry += DemandRow;
                }
            }
        }

        // The views as icons two across beside the panel, the shown one filled cyan, as an info view's menu.
        private static void Views(UiScene s, UiAt at, UiBuildModel m, float x, float y)
        {
            float h = RailHeight(m);
            int menu = UiDraw.Box(s, at, x, y, Rail, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(menu);
            for (int i = 0; i < m.ViewCount; i++)
            {
                float cx = x + UiTokens.S2 + (i % 2) * (Cell + UiTokens.S1), cy = y + UiTokens.S2 + (i / 2) * (Cell + UiTokens.S1);
                int node = UiDraw.IconButton(s, on, cx, cy, Cell, m.ViewIcons[i], UiRole.None, ViewCommand, i, i == m.View);
                if (i == m.View)
                {
                    s.FocusFirst = node;
                }
            }
        }

        // One colour of the legend: a square of the fill, or a bar of the edge's colour for an edge alone, and its words.
        private static void Key(UiScene s, UiAt at, ref UiBuildKey k, float x, float y, float w)
        {
            const float swatch = 16f;
            if (k.FillRgb >= 0)
            {
                Swatch(s, at, UiDraw.SwatchKey(k.FillRgb), x, y + (KeyRow - swatch) / 2f, swatch, swatch);
            }
            else
            {
                Swatch(s, at, UiDraw.SwatchKey(k.EdgeRgb), x, y + (KeyRow - UiTokens.S1) / 2f, swatch, UiTokens.S1);
            }
            UiDraw.TextMiddle(s, at, x + swatch + UiTokens.S3, y, KeyRow, w - swatch - UiTokens.S3, UiStyle.Body, UiRole.Text, k.Words, UiFlags.Truncate);
        }

        // A use's demand: its name, a meter in its tone, the figure and the words.
        private static void Demand(UiScene s, UiAt at, ref UiBuildDemand d, float x, float y, float w)
        {
            const float nameW = 84f, figureW = 28f;
            UiDraw.TextMiddle(s, at, x, y, DemandRow, nameW, UiStyle.Body, UiRole.Text, d.Name, UiFlags.Truncate);
            float mx = x + nameW + UiTokens.S2, mw = 112f;
            UiDraw.Meter(s, at, mx, y + (DemandRow - UiTokens.Segment) / 2f, mw, UiTokens.Segment, Math.Max(0, d.Value), d.Tone);
            float fx = mx + mw + UiTokens.S2;
            UiDraw.TextMiddle(s, at, fx, y, DemandRow, figureW, UiStyle.BodyStrong, d.Tone == UiRole.None ? UiRole.Text : ToneInk(d.Tone), d.Value >= 0 ? d.Value.ToString() : "-", UiFlags.Right);
            float wx = fx + figureW + UiTokens.S2;
            UiDraw.TextMiddle(s, at, wx, y, DemandRow, x + w - wx, UiStyle.Small, UiRole.Text2, d.Words, UiFlags.Truncate);
        }

        private static UiRole ToneInk(UiRole fill)
        {
            switch (fill)
            {
                case UiRole.Ok: return UiRole.OkText;
                case UiRole.Warn: return UiRole.WarnText;
                case UiRole.Danger: return UiRole.DangerText;
                default: return UiRole.AccentText;
            }
        }

        // A square of one colour of the game's own swatch pictures (UiPictures), at its size.
        private static void Swatch(UiScene s, UiAt at, string key, float x, float y, float w, float h)
        {
            var item = new UiItem
            {
                Kind = UiKind.Picture, Text = key, Value = h > 0f ? w / h : 1f, X = x, Y = y, W = w, H = h, Flags = UiFlags.Share | UiFlags.Middle, Clip = at.Clip, Node = -1,
                Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref item);
        }

        // ------------------------------------------------------------------ the hint line and Leave

        private static void Hint(UiScene s, UiAt at, UiBuildModel m, UiFrame frame, float left, float right)
        {
            if (string.IsNullOrEmpty(m.Hint))
            {
                return;
            }
            float room = Math.Max(0f, right - left);
            float lineH = Lh(UiStyle.BodyStrong), moreH = Lh(UiStyle.Small);
            bool more = !string.IsNullOrEmpty(m.HintMore);
            float circle = m.Step > 0 ? UiTokens.S6 + UiTokens.S3 : 0f;
            float wide = Math.Min(room, Math.Max(UiTextRules.Width(m.Hint, UiStyle.BodyStrong), more ? UiTextRules.Width(m.HintMore, UiStyle.Small) : 0f) + circle + UiTokens.S4 * 2f + UiTokens.S1);
            wide = UiDraw.Snap(Math.Max(wide, Math.Min(room, 360f)));
            float h = UiDraw.Snap(UiTokens.S3 * 2f + lineH + (more ? moreH + UiTokens.S1 : 0f));
            float x = UiDraw.SnapDown(left + (room - wide) / 2f), y = UiTokens.S4;
            int box = UiDraw.Box(s, at, x, y, wide, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(box);
            float tx = x + UiTokens.S4;
            if (m.Step > 0)
            {
                int dot = UiDraw.Circle(s, on, tx, y + (h - UiTokens.S6) / 2f, UiTokens.S6, UiTokens.S6, UiRole.Accent, UiFlags.Middle);
                UiDraw.TextMiddle(s, on.In(dot), tx, y + (h - UiTokens.S6) / 2f, UiTokens.S6, UiTokens.S6, UiStyle.Badge, UiRole.OnAccent, m.Step.ToString(), UiFlags.Centre);
                tx += circle;
            }
            float tw = x + wide - UiTokens.S4 - tx;
            UiDraw.Text(s, on, tx, y + UiTokens.S3, tw, UiStyle.BodyStrong, UiRole.Text, m.Hint, UiFlags.Truncate);
            if (more)
            {
                UiDraw.Text(s, on, tx, y + UiTokens.S3 + lineH + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, m.HintMore, UiFlags.Truncate);
            }
        }

        /// <summary>The hint line's box, for the tour to point at: false while there is no hint.</summary>
        public static bool HintBox(UiBuildModel m, UiFrame frame, float left, float right, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            if (string.IsNullOrEmpty(m.Hint))
            {
                return false;
            }
            float room = Math.Max(0f, right - left);
            float lineH = Lh(UiStyle.BodyStrong), moreH = Lh(UiStyle.Small);
            bool more = !string.IsNullOrEmpty(m.HintMore);
            float circle = m.Step > 0 ? UiTokens.S6 + UiTokens.S3 : 0f;
            w = Math.Min(room, Math.Max(UiTextRules.Width(m.Hint, UiStyle.BodyStrong), more ? UiTextRules.Width(m.HintMore, UiStyle.Small) : 0f) + circle + UiTokens.S4 * 2f + UiTokens.S1);
            w = UiDraw.Snap(Math.Max(w, Math.Min(room, 360f)));
            h = UiDraw.Snap(UiTokens.S3 * 2f + lineH + (more ? moreH + UiTokens.S1 : 0f));
            x = UiDraw.SnapDown(left + (room - w) / 2f);
            y = UiTokens.S4;
            return true;
        }

        private static void Leave(UiScene s, UiAt at, UiBuildModel m, float right, IUiKeys keys)
        {
            string label = m.LeaveLabel ?? string.Empty;
            float w = Math.Max(UiDraw.ButtonWidth(UiButtonKind.Secondary, label, UiIcon.Close, UiAction.Count, keys), UiTokens.Control * 2f);
            UiDraw.Button(s, at, right - w, UiTokens.S4, w, UiTokens.Control, UiButtonKind.Secondary, label, UiIcon.Close, UiAction.Count, keys, LeaveCommand, 0);
        }

        // ------------------------------------------------------------------ the tool panel

        /// <summary>The tool panel's height for its lines.</summary>
        public static float SheetHeight(UiBuildModel m)
        {
            float h = UiFrame.Strip + UiTokens.S3;
            for (int i = 0; i < m.LineCount && i < m.Lines.Length; i++)
            {
                h += LineHeight(ref m.Lines[i], SheetWidth - UiTokens.S4 * 2f) + UiTokens.S2;
            }
            return UiDraw.Snap(h + UiTokens.S2);
        }

        private static float LineHeight(ref UiBuildLine l, float iw)
        {
            switch (l.Kind)
            {
                case UiBuildLineKind.Text: return UiDraw.Snap(UiTextRules.WrappedHeight(l.Label, UiStyle.Body, iw, 4));
                case UiBuildLineKind.Fact: return UiTokens.PairRow;
                case UiBuildLineKind.Site: return UiDraw.Snap(UiTokens.S2 * 2f + Lh(UiStyle.BodyStrong) + Lh(UiStyle.Small));
                case UiBuildLineKind.Choices:
                    int n = l.Names == null ? 0 : l.Names.Length, rows = Math.Max(1, (n + 1) / 2);
                    return rows * UiTokens.ControlSmall + (rows - 1) * UiTokens.S2;
                default: return UiTokens.Control;
            }
        }

        private static void Sheet(UiScene s, UiAt at, UiBuildModel m, float x, float y, IUiKeys keys)
        {
            float h = SheetHeight(m);
            int panel = UiDraw.Box(s, at, x, y, SheetWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, SheetWidth, m.SheetIcon, m.SheetTitle, false);
            if (!string.IsNullOrEmpty(m.SheetCount))
            {
                float cw = Math.Min(UiTextRules.Width(m.SheetCount, UiStyle.Small) + UiTokens.S1, SheetWidth * 0.3f);
                UiDraw.TextMiddle(s, on, x + SheetWidth - UiTokens.S3 - cw, y, UiFrame.Strip, cw, UiStyle.Small, UiRole.Text2, m.SheetCount, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
            float ix = x + UiTokens.S4, iw = SheetWidth - UiTokens.S4 * 2f;
            cy += UiTokens.S3;
            bool primary = false;
            for (int i = 0; i < m.LineCount && i < m.Lines.Length; i++)
            {
                ref UiBuildLine l = ref m.Lines[i];
                switch (l.Kind)
                {
                    case UiBuildLineKind.Text:
                        UiDraw.Text(s, on, ix, cy, iw, UiStyle.Body, l.Tone, l.Label, UiFlags.Wrap | UiFlags.Truncate, 4);
                        break;
                    case UiBuildLineKind.Fact:
                        UiDraw.InfoRow(s, on, ix, cy, iw, l.Label, l.Value, false, -1, 0, l.Tone);
                        break;
                    case UiBuildLineKind.Zone:
                        Zone(s, on, ref l, ix, cy, iw);
                        break;
                    case UiBuildLineKind.Buttons:
                        Buttons(s, on, ref l, ix, cy, iw, keys, ref primary);
                        break;
                    case UiBuildLineKind.Choices:
                        Choices(s, on, ref l, ix, cy, iw, keys);
                        break;
                    default:
                        Site(s, on, ref l, ix, cy, iw);
                        break;
                }
                cy += LineHeight(ref l, iw) + UiTokens.S2;
            }
        }

        // A zone's row: its colour, its name and where it stands (allowed, asks City Hall), the chosen one on the cyan fifth.
        private static void Zone(UiScene s, UiAt at, ref UiBuildLine l, float x, float y, float w)
        {
            int face = UiDraw.Box(s, at, x, y, w, UiTokens.Control, l.On ? UiRole.RowSel : UiRole.Row, UiRole.None, UiTokens.RadiusLarge, l.On ? UiFlags.Selected : UiFlags.None);
            UiAt on = at.In(face);
            float sx = x + UiTokens.S3, size = UiTokens.S6;
            if (l.Rgb >= 0)
            {
                Swatch(s, on, UiDraw.SwatchKey(l.Rgb), sx, y + (UiTokens.Control - size) / 2f, size, size);
            }
            else
            {
                UiDraw.Icon(s, on, sx, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Close, UiRole.Text2, UiFlags.Middle);
            }
            float nx = sx + size + UiTokens.S3, stateW = string.IsNullOrEmpty(l.Value) ? 0f : Math.Min(UiTextRules.Width(l.Value, UiStyle.Small) + UiTokens.S1, w * 0.42f);
            UiDraw.TextMiddle(s, on, nx, y, UiTokens.Control, x + w - nx - stateW - UiTokens.S3 * 2f, UiStyle.Body, UiRole.Text, l.Label, UiFlags.Truncate);
            if (stateW > 0f)
            {
                UiDraw.TextMiddle(s, on, x + w - UiTokens.S3 - stateW, y, UiTokens.Control, stateW, UiStyle.Small, l.Tone, l.Value, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.Node(s, at, x, y, w, UiTokens.Control, RowCommand, l.Id, face, !l.Usable);
        }

        // The panel's buttons across: the main one on the cyan where nothing else on the panel is, a second beside it.
        private static void Buttons(UiScene s, UiAt at, ref UiBuildLine l, float x, float y, float w, IUiKeys keys, ref bool primary)
        {
            bool main = !string.IsNullOrEmpty(l.Label), side = !string.IsNullOrEmpty(l.Value);
            float half = main && side ? UiDraw.SnapDown((w - UiTokens.S2) / 2f) : w;
            float sx = main && side ? x + half + UiTokens.S2 : x;
            if (main)
            {
                UiButtonKind kind = !primary && l.Usable ? UiButtonKind.Primary : UiButtonKind.Secondary;
                primary |= kind == UiButtonKind.Primary;
                UiDraw.Button(s, at, x, y, half, UiTokens.Control, kind, l.Label, UiIcon.None, UiAction.Count, keys, RowCommand, l.Id, !l.Usable);
            }
            if (side)
            {
                UiDraw.Button(s, at, sx, y, main ? w - half - UiTokens.S2 : w, UiTokens.Control, UiButtonKind.Secondary, l.Value, UiIcon.None, UiAction.Count, keys, RowCommand, l.Id2, !l.Usable2);
            }
        }

        // A row of choices, two across, the chosen one on the cyan.
        private static void Choices(UiScene s, UiAt at, ref UiBuildLine l, float x, float y, float w, IUiKeys keys)
        {
            int n = l.Names == null ? 0 : l.Names.Length;
            float half = UiDraw.SnapDown((w - UiTokens.S2) / 2f);
            for (int i = 0; i < n; i++)
            {
                float bx = x + (i % 2) * (half + UiTokens.S2), by = y + (i / 2) * (UiTokens.ControlSmall + UiTokens.S2);
                UiDraw.Button(s, at, bx, by, i == n - 1 && n % 2 == 1 ? w : half, UiTokens.ControlSmall, i == l.Chosen ? UiButtonKind.Chosen : UiButtonKind.Secondary, l.Names[i], UiIcon.None,
                    UiAction.Count, keys, RowCommand, l.Id + i, !l.Usable);
            }
        }

        // A plot or lot of "You could build here": its name, what fits it, and a locate glyph; pressed it takes the map's camera there.
        private static void Site(UiScene s, UiAt at, ref UiBuildLine l, float x, float y, float w)
        {
            float h = LineHeight(ref l, w);
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(face);
            float tw = w - UiTokens.S3 * 2f - UiTokens.Icon - UiTokens.S2;
            UiDraw.Text(s, on, x + UiTokens.S3, y + UiTokens.S2, tw, UiStyle.BodyStrong, l.On ? UiRole.AccentText : UiRole.Text, l.Label, UiFlags.Truncate);
            UiDraw.Text(s, on, x + UiTokens.S3, y + UiTokens.S2 + Lh(UiStyle.BodyStrong), tw, UiStyle.Small, UiRole.Text2, l.Value, UiFlags.Truncate);
            UiDraw.Icon(s, on, x + w - UiTokens.S3 - UiTokens.Icon, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Locate, UiRole.AccentText, UiFlags.Middle);
            UiDraw.Node(s, at, x, y, w, h, RowCommand, l.Id, face, !l.Usable);
        }

        // ------------------------------------------------------------------ "Choose a building"

        /// <summary>The design window's box: over the tool row, to the right of the left panel (<paramref name="leftRight"/> is where that ends).</summary>
        public static void CatalogueBox(UiBuildCatalogueModel c, UiFrame frame, float leftRight, out float x, out float y, out float w, out float h)
        {
            x = UiDraw.SnapDown(leftRight + UiTokens.S2);
            w = UiDraw.SnapDown(Math.Min(1180f, frame.Width - x - UiTokens.S4));
            h = UiDraw.Snap(UiTokens.S3 * 2f + UiTokens.Control + UiTokens.S2 + CardRows * CardHeight + (CardRows - 1) * UiTokens.S2 + UiTokens.S3);
            y = UiDraw.SnapDown(frame.BandY - UiTokens.S3 - h);
        }

        private static int Columns(float gridWidth) => Math.Max(1, (int)((gridWidth - UiTokens.S3 * 2f + UiTokens.S2) / (CardWidth + UiTokens.S2)));

        /// <summary>How many cards the search and the favourites switch leave, for the paging and the tests.</summary>
        public static int Shown(UiBuildCatalogueModel c)
        {
            int n = 0;
            for (int i = 0; i < c.CardCount; i++)
            {
                if (Passes(c, ref c.Cards[i]))
                {
                    n++;
                }
            }
            return n;
        }

        private static bool Passes(UiBuildCatalogueModel c, ref UiBuildCard card)
        {
            if (c.FavouritesOnly && !card.Favourite)
            {
                return false;
            }
            return string.IsNullOrWhiteSpace(c.Search) || Matches(card.Name, c.Search);
        }

        private static bool Matches(string name, string query)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            foreach (string word in query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>How many rows of cards fit a catalogue wide as given and how many columns each holds.</summary>
        public static void Grid(float width, out int columns, out int rows)
        {
            columns = Columns(width - DetailWidth - UiTokens.S2);
            rows = CardRows;
        }

        private static void Catalogue(UiScene s, UiAt at, UiBuildCatalogueModel c, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float hx = x + UiTokens.S4, hy = y + UiTokens.S3;
            // The tabs: a use each with its count, then the favourites' switch, the search at the right.
            for (int t = 0; t < c.Tabs.Length; t++)
            {
                string label = c.Tabs[t] ?? string.Empty;
                float tw = UiDraw.ButtonWidth(UiButtonKind.Chip, label, UiIcon.None, UiAction.Count, keys);
                UiDraw.Button(s, on, hx, hy, tw, UiTokens.Control, t == c.Tab ? UiButtonKind.Chosen : UiButtonKind.Chip, label, UiIcon.None, UiAction.Count, keys, TabCommand, t);
                hx += tw + UiTokens.S2;
            }
            float searchW = 240f, sx = x + w - UiTokens.S4 - searchW;
            hx = UiDraw.Snap(hx + UiTokens.S2);
            float favW = UiDraw.SnapDown(Math.Min(UiDraw.Snap(UiTextRules.Width(c.FavouritesLabel, UiStyle.Body) + UiTokens.S1) + 80f, Math.Max(0f, sx - hx - UiTokens.S3)));
            UiDraw.Switch(s, on, hx, hy, favW, c.FavouritesOnly, c.FavouritesLabel, FavouritesCommand, 0, false);
            UiDraw.Search(s, on, sx, hy, searchW, c.SearchPlaceholder, c.Search, null, SearchCommand);
            float by = hy + UiTokens.Control + UiTokens.S2;
            UiDraw.Band(s, on, x, by - UiTokens.S1, w, UiTokens.Hairline);
            float gridW = w - DetailWidth - UiTokens.S2;
            int cols = Columns(gridW), shown = Shown(c);
            int rowsAll = (shown + cols - 1) / cols;
            float gx = x + UiTokens.S3, step = CardHeight + UiTokens.S2, viewH = CardRows * step - UiTokens.S2;
            if (shown == 0)
            {
                c.Scroll = 0f;
                UiDraw.Text(s, on, gx, by + UiTokens.S4, gridW - UiTokens.S3 * 2f, UiStyle.Body, UiRole.Text2, c.NoneLabel, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
            else
            {
                float content = rowsAll * step - UiTokens.S2;
                float offset = Math.Max(0f, Math.Min((float)Math.Round(c.Scroll / step) * step, Math.Max(0f, (float)Math.Ceiling((content - viewH) / step) * step)));
                c.Scroll = offset;
                short clip = s.AddClip(gx, by, gridW - UiTokens.S3, Math.Min(viewH, content), offset);
                s.Clips[clip].ContentHeight = content;
                var inClip = new UiAt { Parent = -1, Clip = clip, Layer = on.Layer, Order = -1, Region = on.Region };
                int first = (int)(offset / step), seen = 0, last = Math.Min(rowsAll - 1, first + CardRows - 1);
                short list = s.AddList(rowsAll, first, last - first + 1, clip, -1);   // a card's row is the list's row: focus beyond those in view scrolls them in (Reveal)
                for (int i = 0; i < c.CardCount; i++)
                {
                    if (!Passes(c, ref c.Cards[i]))
                    {
                        continue;
                    }
                    int row = seen / cols, col = seen % cols;
                    seen++;
                    if (row < first || row > last)
                    {
                        continue;
                    }
                    Card(s, inClip, c, ref c.Cards[i], col * (CardWidth + UiTokens.S2), row * step, list, row);
                }
                if (content > viewH + 0.5f)
                {
                    UiDraw.ScrollThumb(s, on, gx + gridW - UiTokens.S3 - UiTokens.S1, by, Math.Min(viewH, content), offset, content);
                }
            }
            Detail(s, on, c, x + w - DetailWidth, by - UiTokens.S1, DetailWidth, y + h - (by - UiTokens.S1), keys);
        }

        // A design: its picture on a tile with the WANTED badge and the star over it, its name, its size and price, or why it cannot go there.
        private static void Card(UiScene s, UiAt at, UiBuildCatalogueModel c, ref UiBuildCard d, float x, float y, short list, int row)
        {
            int face = UiDraw.Box(s, at, x, y, CardWidth, CardHeight, d.Chosen ? UiRole.RowSel : UiRole.Row, d.Chosen ? UiRole.Accent : UiRole.None, UiTokens.RadiusLarge,
                d.Chosen ? UiFlags.Selected : UiFlags.None);
            UiAt on = at.In(face);
            int drawn = s.ItemCount;
            int tile = UiDraw.Box(s, on, x + UiTokens.S1, y + UiTokens.S1, CardWidth - UiTokens.S1 * 2f, PictureHeight, UiRole.Tile, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
            UiAt over = on.In(tile);
            float side = PictureHeight - UiTokens.S4;
            if (!string.IsNullOrEmpty(d.Picture))
            {
                Picture(s, over, d.Picture, x + (CardWidth - side) / 2f, y + UiTokens.S1 + UiTokens.S2, side);
            }
            float star = UiTokens.Icon;
            if (d.Wanted)
            {
                UiDraw.Chip(s, over, x + UiTokens.S2, y + UiTokens.S2, c.WantedLabel, UiRole.Warn, CardWidth - UiTokens.S2 * 3f - star - UiTokens.S2);
            }
            UiDraw.Icon(s, over, x + CardWidth - UiTokens.S2 - star, y + UiTokens.S2, star, UiIcon.Star, d.Favourite ? UiRole.Gold : UiRole.Muted, UiFlags.Decor, d.Favourite);
            float ty = y + UiTokens.S1 + PictureHeight + UiTokens.S1;
            float tw = CardWidth - UiTokens.S2 * 2f;
            UiDraw.Text(s, on, x + UiTokens.S2, ty, tw, UiStyle.BodyStrong, UiRole.Text, d.Name, UiFlags.Truncate);
            ty += Lh(UiStyle.BodyStrong);
            if (d.Usable)
            {
                UiDraw.Text(s, on, x + UiTokens.S2, ty, tw, UiStyle.Small, UiRole.Text, d.Size, UiFlags.Truncate);
                ty += Lh(UiStyle.Small);
                UiDraw.Text(s, on, x + UiTokens.S2, ty, tw, UiStyle.BodyStrong, UiRole.AccentText, d.Cost, UiFlags.Truncate);
            }
            else
            {
                UiDraw.Text(s, on, x + UiTokens.S2, ty, tw, UiStyle.Small, UiRole.WarnText, d.WhyNot ?? d.Kind, UiFlags.Wrap | UiFlags.Truncate, 2);
            }
            int node = UiDraw.Node(s, at, x, y, CardWidth, CardHeight, CardCommand, d.Index, face, !d.Usable, list, row);
            if (!d.Usable)
            {
                UiDraw.Dim(s, drawn, node);
            }
        }

        private static void Picture(UiScene s, UiAt at, string key, float x, float y, float size)
        {
            var item = new UiItem
            {
                Kind = UiKind.Picture, Text = key, Value = 1f, X = x, Y = y, W = size, H = size, Flags = UiFlags.Share | UiFlags.Middle | UiFlags.Decor, Clip = at.Clip, Node = -1, Layer = at.Layer,
                Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref item);
        }

        // The chosen design in words: its name and kind, then its figures, what it lets for, the council's word and the district's demand for its use, and the buttons.
        private static void Detail(UiScene s, UiAt at, UiBuildCatalogueModel c, float x, float y, float w, float h, IUiKeys keys)
        {
            UiDraw.Band(s, at, x, y, UiTokens.Hairline, h);
            int chosen = -1;
            for (int i = 0; i < c.CardCount; i++)
            {
                if (c.Cards[i].Chosen)
                {
                    chosen = i;
                }
            }
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, cy = y + UiTokens.S3;
            float buttonsH = UiTokens.ControlSmall * 3f + UiTokens.S2 * 2f;
            float by = y + h - UiTokens.S3 - buttonsH;
            if (chosen < 0)
            {
                UiDraw.Text(s, at, ix, cy, iw, UiStyle.Body, UiRole.Text2, c.SummaryLine, UiFlags.Wrap | UiFlags.Truncate, 4);
                DetailButtons(s, at, c, -1, ix, by, iw, keys);
                return;
            }
            ref UiBuildCard d = ref c.Cards[chosen];
            UiDraw.Text(s, at, ix, cy, iw, UiStyle.Title, UiRole.Text, d.Name, UiFlags.Truncate);
            cy += Lh(UiStyle.Title);
            UiDraw.Text(s, at, ix, cy, iw, UiStyle.Small, UiRole.Text2, d.Kind, UiFlags.Truncate);
            cy += Lh(UiStyle.Small) + UiTokens.S2;
            cy += UiDraw.InfoRow(s, at, ix, cy, iw, c.PriceLabel, d.Cost, false, -1, 0, UiRole.AccentText);
            cy += UiDraw.InfoRow(s, at, ix, cy, iw, c.SizeLabel, d.Width + " x " + d.Depth + " m", false, -1, 0, UiRole.Text);
            cy += UiDraw.InfoRow(s, at, ix, cy, iw, c.BuildTimeLabel, d.Weeks + " " + c.WeeksWords, false, -1, 0, UiRole.Text);
            if (!string.IsNullOrEmpty(d.Rent))
            {
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, c.RentLabel, d.Rent, false, -1, 0, UiRole.OkText);
            }
            if (!string.IsNullOrEmpty(d.Planning))
            {
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, c.PlanningLabel, d.Planning, false, -1, 0, d.PlanningOk ? UiRole.OkText : UiRole.WarnText);
            }
            if (d.Demand >= 0)
            {
                float figW = 32f, lw = UiDraw.Snap(Math.Min(UiTextRules.Width(c.DemandLabel, UiStyle.Body) + UiTokens.S1, iw * 0.4f));
                UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, lw, UiStyle.Body, UiRole.Text2, c.DemandLabel, UiFlags.Truncate);
                float mx = ix + lw + UiTokens.S2, mw = UiDraw.SnapDown(iw - lw - UiTokens.S2 - figW - UiTokens.S2);
                UiDraw.Meter(s, at, mx, cy + (UiTokens.PairRow - UiTokens.Segment) / 2f, mw, UiTokens.Segment, d.Demand, d.Wanted ? UiRole.Ok : UiRole.Accent);
                UiDraw.TextMiddle(s, at, ix + iw - figW, cy, UiTokens.PairRow, figW, UiStyle.BodyStrong, d.Wanted ? UiRole.OkText : UiRole.AccentText, d.Demand.ToString(), UiFlags.Right);
            }
            DetailButtons(s, at, c, d.Index, ix, by, iw, keys, d.Favourite);
        }

        // Under the figures, one above the other: the star for the chosen design, Place it on the map (the cyan while it can be), Back to the map.
        private static void DetailButtons(UiScene s, UiAt at, UiBuildCatalogueModel c, int starIndex, float x, float y, float w, IUiKeys keys, bool starred = false)
        {
            UiDraw.Button(s, at, x, y, w, UiTokens.ControlSmall, starred ? UiButtonKind.Chosen : UiButtonKind.Secondary, c.FavouriteLabel, UiIcon.Star, UiAction.Count, keys, StarCommand,
                Math.Max(0, starIndex), starIndex < 0);
            y += UiTokens.ControlSmall + UiTokens.S2;
            UiDraw.Button(s, at, x, y, w, UiTokens.ControlSmall, c.PlaceUsable ? UiButtonKind.Primary : UiButtonKind.Secondary, c.PlaceLabel, UiIcon.None, UiAction.Alt, keys,
                PlaceCommand, 0, !c.PlaceUsable);
            y += UiTokens.ControlSmall + UiTokens.S2;
            UiDraw.Button(s, at, x, y, w, UiTokens.ControlSmall, UiButtonKind.Secondary, c.BackLabel, UiIcon.None, UiAction.Back, keys, BackCommand, 0);
        }

        // ------------------------------------------------------------------ the tour

        // One tip beside the part it is about: the part outlined in the cyan, the card with its dots, Skip and Next.
        private static void Tour(UiScene s, UiAt at, UiBuildModel m, UiFrame frame, float lx, float ly, float lw, float lh, float rightLeft)
        {
            UiBuildTourModel t = m.Tour;
            const float cardW = 400f;
            float tx, ty, tw, th;
            switch (t.Step)
            {
                case 0:
                    if (!HintBox(m, frame, lx + lw + UiTokens.S4, rightLeft - UiTokens.S4, out tx, out ty, out tw, out th))
                    {
                        tx = frame.Width / 2f - 100f;
                        ty = UiTokens.S4;
                        tw = 200f;
                        th = 48f;
                    }
                    break;
                case 1:
                    tw = UiToolRow.Width(m.Tools);
                    tx = UiDraw.SnapDown((frame.Width - tw) / 2f);
                    th = UiDraw.BarTile;
                    ty = UiDraw.BarMiddle(frame) - th / 2f;
                    break;
                case 2:
                    tx = lx;
                    ty = ly;
                    tw = Rail;
                    th = RailHeight(m);
                    break;
                default:
                    tx = lx + Rail + UiTokens.S2;
                    th = UiTokens.S2 + UiTokens.PairRow + m.DemandCount * DemandRow + UiTokens.S2;
                    ty = ly + Math.Max(LeftHeight(m), RailHeight(m)) - th - UiTokens.S2;
                    tw = PanelWidth;
                    break;
            }
            UiDraw.Box(s, at, tx - UiTokens.S1, ty - UiTokens.S1, tw + UiTokens.S2, th + UiTokens.S2, UiRole.None, UiRole.Warn, UiTokens.RadiusPanel, UiFlags.Decor);
            float bodyH = UiDraw.Snap(UiTextRules.WrappedHeight(t.Body, UiStyle.Body, cardW - UiTokens.S4 * 2f, 5));
            float cardH = UiDraw.Snap(UiTokens.S4 + Lh(UiStyle.Small) + UiTokens.S1 + Lh(UiStyle.Title) + UiTokens.S2 + bodyH + UiTokens.S3 + UiTokens.Control + UiTokens.S4);
            float cx, cy;
            switch (t.Step)
            {
                case 0:
                    cx = tx + (tw - cardW) / 2f;
                    cy = ty + th + UiTokens.S4;
                    break;
                case 1:
                    cx = tx + (tw - cardW) / 2f;
                    cy = ty - UiTokens.S4 - cardH;
                    break;
                default:
                    cx = tx + tw + UiTokens.S4;
                    cy = ty;
                    break;
            }
            cx = UiDraw.SnapDown(Math.Max(UiTokens.S4, Math.Min(cx, frame.Width - UiTokens.S4 - cardW)));
            cy = UiDraw.SnapDown(Math.Max(UiTokens.S4, Math.Min(cy, frame.BandY - UiTokens.S4 - cardH)));
            int card = UiDraw.Box(s, at, cx, cy, cardW, cardH, UiRole.Panel, UiRole.Warn, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(card);
            float y = cy + UiTokens.S4, ix = cx + UiTokens.S4, iw = cardW - UiTokens.S4 * 2f;
            UiDraw.Text(s, on, ix, y, iw, UiStyle.Small, UiRole.Muted, t.Count, UiFlags.Truncate);
            y += Lh(UiStyle.Small) + UiTokens.S1;
            UiDraw.Text(s, on, ix, y, iw, UiStyle.Title, UiRole.Text, t.Title, UiFlags.Wrap | UiFlags.Truncate, 1);
            y += Lh(UiStyle.Title) + UiTokens.S2;
            UiDraw.Text(s, on, ix, y, iw, UiStyle.Body, UiRole.Text2, t.Body, UiFlags.Wrap | UiFlags.Truncate, 5);
            y += bodyH + UiTokens.S3;
            float half = UiDraw.SnapDown((iw - UiTokens.S2) / 2f);
            UiDraw.Button(s, on, ix, y, half, UiTokens.Control, UiButtonKind.Secondary, t.Skip, UiIcon.None, UiAction.Count, null, TourSkipCommand, 0);
            UiDraw.Button(s, on, ix + half + UiTokens.S2, y, iw - half - UiTokens.S2, UiTokens.Control, UiButtonKind.Primary, t.Next, UiIcon.None, UiAction.Count, null, TourNextCommand, 0);
        }
    }
}
