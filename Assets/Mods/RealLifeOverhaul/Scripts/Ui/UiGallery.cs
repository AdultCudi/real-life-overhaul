using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The words and samples the gallery shows; the game fills them from the locale (UiText), the checks from fixtures.</summary>
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
        public string[] Fields = new string[0];         // a toggle on, a toggle off, a slider, a choice
        public string SliderFigure;
        public string ChoiceValue;
        public UiRowModel[] Rows = new UiRowModel[0];
        public string RowSection;
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
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The kit's gallery: every component in its states, a page a family (controls, lists, figures, messages, a question,
    /// a call), the tab keys turning the pages and Option turning the look, so the owner sees the whole kit in both themes
    /// from Options, Tests. Each page is framed panels with a caption. Commands: <see cref="PageCommand"/> (a tab) and
    /// <see cref="Sample"/> (anything else: the gallery only shows). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiGallery
    {
        public const int PageCommand = 1, Sample = 2;
        public const int CaptionButtons = 0, CaptionNav = 1, CaptionInputs = 2, CaptionRows = 3, CaptionCards = 4, CaptionData = 5, CaptionFigures = 6,
            CaptionStats = 7, CaptionStatus = 8, CaptionThread = 9, CaptionStates = 10, Captions = 11;
        public const int PageCount = 6;

        /// <summary>Lays the gallery's page out into a scene.</summary>
        public static void Lay(UiScene s, UiGalleryModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("gallery", frame.Width, frame.Height + frame.Band, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt at = screen.In(bg);
            UiDraw.Text(s, at, cx, cy, cw * 0.4f, UiStyle.SectionTitle, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
            float tabsW = TabsWidth(m.Pages, keys);
            UiDraw.Tabs(s, at, cx + cw - tabsW, cy, tabsW, m.Pages, m.Page, keys, PageCommand);
            float y = cy + UiTokens.Control + UiTokens.S5;
            float h = UiDraw.SnapDown(cy + ch - y);
            switch (m.Page)
            {
                case 0: Controls(s, at, m, cx, y, cw, h, keys); break;
                case 1: Lists(s, at, m, cx, y, cw, h, keys); break;
                case 2: Figures(s, at, m, cx, y, cw, h); break;
                case 3: Messages(s, at, m, cx, y, cw, h); break;
                case 4:
                    Lists(s, at, m, cx, y, cw, h, keys);
                    UiDraw.Question(s, 0f, 0f, frame.Width, frame.Height, m.QuestionTitle, m.QuestionBody, m.Answers, m.Answers.Length, keys);
                    break;
                default:
                    Messages(s, at, m, cx, y, cw, h);
                    UiDraw.Call(s, cx, y, cw, h, m.CallName, 12f, m.CallState, true, m.CallOptions, m.CallOptions.Length, m.CallEnd, Sample, Sample);
                    UiDraw.Toast(s, 0f, 0f, frame.Width, frame.Height, m.ToastTitle, m.ToastSub);
                    break;
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, 0f, frame.Height, frame.Width, frame.Band, keys);
        }

        // A framed panel with its caption; returns the inside's place and the first line under the caption.
        private static UiAt Panel(UiScene s, UiAt at, float x, float y, float w, float h, string caption, out float top)
        {
            int p = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
            UiAt inside = at.In(p);
            UiDraw.Text(s, inside, x + UiTokens.S5, y + UiTokens.S5, w - UiTokens.S5 * 2f, UiStyle.SectionHeader, UiRole.Muted, caption, UiFlags.Label | UiFlags.Truncate);
            top = y + UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S4;
            return inside;
        }

        private static void Controls(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float col = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / 3f);
            UiAt p = Panel(s, at, x, y, col, h, m.Captions[CaptionButtons], out float by);
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
            UiAt n = Panel(s, at, nx, y, col, h, m.Captions[CaptionNav], out float ny);
            for (int i = 0; i < m.Nav.Length; i++)
            {
                UiDraw.NavItem(s, n, nx + UiTokens.S3, ny, col - UiTokens.S6, m.Nav[i].Label, m.Nav[i].Count, m.Nav[i].Hue, m.Nav[i].Selected, Sample, 10 + i, m.Nav[i].Loud);
                ny += UiTokens.Control + UiTokens.S1;
            }
            ny += UiTokens.S5;
            UiDraw.Tabs(s, n, nx + UiTokens.S5, ny, col - UiTokens.S5 * 2f, m.Tabs, 0, keys, Sample);
            float ix = nx + col + UiTokens.S4;
            UiAt f = Panel(s, at, ix, y, col, h, m.Captions[CaptionInputs], out float fy);
            float fw = col - UiTokens.S5 * 2f;
            UiDraw.Search(s, f, ix + UiTokens.S5, fy, fw, m.SearchPlaceholder, null, "12", Sample);
            fy += UiTokens.Control + UiTokens.S4;
            UiDraw.Toggle(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[0], true, Sample, 20);
            fy += UiTokens.Control + UiTokens.S1;
            UiDraw.Toggle(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[1], false, Sample, 21);
            fy += UiTokens.Control + UiTokens.S1;
            UiDraw.Slider(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[2], m.SliderFigure, 0.62f, Sample, 22);
            fy += UiTokens.Control + UiTokens.S4 + UiTokens.S1;
            UiDraw.Selector(s, f, ix + UiTokens.S2, fy, fw + UiTokens.S6, m.Fields[3], m.ChoiceValue, Sample, 23);
        }

        private static void Lists(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float col = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / 3f);
            UiAt p = Panel(s, at, x, y, col, h, m.Captions[CaptionRows], out float ry);
            ry += UiDraw.SectionHeader(s, p, x + UiTokens.S4, ry - UiTokens.S4, col - UiTokens.S8, m.RowSection) - UiTokens.S2;
            for (int i = 0; i < m.Rows.Length; i++)
            {
                UiDraw.ListRow(s, p.Arriving(i), x + UiTokens.S4, ry, col - UiTokens.S8, ref m.Rows[i], Sample, 30 + i, -1, i);
                ry += UiTokens.Row + UiTokens.S2;
            }
            float cx2 = x + col + UiTokens.S4;
            UiAt c = Panel(s, at, cx2, y, col, h, m.Captions[CaptionCards], out float cy2);
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
            UiAt d = Panel(s, at, dx, y, col, h, m.Captions[CaptionData], out float dy);
            for (int i = 0; i < m.Pairs.Length; i++)
            {
                dy += UiDraw.KeyValue(s, d, dx + UiTokens.S5, dy, col - UiTokens.S5 * 2f, m.Pairs[i].Key, m.Pairs[i].Value);
            }
            dy += UiTokens.S5;
            UiDraw.Table(s, d, dx + UiTokens.S2, dy, col - UiTokens.S4, UiDraw.SnapDown(y + h - UiTokens.S4 - dy), m.Table, Sample);
        }

        private static void Figures(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, float h)
        {
            int n = Math.Min(3, m.Figures.Length);
            float fw = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / 3f);
            float fh = 0f;
            for (int i = 0; i < n; i++)
            {
                float fx = x + i * (fw + UiTokens.S4);
                UiAt p = Panel(s, at, fx, y, fw, UiTokens.S16 * 4f, m.Captions[CaptionFigures], out float top);
                fh = UiTokens.S16 * 4f;
                UiDraw.Figure(s, p.Arriving(i), fx + UiTokens.S5, top, fw - UiTokens.S5 * 2f, ref m.Figures[i], true);
            }
            float y2 = y + fh + UiTokens.S4, h2 = UiDraw.SnapDown(y + h - y2);
            float half = UiDraw.SnapDown((w - UiTokens.S4) / 2f);
            UiAt st = Panel(s, at, x, y2, half, h2, m.Captions[CaptionStats], out float sy);
            for (int i = 0; i < m.Stats.Length; i++)
            {
                sy += UiDraw.Snap(UiDraw.StatBar(s, st.Arriving(i), x + UiTokens.S5, sy, half - UiTokens.S5 * 2f, ref m.Stats[i])) + UiTokens.S4;
            }
            UiDraw.Steps(s, st, x + UiTokens.S5, sy + UiTokens.S2, half - UiTokens.S5 * 2f, m.StepLabel, 3, 1);
            float x2 = x + half + UiTokens.S4;
            UiAt ss = Panel(s, at, x2, y2, half, h2, m.Captions[CaptionStatus], out float cy);
            float chipX = x2 + UiTokens.S5;
            for (int i = 0; i < m.Statuses.Length; i++)
            {
                chipX += UiDraw.StatusChip(s, ss, chipX, cy, m.Statuses[i], (UiTone)i) + UiTokens.S3;
            }
            cy += UiTokens.S8 + UiTokens.S5;
            float badgeRight = x2 + UiTokens.S5 + UiTokens.S8;
            UiDraw.Badge(s, ss, badgeRight, cy, 3, true);
            UiDraw.Badge(s, ss, badgeRight + UiTokens.S12, cy, 12, false);
            cy += UiTokens.Badge + UiTokens.S5;
            float ax = x2 + UiTokens.S5;
            for (int i = 0; i < m.AvatarNames.Length; i++)
            {
                UiDraw.Avatar(s, ss, ax, cy, UiTokens.AvatarRow, m.AvatarNames[i], UiColour.HueOf(m.AvatarNames[i]));
                ax += UiTokens.AvatarRow + UiTokens.S3;
            }
        }

        private static void Messages(UiScene s, UiAt at, UiGalleryModel m, float x, float y, float w, float h)
        {
            float tw = UiDraw.SnapDown(w * 0.6f);
            UiAt p = Panel(s, at, x, y, tw, h, m.Captions[CaptionThread], out float ty);
            float inner = tw - UiTokens.S6 * 2f;
            for (int i = 0; i < m.Messages.Length; i++)
            {
                ty += UiDraw.Bubble(s, p.Arriving(i), x + UiTokens.S6, ty, inner, ref m.Messages[i]) + UiTokens.S3;
            }
            ty += UiDraw.Chips(s, p, x + UiTokens.S6, ty, inner, m.Replies, m.Replies.Length, Sample) + UiTokens.S3;
            UiDraw.Composer(s, p, x + UiTokens.S6, ty, inner, m.ComposerPlaceholder, null, m.SendLabel, Sample, Sample);
            ty += UiTokens.Control + UiTokens.S2 + UiTokens.S3;
            UiDraw.Composer(s, p, x + UiTokens.S6, ty, inner, m.ComposerPlaceholder, m.Draft, m.SendLabel, Sample, Sample);
            float sx = x + tw + UiTokens.S4, sw = x + w - sx;
            UiAt st = Panel(s, at, sx, y, sw, h, m.Captions[CaptionStates], out float sy);
            float half = UiDraw.SnapDown((y + h - sy) / 2f);
            UiDraw.Empty(s, st, sx, sy, sw, half, m.EmptyTitle, m.EmptyBody);
            UiDraw.Busy(s, st, sx, sy + half, sw, half - UiTokens.S5, m.BusyLabel);
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
