using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A share of money by kind, as a donut with its legend: its label, each kind's words and money, and their shares.</summary>
    public sealed class UiEconoShare
    {
        public string Title;
        public string[] Labels = new string[0];
        public string[] Values = new string[0];
        public float[] Shares = new float[0];
        public string Empty;          // the sentence in its room when nothing moved that way
    }

    /// <summary>
    /// A control on a band of them, each on one row: its words at the left and the game's control at the right (an amount
    /// between a minus and a plus, or a switch), with the button that acts on it after it where it has one.
    /// </summary>
    public struct UiEconoControl
    {
        public string Label;
        public bool Switch;           // a switch rather than an amount
        public bool On;               // the switch's state
        public string Word;           // the switch's state in words, or null
        public string Figure;         // the amount as the game's field writes it
        public bool CanLess, CanMore;
        public bool Disabled;
        public string Action;         // the button after it ("Pay off"), or null
        public UiButtonKind ActionKind;
        public UiIcon ActionIcon;
        public UiAction ActionKey;    // F for the page's main one, Count for none
        public bool ActionDisabled;
    }

    /// <summary>
    /// What EconoView shows now, as the money app's page draws it: its tabs, then the page of the tab in view (the
    /// overview's income statement, cash and shares; the full statement; a loan, a fund, the taxes; one business's
    /// statement), an open list over a choice, and the prompts. Plain data.
    /// </summary>
    public sealed class UiEconoModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.ChartPie;
        public UiNavEntry[] Tabs = new UiNavEntry[0];
        public int Tab;
        public int Page;                                  // UiEcono.Overview and the rest
        public bool NewPage;                              // the page changed since the last layout: focus starts on its tab again

        // A statement: the income statement (overview), every transaction (transactions) or one business's (business), with
        // the row of the screen's own rows each of its groups and lines stands for, so a press reaches the game's row.
        public UiStatementModel Statement = new UiStatementModel();
        public int[] GroupRows = new int[0];
        public int[][] GroupLines = new int[0][];

        // The overview's right: the cash with its line, where the money came from and went, and the last few lines.
        public UiFigureModel[] Cash = new UiFigureModel[0];
        public UiEconoShare In, Out;
        public string LatestTitle;
        public UiColumn[] LatestColumns = new UiColumn[0];
        public UiAppRow[] Latest = new UiAppRow[0];
        public string LatestLink;                         // the way to every transaction

        // The full statement's tool row: its filters as choices, and its buttons (export, close).
        public string[] Filters = new string[0];
        public UiActionModel[] ToolActions = new UiActionModel[0];

        // A page of a few things to choose one from (loans, funds, taxes due) and the chosen one beside, with the figures
        // over the list that sum them all, and the game's own records of the chosen one under its controls.
        public UiFigureModel[] Totals = new UiFigureModel[0];
        public UiPickListModel Picks;
        public UiEconoShare Split;                        // how the sum shares out between them (owed to each bank), under the list
        public UiHeroModel Hero;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public string RowsTitle;
        public string[] RowLabels = new string[0];
        public string[] RowValues = new string[0];
        public string ControlsTitle;
        public UiEconoControl[] Controls = new UiEconoControl[0];
        public UiChartModel Chart;
        public string NoteTitle;                          // a band of lines under the rows (the taxes' deductible kinds): its label
        public string Note;                               // and its lines
        public string Link;                               // a link under the rows (the IRS contact)
        public string HistoryTitle;
        public UiColumn[] HistoryColumns = new UiColumn[0];
        public UiAppRow[] History = new UiAppRow[0];
        public string HistoryEmpty;                       // the sentence under its head when there is nothing yet

        // One business: its choice in the head, what it is, its link and its buttons.
        public string Choice;
        public string ChoiceLine;
        public UiActionModel[] HeadActions = new UiActionModel[0];

        // A tab with nothing in it yet: its glyph and its sentence fill the page.
        public UiIcon EmptyIcon = UiIcon.Chart;
        public string EmptyTitle;
        public string EmptyBody;

        public int OpenList = -1;                         // UiEcono.ListType and the rest, or -1
        public string[] Options = new string[0];
        public int OptionChosen = -1;
        public float OptionOffset;
        public UiPrompt[] Prompts = new UiPrompt[0];

        // Written back as it is laid out: the lists in the scene, and where each choice stood so its list opens under it.
        public short StatementList = -1, PickList = -1, OptionList = -1;
        public readonly float[] FieldX = new float[ListCount], FieldY = new float[ListCount], FieldW = new float[ListCount];
        public const int ListCount = 4;
    }

    /// <summary>
    /// EconoView, the game's money app, on the kit: an app window with icon tabs across, its pages each shaped as a bank's
    /// papers are rather than as a dashboard. The overview is the income statement as a statement whose headings fold, with
    /// the cash and its line, a donut of where the money came from and one of where it went, and the last few transactions
    /// beside it; every transaction is the same statement grouped by day under its filter row; loans and funds are a short
    /// list to choose from with the chosen one beside it (its picture, its figures, its rows and the game's controls: an
    /// amount between a minus and a plus, a switch, the button that acts); the taxes are what is due and when beside what
    /// they stand on; one business is its statement under its choice. Commands: the window's (<see cref="UiAppWindow.NavCommand"/>
    /// a tab, <see cref="UiAppWindow.ActionCommand"/> a head or tool button, <see cref="UiAppWindow.RowCommand"/> a last
    /// transaction), the statement's from <see cref="StatementFirst"/>, the chart's from <see cref="UiChart.First"/>, and
    /// this page's own (<see cref="PickCommand"/> to <see cref="ChoiceCommand"/>). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiEcono
    {
        public const int Overview = 0, Transactions = 1, Loans = 2, Investments = 3, Taxes = 4, Business = 5;
        public const int ListType = 0, ListAmount = 1, ListPeriod = 2, ListBusiness = 3;
        public const int StatementFirst = UiStatement.First;
        public const int PickCommand = 210, FilterCommand = 211, OptionCommand = 212, LessCommand = 213, AmountCommand = 214, MoreCommand = 215,
            SwitchCommand = 216, ButtonCommand = 217, LinkCommand = 218, ChoiceCommand = 219;
        public const int LinkAll = 0, LinkIrs = 1, LinkBizMan = 2;   // LinkCommand's argument

        private const float StatementShare = 0.62f;   // of the overview's width, the income statement's
        private const float PickShare = 0.4f;         // of a list page's width, the list's
        private const float TaxShare = 0.58f;         // of the taxes' width, what is due
        private const float Donut = 104f;             // px: a share's donut, its legend's five rows beside it
        private const float LegendRow = 20f;          // px: UiDraw.Legend's row
        private const float LegendMost = 440f;        // px: a legend's widest
        private const float TypeField = 280f;         // px: the type filter's choice
        private const float AmountField = 200f;       // px: the amount filter's choice
        private const float BusinessField = 360f;     // px: the business's choice in its head
        private const float StepperW = 240f;          // px: an amount between its minus and plus
        private const float ChartH = 240f;            // px: a fund's year as a line
        private const float FigureShare = 0.32f;      // of the chosen's width, its figures beside its picture

        public static void Lay(UiScene s, UiEconoModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("econoview", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Tabs, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            if (m.NewPage)
            {
                s.FocusReset = true;   // a new page: the keys start on its tab, not on the old page's target of the same number
            }
            m.StatementList = -1;
            m.PickList = -1;
            m.OptionList = -1;
            for (int i = 0; i < UiEconoModel.ListCount; i++)
            {
                m.FieldW[i] = 0f;
            }
            if (!string.IsNullOrEmpty(m.EmptyTitle))
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, -1, keys);
            }
            else
            {
                switch (m.Page)
                {
                    case Transactions: TransactionsPage(s, at, m, x, y, w, bottom, keys); break;
                    case Loans:
                    case Investments: ChosenPage(s, at, m, x, y, w, bottom, keys); break;
                    case Taxes: TaxesPage(s, at, m, x, y, w, bottom); break;
                    case Business: BusinessPage(s, at, m, x, y, w, bottom, keys); break;
                    default: OverviewPage(s, at, m, x, y, w, bottom, keys); break;
                }
            }
            OpenList(s, at, m);
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // ------------------------------------------------------------------------------------------------- the overview

        // The income statement at the left; at the right the cash with its line, where the money came from and went, and
        // the last few transactions with the way to every one.
        private static void OverviewPage(UiScene s, UiAt at, UiEconoModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float lw = UiDraw.SnapDown(w * StatementShare), rx = x + lw + UiTokens.S6, rw = x + w - rx;
            m.StatementList = UiStatement.Lay(s, at, m.Statement, x, y, lw, bottom - y, keys, StatementFirst);
            Field(s, m, ListPeriod, StatementFirst + UiStatement.Period);
            float ry = UiAppWindow.Figures(s, at, m.Cash, rx, y, rw);
            ry = Share(s, at, m.In, rx, ry, rw) + UiTokens.S4;
            ry = Share(s, at, m.Out, rx, ry, rw) + UiTokens.S5;
            float linkH = string.IsNullOrEmpty(m.LatestLink) ? 0f : UiTokens.Control;
            float under = ry;
            if (m.LatestColumns.Length > 0)
            {
                under = UiAppWindow.Table(s, at, m.LatestTitle, m.LatestColumns, 2, m.Latest, UiAppWindow.RowCommand, rx, ry, rw, bottom - ry - linkH);
                if (m.Latest.Length == 0)
                {
                    under = Math.Min(bottom - linkH, under + UiTokens.S2);
                }
            }
            if (linkH > 0f)
            {
                UiDraw.LinkRow(s, at, rx, under, rw, linkH, m.LatestLink, LinkCommand, LinkAll);
            }
            UiDraw.JoinColumns(s, x, rx - UiTokens.S3, x + w);
        }

        /// <summary>A share's band's height: its label, its donut and a step of room round them.</summary>
        public static float ShareHeight => UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3 + Donut + UiTokens.S4;

        /// <summary>
        /// A share of money by kind on its band: its label in capitals, the donut in the chart hues and its legend beside it
        /// with each kind's money, or its sentence when nothing moved that way. Returns the y under it.
        /// </summary>
        public static float Share(UiScene s, UiAt at, UiEconoShare share, float x, float y, float w)
        {
            if (share == null)
            {
                return y;
            }
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float h = ShareHeight;
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            UiDraw.Text(s, on, ix, y + UiTokens.S3, iw, UiStyle.SectionHeader, UiRole.Text, share.Title, UiFlags.Label | UiFlags.Truncate);
            float dy = y + UiTokens.S3 + lh + UiTokens.S3;
            int count = Math.Min(share.Labels.Length, Math.Min(share.Values.Length, share.Shares.Length));
            UiDraw.Donut(s, on, ix, dy, Donut, share.Shares, count);
            float lx = ix + Donut + UiTokens.S6, lw = Math.Min(ix + iw - lx, LegendMost);   // a wide band keeps each kind's money near its name
            if (count == 0)
            {
                UiDraw.Text(s, on, lx, dy + UiDraw.SnapDown((Donut - UiTextRules.WrappedHeight(share.Empty, UiStyle.Body, lw, 3)) / 2f), lw, UiStyle.Body, UiRole.Text2,
                    share.Empty, UiFlags.Label | UiFlags.Wrap, 3);
            }
            else
            {
                UiDraw.Legend(s, on, lx, dy + UiDraw.SnapDown((Donut - count * LegendRow) / 2f), lw, share.Labels, share.Values, count);
            }
            return y + h;
        }

        // ------------------------------------------------------------------------------------------------- every transaction

        // The filter row (the kinds, money in or out) with export and close at its right, then every transaction as the
        // statement grouped by day, its days' choice in its head.
        private static void TransactionsPage(UiScene s, UiAt at, UiEconoModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float h = UiTokens.Control, fx = x;
            float[] widths = { TypeField, AmountField };
            for (int i = 0; i < m.Filters.Length && i < widths.Length; i++)
            {
                UiDraw.Choice(s, at, fx, y, widths[i], m.Filters[i], FilterCommand, i, false);
                m.FieldX[i] = fx;
                m.FieldY[i] = y;
                m.FieldW[i] = widths[i];
                fx += widths[i] + UiTokens.S2;
            }
            Buttons(s, at, m.ToolActions, x + w, y, keys);
            float ty = y + h + UiTokens.S4;
            m.StatementList = UiStatement.Lay(s, at, m.Statement, x, ty, w, bottom - ty, keys, StatementFirst);
            Field(s, m, ListPeriod, StatementFirst + UiStatement.Period);
        }

        // ------------------------------------------------------------------------------------------------- a loan or a fund

        // At the left all of them: their sums, the list to choose from and how the sum shares out between them; at the right
        // the chosen one: its picture and figures or its year as a line, the game's controls for it, its rows, and the
        // game's records of it.
        private static void ChosenPage(UiScene s, UiAt at, UiEconoModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float lw = UiDraw.SnapDown(w * PickShare), rx = x + lw + UiTokens.S6, rw = x + w - rx;
            float ly = UiAppWindow.Figures(s, at, m.Totals, x, y, lw);
            ly = Picks(s, at, m, x, ly, lw, bottom) + UiTokens.S5;
            if (m.Split != null && bottom - ly >= ShareHeight)
            {
                Share(s, at, m.Split, x, ly, lw);
            }
            float ry = y;
            if (m.Hero != null)
            {
                float fw = m.Figures.Length > 0 ? UiDraw.SnapDown(rw * FigureShare) : 0f;
                float hh = UiAppWindow.Hero(s, at, m.Hero, rx, ry, fw > 0f ? rw - fw - UiTokens.S4 : rw);
                if (fw > 0f)
                {
                    UiAppWindow.Figures(s, at, m.Figures, rx + rw - fw, ry, fw, hh);
                }
                ry += hh + UiTokens.S5;
            }
            if (m.Chart != null)
            {
                UiChart.Lay(s, at, m.Chart, rx, ry, rw, ChartH);
                ry += ChartH + UiTokens.S5;
            }
            ry = Controls(s, at, m.ControlsTitle, m.Controls, rx, ry, rw, keys);
            ry = Rows(s, at, m.RowsTitle, m.RowLabels, m.RowValues, rx, ry, rw, 2);
            History(s, at, m, rx, ry, rw, bottom);
            UiDraw.JoinColumns(s, x, rx - UiTokens.S3, x + w);
        }

        // The list of things to choose from, no taller than its rows (the room under it is the next block's). Returns the y under it.
        private static float Picks(UiScene s, UiAt at, UiEconoModel m, float x, float y, float w, float bottom)
        {
            if (m.Picks == null)
            {
                return y;
            }
            float h = Math.Min(bottom - y, UiPickList.Height(m.Picks, m.Picks.Rows.Length));
            m.PickList = UiPickList.Lay(s, at, m.Picks, x, y, w, h, PickCommand);
            return y + h;
        }

        // The game's own records of the thing in view as a table under its title, or the sentence that says there are none yet.
        private static void History(UiScene s, UiAt at, UiEconoModel m, float x, float y, float w, float bottom)
        {
            if (m.HistoryColumns.Length == 0 || bottom - y < UiTokens.S16)
            {
                return;
            }
            float under = UiAppWindow.Table(s, at, m.HistoryTitle, m.HistoryColumns, 2, m.History, UiAppWindow.MoreCommand, x, y, w, bottom - y);
            if (!string.IsNullOrEmpty(m.HistoryEmpty))
            {
                UiDraw.Text(s, at, x, under + UiTokens.S3, w, UiStyle.Body, UiRole.Text2, m.HistoryEmpty, UiFlags.Label | UiFlags.Wrap, 2);
            }
        }

        /// <summary>
        /// Rows of a thing on its band under a label in capitals: its labels in the quiet grey blue and its values in soft
        /// white, running down as many columns as asked so a short block fills its width. Returns the y under it.
        /// </summary>
        public static float Rows(UiScene s, UiAt at, string title, string[] labels, string[] values, float x, float y, float w, int columns)
        {
            int n = Math.Min(labels.Length, values.Length);
            if (n == 0)
            {
                return y;
            }
            columns = Math.Max(1, Math.Min(columns, n));
            int rows = (n + columns - 1) / columns;
            float lh = string.IsNullOrEmpty(title) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            float h = UiTokens.S3 + lh + rows * UiTokens.PairRow + UiTokens.S3;
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, gap = UiTokens.S8;
            float cw = UiDraw.SnapDown((iw - gap * (columns - 1)) / columns);
            if (lh > 0f)
            {
                UiDraw.Text(s, on, ix, y + UiTokens.S3, iw, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            }
            float ty = y + UiTokens.S3 + lh;
            for (int i = 0; i < n; i++)
            {
                UiDraw.InfoRow(s, on, ix + (i / rows) * (cw + gap), ty + (i % rows) * UiTokens.PairRow, cw, labels[i], values[i]);
            }
            return y + h + UiTokens.S5;
        }

        /// <summary>
        /// The game's controls for the thing in view on their band, one a row: its words at the left, and at the right an
        /// amount between a minus and a plus (typed through the host) or a switch, then its button. Returns the y under it.
        /// </summary>
        public static float Controls(UiScene s, UiAt at, string title, UiEconoControl[] controls, float x, float y, float w, IUiKeys keys)
        {
            if (controls == null || controls.Length == 0)
            {
                return y;
            }
            float lh = string.IsNullOrEmpty(title) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            float rowH = UiTokens.Control, gap = UiTokens.S2;
            float h = UiTokens.S3 + lh + controls.Length * rowH + (controls.Length - 1) * gap + UiTokens.S3;
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            if (lh > 0f)
            {
                UiDraw.Text(s, on, ix, y + UiTokens.S3, iw, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            }
            // One column of controls and one of buttons for every row, so the fields and the buttons stand at one edge each.
            float bw = 0f;
            for (int i = 0; i < controls.Length; i++)
            {
                if (!string.IsNullOrEmpty(controls[i].Action))
                {
                    bw = Math.Max(bw, UiDraw.ButtonWidth(controls[i].ActionKind, controls[i].Action, controls[i].ActionIcon, controls[i].ActionKey, keys));
                }
            }
            float right = ix + iw, cx = right - (bw > 0f ? bw + UiTokens.S3 : 0f) - StepperW;
            float ry = y + UiTokens.S3 + lh;
            for (int i = 0; i < controls.Length; i++)
            {
                ref UiEconoControl c = ref controls[i];
                UiDraw.TextMiddle(s, on, ix, ry, rowH, cx - UiTokens.S4 - ix, UiStyle.Body, c.Disabled ? UiRole.Muted : UiRole.Text2, c.Label, UiFlags.Label | UiFlags.Truncate);
                if (c.Switch)
                {
                    UiDraw.Switch(s, on, cx, ry, StepperW, c.On, c.Word, SwitchCommand, i, c.Disabled);
                }
                else
                {
                    UiDraw.Stepper(s, on, cx, ry, StepperW, c.Figure, c.CanLess, c.CanMore, LessCommand, AmountCommand, MoreCommand, i, c.Disabled);
                }
                if (!string.IsNullOrEmpty(c.Action))
                {
                    UiDraw.Button(s, on, right - bw, ry, bw, rowH, c.ActionKind, c.Action, c.ActionIcon, c.ActionKey, keys, ButtonCommand, i, c.ActionDisabled);
                }
                ry += rowH + gap;
            }
            return y + h + UiTokens.S5;
        }

        // ------------------------------------------------------------------------------------------------- the taxes

        // What is due and when at the left, a row each with its time through the period as a meter, and what this year's tax
        // is on as a donut under it; at the right how it is worked out: the rate, the deductions and the kinds that count, the
        // way to the IRS and the taxes paid.
        private static void TaxesPage(UiScene s, UiAt at, UiEconoModel m, float x, float y, float w, float bottom)
        {
            float lw = UiDraw.SnapDown(w * TaxShare), rx = x + lw + UiTokens.S6, rw = x + w - rx;
            float ly = Picks(s, at, m, x, y, lw, bottom) + UiTokens.S6;
            if (m.Split != null && bottom - ly >= ShareHeight)
            {
                Share(s, at, m.Split, x, ly, lw);
            }
            float ry = Rows(s, at, m.RowsTitle, m.RowLabels, m.RowValues, rx, y, rw, 1);
            ry = Lines(s, at, m.NoteTitle, m.Note, rx, ry, rw);
            if (!string.IsNullOrEmpty(m.Link))
            {
                UiDraw.LinkRow(s, at, rx, ry, rw, UiTokens.Control, m.Link, LinkCommand, LinkIrs);
                ry += UiTokens.Control + UiTokens.S5;
            }
            History(s, at, m, rx, ry, rw, bottom);
            UiDraw.JoinColumns(s, x, rx - UiTokens.S3, x + w);
        }

        /// <summary>A band of plain lines under a label in capitals (the kinds of costs that lower the tax). Returns the y under it.</summary>
        public static float Lines(UiScene s, UiAt at, string title, string lines, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(lines))
            {
                return y;
            }
            float iw = w - UiTokens.S4 * 2f;
            float lh = string.IsNullOrEmpty(title) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            float th = UiDraw.Snap(UiTextRules.WrappedHeight(lines, UiStyle.Body, iw, 16));
            float h = UiTokens.S3 + lh + th + UiTokens.S3;
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            if (lh > 0f)
            {
                UiDraw.Text(s, on, x + UiTokens.S4, y + UiTokens.S3, iw, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            }
            UiDraw.Text(s, on, x + UiTokens.S4, y + UiTokens.S3 + lh, iw, UiStyle.Body, UiRole.Text2, lines, UiFlags.Label | UiFlags.Wrap, 16);
            return y + h + UiTokens.S5;
        }

        // ------------------------------------------------------------------------------------------------- one business

        // The business as a choice with what it is beside it, its buttons and the way to it in BizMan at the right; under them
        // its statement at the left, and at the right its sales with their line, where its money went, and its profit by day.
        private static void BusinessPage(UiScene s, UiAt at, UiEconoModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float h = UiTokens.Control;
            float cw = Math.Min(BusinessField, UiDraw.SnapDown(w * 0.3f));
            UiDraw.Choice(s, at, x, y, cw, m.Choice, ChoiceCommand, 0, false);
            m.FieldX[ListBusiness] = x;
            m.FieldY[ListBusiness] = y;
            m.FieldW[ListBusiness] = cw;
            float right = Buttons(s, at, m.HeadActions, x + w, y, keys);
            if (!string.IsNullOrEmpty(m.Link))
            {
                float linkW = Math.Min(UiDraw.Snap(UiTextRules.Width(m.Link, UiStyle.BodyStrong) + UiTokens.S1 + UiDraw.LinkGap + UiTokens.IconSmall), UiDraw.SnapDown((right - x - cw) / 2f));
                UiDraw.LinkRow(s, at, right - UiTokens.S4 - linkW, y, linkW, h, m.Link, LinkCommand, LinkBizMan);
                right -= UiTokens.S4 + linkW + UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(m.ChoiceLine))
            {
                float lx = x + cw + UiTokens.S4;
                UiDraw.TextMiddle(s, at, lx, y, h, right - UiTokens.S4 - lx, UiStyle.Body, UiRole.Text2, m.ChoiceLine, UiFlags.Truncate);
            }
            float ty = y + h + UiTokens.S4;
            bool side = m.Chart != null || m.Out != null || m.Cash.Length > 0;
            float lw = side ? UiDraw.SnapDown(w * StatementShare) : w, rx = x + lw + UiTokens.S6, rw = x + w - rx;
            m.StatementList = UiStatement.Lay(s, at, m.Statement, x, ty, lw, bottom - ty, keys, StatementFirst);
            Field(s, m, ListPeriod, StatementFirst + UiStatement.Period);
            if (!side)
            {
                return;
            }
            float ry = UiAppWindow.Figures(s, at, m.Cash, rx, ty, rw);
            ry = Share(s, at, m.Out, rx, ry, rw) + UiTokens.S5;
            if (m.Chart != null && bottom - ry >= UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, m.Chart, rx, ry, rw, bottom - ry);
            }
            UiDraw.JoinColumns(s, x, rx - UiTokens.S3, x + w);
        }

        // ------------------------------------------------------------------------------------------------- the parts

        // Buttons ending at a right edge, the main one last. Returns the left edge of the first.
        private static float Buttons(UiScene s, UiAt at, UiActionModel[] actions, float right, float y, IUiKeys keys)
        {
            for (int i = (actions == null ? 0 : actions.Length) - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, UiAppWindow.ActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            return right;
        }

        // Where a choice the statement drew stands, read off its target, so its list opens under it.
        private static void Field(UiScene s, UiEconoModel m, int list, int command)
        {
            for (int i = s.NodeCount - 1; i >= 0; i--)
            {
                if (s.Nodes[i].Command == command)
                {
                    s.NodeBox(i, out float nx, out float ny, out float nw, out float _);
                    m.FieldX[list] = nx;
                    m.FieldY[list] = ny;
                    m.FieldW[list] = nw;
                    return;
                }
            }
        }

        // An open choice's list on the overlay under its own field.
        private static void OpenList(UiScene s, UiAt at, UiEconoModel m)
        {
            if (m.OpenList < 0 || m.OpenList >= UiEconoModel.ListCount || m.Options.Length == 0 || m.FieldW[m.OpenList] <= 0f)
            {
                return;
            }
            int l = m.OpenList;
            m.OptionList = UiDraw.ChoiceList(s, at, m.FieldX[l], m.FieldY[l] + UiTokens.Control + UiTokens.S1, m.FieldW[l], m.Options, m.Options.Length, m.OptionChosen,
                m.OptionOffset, OptionCommand);
        }
    }
}
