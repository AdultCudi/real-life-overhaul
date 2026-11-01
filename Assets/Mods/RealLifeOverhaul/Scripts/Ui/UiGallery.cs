using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The words and samples the gallery shows; the game fills them from the locale (UiGalleryScreen), the checks from fixtures.</summary>
    public sealed class UiGalleryModel
    {
        public string Title;
        public string[] Pages = new string[0];
        public int Page;
        public string[] Captions = new string[0];       // each panel's title, by UiGallery.Caption*
        public string[] Buttons = new string[0];        // primary, secondary, destructive, armed, chip, quiet, disabled
        public UiNavModel[] Nav = new UiNavModel[0];
        public string[] Tabs = new string[0];
        public string SearchPlaceholder;
        public string[] Fields = new string[0];         // a toggle on, a toggle off, a slider, a choice, another choice
        public string SliderFigure;
        public string ChoiceValue;
        public string MotionValue;                      // the second choice's value
        public string Query;                            // words typed into the second search box
        public UiRowModel[] Rows = new UiRowModel[0];
        public UiServiceModel[] Services = new UiServiceModel[0];
        public UiChoiceModel[] Choices = new UiChoiceModel[0];
        public UiPairModel[] Pairs = new UiPairModel[0];
        public UiTableModel Table = new UiTableModel();
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public UiStatModel[] Stats = new UiStatModel[0];
        public string StepLabel;
        public string[] Statuses = new string[0];       // quiet, ok, warn, danger
        public string[] AvatarNames = new string[0];
        public UiMessageModel[] Messages = new UiMessageModel[0];
        public string[] Replies = new string[0];
        public string ComposerPlaceholder;
        public string Draft;
        public string SendLabel;
        public string EmptyTitle;
        public string EmptyBody;
        public string BusyLabel;
        public string QuestionTitle;
        public string QuestionBody;
        public UiActionModel[] Answers = new UiActionModel[0];
        public string CallName;
        public string CallState;
        public UiServiceModel[] CallOptions = new UiServiceModel[0];
        public string CallEnd;
        public string ToastTitle;
        public string ToastSub;
        public string[] TokenCaptions = new string[0];  // the tokens' panels: type, colours, motion, sizes
        public string[] TokenColumns = new string[0];   // their columns, by UiGallery.Column*
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The kit's gallery: every component in its states, a page a family (controls, lists, figures, messages, a question,
    /// a call), the tab keys turning the pages and Option turning the look, so the owner sees the whole kit in both themes
    /// from Options, Tests. Each page is a row of framed panels with a caption, every panel of a row as tall as the
    /// fullest one's content, so no frame holds a hole, and under them a table of the kit's own tokens (its type, colours,
    /// motion and sizes) that takes the room left in whole rows, so a page reaches its band without padding. Commands:
    /// <see cref="PageCommand"/> (a tab), <see cref="Answer"/>, <see cref="CallOption"/> and <see cref="CallEnd"/> (the
    /// question and the call close as real ones do) and <see cref="Sample"/> (anything else: the gallery only shows).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiGallery
    {
        public const int PageCommand = 1, Sample = 2, Answer = 3, CallOption = 4, CallEnd = 5;
        public const int CaptionButtons = 0, CaptionNav = 1, CaptionInputs = 2, CaptionRows = 3, CaptionCards = 4, CaptionData = 5, CaptionFigures = 6,
            CaptionStats = 7, CaptionStatus = 8, CaptionThread = 9, CaptionStates = 10, Captions = 11;
        public const int PageCount = 6;
        public const int TokensType = 0, TokensColours = 1, TokensMotion = 2, TokensSizes = 3, TokenKinds = 4;
        public const int ColumnStyle = 0, ColumnFace = 1, ColumnSize = 2, ColumnWeight = 3, ColumnRole = 4, ColumnDark = 5, ColumnLight = 6, ColumnMotion = 7,
            ColumnFull = 8, ColumnCalm = 9, ColumnToken = 10, ColumnPx = 11, ColumnKind = 12, Columns = 13;

        /// <summary>Lays the gallery's page out into a scene.</summary>
        public static void Lay(UiScene s, UiGalleryModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("gallery", frame.Width, frame.Height, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt at = screen.In(bg);
            // The masthead names the gallery already: its pages' tabs head the page, with no title repeating it.
            float tabsW = TabsWidth(m.Pages, keys);
            UiDraw.Tabs(s, at, cx, cy, tabsW, m.Pages, m.Page, keys, PageCommand);
            float y = cy + UiTokens.Control + UiTokens.S5;
            float h = UiDraw.SnapDown(cy + ch - y);
            float under;
            switch (m.Page)
            {
                case 0: under = Controls(s, at, m, cx, y, cw, keys); break;
                case 1: under = Lists(s, at, m, cx, y, cw, h, keys); break;
                case 2: under = Figures(s, at, m, cx, y, cw, keys); break;
                case 3: under = Messages(s, at, m, cx, y, cw); break;
                case 4: under = Lists(s, at, m, cx, y, cw, h, keys); break;
                default: under = Messages(s, at, m, cx, y, cw); break;
            }
            // The pages under the question and the call show the tokens their own pages show.
            Tokens(s, at, m, cx, under + UiTokens.S4, cw, cy + ch - under - UiTokens.S4, m.Page == 4 ? TokensColours : m.Page == 5 ? TokensSizes : m.Page);
            if (m.Page == 4)
            {
                UiDraw.Question(s, 0f, frame.ContentTop, frame.Width, frame.BandY - frame.ContentTop, m.QuestionTitle, m.QuestionBody, m.Answers, m.Answers.Length, keys);
            }
            else if (m.Page == 5)
            {
                UiDraw.Call(s, cx, y, cw, h, m.CallName, 12f, m.CallState, true, m.CallOptions, m.CallOptions.Length, m.CallEnd, CallOption, CallEnd,
                    0f, frame.ContentTop, frame.Width, frame.BandY - frame.ContentTop);
                UiDraw.Toast(s, 0f, frame.ContentTop, frame.Width, frame.BandY - frame.ContentTop, m.ToastTitle, m.ToastSub);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
            UiDraw.Chrome(s, frame);
        }

        // A panel with its caption, its height set once its content is in (Fit); returns its index and the first line under the
        // caption. A panel whose content names itself (a figure's own label) takes no caption, so no label is said twice.
        private static int Panel(UiScene s, UiAt at, float x, float y, float w, string caption, out float top)
        {
            int p = UiDraw.Box(s, at, x, y, w, UiTokens.S16, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            if (caption == null)
            {
                top = y + UiTokens.S5;
                return p;
            }
            UiDraw.Text(s, at.In(p), x + UiTokens.S5, y + UiTokens.S5, w - UiTokens.S5 * 2f, UiStyle.SectionHeader, UiRole.Muted, caption, UiFlags.Label | UiFlags.Truncate);
            top = y + UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S4;
            return p;
        }

        // Every panel of a row as tall as the fullest one: its padding under the lowest content of any (what scrolls given as lowest).
        // Each panel as tall as its own content (the last one at least to the floor given, a table's foot under its rows),
        // and those nearly as tall as the fullest levelled with it: one foot reads tidier, while a panel far shorter keeps
        // its own height rather than a hole under its last line. Returns the row's height.
        private static float Fit(UiScene s, float y, float lowest, params int[] panels)
        {
            var own = new float[panels.Length];
            float most = 0f;
            for (int i = 0; i < panels.Length; i++)
            {
                float foot = Math.Max(UiDraw.Lowest(s, panels[i]), i == panels.Length - 1 ? lowest : y);
                own[i] = UiDraw.Snap(foot - y + UiTokens.S5);
                most = Math.Max(most, own[i]);
            }
            for (int i = 0; i < panels.Length; i++)
            {
                s.Items[panels[i]].H = most - own[i] <= UiTokens.MostSlack - UiTokens.S5 ? most : own[i];
            }
            return most;
        }

        private static float Controls(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, IUiKeys keys)
        {
            float col = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / 3f);
            int pb = Panel(s, at, x, y, col, m.Captions[CaptionButtons], out float by);
            UiAt p = at.In(pb);
            var kinds = new[] { UiButtonKind.Primary, UiButtonKind.Secondary, UiButtonKind.Destructive, UiButtonKind.Armed, UiButtonKind.Chip, UiButtonKind.Quiet, UiButtonKind.Secondary };
            float bw = col - UiTokens.S5 * 2f;
            for (int i = 0; i < kinds.Length && i < m.Buttons.Length; i++)
            {
                float bh = kinds[i] == UiButtonKind.Chip ? UiTokens.Chip : UiTokens.Control;
                bool first = i == 0, last = i == kinds.Length - 1;   // the primary with its key and icon; the last one shown disabled
                UiDraw.Button(s, p, x + UiTokens.S5, by, bw, bh, kinds[i], m.Buttons[i], first ? UiIcon.Phone : UiIcon.None, first ? UiAction.Alt : UiAction.Count, keys, Sample, i, last);
                by += bh + UiTokens.S3;
            }
            float nx = x + col + UiTokens.S4;
            int pn = Panel(s, at, nx, y, col, m.Captions[CaptionNav], out float ny);
            UiAt n = at.In(pn);
            for (int i = 0; i < m.Nav.Length; i++)
            {
                UiDraw.NavItem(s, n, nx + UiTokens.S3, ny, col - UiTokens.S6, m.Nav[i].Label, m.Nav[i].Count, m.Nav[i].Hue, m.Nav[i].Selected, Sample, 10 + i, m.Nav[i].Loud);
                ny += UiTokens.Control + UiTokens.S1;
            }
            ny += UiTokens.S5;
            UiDraw.Tabs(s, n, nx + UiTokens.S5, ny, col - UiTokens.S5 * 2f, m.Tabs, 0, keys, Sample);
            float ix = nx + col + UiTokens.S4;
            int pf = Panel(s, at, ix, y, col, m.Captions[CaptionInputs], out float fy);
            UiAt f = at.In(pf);
            float fw = col - UiTokens.S5 * 2f;
            UiDraw.Search(s, f, ix + UiTokens.S5, fy, fw, m.SearchPlaceholder, null, "12", Sample);
            fy += UiTokens.Control + UiTokens.S4;
            UiDraw.Search(s, f, ix + UiTokens.S5, fy, fw, m.SearchPlaceholder, m.Query, "1", Sample);
            fy += UiTokens.Control + UiTokens.S4;
            UiDraw.Toggle(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[0], true, Sample, 20);
            fy += UiTokens.Control + UiTokens.S1;
            UiDraw.Toggle(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[1], false, Sample, 21);
            fy += UiTokens.Control + UiTokens.S1;
            UiDraw.Slider(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[2], m.SliderFigure, 0.62f, Sample, 22);
            fy += UiTokens.Control + UiTokens.S4 + UiTokens.S1;
            UiDraw.Selector(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[3], m.ChoiceValue, Sample, 23);
            fy += UiTokens.Control + UiTokens.S1;
            UiDraw.Selector(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[4], m.MotionValue, Sample, 24);
            return y + Fit(s, y, y, pb, pn, pf);
        }

        private static float Lists(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float col = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / 3f);
            int pr = Panel(s, at, x, y, col, null, out float ry);   // its sections head it ("New messages"): a caption over them would be a second label
            UiAt p = at.In(pr);
            for (int i = 0; i < m.Rows.Length; i++)
            {
                if (m.Rows[i].Section != null)
                {
                    ry += UiDraw.SectionHeader(s, p, x + UiTokens.S4, ry - UiTokens.S4, col - UiTokens.S8, m.Rows[i].Section) - UiTokens.S2;
                }
                UiDraw.ListRow(s, p.Arriving(i), x + UiTokens.S4, ry, col - UiTokens.S8, ref m.Rows[i], Sample, 30 + i, -1, i);
                ry += UiTokens.Row + UiTokens.S2;
            }
            float cx2 = x + col + UiTokens.S4;
            int pc = Panel(s, at, cx2, y, col, m.Captions[CaptionCards], out float cy2);
            UiAt c = at.In(pc);
            for (int i = 0; i < m.Services.Length; i++)
            {
                UiDraw.ServiceCard(s, c.Arriving(i), cx2 + UiTokens.S5, cy2, col - UiTokens.S5 * 2f, ref m.Services[i], Sample, 40 + i, out float sh);
                cy2 += sh + UiTokens.S2;
            }
            cy2 += UiTokens.S3;
            for (int i = 0; i < m.Choices.Length; i++)
            {
                UiDraw.ChoiceCard(s, c.Arriving(i + 2), cx2 + UiTokens.S5, cy2, col - UiTokens.S5 * 2f, ref m.Choices[i], keys, Sample, 50 + i, out float chh);
                cy2 += chh + UiTokens.S2;
            }
            float dx = cx2 + col + UiTokens.S4;
            int pd = Panel(s, at, dx, y, col, m.Captions[CaptionData], out float dy);
            UiAt d = at.In(pd);
            for (int i = 0; i < m.Pairs.Length; i++)
            {
                dy += UiDraw.KeyValue(s, d, dx + UiTokens.S5, dy, col - UiTokens.S5 * 2f, m.Pairs[i].Key, m.Pairs[i].Value);
            }
            dy += UiTokens.S5;
            float tableH = UiDraw.TableHeight(m.Table, UiDraw.SnapDown(y + h - UiTokens.S4 - dy));
            UiDraw.Table(s, d, dx + UiTokens.S2, dy, col - UiTokens.S4, tableH, m.Table, Sample);
            return y + Fit(s, y, dy + tableH - UiTokens.S1, pr, pc, pd);
        }

        private static float Figures(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, IUiKeys keys)
        {
            int n = Math.Min(3, m.Figures.Length);
            float fw = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / 3f);
            var figurePanels = new int[n];
            for (int i = 0; i < n; i++)
            {
                float fx = x + i * (fw + UiTokens.S4);
                figurePanels[i] = Panel(s, at, fx, y, fw, null, out float top);
                UiDraw.Figure(s, at.In(figurePanels[i]).Arriving(i), fx + UiTokens.S5, top, fw - UiTokens.S5 * 2f, ref m.Figures[i], true);
            }
            float y2 = y + Fit(s, y, y, figurePanels) + UiTokens.S4;
            float half = UiDraw.SnapDown((w - UiTokens.S4) / 2f);
            int ps = Panel(s, at, x, y2, half, m.Captions[CaptionStats], out float sy);
            UiAt st = at.In(ps);
            for (int i = 0; i < m.Stats.Length; i++)
            {
                sy += UiDraw.Snap(UiDraw.StatBar(s, st.Arriving(i), x + UiTokens.S5, sy, half - UiTokens.S5 * 2f, ref m.Stats[i])) + UiTokens.S4;
            }
            UiDraw.Steps(s, st, x + UiTokens.S5, sy + UiTokens.S2, half - UiTokens.S5 * 2f, m.StepLabel, 3, 1);
            float x2 = x + half + UiTokens.S4;
            int pt = Panel(s, at, x2, y2, half, m.Captions[CaptionStatus], out float cy);
            UiAt ss = at.In(pt);
            float chipX = x2 + UiTokens.S5;
            for (int i = 0; i < m.Statuses.Length; i++)
            {
                chipX += UiDraw.StatusChip(s, ss, chipX, cy, m.Statuses[i], (UiTone)i) + UiTokens.S3;
            }
            cy += UiTokens.S8 + UiTokens.S6;
            float badgeRight = x2 + UiTokens.S5 + UiTokens.S8;
            UiDraw.Badge(s, ss, badgeRight, cy, 3, true);
            UiDraw.Badge(s, ss, badgeRight + UiTokens.S12, cy, 12, false);
            cy += UiTokens.Badge + UiTokens.S6;
            float ax = x2 + UiTokens.S5;
            for (int i = 0; i < m.AvatarNames.Length; i++)
            {
                UiDraw.Avatar(s, ss, ax, cy, UiTokens.AvatarRow, m.AvatarNames[i], UiColour.HueOf(m.AvatarNames[i]));
                ax += UiTokens.AvatarRow + UiTokens.S3;
            }
            cy += UiTokens.AvatarRow + UiTokens.S5;
            if (keys != null)
            {
                // The prompt bar's key caps: what the keyboard or the pad shows for each of the kit's actions.
                float kx = x2 + UiTokens.S5;
                foreach (UiAction action in new[] { UiAction.Confirm, UiAction.Back, UiAction.Alt, UiAction.Option, UiAction.TabPrev, UiAction.TabNext })
                {
                    kx += UiDraw.KeyCap(s, ss, kx, cy, action, keys, UiRole.Track, UiRole.Text) + UiTokens.S3;
                }
            }
            return y2 + Fit(s, y2, y2, ps, pt);
        }

        private static float Messages(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w)
        {
            float tw = UiDraw.SnapDown(w * 0.6f);
            int pm = Panel(s, at, x, y, tw, m.Captions[CaptionThread], out float ty);
            UiAt p = at.In(pm);
            float inner = tw - UiTokens.S6 * 2f;
            for (int i = 0; i < m.Messages.Length; i++)
            {
                ty += UiDraw.Bubble(s, p.Arriving(i), x + UiTokens.S6, ty, inner, ref m.Messages[i]) + UiTokens.S3;
            }
            ty += UiDraw.Chips(s, p, x + UiTokens.S6, ty, inner, m.Replies, m.Replies.Length, Sample) + UiTokens.S3;
            UiDraw.Composer(s, p, x + UiTokens.S6, ty, inner, m.ComposerPlaceholder, null, m.SendLabel, Sample, Sample);
            ty += UiTokens.Control + UiTokens.S2 + UiTokens.S3;
            UiDraw.Composer(s, p, x + UiTokens.S6, ty, inner, m.ComposerPlaceholder, m.Draft, m.SendLabel, Sample, Sample);
            float ph = Fit(s, y, y, pm);
            // The states beside it, sharing the thread's height with a rule between them rather than a box in a box: words
            // where nothing is, dots while it loads.
            float sx = x + tw + UiTokens.S4, sw = x + w - sx;
            int pe = Panel(s, at, sx, y, sw, m.Captions[CaptionStates], out float sy);
            s.Items[pe].H = ph;
            UiAt st = at.In(pe);
            float ww = sw - UiTokens.S5 * 2f, wellsH = y + ph - UiTokens.S5 - sy;
            float emptyH = UiDraw.SnapDown((wellsH - UiTokens.S3) * 0.6f), busyH = wellsH - UiTokens.S3 - emptyH;
            UiDraw.Empty(s, st, sx + UiTokens.S5, sy, ww, emptyH, m.EmptyTitle, m.EmptyBody);
            UiDraw.Box(s, st, sx + UiTokens.S5, sy + emptyH + UiTokens.S1, ww, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            int busy = UiDraw.Box(s, st, sx + UiTokens.S5, sy + emptyH + UiTokens.S3, ww, busyH, UiRole.None, UiRole.None, 0f);
            UiDraw.Busy(s, st.In(busy), sx + UiTokens.S5, sy + emptyH + UiTokens.S3, ww, busyH, m.BusyLabel);
            return y + ph;
        }

        // A kind of the kit's tokens in a framed table under a page, as many whole rows as the room left takes (a list that
        // scrolls for the rest); nothing when not even a row would fit.
        private static void Tokens(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, float room, int kind)
        {
            float top = UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S4;
            float most = room - top - UiTokens.S4;
            if (m.TokenCaptions.Length < TokenKinds || m.TokenColumns.Length < Columns || most < UiTokens.ControlSmall + UiTokens.TableRow)
            {
                return;
            }
            UiTableModel table = TokenTable(m, kind);
            float tableH = UiDraw.TableHeight(table, most);
            int panel = Panel(s, at, x, y, w, m.TokenCaptions[kind], out float tableY);
            UiDraw.Table(s, at.In(panel), x + UiTokens.S2, tableY, w - UiTokens.S4, tableH, table, Sample);
            s.Items[panel].H = UiDraw.Snap(tableY + tableH + UiTokens.S4 - y);
        }

        // The rows of a kind of token: each text style in its face and size, each colour role in both looks, each motion at
        // full and calm, and the spacing steps and the sizes of the controls.
        private static UiTableModel TokenTable(UiGalleryModel m, int kind)
        {
            const float figure = UiTokens.S16 * 2f + UiTokens.S8;
            var rows = new System.Collections.Generic.List<string[]>(40);
            UiColumn[] columns;
            switch (kind)
            {
                case TokensType:
                    columns = new[] { Column(m, ColumnStyle, 0f), Column(m, ColumnFace, 0f), Column(m, ColumnSize, figure), Column(m, ColumnWeight, figure) };
                    for (int i = 0; i < (int)UiStyle.Count; i++)
                    {
                        UiType t = UiTokens.Type((UiStyle)i);
                        rows.Add(new[] { Words(((UiStyle)i).ToString()), Face(t.Family), Px(t.Size), t.Weight.ToString(Inv) });
                    }
                    break;
                case TokensColours:
                    columns = new[] { Column(m, ColumnRole, 0f), Column(m, ColumnDark, figure), Column(m, ColumnLight, figure) };
                    for (int i = 1; i < (int)UiRole.Count; i++)
                    {
                        rows.Add(new[] { Words(((UiRole)i).ToString()), Hex(UiTokens.Colour((UiRole)i, true)), Hex(UiTokens.Colour((UiRole)i, false)) });
                    }
                    break;
                case TokensMotion:
                    columns = new[] { Column(m, ColumnKind, 0f), Column(m, ColumnFull, figure), Column(m, ColumnCalm, figure) };   // the caption says Motion already
                    for (int i = 0; i < (int)UiMotionKind.Count; i++)
                    {
                        rows.Add(new[] { Words(((UiMotionKind)i).ToString()), Ms(UiTokens.Duration((UiMotionKind)i, UiMotionSetting.Full)),
                            Ms(UiTokens.Duration((UiMotionKind)i, UiMotionSetting.Calm)) });
                    }
                    break;
                default:
                    columns = new[] { Column(m, ColumnToken, 0f), Column(m, ColumnPx, figure) };
                    foreach (System.Collections.Generic.KeyValuePair<string, float> size in Sizes)
                    {
                        rows.Add(new[] { size.Key, Px(size.Value) });
                    }
                    break;
            }
            return new UiTableModel { Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, Inert = true };
        }

        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        // The kit's spacing steps and the sizes its controls are built on, by their names in UiTokens.
        private static readonly System.Collections.Generic.KeyValuePair<string, float>[] Sizes =
        {
            Size("S1", UiTokens.S1), Size("S2", UiTokens.S2), Size("S3", UiTokens.S3), Size("S4", UiTokens.S4), Size("S5", UiTokens.S5), Size("S6", UiTokens.S6),
            Size("S8", UiTokens.S8), Size("S10", UiTokens.S10), Size("S12", UiTokens.S12), Size("S16", UiTokens.S16), Size("Control", UiTokens.Control),
            Size("Control small", UiTokens.ControlSmall), Size("Chip", UiTokens.Chip), Size("Least target", UiTokens.MinTarget), Size("Pair row", UiTokens.PairRow),
            Size("Table row", UiTokens.TableRow), Size("Table row, most", UiTokens.TableRowMost), Size("List row", UiTokens.Row), Size("Avatar", UiTokens.AvatarRow),
            Size("Avatar, hero", UiTokens.AvatarHero), Size("Icon", UiTokens.Icon), Size("Key cap", UiTokens.KeyCap), Size("Badge", UiTokens.Badge),
            Size("Column rule", UiTokens.FrameRule), Size("Focus ring", UiTokens.FocusStroke), Size("Smallest text", UiTokens.MinText)
        };

        private static System.Collections.Generic.KeyValuePair<string, float> Size(string name, float px) => new System.Collections.Generic.KeyValuePair<string, float>(name, px);

        private static UiColumn Column(UiGalleryModel m, int column, float width) => new UiColumn { Title = m.TokenColumns[column], Width = width, Right = width > 0f };

        // A token's name as words: "SectionTitle" reads "Section title".
        private static string Words(string name)
        {
            var b = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                b.Append(i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString());
            }
            return b.ToString();
        }

        private static string Face(UiFamily family) => family == UiFamily.Franklin ? "Libre Franklin" : family == UiFamily.SerifDisplay ? "Source Serif 4 Display" : "Source Serif 4";

        private static string Px(float px) => px.ToString("0.#", Inv) + " px";

        private static string Ms(float seconds) => (seconds * 1000f).ToString("0", Inv) + " ms";

        // A colour as its hex, with its share where it is not whole.
        private static string Hex(uint rgba)
        {
            uint a = rgba & 0xFF;
            string hex = "#" + (rgba >> 8).ToString("X6", Inv);
            return a == 0xFF ? hex : hex + " " + (a * 100 / 255).ToString(Inv) + "%";
        }

        private static float TabsWidth(string[] tabs, IUiKeys keys)
        {
            float w = keys != null ? UiDraw.KeyCapWidth(UiAction.TabPrev, keys) + UiTokens.S3 + UiTokens.S2 + UiDraw.KeyCapWidth(UiAction.TabNext, keys) : 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                w += UiDraw.Snap(UiTextRules.Width(tabs[i], UiStyle.Nav) + UiTokens.S4 * 2f) + UiTokens.S1;
            }
            return UiDraw.Snap(w);
        }
    }
}
