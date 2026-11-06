using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// BizMan on the kit, as a manager's office rather than a list of forms: an app window with a Football Manager sidebar.
    /// The portfolio's sidebar holds the player's kinds of place (businesses, homes, warehouses, headquarters) with their
    /// counts; its page is a slim line of what the portfolio comes to and the big table of the kind chosen, sorted and
    /// filtered as the game's own list is. A place's page has its sections down the sidebar (the game's tabs, another mod's
    /// among them) and opens with the firm's head (UiFirmHead), then the shown section in the shape its job needs (the parts
    /// in BizManPages.Parts.cs). Every word and figure comes from the model, which the game's side fills from the game's own
    /// widgets. Game-free. Cost: a layout a change.
    /// </summary>
    public static partial class BizManPages
    {
        public const int Section = UiAppWindow.NavCommand;
        public const int Action = 300, Type = 301, Less = 302, More = 303, FilterOption = 304, Period = 305, Item = 306, ItemLink = 307, Toggle = 308,
            Repeat = 309, Kind = 310, KindOption = 311, Font = 312, FontOption = 313, Uniforms = 314, FactLink = 315, Slider = 316, Classic = 317,
            Cancel = 319, Page = 320, Heading = 321, Pages = 322;
        public const int SecondColumn = 1000;         // a heading of the right column: its argument from here, the left's from 0

        // The logo editor's commands (this job's block 9600 to 9619; used 9600 to 9603 and 9610 to 9612).
        public const int LogoShape = 9600, LogoOwn = 9601, LogoColourAdd = 9602, LogoTab = 9603, LogoColour = 9610;   // a colour row's swatches: LogoColour plus the row (0 to 2)

        // What an action does, its argument: the game's side presses the game's own button for each.
        public const int DoRent = 1, DoStart = 2, DoTerminate = 3, DoPreview = 4, DoBuy = 5, DoTakeover = 6, DoBrowse = 7, DoEmployees = 8,
            DoRandom = 11, DoSave = 12, DoShutdown = 13, DoSaveLogo = 14, DoLogoEdit = 15, DoLogoDone = 28, DoApplyRent = 16, DoTakeoverUnit = 17, DoMarkSale = 18, DoCancelSale = 19,
            DoOrder = 20, DoUrgent = 21, DoCancelContract = 22, DoEndContract = 23, DoEconoView = 25, DoClassic = 26, DoRandomName = 27;
        public const int DoLine = 100;                // a button a page of lines offers (the Interior tab's), its index from here
        public const int StartKinds = 9800;           // the start page's kinds table, from here as UiDataTable's from its First (this job's block 9800 to 9819)
        public const int DoStartHere = 9810;              // the start page's own Start business, the game's SetUpBusiness
        public const int Goods = 400;                 // the goods' table's commands, from here as UiDataTable's from its First
        public const int FieldName = 0, FieldOffer = 1, FieldBuy = 2, FieldSale = 3;
        private const float SummaryH = UiTokens.Control;   // px: the portfolio's slim line

        /// <summary>Every command a BizMan page answers, for the checks' list of known targets.</summary>
        public static readonly int[] Commands =
        {
            Section, Action, Type, Less, More, FilterOption, Period, Item, ItemLink, Toggle, Repeat, Kind, KindOption, Font, FontOption, Uniforms, FactLink, Slider,
            Classic, Cancel, Page, Heading, Pages, LogoShape, LogoOwn, LogoColourAdd, LogoTab, LogoColour, LogoColour + 1, LogoColour + 2,
            UiFirmHead.First + UiFirmHead.Name, UiFirmHead.First + UiFirmHead.Option, UiFirmHead.First + UiFirmHead.Place, UiFirmHead.First + UiFirmHead.Switch,
            UiFirmHead.First + UiFirmHead.Todo, UiFirmHead.First + UiFirmHead.Action,
            UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Filter, UiDataTable.First + UiDataTable.Search, UiDataTable.First + UiDataTable.Row,
            UiDataTable.First + UiDataTable.Clear, UiDataTable.First + UiDataTable.Act,
            Goods + UiDataTable.Head, Goods + UiDataTable.Less, Goods + UiDataTable.Type, Goods + UiDataTable.More, Goods + UiDataTable.Row,
            UiChart.First + UiChart.Point, StartKinds + UiDataTable.Search, StartKinds + UiDataTable.Row, StartKinds + UiDataTable.Clear,
            OpsRow, OpsChoice, OpsOption, OpsLess, OpsType, OpsMore, OpsSlider, OpsPlan, OpsAction, OpsTab, OpsStop, OpsStopRemove, OpsStopChoice, OpsDayOpen, OpsSearch,
            OpsCancelFill, OpsFirstPlan, OpsHours, OpsHour, OpsSheetDone, OpsFilter, UiBoard.ShiftCommand, UiBoard.LaneCommand, UiBoard.DayCommand, UiBoard.ActionCommand,
            UiCard.ButtonCommand, UiCard.CheckCommand,
            People + UiDataTable.Head, People + UiDataTable.Filter, People + UiDataTable.Search, People + UiDataTable.Row, People + UiDataTable.Clear,
            Table + UiDataTable.Head, Table + UiDataTable.Filter, Table + UiDataTable.Search, Table + UiDataTable.Row, Table + UiDataTable.Clear,
            Table + UiDataTable.Pick, Table + UiDataTable.Mass, Table + UiDataTable.Done, Table + UiDataTable.Less, Table + UiDataTable.Type, Table + UiDataTable.More,
            Table + UiDataTable.Act, OpsFormTab, Crew + UiDataTable.Row
        };

        [ThreadStatic] private static float _split;   // where a page's own layout splits its two columns this layout (a photo's page), or NaN for the middle

        /// <summary>Lays BizMan out: the window and its sidebar, then the portfolio or the place's page, then the keys' bar.</summary>
        public static void Lay(UiScene s, BizManModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("bizman", frame.Width, frame.Height, dark);
            _split = float.NaN;
            _keptCount = 0;
            _headChain = false;
            // One section needs no sidebar: its page takes the window's width, and Back goes to the portfolio (or closes it).
            UiNavEntry[] nav = m.Nav.Length > 1 ? m.Nav : new UiNavEntry[0];
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, UiIcon.Building, nav, m.NavSelected, false, keys, out float x, out float y, out float w, out float bottom);
            int page = s.NodeCount;
            if (m.Page == BizManPage.Portfolio)
            {
                Portfolio(s, at, m, x, y, w, bottom, keys);
            }
            else
            {
                Business(s, at, m, x, y, w, bottom, keys);
            }
            float split = float.IsNaN(_split) ? x + w / 2f : _split;
            _split = float.NaN;
            UiPageFence.Lay(s, page, split, split);
            Restore(s);   // the schedule's board keeps its own way from a lane along its shifts
            if (m.Page == BizManPage.Business && m.Ops.Card != null)
            {
                // A question the game asks inside the page stands over it as its own boxes do, its prompt bar in place of the window's.
                UiCard.LayOver(s, m.Ops.Card, frame, keys);
                return;
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The page dimmed under a list or a card opened over it, so what it covers reads as behind it.</summary>
        private static void Behind(UiScene s, float x, float y, float w, float bottom)
        {
            UiDraw.Scrim(s, x - UiTokens.S5, y - UiTokens.S4, w + UiTokens.S5 * 2f, bottom - y + UiTokens.S4 * 2f, 1);
        }

        // ------------------------------------------------------------------------------------------------- the portfolio

        // The slim line of what the portfolio comes to, the big table of the kind chosen with the game's filters and search,
        // and an open filter's choices over it.
        private static void Portfolio(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Summary.Length > 0)
            {
                y = Summary(s, at, m, x, y, w);
            }
            if (m.Table.RowCount == 0 && m.Summary.Length == 0 && string.IsNullOrEmpty(m.Table.EmptyAction))
            {
                // Nothing owned yet: no tools and no heads over nothing, only what will fill the place and how.
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, m.Table.EmptyIcon, m.Table.EmptyTitle, m.Table.EmptyBody, null, -1, keys);
                return;
            }
            UiDataTable.Lay(s, at, m.Table, x, y, w, bottom - y, keys, UiDataTable.First);
            if (m.FilterOpen >= 0 && m.FilterOptions != null)
            {
                int field = s.FindNode(UiDataTable.First + UiDataTable.Filter, m.FilterOpen);
                if (field >= 0)
                {
                    Behind(s, x, y, w, bottom);
                    s.NodeBox(field, out float fx, out float fy, out float fw, out float fh);
                    UiDraw.ChoiceList(s, at, fx, fy + fh + UiTokens.S1, Math.Max(fw, UiTokens.S16 * 4f), m.FilterOptions, m.FilterOptions.Length, m.FilterChosen,
                        m.FilterOffset, FilterOption);
                }
            }
        }

        // What the portfolio comes to, on one band: each figure behind its glyph in the glyph's own colour, a step of room between.
        private static float Summary(UiScene s, UiAt at, BizManModel m, float x, float y, float w)
        {
            int band = UiDraw.Box(s, at, x, y, w, SummaryH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float cx = x + UiTokens.S4, right = x + w - UiTokens.S4;
            for (int i = 0; i < m.Summary.Length && cx < right; i++)
            {
                UiIcon icon = i < m.SummaryIcons.Length ? m.SummaryIcons[i] : UiIcon.None;
                if (icon != UiIcon.None)
                {
                    UiDraw.Icon(s, on, cx, y + (SummaryH - UiTokens.Icon) / 2f, UiTokens.Icon, icon, UiRole.None, UiFlags.Middle, true);
                    cx += UiTokens.Icon + UiTokens.S2;
                }
                float tw = Math.Min(UiDraw.Snap(UiTextRules.Width(m.Summary[i], UiStyle.BodyStrong) + UiTokens.S1), Math.Max(0f, right - cx));
                UiDraw.TextMiddle(s, on, cx, y, SummaryH, tw, UiStyle.BodyStrong, UiRole.Text, m.Summary[i], UiFlags.Truncate);
                cx += tw + UiTokens.S8;
            }
            return y + SummaryH + UiTokens.S4;
        }

        // ------------------------------------------------------------------------------------------------- a place's page

        // The firm's head, then the shown section in its own shape; the head's lists open last, over the page.
        private static void Business(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float under = m.Head != null ? UiDraw.Snap(y + UiFirmHead.Height + UiTokens.S6) : y;
            switch (m.Part)
            {
                case BizManPart.Start: StartPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Insight: InsightPage(s, at, m, x, under, w, bottom); break;
                case BizManPart.Pricing: PricingPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Deliveries: DeliveriesPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Marketing: MarketingPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Settings:
                    if (m.LogoEdit)
                    {
                        LogoPage(s, at, m, x, under, w, bottom, keys);
                    }
                    else
                    {
                        SettingsPage(s, at, m, x, under, w, bottom, keys);
                    }
                    break;
                case BizManPart.Estate: EstatePage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Classic: ClassicPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Interior: InteriorPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Schedule: SchedulePage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Managers: ManagersPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Factory: FactoryPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Drivers: DriversPage(s, at, m, x, under, w, bottom, keys); break;
                case BizManPart.Stock: StockPage(s, at, m, x, under, w, bottom, keys); break;
                default: BuildingPage(s, at, m, x, under, w, bottom, keys); break;
            }
            if (m.Head != null)
            {
                // Laid after the page so its lists open over it, as a choice's list does.
                if (m.Head.ListOpen || m.Head.TodoOpen)
                {
                    Behind(s, x, y, w, bottom);
                }
                UiFirmHead.Lay(s, at, m.Head, x, y, w, keys);
            }
        }

        // ------------------------------------------------------------------------------------------------- shared blocks

        /// <summary>A section's title in capitals over its block. Returns the y under it.</summary>
        private static float Title(UiScene s, UiAt at, float x, float y, float w, string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return y;
            }
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            return y + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
        }

        /// <summary>A section's title in capitals on the middle of a control's row, level with the controls beside it. Returns the y under the row.</summary>
        private static float RowTitle(UiScene s, UiAt at, float x, float y, float w, string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            }
            return y + UiTokens.Control + UiTokens.S2;
        }

        /// <summary>Facts as compact rows, a link's value a target that opens it. Returns the y under them.</summary>
        private static float Facts(UiScene s, UiAt at, BizManFact[] facts, float x, float y, float w)
        {
            for (int i = 0; i < facts.Length; i++)
            {
                ref BizManFact f = ref facts[i];
                if (string.IsNullOrEmpty(f.Value))
                {
                    continue;
                }
                y += UiDraw.InfoRow(s, at, x, y, w, f.Label, f.Value, f.Link, FactLink, f.Arg, f.Tone == UiRole.None ? UiRole.Text : f.Tone);
            }
            return y;
        }

        /// <summary>A picture of the game's on a slate tile, letterboxed in its own shape; its glyph when there is none.</summary>
        private static void Picture(UiScene s, UiAt at, string picture, float aspect, UiIcon glyph, float x, float y, float w, float h, float glyphSize = UiTokens.S16,
            bool logo = false)
        {
            if (!string.IsNullOrEmpty(picture) && aspect > 0f && w > h * aspect)
            {
                w = UiDraw.SnapDown(h * aspect);   // the frame takes the photo's own shape, so no slate stands either side of it
            }
            int tile = UiDraw.Box(s, at, x, y, w, h, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            var card = new UiCatalogueCard { Picture = picture, Aspect = aspect, Icon = glyph };
            int from = s.ItemCount;
            UiCatalogue.Picture(s, at.In(tile), ref card, x, y, w, h, glyphSize);
            if (logo)
            {
                UiFirmHead.Soften(s, from);
            }
        }

        /// <summary>The width a big table needs to show every column whole: each fixed one at its words, each shared one at a name's room.</summary>
        private static float TableNeeds(UiDataTableModel m)
        {
            float sum = UiTokens.S4;
            for (int c = 0; c < m.Columns.Length; c++)
            {
                ref UiDataColumn col = ref m.Columns[c];
                float words = UiTextRules.Width(col.Title, UiStyle.SectionHeader) + UiTokens.S1 + (col.Sortable ? UiTokens.IconSmall + UiTokens.S1 : 0f);
                sum += (col.Width > 0f ? Math.Max(col.Width, words) : Math.Max(UiTokens.S16 * 2.5f, words)) + (c > 0 ? UiTokens.S4 : 0f);
            }
            return UiDraw.Snap(sum);
        }

        /// <summary>Buttons ending at a right edge, the main one last. Returns the left edge of the first.</summary>
        private static float Buttons(UiScene s, UiAt at, UiActionModel[] actions, float right, float y, IUiKeys keys, int command)
        {
            for (int i = (actions == null ? 0 : actions.Length) - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            return right;
        }

        /// <summary>
        /// An amount to offer or set: its title in capitals, the line it is measured against, then the stepper and the button
        /// that sends it on one row. Returns the y under it.
        /// </summary>
        private static float Amount(UiScene s, UiAt at, BizManAmount a, float x, float y, float w, IUiKeys keys)
        {
            if (a == null)
            {
                return y;
            }
            y = Title(s, at, x, y, w, a.Title);
            string line = AmountLine(a);
            for (int start = 0; !string.IsNullOrEmpty(line) && start <= line.Length;)
            {
                int end = line.IndexOf('\n', start);
                end = end < 0 ? line.Length : end;
                UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, line.Substring(start, end - start), UiFlags.Label | UiFlags.Truncate);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
                start = end + 1;
            }
            y += string.IsNullOrEmpty(line) ? 0f : UiTokens.S2;
            float bw = string.IsNullOrEmpty(a.Send.Label) ? 0f : UiDraw.ButtonWidth(a.Send.Kind, a.Send.Label, a.Send.Icon, a.Send.Key, keys);
            float sw = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 4f, w - bw - (bw > 0f ? UiTokens.S3 : 0f)));
            string figure = a.Figure ?? string.Empty;   // an empty field stays empty: its line says what to type
            UiDraw.Stepper(s, at, x, y, sw, figure, a.CanLess, a.CanMore, Less, Type, More, a.Field, a.Disabled, a.Placeholder);
            if (bw > 0f)
            {
                UiDraw.Button(s, at, x + sw + UiTokens.S3, y, bw, UiTokens.Control, a.Send.Kind, a.Send.Label, a.Send.Icon, a.Send.Key, keys, Action, a.Send.Arg,
                    a.Send.Disabled || a.Disabled);
            }
            return y + UiTokens.Control + UiTokens.S6;
        }

        // The lines over an amount, one a fact: whose it is, its price, what it is measured against.
        private static string AmountLine(BizManAmount a) => a.Line;

        /// <summary>A sentence over a block, wrapped to its width in the quiet colour. Returns the y under it.</summary>
        private static float Lead(UiScene s, UiAt at, string words, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(words))
            {
                return y;
            }
            float h = UiDraw.Snap(UiTextRules.WrappedHeight(words, UiStyle.Body, w, 3));
            UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, words, UiFlags.Label | UiFlags.Wrap, 3);
            return y + h + UiTokens.S4;
        }
    }
}
