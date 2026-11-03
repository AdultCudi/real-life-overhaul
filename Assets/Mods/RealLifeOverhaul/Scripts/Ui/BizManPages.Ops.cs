using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The operations of a place, each in the shape its job has: the schedule as Two Point Hospital's rota (the day's line
    /// with its opening and its actions, the board of shifts on an hour ruler under the day's tabs at the page's width, the
    /// opening hours as a sheet of the day's hours, and while an hour is being filled, the game's picker as a sheet at the
    /// right, sorted and searched as the game sorts it); a manager kind as its plans down the left and the chosen plan as a
    /// form in two columns over its goods, a route's stops for logistics as Anno's trade routes; a factory as its machine
    /// groups across the top, the group's machines and the chosen one's recipe beside them; a warehouse as its vehicle
    /// slots, each with its driver, and its stock as the big table with Sell all. Every figure and word is the model's.
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static partial class BizManPages
    {
        public const int OpsRow = 500, OpsChoice = 501, OpsOption = 502, OpsLess = 503, OpsType = 504, OpsMore = 505, OpsSlider = 506, OpsPlan = 507,
            OpsAction = 508, OpsTab = 509, OpsStop = 510, OpsStopRemove = 511, OpsStopChoice = 512, OpsDayOpen = 513, OpsSearch = 514, OpsCancelFill = 515,
            OpsFirstPlan = 516,   // a kind with no plans: its empty page's own button, the plans' last action (Add plan)
            OpsHours = 517,       // the day's opening hours: their sheet opened or closed
            OpsHour = 518,        // an hour of that sheet, opened or closed by the game's own toggle (its argument the hour)
            OpsSheetDone = 519,   // a sheet's Done or Cancel: the hours' sheet closed, or the game's picker
            OpsFilter = 520,      // a table's filter chosen, its argument the choice
            OpsFormTab = 521;     // a half of the chosen plan's form, its argument the half
        public const int People = 600;                // the picker's table, its commands from here as UiDataTable's from its First
        public const int Table = 700;                 // a page's big table (a plan's goods, the machines, the stock)
        public const int Crew = 800;                  // the schedule's people beside the board, its commands from here as UiDataTable's from its First
        public const int OpsStopList = 1000;          // ListOpen for a stop's business choice: from here
        public const int OpsPeopleList = 2000;        // ListOpen for the picker's filter
        private const float SheetW = 560f;            // px: the picker's sheet at the least: a face, a name and three figures, inside its padding
        private const float PickName = 192f;          // px a name in the picker keeps beside its face, in every language
        private const float HourChip = 48f;           // px: an hour of the opening hours' sheet at the least, its figure and its tick
        private const int HourColumns = 6;            // the day in four rows of six hours: the night, the morning, the afternoon, the evening
        private const float PlansW = 360f;            // px: a manager kind's plans down the left
        private const float CrewW = 352f;             // px: the schedule's people beside the board: a face, a name and their hours
        private const float CrewLeastHour = 30f;      // px: an hour of the board at the least beside them; narrower, the column waits for a wider screen
        private const float StopRow = 48f;            // px: a stop of a route, its tile and two lines
        private const float FormLabel = 0.42f;        // the share of a form's column its labels take, at most
        private const float MachineFormShare = 0.34f; // the share of a factory's page its chosen machine's form takes beside the machines
        private const float MachineFormLeast = 320f;  // px: and the least, a label and a control side by side

        [ThreadStatic] private static int[] _kept;    // the board's own sideways links, as target, left, right: the page fence would replace them
        [ThreadStatic] private static int _keptCount;
        [ThreadStatic] private static bool _headChain;   // the firm's head steps down its own lines on this page

        // ------------------------------------------------------------------------------------------------- the schedule

        // The day's line across the page, the board under it at the page's width, then a sheet over them where one is open:
        // the opening hours under their button, or the game's picker at the right.
        private static void SchedulePage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            _headChain = true;
            DayLine(s, at, m, x, y, w, keys);
            float top = Facts(s, at, o, x, y + UiTokens.Control + UiTokens.S5, w, keys);
            bool crew = o.Crew != null && o.Board != null && o.Board.Lanes.Length > 0
                        && (w - CrewW - UiTokens.S8 - UiBoard.LaneHead) / Math.Max(1, o.Board.LastHour - o.Board.FirstHour) >= CrewLeastHour;
            if (crew)
            {
                // The board beside the people, as many of them in view as the page's targets leave room for (the firm's head,
                // laid after the page, counted too); with fewer than a few, the board takes the width and the picker lists them.
                UiMark mark = UiMark.Of(s);
                float bw = w - CrewW - UiTokens.S8;
                Board(s, at, o, x, top, bw, bottom, keys);
                int room = UiCheckRules.MostTargets - PageTargets(s) - HeadTargets(s, at, m, x, y, w, keys);
                crew = room >= CrewLeastRows && CrewColumn(s, at, o, x + bw + UiTokens.S8, top, CrewW, bottom, keys, room);
                if (!crew)
                {
                    mark.Back(s);
                }
            }
            if (!crew)
            {
                Board(s, at, o, x, top, w, bottom, keys);
            }
            float above = m.Head != null ? y - UiFirmHead.Height - UiTokens.S6 : y;   // a sheet dims the firm's head too: it waits under the sheet
            if (o.HoursOpen && o.HoursPress)
            {
                Behind(s, x, above, w, bottom);
                HoursSheet(s, at, o, x, w, bottom, keys);
            }
            else if (o.Picking && o.People != null)
            {
                Behind(s, x, above, w, bottom);
                float sw = PickWidth(o.People, UiDraw.SnapDown(w * 0.55f));
                PickSheet(s, at, o, x + w - sw, y, sw, bottom, keys);
            }
        }

        // The place's people beside the board, as a rota's picker column: their title and what a press does, then each
        // person's face, name, role and wage, and their hours this week as a chip; the one chosen to place in the cyan fifth.
        // A press chooses them; a press on an hour of a lane then gives them that shift through the game's own schedule.
        // Returns false when not even the least of them fit in view (the column is then taken back).
        private static bool CrewColumn(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys, int most)
        {
            float cy = Title(s, at, x, y, w, o.CrewTitle);
            cy = Lead(s, at, o.CrewLine, x, cy, w);
            UiMark mark = UiMark.Of(s);
            int before = PageTargets(s);
            UiDataTable.Lay(s, at, o.Crew, x, cy, w, bottom - cy, keys, Crew);
            int shown = PageTargets(s) - before;
            if (shown > most)
            {
                // Fewer rows in view, the rest a scroll away (the wheel, or the picker's sheet with every one of them).
                mark.Back(s);
                float h = bottom - cy - (shown - most) * UiDataTable.RowHeight(o.Crew);
                before = PageTargets(s);
                UiDataTable.Lay(s, at, o.Crew, x, cy, w, h, keys, Crew);
                shown = PageTargets(s) - before;
            }
            return shown >= Math.Min(CrewLeastRows, o.Crew.RowCount) && shown <= most;
        }

        private const int CrewLeastRows = 3;   // people in view beside the board at the least; fewer, the column is left out

        // The board, its shifts' and lanes' left and right kept for the keys.
        private static void Board(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (o.Board != null)
            {
                int first = s.NodeCount;
                UiBoard.Lay(s, at, o.Board, x, y, w, bottom - y, keys);
                Keep(s, first);
            }
        }

        // The focus targets on the page's own layer so far, the frame's app row not counted (UiCheckRules.Crowded's count).
        private static int PageTargets(UiScene s)
        {
            int n = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Layer == 0 && !UiDraw.IsChrome(s.Nodes[i].Command))
                {
                    n++;
                }
            }
            return n;
        }

        // The targets the firm's head will add when it is laid after the page: laid here, counted and taken back.
        private static int HeadTargets(UiScene s, UiAt at, BizManModel m, float x, float y, float w, IUiKeys keys)
        {
            if (m.Head == null)
            {
                return 0;
            }
            UiMark mark = UiMark.Of(s);
            int before = PageTargets(s);
            UiFirmHead.Lay(s, at, m.Head, x, y - UiFirmHead.Height - UiTokens.S6, w, keys);
            int n = PageTargets(s) - before;
            mark.Back(s);
            return n;
        }

        // The day's facts on a line under the day's line, each its quiet label and its figure (one that needs the player in
        // the warning's colour), what mends the day's problems at its right (a control's height then), and under them the
        // day's problems, each with the warning's glyph. Returns the y under them.
        private static float Facts(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, IUiKeys keys)
        {
            if (o.FactLabels.Length == 0 && o.Problems.Length == 0 && o.FactActions.Length == 0 && string.IsNullOrEmpty(o.BoardHint))
            {
                return y;
            }
            bool acts = o.FactActions.Length > 0;
            float rowH = acts ? UiTokens.Control : UiTokens.PairRow;
            float right = acts ? Buttons(s, at, o.FactActions, x + w, y, keys, OpsAction) - UiTokens.S5 : x + w;
            float fx = x;
            for (int i = 0; i < o.FactLabels.Length && i < o.FactValues.Length; i++)
            {
                float lw = UiDraw.Snap(UiTextRules.Width(o.FactLabels[i], UiStyle.Small) + UiTokens.S1);
                float vw = UiDraw.Snap(UiTextRules.Width(o.FactValues[i], UiStyle.SmallStrong) + UiTokens.S1);
                if (fx + lw + UiTokens.S2 + vw > right)
                {
                    break;   // a narrow page keeps the facts that fit, the first ones
                }
                bool warn = i < o.FactWarn.Length && o.FactWarn[i];
                UiDraw.TextMiddle(s, at, fx, y, rowH, lw, UiStyle.Small, UiRole.Muted, o.FactLabels[i], UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, fx + lw + UiTokens.S2, y, rowH, vw, UiStyle.SmallStrong, warn ? UiRole.WarnText : UiRole.Text, o.FactValues[i],
                    UiFlags.Truncate);
                fx += lw + UiTokens.S2 + vw + UiTokens.S8;
            }
            y += o.FactLabels.Length > 0 || acts ? rowH : 0f;
            for (int i = 0; i < o.Problems.Length && i < 2; i++)
            {
                UiDraw.Icon(s, at, x, y + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, UiRole.Warn, UiFlags.Middle);
                UiDraw.TextMiddle(s, at, x + UiTokens.IconSmall + UiTokens.S2, y, UiTokens.PairRow, w - UiTokens.IconSmall - UiTokens.S2, UiStyle.Small, UiRole.WarnText,
                    o.Problems[i], UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            if (!string.IsNullOrEmpty(o.BoardHint))
            {
                // A day nobody works yet: the first thing to do, with the note's glyph.
                UiDraw.Icon(s, at, x, y + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Info, UiRole.None, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, at, x + UiTokens.IconSmall + UiTokens.S2, y, UiTokens.PairRow, w - UiTokens.IconSmall - UiTokens.S2, UiStyle.Small, UiRole.Text,
                    o.BoardHint, UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            return y + UiTokens.S3;
        }

        // The day's switch with its words and its opening hours at the left (a button that opens their sheet), the lanes'
        // search after them, and the day's actions at the right (or the auto fill at work, its share and its Cancel). The
        // actions keep their room; the search takes what the left leaves and gives way first, then the hours' words, then
        // the day's.
        private static void DayLine(UiScene s, UiAt at, BizManModel m, float x, float y, float w, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            float right = x + w;
            if (!string.IsNullOrEmpty(o.Progress))
            {
                float cw = UiDraw.ButtonWidth(UiButtonKind.Secondary, o.CancelWords, UiIcon.Close, UiAction.Count, keys);
                right -= cw;
                UiDraw.Button(s, at, right, y, cw, UiTokens.Control, UiButtonKind.Secondary, o.CancelWords, UiIcon.Close, UiAction.Count, keys, OpsCancelFill, 0);
                float meterW = UiTokens.S16 * 3f;
                right -= UiTokens.S3 + meterW;
                UiDraw.Meter(s, at, right, y + (UiTokens.Control - UiTokens.S1) / 2f, meterW, UiTokens.S1, o.ProgressShare, UiRole.Accent);
                float pw = Math.Min(UiDraw.Snap(UiTextRules.Width(o.Progress, UiStyle.Body) + UiTokens.S1), UiTokens.S16 * 4f);
                right -= UiTokens.S3 + pw;
                UiDraw.TextMiddle(s, at, right, y, UiTokens.Control, pw, UiStyle.Body, UiRole.Text2, o.Progress, UiFlags.Truncate);
                right -= UiTokens.S6;
            }
            else
            {
                right = Buttons(s, at, m.Actions, right, y, keys, OpsAction) - UiTokens.S6;
            }
            float track = UiTokens.S16;
            bool press = o.HoursPress && !string.IsNullOrEmpty(o.HoursWords);
            float hours = string.IsNullOrEmpty(o.HoursWords) ? 0f : press ? UiDraw.ButtonWidth(UiButtonKind.Secondary, o.HoursWords, UiIcon.Clock, UiAction.Count, keys)
                : UiDraw.Snap(UiTokens.Icon + UiTokens.S2 + UiTextRules.Width(o.HoursWords, UiStyle.Body) + UiTokens.S1);
            // The opening hours keep their whole words: where the day's words and the hours do not both fit before the actions,
            // the day's words give way to the day's own name.
            string dayWords = o.DayOpenWords;
            if (!string.IsNullOrEmpty(o.DayOpenShort) && !string.IsNullOrEmpty(dayWords)
                && x + UiTextRules.Width(dayWords, UiStyle.BodyStrong) + UiTokens.S1 + UiTokens.S3 + track + UiTokens.S5 + hours > right)
            {
                dayWords = o.DayOpenShort;
            }
            float word = string.IsNullOrEmpty(dayWords) ? 0f : UiDraw.Snap(UiTextRules.Width(dayWords, UiStyle.BodyStrong) + UiTokens.S1);
            float search = o.LaneSearchHint != null ? UiDraw.Snap(Math.Max(UiTokens.S16 * 3f, UiTextRules.Width(o.LaneSearchHint, UiStyle.Body) + UiTokens.S1 + UiTokens.S8)) : 0f;
            float left = word + UiTokens.S3 + track + (hours > 0f ? UiTokens.S5 + hours : 0f);
            if (search > 0f && x + left + UiTokens.S6 + search <= right)
            {
                // The search after what the left needs, four steps wide where the room up to the actions allows.
                float fx = x + left + UiTokens.S6;
                float sw = UiDraw.SnapDown(Math.Min(Math.Max(search, UiTokens.S16 * 4f), right - fx));
                UiDraw.TextField(s, at, fx, y, sw, o.LaneSearch, o.LaneSearchHint, OpsSearch, 0, false);
                right = fx - UiTokens.S6;
            }
            float lead = Math.Min(word, Math.Max(0f, right - x - track - UiTokens.S3));
            if (lead > 0f)
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lead, UiStyle.BodyStrong, UiRole.Text, dayWords, UiFlags.Truncate);
            }
            float sx = x + (lead > 0f ? lead + UiTokens.S3 : 0f);
            UiDraw.Switch(s, at, sx, y, track, o.DayOpen, null, OpsDayOpen, 0, o.DayOpenDisabled);
            float hx = sx + track + UiTokens.S5, room = right - hx;
            if (hours <= 0f || room < UiTokens.S16)
            {
                return;
            }
            if (press && hours <= room)
            {
                UiDraw.Button(s, at, hx, y, hours, UiTokens.Control, UiButtonKind.Secondary, o.HoursWords, UiIcon.Clock, UiAction.Count, keys, OpsHours, 0);
                return;
            }
            UiDraw.Icon(s, at, hx, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Clock, UiRole.None, UiFlags.Middle, true);
            UiDraw.TextMiddle(s, at, hx + UiTokens.Icon + UiTokens.S2, y, UiTokens.Control, room - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text2, o.HoursWords,
                UiFlags.Truncate);
        }

        // The opening hours' sheet under their button: its title and what a press does, the day's hours in four rows of six
        // (an open one on its green tile with its tick, as the board's figures say it), and Done. Each hour is the game's own
        // toggle, pressed for it.
        private static void HoursSheet(UiScene s, UiAt at, BizOpsModel o, float x, float w, float bottom, IUiKeys keys)
        {
            int button = s.FindNode(OpsHours, 0);
            if (button < 0)
            {
                return;
            }
            s.NodeBox(button, out float bx, out float by, out float _, out float bh);
            bool[] open = o.Board != null ? o.Board.OpenHours : null;
            int count = o.HourWords.Length;
            int rows = (count + HourColumns - 1) / HourColumns;
            float widest = 0f;
            for (int h = 0; h < count; h++)
            {
                widest = Math.Max(widest, UiTextRules.Width(o.HourWords[h], UiStyle.SmallStrong));
            }
            float chipW = Math.Max(HourChip, UiDraw.Snap(widest + UiTokens.S1 + UiTokens.IconSmall + UiTokens.S1 + UiTokens.S2 * 2f));   // a twelve hour clock's words are wider
            float inner = HourColumns * chipW + (HourColumns - 1) * UiTokens.S2;
            float cw = inner + UiTokens.S5 * 2f;
            float lineH = string.IsNullOrEmpty(o.HoursLine) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(o.HoursLine, UiStyle.Body, inner, 3));
            float titleH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float ch = UiTokens.S5 + titleH + UiTokens.S3 + (lineH > 0f ? lineH + UiTokens.S4 : 0f) + rows * UiTokens.ControlSmall + Math.Max(0, rows - 1) * UiTokens.S2
                + UiTokens.S5 + UiTokens.Control + UiTokens.S5;
            float cx = Math.Min(bx, x + w - cw), cy = Math.Min(by + bh + UiTokens.S1, bottom - ch);
            var on = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = at.Region };
            int card = UiDraw.Box(s, on, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = on.In(card);
            float px = cx + UiTokens.S5, py = Title(s, inside, px, cy + UiTokens.S5, inner, o.HoursTitle);
            py = Lead(s, inside, o.HoursLine, px, py, inner);
            for (int h = 0; h < count; h++)
            {
                float hx = px + (h % HourColumns) * (chipW + UiTokens.S2), hy = py + (h / HourColumns) * (UiTokens.ControlSmall + UiTokens.S2);
                bool isOpen = open != null && h < open.Length && open[h];
                int face = UiDraw.Box(s, inside, hx, hy, chipW, UiTokens.ControlSmall, isOpen ? UiRole.OkSoft : UiRole.Tile, UiRole.None, UiTokens.RadiusMedium);
                UiAt chip = inside.In(face);
                float tw = UiDraw.Snap(UiTextRules.Width(o.HourWords[h], UiStyle.SmallStrong) + UiTokens.S1);
                float glyph = isOpen ? UiTokens.IconSmall + UiTokens.S1 : 0f;
                float tx = hx + UiDraw.SnapDown((chipW - tw - glyph) / 2f);
                if (isOpen)
                {
                    UiDraw.Icon(s, chip, tx, hy + (UiTokens.ControlSmall - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Check, UiRole.OkText, UiFlags.Middle);
                }
                UiDraw.TextMiddle(s, chip, tx + glyph, hy, UiTokens.ControlSmall, Math.Min(tw, chipW - glyph), UiStyle.SmallStrong, isOpen ? UiRole.OkText : UiRole.Text2,
                    o.HourWords[h], UiFlags.Truncate);
                UiDraw.Node(s, on, hx, hy, chipW, UiTokens.ControlSmall, OpsHour, h, face);
            }
            float dw = UiDraw.ButtonWidth(UiButtonKind.Primary, o.DoneWords, UiIcon.Check, UiAction.Count, keys);
            UiDraw.Button(s, inside, cx + cw - UiTokens.S5 - dw, cy + ch - UiTokens.S5 - UiTokens.Control, dw, UiTokens.Control, UiButtonKind.Primary, o.DoneWords, UiIcon.Check,
                UiAction.Count, keys, OpsSheetDone, 0);
            int firstHour = s.FindNode(OpsHour, 0);
            if (firstHour >= 0)
            {
                s.FocusFirst = firstHour;
            }
        }

        // The game's picker as a sheet at the page's right, over the board: the hour it fills and what a press does with its
        // Cancel, then its people in its order with its search, its sorts and its demand filter, each one a target that
        // puts them there; the filter's list opens over the sheet.
        private static void PickSheet(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys)
        {
            var on = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = at.Region };
            float px = x + UiTokens.S5, pw = w - UiTokens.S5 * 2f;
            float leadH = string.IsNullOrEmpty(o.PeopleLine) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(o.PeopleLine, UiStyle.Body, pw, 3)) + UiTokens.S4;
            float tableTop = y + UiTokens.S5 + UiTokens.Control + UiTokens.S3 + leadH;
            // As tall as its people need, at most the page: a short list ends its sheet under its last row.
            float rows = Math.Max(4, o.People.RowCount) * UiDataTable.RowHeight(o.People);
            float h = Math.Min(bottom - y, UiDraw.Snap(tableTop + UiDataTable.Chrome(o.People) + rows + UiTokens.S5 - y));
            int card = UiDraw.Box(s, on, x, y, w, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = on.In(card);
            float py = y + UiTokens.S5;
            float cw = UiDraw.ButtonWidth(UiButtonKind.Secondary, o.CancelWords, UiIcon.Close, UiAction.Count, keys);
            UiDraw.Button(s, inside, px + pw - cw, py, cw, UiTokens.Control, UiButtonKind.Secondary, o.CancelWords, UiIcon.Close, UiAction.Count, keys, OpsSheetDone, 0);
            // The hour's station is the player's own name for it: it may be cut, as a name in a row is.
            UiDraw.TextMiddle(s, inside, px, py, UiTokens.Control, pw - cw - UiTokens.S4, UiStyle.SectionHeader, UiRole.Text, o.PeopleTitle, UiFlags.Truncate);
            py += UiTokens.Control + UiTokens.S3;
            py = Lead(s, inside, o.PeopleLine, px, py, pw);
            UiDataTable.Lay(s, inside, o.People, px, py, pw, y + h - UiTokens.S5 - py, keys, People);
            int row = s.FindNode(People + UiDataTable.Row, FirstShown(s, People + UiDataTable.Row));
            if (row >= 0)
            {
                s.FocusFirst = row;
            }
            if (o.ListOpen == OpsPeopleList && o.Options.Length > 0)
            {
                int field = s.FindNode(People + UiDataTable.Filter, 0);
                if (field >= 0)
                {
                    s.NodeBox(field, out float fx, out float fy, out float fw, out float fh);
                    UiDraw.Scrim(s, x, y, w, h, 2);
                    float lw = Math.Max(fw, UiTokens.S16 * 4f);
                    UiDraw.ChoiceList(s, on, Math.Min(fx, x + w - UiTokens.S5 - lw), fy + fh + UiTokens.S1, lw, o.Options, o.Options.Length, o.OptionChosen, o.OptionOffset, OpsOption, 2);
                }
            }
        }

        // The picker's sheet as wide as its columns need in this language (a chip column as wide as its widest chip, as the
        // table widens a column for its head) with a name's room beside its face, at most a little over half the page.
        private static float PickWidth(UiDataTableModel m, float most)
        {
            float need = UiTokens.S4 + UiTokens.S1 + UiTokens.AvatarSmall + UiTokens.S3 + PickName;   // the table's inset, the face and the name
            for (int c = 0; c < m.Columns.Length; c++)
            {
                ref UiDataColumn col = ref m.Columns[c];
                if (col.Kind == UiCellKind.Chip)
                {
                    for (int r = 0; r < m.RowCount; r++)
                    {
                        string words = m.Rows[r].Cells != null && c < m.Rows[r].Cells.Length ? m.Rows[r].Cells[c] : null;
                        if (!string.IsNullOrEmpty(words))
                        {
                            col.Width = Math.Max(col.Width, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1));
                        }
                    }
                }
                if (col.Width > 0f)
                {
                    need += UiTokens.S4 + Math.Max(col.Width, UiDraw.Snap(UiTextRules.Width(col.Title, UiStyle.SectionHeader) + UiTokens.S1
                                                                          + (col.Sortable ? UiTokens.IconSmall + UiTokens.S1 : 0f)));
                }
            }
            return Math.Min(most, Math.Max(SheetW, UiDraw.Snap(need + UiTokens.S5 * 2f)));
        }

        // The firm's head's to-do, on the line under its place, is Down from the place: the day's line under the head may hold
        // nothing that stands under the to-do (its buttons disabled, a long language's words), which the page fence would
        // then leave out of the d-pad's reach.
        private static void HeadChain(UiScene s)
        {
            int place = s.FindNode(UiFirmHead.First + UiFirmHead.Place, 0), todo = s.FindNode(UiFirmHead.First + UiFirmHead.Todo, 0);
            if (place < 0 || todo < 0 || s.Nodes[todo].Disabled || s.Nodes[todo].Layer != s.Nodes[place].Layer || s.Nodes[todo].Y < s.Nodes[place].Y + s.Nodes[place].H)
            {
                return;
            }
            s.Nodes[place].Down = todo;
            s.Nodes[todo].Up = place;
        }

        // The first argument a command's targets carry in view (a table's first row shown), or 0.
        private static int FirstShown(UiScene s, int command)
        {
            int least = int.MaxValue;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Command == command && s.Nodes[i].Arg < least)
                {
                    least = s.Nodes[i].Arg;
                }
            }
            return least == int.MaxValue ? 0 : least;
        }

        // The board's own sideways links, from a lane to its shifts and along them, kept for after the page fence: inside a
        // lane's row a search for the nearest target that way would miss its first shifts.
        private static void Keep(UiScene s, int first)
        {
            _keptCount = 0;
            for (int i = first; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if ((n.Command != UiBoard.ShiftCommand && n.Command != UiBoard.LaneCommand) || (n.Left < 0 && n.Right < 0))
                {
                    continue;
                }
                Kept(i, n.Left, n.Right);
            }
        }

        // A target's own sideways links to put back after the page fence; -1 leaves that way the fence's.
        private static void Kept(int node, int left, int right)
        {
            if (_kept == null || _kept.Length < (_keptCount + 1) * 3)
            {
                var grown = new int[Math.Max(96, (_keptCount + 1) * 6)];
                if (_kept != null)
                {
                    Array.Copy(_kept, grown, _keptCount * 3);
                }
                _kept = grown;
            }
            _kept[_keptCount * 3] = node;
            _kept[_keptCount * 3 + 1] = left;
            _kept[_keptCount * 3 + 2] = right;
            _keptCount++;
        }

        // The board's links put back over the fence's, where the board set them; on the schedule, the firm's head steps down
        // its own lines.
        private static void Restore(UiScene s)
        {
            if (_headChain)
            {
                HeadChain(s);
                _headChain = false;
            }
            for (int k = 0; k < _keptCount; k++)
            {
                int i = _kept[k * 3];
                if (i < 0 || i >= s.NodeCount)
                {
                    continue;
                }
                if (_kept[k * 3 + 1] >= 0)
                {
                    s.Nodes[i].Left = _kept[k * 3 + 1];
                }
                if (_kept[k * 3 + 2] >= 0)
                {
                    s.Nodes[i].Right = _kept[k * 3 + 2];
                }
            }
            _keptCount = 0;
        }

        // ------------------------------------------------------------------------------------------------- a manager kind

        // The kind's tabs across the top where it has them, its plans down the left with their actions at the foot, and the
        // chosen plan at the right: its title (its actions beside it where they fit) and what it covers, its form in two
        // columns, its actions under it where they did not fit, then its goods or its route's stops in the room left. No
        // plans yet: what a plan does across the page, with the way to add the first.
        private static void ManagersPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            if (o.Plans.Length == 0)
            {
                float ty = Title(s, at, x, y, w, o.PlansTitle);
                string add = o.PlanActions.Length > 0 && !o.PlanActions[o.PlanActions.Length - 1].Disabled ? o.PlanActions[o.PlanActions.Length - 1].Label : null;
                UiDraw.EmptyState(s, at, x, ty, w, bottom - ty, UiIcon.Receipt, o.PlansEmpty, o.PlansEmptyBody, add, OpsFirstPlan, keys);
                return;
            }
            if (o.Tabs.Length > 1)
            {
                UiDraw.Tabs(s, at, x, y, Math.Min(w, TabsWidth(o.Tabs)), o.Tabs, o.Tab, null, OpsTab);
                y += UiTokens.Control + UiTokens.S5;
            }
            float rx = x + PlansW + UiTokens.S8, rw = x + w - rx;
            _split = rx - UiTokens.S4;
            bool form = o.Rows.Length > 0 || o.Table != null || o.Stops.Length > 0;
            bool onTitle = form && ActionsOnTitle(o, rw, keys);   // both columns' titles then stand level on a control's row
            PlanList(s, at, o, x, y, PlansW, bottom, onTitle, keys);
            if (!form)
            {
                if (o.Plans.Length > 0 && !string.IsNullOrEmpty(o.FormEmpty))
                {
                    UiDraw.EmptyState(s, at, rx, y, rw, bottom - y, UiIcon.Receipt, o.FormEmpty, o.FormEmptyBody, null, -1, keys);
                }
                return;
            }
            Form(s, at, o, rx, y, rw, bottom, onTitle, keys);
            OpenList(s, at, m, x, y, w, bottom);
        }

        private static float TabsWidth(string[] tabs)
        {
            float sum = 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                sum += UiDraw.Snap(UiTextRules.Width(tabs[i], UiStyle.Nav) + UiTokens.S4 * 2f) + (i > 0 ? UiTokens.S1 : 0f);
            }
            return sum;
        }

        // The plans as rows under their title, the chosen one in the cyan fifth, a plan without a manager marked; the actions
        // (add one, move the chosen one up or down) at the foot. No plans: what one is and the way to add one.
        private static void PlanList(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, bool onRow, IUiKeys keys)
        {
            float actionsH = o.PlanActions.Length > 0 ? UiTokens.Control + UiTokens.S4 : 0f;
            Items(s, at, o.PlansTitle, o.Plans, x, y, w, bottom - actionsH, OpsPlan, -1, onRow, keys);
            if (actionsH > 0f)
            {
                PlanActions(s, at, o.PlanActions, x, bottom - UiTokens.Control, w, keys);
            }
        }

        // The plans' actions on one row: adding one in its words at the left, the chosen one's moves up and down its list as
        // square arrows at the right (an action without words), so the narrow column keeps them on one line in every language.
        private static void PlanActions(UiScene s, UiAt at, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            float bx = x, rx = x + w;
            for (int i = 0; i < actions.Length; i++)
            {
                ref UiActionModel a = ref actions[i];
                if (string.IsNullOrEmpty(a.Label))
                {
                    rx -= UiTokens.Control;
                    UiDraw.Button(s, at, rx, y, UiTokens.Control, UiTokens.Control, a.Kind, null, a.Icon, a.Key, keys, OpsAction, a.Arg, a.Disabled);
                    rx -= UiTokens.S2;
                }
            }
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                if (string.IsNullOrEmpty(a.Label))
                {
                    continue;
                }
                float bw = Math.Min(UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys), rx - UiTokens.S2 - bx);
                if (bw <= UiTokens.S8)
                {
                    break;
                }
                UiDraw.Button(s, at, bx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, OpsAction, a.Arg, a.Disabled);
                bx += bw + UiTokens.S3;
            }
        }

        // The chosen plan or machine: its title with its actions at the right where they fit, its lead, its rows in two
        // columns, its actions under them where they did not, then its stops or its table.
        private static void Form(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, bool onTitle, IUiKeys keys)
        {
            float fy;
            if (onTitle)
            {
                float acts = ActionsWidth(o.Actions, keys);
                Buttons(s, at, o.Actions, x + w, y, keys, OpsAction);
                fy = RowTitle(s, at, x, y, w - acts - UiTokens.S6, o.FormTitle);
            }
            else
            {
                fy = Title(s, at, x, y, w, o.FormTitle);
            }
            fy = Lead(s, at, o.FormLead, x, fy, w);
            if (o.FormTabs.Length > 1)
            {
                UiDraw.Tabs(s, at, x, fy, Math.Min(w, TabsWidth(o.FormTabs)), o.FormTabs, o.FormTab, null, OpsFormTab);
                fy += UiTokens.Control + UiTokens.S5;
            }
            fy = Rows(s, at, o.Rows, x, fy, w, keys);
            if (!onTitle && o.Actions.Length > 0)
            {
                fy += UiTokens.S2;
                Buttons(s, at, o.Actions, x + w, fy, keys, OpsAction);
                fy += UiTokens.Control + UiTokens.S5;
            }
            if (o.Stops.Length > 0 || !string.IsNullOrEmpty(o.StopsTitle))
            {
                fy = Stops(s, at, o, x, fy, w, bottom, keys) + UiTokens.S4;   // the chosen stop's goods under the route, its title the stop's
            }
            if (o.Table != null && bottom - fy > UiTokens.S16)
            {
                fy = Title(s, at, x, fy, w, o.TableTitle);
                // The chosen stop's card says whose goods the table holds; the words say it where the route is a list.
                fy = Lead(s, at, o.Status ?? (StopsAsCards(o, w) ? null : o.TableLead), x, fy, w);
                fy = Filter(s, at, o, x, fy, w);
                int first = s.NodeCount;
                UiDataTable.Lay(s, at, o.Table, x, fy, w, bottom - fy, keys, Table);
                KeepStepper(s, first, Table);
            }
        }

        // The chosen row's stepper as the table means it: right from the row along its minus, its figure and its plus, and
        // left back the same way, kept over the page fence's. The fence's search for the nearest target that way misses the
        // minus where the stepper stands across the row's middle (a column of amounts with others after it). A step at its
        // end takes no keys, so the way passes it; a table whose every row holds its own stepper keeps the fence's.
        private static void KeepStepper(UiScene s, int first, int command)
        {
            int less = -1, type = -1, more = -1, count = 0;
            for (int i = first; i < s.NodeCount; i++)
            {
                int c = s.Nodes[i].Command;
                count += c == command + UiDataTable.Type ? 1 : 0;
                less = c == command + UiDataTable.Less ? i : less;
                type = c == command + UiDataTable.Type ? i : type;
                more = c == command + UiDataTable.More ? i : more;
            }
            int row = -1;
            for (int i = first; count == 1 && i < s.NodeCount && row < 0; i++)
            {
                row = s.Nodes[i].Command == command + UiDataTable.Row && s.Nodes[i].Arg == s.Nodes[type].Arg ? i : -1;
            }
            if (row >= 0)
            {
                Along(s, Along(s, Along(s, row, less), type), more);
            }
        }

        // A step of a kept way, to the next target if it takes the keys; returns where the way stands after it.
        private static int Along(UiScene s, int from, int to)
        {
            if (to < 0 || s.Nodes[to].Disabled)
            {
                return from;
            }
            Kept(from, -1, to);
            Kept(to, from, -1);
            return to;
        }

        // Whether the form's actions stand on its title's row, at the right: where they fit beside the whole title, so the
        // stop's goods or the plan's products keep the room a row of their own would take.
        private static bool ActionsOnTitle(BizOpsModel o, float w, IUiKeys keys)
        {
            float acts = ActionsWidth(o.Actions, keys);
            return acts > 0f && !string.IsNullOrEmpty(o.FormTitle)
                && UiDraw.Snap(UiTextRules.Width(o.FormTitle, UiStyle.SectionHeader) + UiTokens.S1) + UiTokens.S6 + acts <= w;
        }

        // The width a row of actions takes, its gaps between them.
        private static float ActionsWidth(UiActionModel[] actions, IUiKeys keys)
        {
            float sum = 0f;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ref UiActionModel a = ref actions[i];
                sum += UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys) + (i > 0 ? UiTokens.S3 : 0f);
            }
            return sum;
        }

        // A table's filter as tabs over it, each its words and its count. Returns the y under them.
        private static float Filter(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w)
        {
            if (o.Filters.Length < 2)
            {
                return y;
            }
            UiDraw.Tabs(s, at, x, y, Math.Min(w, TabsWidth(o.Filters)), o.Filters, o.Filter, null, OpsFilter);
            return y + UiTokens.Control + UiTokens.S3;
        }

        // A line under a big table: its label, a meter of how full, its words, and a figure at the right.
        private static float Foot(UiScene s, UiAt at, BizOpsModel o, float x, float bottom, float w)
        {
            if (string.IsNullOrEmpty(o.FootLabel) && string.IsNullOrEmpty(o.FootRight))
            {
                return bottom;
            }
            float y = bottom - UiTokens.PairRow;
            float fx = x;
            if (!string.IsNullOrEmpty(o.FootLabel))
            {
                float lw = UiDraw.Snap(UiTextRules.Width(o.FootLabel, UiStyle.Small) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, fx, y, UiTokens.PairRow, lw, UiStyle.Small, UiRole.Text2, o.FootLabel, UiFlags.Truncate);
                fx += lw + UiTokens.S3;
            }
            if (o.FootShare >= 0)
            {
                float mw = UiTokens.S16 * 4f;
                UiDraw.Meter(s, at, fx, y + (UiTokens.PairRow - UiTokens.S1) / 2f, mw, UiTokens.S1, Math.Min(100, o.FootShare), o.FootShare >= 90 ? UiRole.Warn : UiRole.Accent);
                fx += mw + UiTokens.S3;
            }
            float rw = string.IsNullOrEmpty(o.FootRight) ? 0f : UiDraw.Snap(UiTextRules.Width(o.FootRight, UiStyle.SmallStrong) + UiTokens.S1);
            if (rw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - rw, y, UiTokens.PairRow, rw, UiStyle.SmallStrong, UiRole.Text, o.FootRight, UiFlags.Truncate);
            }
            if (!string.IsNullOrEmpty(o.FootWords))
            {
                UiDraw.TextMiddle(s, at, fx, y, UiTokens.PairRow, Math.Max(0f, x + w - rw - UiTokens.S6 - fx), UiStyle.Small, UiRole.Text2, o.FootWords, UiFlags.Truncate);
            }
            return y - UiTokens.S3;
        }

        /// <summary>
        /// A form's rows: a heading starts a block, a fact is a compact row, a control stands at the right of its label on a
        /// control's row, a meter is a graded bar. Rows marked Right take the right column when any do, so a form of two
        /// blocks runs across. Returns the y under the taller column.
        /// </summary>
        private static float Rows(UiScene s, UiAt at, BizOpsRow[] rows, float x, float y, float w, IUiKeys keys)
        {
            bool two = false;
            for (int i = 0; i < rows.Length && !two; i++)
            {
                two = rows[i].Right;
            }
            float cw = two ? Half(w) : w;
            float left = Column(s, at, rows, false, x, y, cw, keys);
            float right = two ? Column(s, at, rows, true, x + cw + UiTokens.S8, y, cw, keys) : y;
            return Math.Max(left, right);
        }

        /// <summary>A form's rows in one column: its left blocks, then its right ones under them. Returns the y under them.</summary>
        private static float Stack(UiScene s, UiAt at, BizOpsRow[] rows, float x, float y, float w, IUiKeys keys)
        {
            y = Column(s, at, rows, false, x, y, w, keys);
            bool more = false;
            for (int i = 0; i < rows.Length && !more; i++)
            {
                more = rows[i].Right;
            }
            return more ? Column(s, at, rows, true, x, y + UiTokens.S4, w, keys) : y;
        }

        private static float Column(UiScene s, UiAt at, BizOpsRow[] rows, bool right, float x, float y, float w, IUiKeys keys)
        {
            float lw = LabelWidth(rows, right, w);
            float cx = x + lw + UiTokens.S4, cw = w - lw - UiTokens.S4;
            bool first = true;
            for (int i = 0; i < rows.Length; i++)
            {
                ref BizOpsRow r = ref rows[i];
                if (r.Right != right)
                {
                    continue;
                }
                switch (r.Kind)
                {
                    case BizOpsRowKind.Heading:
                        y = Title(s, at, x, y + (first ? 0f : UiTokens.S4), w, r.Label);   // a block after another stands a step apart
                        break;
                    case BizOpsRowKind.Info:
                        y += UiDraw.InfoRow(s, at, x, y, w, r.Label, r.Value, false, -1, 0, r.Tone == UiRole.None ? UiRole.Text : r.Tone);
                        break;
                    case BizOpsRowKind.Note:
                        y = Lead(s, at, r.Value, x, y, w);
                        break;
                    case BizOpsRowKind.Meter:
                        y += UiDraw.GradeBar(s, at, x, y, w, r.Label, r.Grade, lw);
                        break;
                    case BizOpsRowKind.Button:
                    {
                        UiButtonKind kind = r.Tone == UiRole.Danger ? UiButtonKind.Destructive : UiButtonKind.Secondary;
                        float bw = Math.Min(cw, UiDraw.ButtonWidth(kind, r.Value, r.Icon, UiAction.Count, keys));
                        RowLabel(s, at, x, y, lw, r.Label);
                        // At the column's right edge, as the form's own actions stand under it: the d-pad steps between them
                        // whatever the buttons' words' widths.
                        UiDraw.Button(s, at, x + w - bw, y, bw, UiTokens.Control, kind, r.Value, r.Icon, UiAction.Count, keys, OpsRow, r.Arg, r.Disabled);
                        y += UiTokens.Control + UiTokens.S2;
                        break;
                    }
                    default:
                        RowLabel(s, at, x, y, lw, r.Label);
                        Control(s, at, ref r, r.Arg, cx, y, cw);   // the row's own target, as the screen presses it (its widget)
                        y += UiTokens.Control + UiTokens.S2;
                        break;
                }
                first = false;
            }
            return y;
        }

        // A form row's label at the left of its control: on one line, or on two where a long translation needs them in the
        // labels' share of the column (the row keeps its height), never cut.
        private static void RowLabel(UiScene s, UiAt at, float x, float y, float w, string words)
        {
            if (string.IsNullOrEmpty(words) || UiTextRules.Width(words, UiStyle.Body) + UiTokens.S1 <= w)
            {
                Label(s, at, x, y, w, words);
                return;
            }
            float lh = UiTextRules.WrappedHeight(words, UiStyle.Body, w, 2);
            UiDraw.Text(s, at, x, y + (UiTokens.Control - lh) / 2f, w, UiStyle.Body, UiRole.Text2, words, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 2);
        }

        // A row's control in its control-only form, the game's widget mirrored.
        private static void Control(UiScene s, UiAt at, ref BizOpsRow r, int i, float x, float y, float w)
        {
            switch (r.Kind)
            {
                case BizOpsRowKind.Switch:
                    UiDraw.Switch(s, at, x, y, w, r.On, r.Value, OpsRow, i, r.Disabled);
                    break;
                case BizOpsRowKind.Choice:
                    UiDraw.Choice(s, at, x, y, w, r.Value, OpsChoice, i, r.Disabled);
                    break;
                case BizOpsRowKind.Slider:
                    UiDraw.Slider(s, at, x, y, w, r.Value, r.Share, OpsSlider, i, r.Disabled, r.Held);
                    break;
                case BizOpsRowKind.Stepper:
                {
                    // At the column's right end, as a switch stands: in a wide column the keys go down the rows' ends alike.
                    float sw = UiDraw.SnapDown(Math.Min(w, UiDataTable.StepperWidth));
                    UiDraw.Stepper(s, at, x + w - sw, y, sw, r.Value ?? string.Empty, r.CanLess && !r.Disabled,
                        r.CanMore && !r.Disabled, OpsLess, OpsType, OpsMore, i, r.Disabled, r.Placeholder);
                    break;
                }
                case BizOpsRowKind.Text:
                    UiDraw.TextField(s, at, x, y, w, r.Value, r.Placeholder, OpsType, i, r.Disabled, r.Typing);
                    break;
            }
        }

        // The labels' column of one side of a form: its longest label, kept to a share of the column.
        private static float LabelWidth(BizOpsRow[] rows, bool right, float w)
        {
            float most = 0f;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Right == right && rows[i].Kind != BizOpsRowKind.Heading && rows[i].Kind != BizOpsRowKind.Note && rows[i].Kind != BizOpsRowKind.Info
                    && !string.IsNullOrEmpty(rows[i].Label))
                {
                    most = Math.Max(most, UiTextRules.Width(rows[i].Label, UiStyle.Body));
                }
            }
            return UiDraw.Snap(Math.Min(most + UiTokens.S1, w * FormLabel));
        }

        // A route's stops: each its tile, its goods' count and its place; the chosen one's chevron down (its goods are the
        // table under the route) with its place as a choice and its remove, another a row that opens it; a stop past the
        // route's limit marked. Returns the y under them.
        private const float StopCardLeast = 232f;     // px: a stop's card at the least; narrower, the route is a list
        private const int StopCardGoods = 3;          // goods lines on a stop's card
        private const float StopGoodRow = 28f;        // px: one of them

        // The route as cards left to right with arrows between, as a delivery route reads: each stop's number, its place
        // and whether it is open, and the first goods the route keeps there with their targets. The chosen stop's place
        // is its choice with its remove, its goods the table under the route. All on one row, or the route as a list.
        private static float StopCards(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, IUiKeys keys)
        {
            int n = o.Stops.Length;
            float gap = UiTokens.S8;
            float cw = UiDraw.SnapDown((w - gap * (n - 1)) / n);
            float pad = UiTokens.S3, head = UiTokens.Control, tile = UiTokens.ControlSmall;
            float foot = UiDraw.Snap(Math.Max(UiTextRules.LineHeight(UiStyle.Small), UiTokens.Badge + UiTokens.S1));   // the foot's row, on the grid
            float ch = pad + head + UiTokens.S2 + StopCardGoods * StopGoodRow + UiTokens.S1 + foot + pad;
            float removeW = UiTokens.ControlSmall;
            for (int i = 0; i < n; i++)
            {
                ref BizOpsStop st = ref o.Stops[i];
                float cx = x + i * (cw + gap);
                int card = UiDraw.Box(s, at, cx, y, cw, ch, st.Open ? UiRole.RowSel : UiRole.Row, st.Open ? UiRole.RowSelEdge : UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(card);
                if (!st.Open)
                {
                    UiDraw.Node(s, at, cx, y, cw, ch, OpsStop, i, card);   // another stop: the whole card opens it
                }
                float ix = cx + pad, iw = cw - pad * 2f, hy = y + pad;
                int t = UiDraw.Tile(s, inside, ix, hy + (head - tile) / 2f, tile, st.Warn ? UiRole.Warn : UiRole.None, false);
                UiDraw.TextMiddle(s, inside.In(t), ix, hy + (head - tile) / 2f, tile, tile, UiStyle.SmallStrong, st.Warn ? UiRole.WarnText : UiRole.Text,
                    (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), UiFlags.Centre);
                float tx = ix + tile + UiTokens.S2, tw = ix + iw - tx;
                if (st.Open)
                {
                    float chw = tw - (st.Removable ? removeW + UiTokens.S1 : 0f);
                    UiDraw.Choice(s, inside, tx, hy, chw, st.Name, OpsStopChoice, i, false);
                    if (st.Removable)
                    {
                        UiDraw.Button(s, inside, ix + iw - removeW, hy + (head - removeW) / 2f, removeW, removeW, UiButtonKind.Quiet, null, UiIcon.Trash, UiAction.Count, keys, OpsStopRemove, i);
                    }
                }
                else
                {
                    UiDraw.TextMiddle(s, inside, tx, hy, head, tw, UiStyle.Title, UiRole.Text, st.Name, UiFlags.Truncate);
                }
                float gy = hy + head + UiTokens.S2;
                int shown = st.Goods != null ? Math.Min(StopCardGoods, st.Goods.Length) : 0;
                float pic = UiTokens.IconSmall + UiTokens.S1;
                for (int k = 0; k < shown; k++)
                {
                    float ry = gy + k * StopGoodRow;
                    string key = st.Pictures != null && k < st.Pictures.Length ? st.Pictures[k] : null;
                    if (!string.IsNullOrEmpty(key))
                    {
                        var item = new UiItem
                        {
                            Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = ix, Y = ry + (StopGoodRow - pic) / 2f, W = pic, H = pic, Text = key, Value = 1f,
                            Clip = inside.Clip, Node = -1, Layer = inside.Layer, Order = inside.Order, Parent = card, Alpha = 1f
                        };
                        s.Add(ref item);
                    }
                    else
                    {
                        UiDraw.Icon(s, inside, ix, ry + (StopGoodRow - pic) / 2f, pic, UiIcon.Package, UiRole.Muted, UiFlags.Middle);
                    }
                    string fill = st.Fills != null && k < st.Fills.Length ? st.Fills[k] : null;
                    float fw = string.IsNullOrEmpty(fill) ? 0f : UiDraw.Snap(UiTextRules.Width(fill, UiStyle.SmallStrong) + UiTokens.S1);
                    UiDraw.TextMiddle(s, inside, ix + pic + UiTokens.S2, ry, StopGoodRow, iw - pic - UiTokens.S2 - fw - UiTokens.S2, UiStyle.Small, UiRole.Text, st.Goods[k], UiFlags.Truncate);
                    if (fw > 0f)
                    {
                        UiDraw.TextMiddle(s, inside, ix + iw - fw, ry, StopGoodRow, fw, UiStyle.SmallStrong, UiRole.Text, fill, UiFlags.Right);
                    }
                }
                if (shown == 0)
                {
                    UiDraw.TextMiddle(s, inside, ix, gy, StopGoodRow, iw, UiStyle.Small, UiRole.Muted, st.Line, UiFlags.Truncate);
                }
                // The foot: how many more goods, and the place's opening as a chip at the right.
                float fy = y + ch - pad - foot;
                float chip = 0f;
                if (!string.IsNullOrEmpty(st.Open2))
                {
                    float most = iw / 2f;
                    chip = Math.Min(most, ChipW(st.Open2));
                    UiDraw.Chip(s, inside, ix + iw - chip, fy + UiDraw.SnapDown((foot - UiTokens.Badge - UiTokens.S1) / 2f), st.Open2, st.OpenTone, most);
                }
                if (!string.IsNullOrEmpty(st.More))
                {
                    UiDraw.TextMiddle(s, inside, ix, fy, foot, iw - chip - UiTokens.S2, UiStyle.Small, UiRole.Text, st.More, UiFlags.Truncate);
                }
                if (i < n - 1)
                {
                    float g = UiTokens.Icon;
                    UiDraw.Icon(s, at, cx + cw + (gap - g) / 2f, y + (ch - g) / 2f, g, UiIcon.Next, UiRole.AccentText, UiFlags.Middle | UiFlags.Centre);
                }
            }
            return y + ch;
        }

        private static bool StopsAsCards(BizOpsModel o, float w) => o.Stops.Length > 0 && (w - UiTokens.S8 * (o.Stops.Length - 1)) / o.Stops.Length >= StopCardLeast;

        // A status chip's width as UiDraw.Chip lays it.
        private static float ChipW(string text) =>
            UiDraw.Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + UiTextRules.Width(text, UiStyle.SmallStrong) + UiTokens.S1 + UiTokens.S2);

        private static float Stops(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Title(s, at, x, y, w, o.StopsTitle);
            if (StopsAsCards(o, w))
            {
                return StopCards(s, at, o, x, y, w, keys);
            }
            float tile = UiTokens.IconTile;
            float choiceW = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 5f, w * 0.45f));
            float removeW = UiTokens.ControlSmall;
            for (int i = 0; i < o.Stops.Length && y + StopRow <= bottom; i++)
            {
                ref BizOpsStop st = ref o.Stops[i];
                int row = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, StopRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(row);
                int t = UiDraw.Tile(s, inside, x, y + (StopRow - tile) / 2f, tile, UiRole.None, false);
                float g = UiTokens.Icon + UiTokens.S1;
                UiDraw.Icon(s, inside.In(t), x + (tile - g) / 2f, y + (StopRow - g) / 2f, g, UiIcon.MapPin, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                float tx = x + tile + UiTokens.S3;
                float rx = x + w - removeW;
                float cx = rx - UiTokens.S3 - choiceW;
                float lw = cx - UiTokens.S3 - tx;
                if (st.Open)
                {
                    // The chosen stop: its place as a choice and its remove, its goods the table under the route.
                    UiDraw.Choice(s, inside, cx, y + (StopRow - UiTokens.Control) / 2f, choiceW, st.Name, OpsStopChoice, i, false);
                    if (st.Removable)
                    {
                        UiDraw.Button(s, inside, rx, y + (StopRow - removeW) / 2f, removeW, removeW, UiButtonKind.Quiet, null, UiIcon.Trash, UiAction.Count, keys, OpsStopRemove, i);
                    }
                    UiDraw.Node(s, at, tx, y, lw, StopRow, OpsStop, i, row);
                }
                else
                {
                    // Another stop: its place in words where the chosen one's choice stands, the whole row the target that opens it.
                    UiDraw.TextMiddle(s, inside, cx + UiTokens.S4, y, StopRow, choiceW - UiTokens.S4, UiStyle.Body, UiRole.Text, st.Name, UiFlags.Truncate);
                    UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, StopRow, OpsStop, i, row);
                }
                if (st.Warn)
                {
                    UiDraw.Icon(s, inside, tx, y + (StopRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, UiRole.Warn, UiFlags.Middle);
                    tx += UiTokens.IconSmall + UiTokens.S1;
                    lw -= UiTokens.IconSmall + UiTokens.S1;
                }
                UiDraw.Icon(s, inside, tx, y + (StopRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, st.Open ? UiIcon.Down : UiIcon.Next, UiRole.Muted, UiFlags.Middle);
                tx += UiTokens.IconSmall + UiTokens.S2;
                lw -= UiTokens.IconSmall + UiTokens.S2;
                UiDraw.TextMiddle(s, inside, tx, y, StopRow, lw, UiStyle.Body, st.Warn ? UiRole.WarnText : UiRole.Text2, st.Line, UiFlags.Truncate);
                y += StopRow + UiTokens.S1;
            }
            return y;
        }

        // A form row's or a stop's list of choices open over the page, under its field.
        private static void OpenList(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom)
        {
            BizOpsModel o = m.Ops;
            if (o.ListOpen < 0 || o.Options.Length == 0)
            {
                return;
            }
            int field = o.ListOpen >= OpsStopList && o.ListOpen < OpsPeopleList ? s.FindNode(OpsStopChoice, o.ListOpen - OpsStopList) : s.FindNode(OpsChoice, o.ListOpen);
            if (field < 0)
            {
                return;
            }
            Behind(s, x, y, w, bottom);
            s.NodeBox(field, out float fx, out float fy, out float fw, out float fh);
            UiDraw.ChoiceList(s, at, fx, fy + fh + UiTokens.S1, fw, o.Options, o.Options.Length, o.OptionChosen, o.OptionOffset, OpsOption);
        }

        // ------------------------------------------------------------------------------------------------- a factory

        // The machine groups as tabs, the group's machines as the big table across most of the page (each machine's state a
        // chip, its picture on its tile), and in a column at the right the chosen machine's recipe as a form, its blocks one
        // under another, or the group's production when no machine is chosen.
        private static void FactoryPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            if (o.Tabs.Length > 0)
            {
                UiDraw.Tabs(s, at, x, y, Math.Min(w, TabsWidth(o.Tabs)), o.Tabs, o.Tab, null, OpsTab);
                y += UiTokens.Control + UiTokens.S5;
            }
            if (o.Table == null || (o.Table.RowCount == 0 && o.Tabs.Length == 0))
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Factory, o.PlansEmpty, o.PlansEmptyBody, null, -1, keys);
                return;
            }
            float side = UiDraw.SnapDown(Math.Max(MachineFormLeast, (w - UiTokens.S8) * MachineFormShare));
            float tw = w - side - UiTokens.S8, rx = x + tw + UiTokens.S8;
            _split = rx - UiTokens.S4;
            float ty = Title(s, at, x, y, tw, o.TableTitle);
            ty = Lead(s, at, o.TableLead, x, ty, tw);
            ty = Filter(s, at, o, x, ty, tw);
            UiDataTable.Lay(s, at, o.Table, x, ty, tw, bottom - ty, keys, Table);
            float fy = Title(s, at, rx, y, side, o.FormTitle);
            fy = Lead(s, at, o.FormLead, rx, fy, side);
            fy = Stack(s, at, o.Rows, rx, fy, side, keys);
            if (o.Actions.Length > 0)
            {
                Buttons(s, at, o.Actions, rx + side, fy + UiTokens.S2, keys, OpsAction);
            }
            OpenList(s, at, m, x, y, w, bottom);
        }

        // ------------------------------------------------------------------------------------------------- a warehouse

        // The vehicle slots as a form, each slot's vehicle and its driver as a choice, in two columns where there are many.
        private static void DriversPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            if (o.Rows.Length == 0 && o.Slots.Length == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Truck, o.PlansEmpty, o.PlansEmptyBody, null, -1, keys);
                return;
            }
            float fy = Title(s, at, x, y, w, o.FormTitle);
            fy = Lead(s, at, o.FormLead, x, fy, w);
            if (o.Slots.Length > 0)
            {
                SlotCards(s, at, o.Slots, x, fy, w, bottom);
            }
            else
            {
                Rows(s, at, o.Rows, x, fy, w, keys);
            }
            OpenList(s, at, m, x, y, w, bottom);
        }

        private const float SlotLeast = 296f;         // px: a vehicle slot's card at the least; fewer cards a row below it
        private const int SlotsAcross = 4;            // the most cards a row

        // A warehouse's vehicle slots as cards, as a fleet screen shows its vehicles: each its vehicle's tile, its name and
        // load, its driver as a choice, and where it is as a chip. Up to four a row, more rows under them.
        private static void SlotCards(UiScene s, UiAt at, BizOpsSlot[] slots, float x, float y, float w, float bottom)
        {
            int across = Math.Max(1, Math.Min(SlotsAcross, (int)((w + UiTokens.S4) / (SlotLeast + UiTokens.S4))));
            float cw = UiDraw.SnapDown((w - UiTokens.S4 * (across - 1)) / across);
            float tile = UiTokens.S12, pad = UiTokens.S4;
            float chipH = UiTokens.ControlSmall;
            float ch = pad + tile + UiTokens.S3 + UiTokens.Control + UiTokens.S3 + chipH + pad;
            for (int i = 0; i < slots.Length; i++)
            {
                ref BizOpsSlot sl = ref slots[i];
                float cx = x + (i % across) * (cw + UiTokens.S4), cy = y + (i / across) * (ch + UiTokens.S4);
                if (cy + ch > bottom)
                {
                    break;
                }
                int card = UiDraw.Box(s, at, cx, cy, cw, ch, UiRole.Row, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(card);
                float ix = cx + pad, iw = cw - pad * 2f;
                int t = UiDraw.Tile(s, inside, ix, cy + pad, tile, UiRole.None, false);
                float g = UiTokens.Icon + UiTokens.S2;
                UiDraw.Icon(s, inside.In(t), ix + (tile - g) / 2f, cy + pad + (tile - g) / 2f, g, UiIcon.Truck, sl.Vehicle ? UiRole.None : UiRole.Muted, UiFlags.Middle | UiFlags.Centre, sl.Vehicle);
                float tx = ix + tile + UiTokens.S3, tw = ix + iw - tx;
                float nameH = UiTextRules.LineHeight(UiStyle.Title), lineH = UiTextRules.LineHeight(UiStyle.Small);
                float top = cy + pad + (tile - nameH - lineH) / 2f;
                UiDraw.Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, sl.Title, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, inside, tx, top + nameH, tw, UiStyle.Small, UiRole.Text2, sl.Line, UiFlags.Truncate | UiFlags.Middle);
                float dy = cy + pad + tile + UiTokens.S3;
                if (sl.DriverArg >= 0)
                {
                    UiDraw.Choice(s, inside, ix, dy, iw, sl.Driver, OpsChoice, sl.DriverArg, sl.DriverDisabled);
                }
                if (!string.IsNullOrEmpty(sl.Status))
                {
                    UiDraw.Chip(s, inside, ix, dy + UiTokens.Control + UiTokens.S3 + (chipH - UiTokens.ControlSmall) / 2f, sl.Status, sl.Tone, iw);
                }
            }
        }

        // The stock as the big table under how full the warehouse is, Sell all at the right of that line.
        private static void StockPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            float left = Buttons(s, at, o.Actions, x + w, y, keys, OpsAction);
            if (!string.IsNullOrEmpty(o.FormLead))
            {
                UiDraw.Icon(s, at, x, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Package, UiRole.None, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S2, y, UiTokens.Control, left - x - UiTokens.Icon - UiTokens.S6, UiStyle.BodyStrong, UiRole.Text, o.FormLead,
                    UiFlags.Truncate);
            }
            // The goods' search stands on the actions' row, at its left, where no words lead the page.
            bool level = string.IsNullOrEmpty(o.FormLead) && o.Table != null && !string.IsNullOrEmpty(o.Table.SearchPlaceholder) && left - x >= UiTokens.S16 * 6f;
            float ty = level ? y : y + UiTokens.Control + UiTokens.S5;
            float foot = Foot(s, at, o, x, bottom, w);
            if (o.Table != null)
            {
                UiDataTable.Lay(s, at, o.Table, x, ty, w, foot - ty, keys, Table);
            }
        }
    }
}
