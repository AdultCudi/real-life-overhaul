using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The operations of a place, each in the shape its job has: the schedule as Two Point Hospital's rota (the day's line
    /// with its opening and its actions, the board of shifts on an hour ruler under the day's tabs, the people beside it,
    /// sorted and searched as the game's picker sorts them while an hour is being filled); a manager kind as its plans down
    /// the left and the chosen plan as a form in two columns over its goods, a route's stops for logistics as Anno's trade
    /// routes; a factory as its machine groups across the top, the group's machines and the chosen one's recipe beside them;
    /// a warehouse as its vehicle slots, each with its driver, and its stock as the big table with Sell all. Every figure and
    /// word is the model's. Game-free. Cost: a layout a change.
    /// </summary>
    public static partial class BizManPages
    {
        public const int OpsRow = 500, OpsChoice = 501, OpsOption = 502, OpsLess = 503, OpsType = 504, OpsMore = 505, OpsSlider = 506, OpsPlan = 507,
            OpsAction = 508, OpsTab = 509, OpsStop = 510, OpsStopRemove = 511, OpsStopChoice = 512, OpsDayOpen = 513, OpsSearch = 514, OpsCancelFill = 515;
        public const int People = 600;                // the people's table beside the board, its commands from here as UiDataTable's from its First
        public const int Table = 700;                 // a page's big table (a plan's goods, the machines, the stock)
        public const int OpsStopList = 1000;          // ListOpen for a stop's business choice: from here
        public const int OpsPeopleList = 2000;        // ListOpen for the people's filter
        private const float PeopleW = 440f;           // px: the people beside the board: a face, a name and three figures
        private const float PlansW = 360f;            // px: a manager kind's plans down the left
        private const float StopRow = 48f;            // px: a stop of a route, its tile and two lines
        private const float FormLabel = 0.42f;        // the share of a form's column its labels take, at most

        // ------------------------------------------------------------------------------------------------- the schedule

        // The day's line across the page, then the board at the left and the people at the right, both from under it.
        private static void SchedulePage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            float bw = w - PeopleW - UiTokens.S8, px = x + bw + UiTokens.S8;
            _split = px - UiTokens.S4;
            DayLine(s, at, m, x, y, w, keys);
            float top = y + UiTokens.Control + UiTokens.S5;
            if (o.Board != null)
            {
                UiBoard.Lay(s, at, o.Board, x, top, bw, bottom - top, keys);
            }
            PeopleColumn(s, at, o, px, top, PeopleW, bottom, keys);
            if (o.ListOpen == OpsPeopleList && o.Options.Length > 0)
            {
                int field = s.FindNode(People + UiDataTable.Filter, 0);
                if (field >= 0)
                {
                    Behind(s, x, y, w, bottom);
                    s.NodeBox(field, out float fx, out float fy, out float fw, out float fh);
                    UiDraw.ChoiceList(s, at, fx, fy + fh + UiTokens.S1, Math.Max(fw, UiTokens.S16 * 4f), o.Options, o.Options.Length, o.OptionChosen, o.OptionOffset, OpsOption);
                }
            }
        }

        // The day's switch with its opening hours in words at the left, the lanes' search, and the day's actions at the right
        // (or the auto fill at work, its share and its Cancel).
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
                right = Buttons(s, at, m.Actions, right, y, keys, OpsAction) - UiTokens.S3;
            }
            float sw = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 4f, Math.Max(0f, right - x - UiTokens.S16 * 6f)));
            if (o.LaneSearchHint != null && sw >= UiTokens.S16 * 2f)
            {
                right -= sw;
                UiDraw.TextField(s, at, right, y, sw, o.LaneSearch, o.LaneSearchHint, OpsSearch, 0, false);
                right -= UiTokens.S6;
            }
            float word = string.IsNullOrEmpty(o.DayOpenWords) ? 0f : UiDraw.Snap(UiTextRules.Width(o.DayOpenWords, UiStyle.BodyStrong) + UiTokens.S1);
            float track = UiTokens.S16;
            float lead = Math.Min(word, Math.Max(0f, right - x - track - UiTokens.S3));
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lead, UiStyle.BodyStrong, UiRole.Text, o.DayOpenWords, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Switch(s, at, x + lead + UiTokens.S3, y, track, o.DayOpen, null, OpsDayOpen, 0, o.DayOpenDisabled);
            float hx = x + lead + UiTokens.S3 + track + UiTokens.S5;
            if (!string.IsNullOrEmpty(o.HoursWords) && right - hx > UiTokens.S16)
            {
                UiDraw.Icon(s, at, hx, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Clock, UiRole.None, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, at, hx + UiTokens.Icon + UiTokens.S2, y, UiTokens.Control, right - hx - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text2, o.HoursWords,
                    UiFlags.Truncate);
            }
        }

        // The people beside the board: who they are, what they are good at and how many hours they have this week; while an
        // hour is being filled, the game's picker in its order, its search and filter, each one a target that puts them there.
        private static void PeopleColumn(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float py = Title(s, at, x, y + (UiTokens.Control - UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader))) / 2f, w, o.PeopleTitle);
            py = Math.Max(py, y + UiTokens.Control + UiTokens.S1);
            py = Lead(s, at, o.PeopleLine, x, py, w);
            if (o.People != null)
            {
                UiDataTable.Lay(s, at, o.People, x, py, w, bottom - py, keys, People);
            }
        }

        // ------------------------------------------------------------------------------------------------- a manager kind

        // The kind's tabs across the top where it has them, its plans down the left with their actions at the foot, and the
        // chosen plan at the right: its title and what it covers, its form in two columns, its actions, then its goods or its
        // route's stops in the room left.
        private static void ManagersPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            if (o.Tabs.Length > 1)
            {
                UiDraw.Tabs(s, at, x, y, Math.Min(w, TabsWidth(o.Tabs)), o.Tabs, o.Tab, null, OpsTab);
                y += UiTokens.Control + UiTokens.S5;
            }
            float rx = x + PlansW + UiTokens.S8, rw = x + w - rx;
            _split = rx - UiTokens.S4;
            PlanList(s, at, o, x, y, PlansW, bottom, keys);
            if (o.Rows.Length == 0 && o.Table == null && o.Stops.Length == 0)
            {
                if (o.Plans.Length > 0 && !string.IsNullOrEmpty(o.FormEmpty))
                {
                    UiDraw.EmptyState(s, at, rx, y, rw, bottom - y, UiIcon.Receipt, o.FormEmpty, o.FormEmptyBody, null, -1, keys);
                }
                return;
            }
            Form(s, at, o, rx, y, rw, bottom, keys);
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
        private static void PlanList(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float actionsH = o.PlanActions.Length > 0 ? UiTokens.Control + UiTokens.S4 : 0f;
            if (o.Plans.Length == 0)
            {
                float ty = Title(s, at, x, y, w, o.PlansTitle);
                UiDraw.EmptyState(s, at, x, ty, w, bottom - ty - actionsH, UiIcon.Receipt, o.PlansEmpty, o.PlansEmptyBody, null, -1, keys);
            }
            else
            {
                Items(s, at, o.PlansTitle, o.Plans, x, y, w, bottom - actionsH, OpsPlan, -1, false, keys);
            }
            if (actionsH > 0f)
            {
                PlanActions(s, at, o.PlanActions, x, bottom - UiTokens.Control, w, keys);
            }
        }

        // The plans' actions on one row: the main one (add a plan) at the left, the moves as quiet buttons after it.
        private static void PlanActions(UiScene s, UiAt at, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            float bx = x;
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = Math.Min(UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys), x + w - bx);
                if (bw <= UiTokens.S8)
                {
                    break;
                }
                UiDraw.Button(s, at, bx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, OpsAction, a.Arg, a.Disabled);
                bx += bw + UiTokens.S3;
            }
        }

        // The chosen plan or machine: its title, its lead, its rows in two columns, its actions, then its stops or its table.
        private static void Form(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float fy = Title(s, at, x, y, w, o.FormTitle);
            fy = Lead(s, at, o.FormLead, x, fy, w);
            fy = Rows(s, at, o.Rows, x, fy, w, keys);
            if (o.Actions.Length > 0)
            {
                fy += UiTokens.S2;
                Buttons(s, at, o.Actions, x + w, fy, keys, OpsAction);
                fy += UiTokens.Control + UiTokens.S5;
            }
            if (o.Stops.Length > 0 || !string.IsNullOrEmpty(o.StopsTitle))
            {
                Stops(s, at, o, x, fy, w, bottom, keys);
                return;
            }
            if (o.Table != null && bottom - fy > UiTokens.S16)
            {
                fy = Title(s, at, x, fy, w, o.TableTitle);
                UiDataTable.Lay(s, at, o.Table, x, fy, w, bottom - fy, keys, Table);
            }
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
                        Label(s, at, x, y, lw, r.Label);
                        UiDraw.Button(s, at, cx, y, bw, UiTokens.Control, kind, r.Value, r.Icon, UiAction.Count, keys, OpsRow, i, r.Disabled);
                        y += UiTokens.Control + UiTokens.S2;
                        break;
                    }
                    default:
                        Label(s, at, x, y, lw, r.Label);
                        Control(s, at, ref r, i, cx, y, cw);
                        y += UiTokens.Control + UiTokens.S2;
                        break;
                }
                first = false;
            }
            return y;
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
                    UiDraw.Stepper(s, at, x, y, UiDraw.SnapDown(Math.Min(w, UiDataTable.StepperWidth)), r.Value ?? string.Empty, r.CanLess && !r.Disabled,
                        r.CanMore && !r.Disabled, OpsLess, OpsType, OpsMore, i, r.Disabled, r.Placeholder);
                    break;
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

        // A route's stops: each its tile, its place as a choice, its goods' count, its goods opened beneath it, and its remove;
        // a stop past the route's limit marked.
        private static void Stops(UiScene s, UiAt at, BizOpsModel o, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Title(s, at, x, y, w, o.StopsTitle);
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
                UiDraw.Choice(s, inside, cx, y + (StopRow - UiTokens.Control) / 2f, choiceW, st.Name, OpsStopChoice, i, false);
                if (st.Removable)
                {
                    UiDraw.Button(s, inside, rx, y + (StopRow - removeW) / 2f, removeW, removeW, UiButtonKind.Quiet, null, UiIcon.Trash, UiAction.Count, keys, OpsStopRemove, i);
                }
                float lw = cx - UiTokens.S3 - tx;
                UiDraw.Node(s, at, tx, y, lw, StopRow, OpsStop, i, row);
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
                y += StopRow;
                if (st.Open && st.Goods != null)
                {
                    for (int k = 0; k < st.Goods.Length && y + UiTokens.PairRow <= bottom; k++)
                    {
                        y += UiDraw.InfoRow(s, at, x + tile + UiTokens.S3, y, w - tile - UiTokens.S3 - removeW - UiTokens.S3, st.Goods[k], st.Amounts != null && k < st.Amounts.Length ? st.Amounts[k] : null);
                    }
                    y += UiTokens.S2;
                }
                y += UiTokens.S1;
            }
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

        // The machine groups as tabs, the group's machines as the big table at the left, and at the right the chosen machine's
        // recipe as a form, or the group's production when no machine is chosen.
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
            float half = Half(w), rx = x + half + UiTokens.S8;
            _split = rx - UiTokens.S4;
            float ty = Title(s, at, x, y, half, o.TableTitle);
            UiDataTable.Lay(s, at, o.Table, x, ty, half, bottom - ty, keys, Table);
            float fy = Title(s, at, rx, y, half, o.FormTitle);
            fy = Lead(s, at, o.FormLead, rx, fy, half);
            fy = Rows(s, at, o.Rows, rx, fy, half, keys);
            if (o.Actions.Length > 0)
            {
                Buttons(s, at, o.Actions, rx + half, fy + UiTokens.S2, keys, OpsAction);
            }
            OpenList(s, at, m, x, y, w, bottom);
        }

        // ------------------------------------------------------------------------------------------------- a warehouse

        // The vehicle slots as a form, each slot's vehicle and its driver as a choice, in two columns where there are many.
        private static void DriversPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            BizOpsModel o = m.Ops;
            if (o.Rows.Length == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Truck, o.PlansEmpty, o.PlansEmptyBody, null, -1, keys);
                return;
            }
            float fy = Title(s, at, x, y, w, o.FormTitle);
            fy = Lead(s, at, o.FormLead, x, fy, w);
            Rows(s, at, o.Rows, x, fy, w, keys);
            OpenList(s, at, m, x, y, w, bottom);
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
            float ty = y + UiTokens.Control + UiTokens.S5;
            if (o.Table != null)
            {
                UiDataTable.Lay(s, at, o.Table, x, ty, w, bottom - ty, keys, Table);
            }
        }
    }
}
