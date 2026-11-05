using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Company app on the kit: an app window with an FM sidebar in two groups (your company, and money and rules) and a page for each of the seven tabs. Each
    /// page is its own layout of its own section of the facts, built from a reset view of what is chosen, so no tab can show another's parts. Game-free: the facts
    /// say what, this says where. Cost: a layout when the facts or a choice change; nothing while idle.
    /// </summary>
    public static partial class CompanyFace
    {
        // Commands 7410 to 7449 are the Company app's own (the organisation tree's are 7400 to 7409). Every kind of target has a number of its own, and no kind shares
        // one with another: the question card's choices and buttons run Choice and Act as the Lead's do (UiDecisionModel.ChoiceCmd, ButtonCmd), so that nothing is told
        // apart by the state a page is in. The kit's parts keep their own numbers (the sidebar's 1, the board's 30 to 33, the tree's 7400 to 7403), which none of these meet.
        public const int Act = 7410;        // a button, a switch or a link: the index of its action in the layout
        public const int Row = 7411;        // a row of the page's list: its index
        public const int Choice = 7412;     // a choice of the Lead or of the question's card: its index
        public const int DayHead = 7415;    // a day's column head or its hour bar: the day
        public const int Item = 7416;       // a row of a card's list: the index of its action
        public const int Filter = 7417;     // a chip over a list: its index
        public const int Badge = 7418;      // a business's badge in the head: its index
        public const int Opt = 7419;        // an option of a choice: the index of its action
        public const int Last = 7449;

        // The regions of a page's columns for the d-pad (the sidebar's are the kit's): a list or tree at the left, the page beside it, and a column beside that.
        internal const byte RegionList = 3, RegionMain = 4, RegionSide = 5;

        /// <summary>Every command the screen answers, for the checks: its own, the tree's and the tables'.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int>
            {
                UiAppWindow.NavCommand, Act, Row, Choice, DayHead, Item, Filter, Badge, Opt, StepLess, StepMore, StepType,
                UiOrgTree.First + UiOrgTree.Pick, UiOrgTree.First + UiOrgTree.All, UiOrgTree.First + UiOrgTree.Foot,
                UiBoard.ShiftCommand, UiBoard.LaneCommand, UiBoard.DayCommand, UiBoard.ActionCommand
            };
            return list.ToArray();
        }

        public static void Lay(UiScene s, CompanyFacts f, CompanyView v, CompanyWords w, CompanyLayout lay, UiFrame frame, bool dark, IUiKeys keys)
        {
            lay.Reset();
            _layNow = lay;
            _viewNow = v;
            if (v.Question != 0 && f.Question != null)
            {
                LayQuestion(s, f.Question, v, w, lay, frame, dark, keys);
                return;
            }
            s.Begin("company", frame.Width, frame.Height, dark);
            UiNavEntry[] nav = Nav(f, w);
            int tab = Math.Max(0, Math.Min(CompanyView.Tabs - 1, v.Tab));
            string title = w.T("reallife_co_app", "Company");   // the app, as every kit app is titled: the company's own name is the overview's head, so no page repeats the window's title
            UiAt at = UiAppWindow.Shell(s, frame, title, UiIcon.Briefcase, nav, tab, false, keys, out float x, out float y, out float cw, out float bottom);
            if (!f.Ready)
            {
                OffPage(s, at, w, string.IsNullOrEmpty(f.OffTitle) ? w.T("reallife_co_off_title", "The company is not running") : f.OffTitle,
                    string.IsNullOrEmpty(f.OffBody) ? w.T("reallife_co_off_body", "Load a save with the economy switched on to run your company.") : f.OffBody, x, y, cw, bottom, keys);
            }
            else
            {
                switch (tab)
                {
                    case CompanyView.Overview: OverviewPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case CompanyView.Staff: StaffPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case CompanyView.Managers: ManagersPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case CompanyView.Rota: RotaPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case CompanyView.Marketing: ListPage(s, at, f.Marketing, v, w, lay, x, y, cw, bottom, keys); break;
                    case CompanyView.Cover: ListPage(s, at, f.Cover, v, w, lay, x, y, cw, bottom, keys); break;
                    default: TaxPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                }
            }
            SidebarIntoPage(s);
            for (int i = 0; i < 3; i++)
            {
                s.AddPrompt(i == 0 ? UiAction.Move : i == 1 ? UiAction.Confirm : UiAction.Back,
                    w.T(i == 0 ? "reallife_ui_prompt_move" : i == 1 ? "reallife_ui_prompt_select" : "reallife_ui_prompt_back", i == 0 ? "Move" : i == 1 ? "Select" : "Back"));
            }
            // Move, Select, Back and the page's main action, as the other apps' bars hold: the app row keeps its tiles (a fifth, long prompt pushed the bar over them). The second
            // action keeps its key on its own button.
            if (lay.Main >= 0 && !lay.Acts[lay.Main].Disabled)
            {
                s.AddPrompt(UiAction.Alt, UiTextRules.Cut(lay.Acts[lay.Main].Label ?? string.Empty, UiStyle.BodyStrong, 168f));
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>
        /// The way from the sidebar into a page, in reading order: Right from any entry goes to the top of the page's left column, where the eye goes next, and not to whatever target stands nearest
        /// the entry's own height (the second row of a list, a switch half way down the card). Boxes are read through NodeBox: a list's rows keep their place inside the list, so their own X is
        /// 0 and only the box says where they stand, which is why a page with a list once sent the pad to its far right column. The sidebar is the entries of the sidebar's number that stand at the
        /// leftmost of them, because other kit parts reuse that number further right on a page.
        /// </summary>
        private static void SidebarIntoPage(UiScene s)
        {
            float navX = float.MaxValue;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Command == UiAppWindow.NavCommand)
                {
                    navX = Math.Min(navX, s.Nodes[i].X);
                }
            }
            float pageX = 0f;   // the sidebar's right edge: a row that bleeds a little past the page's margin still belongs to the page
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (IsSidebar(s, i, navX))
                {
                    pageX = Math.Max(pageX, s.Nodes[i].X + s.Nodes[i].W);
                }
            }
            // The first column is the first share of the page's width: a card's button stands a little right of a row's edge below it, and it is the card that comes first to the eye.
            float right = pageX;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (OnPage(s, i, navX, pageX))
                {
                    s.NodeBox(i, out float bx, out _, out float bw, out _);
                    right = Math.Max(right, bx + bw);
                }
            }
            float edge = pageX + (right - pageX) * LeftShare;
            int first = -1;
            float firstY = 0f, firstX = 0f;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (!OnPage(s, i, navX, pageX))
                {
                    continue;
                }
                s.NodeBox(i, out float bx, out float by, out _, out _);
                if (bx < edge && (first < 0 || by < firstY - 0.5f || (by < firstY + 0.5f && bx < firstX)))
                {
                    first = i;
                    firstY = by;
                    firstX = bx;
                }
            }
            for (int i = 0; i < s.NodeCount && first >= 0; i++)
            {
                if (IsSidebar(s, i, navX))
                {
                    s.Nodes[i].Right = first;
                }
            }
        }

        /// <summary>The share of the page's width, from the sidebar to its last target, that counts as its first column, for where the pad's Right from the sidebar lands.</summary>
        internal const float LeftShare = 0.45f;

        private static bool IsSidebar(UiScene s, int i, float navX) => s.Nodes[i].Command == UiAppWindow.NavCommand && s.Nodes[i].X < navX + 1f;

        private static bool OnPage(UiScene s, int i, float navX, float pageX)
        {
            if (s.Nodes[i].Layer != 0 || s.Nodes[i].Disabled || IsSidebar(s, i, navX))
            {
                return false;
            }
            s.NodeBox(i, out float bx, out _, out _, out _);
            return bx >= pageX;
        }

        /// <summary>The sidebar: your company, then money and rules, each entry its own glyph.</summary>
        public static UiNavEntry[] Nav(CompanyFacts f, CompanyWords w)
        {
            string[] c = f != null ? f.Counts : new string[CompanyView.Tabs];
            return new[]
            {
                new UiNavEntry { Icon = UiIcon.Home, Label = w.T("reallife_co_tab_overview", "Overview"), Count = c[0], Group = w.T("reallife_co_group_company", "Your company") },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_co_tab_staff", "Staff"), Count = c[1] },
                new UiNavEntry { Icon = UiIcon.IdBadge, Label = w.T("reallife_co_tab_managers", "Managers"), Count = c[2] },
                new UiNavEntry { Icon = UiIcon.Calendar, Label = w.T("reallife_co_tab_rota", "Rota"), Count = c[3] },
                new UiNavEntry { Icon = UiIcon.Megaphone, Label = w.T("reallife_co_tab_marketing", "Marketing"), Count = c[4], Group = w.T("reallife_co_group_money", "Money and rules") },
                new UiNavEntry { Icon = UiIcon.ShieldCheck, Label = w.T("reallife_co_tab_cover", "Cover"), Count = c[5] },
                new UiNavEntry { Icon = UiIcon.Receipt, Label = w.T("reallife_co_nav_tax", "Tax and licences"), Count = c[6] }
            };
        }

        // ------------------------------------------------------------------------------------------------- shared parts

        internal static float Title(UiScene s, UiAt at, string words, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(words))
            {
                return y;
            }
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.SectionHeader, UiRole.Text2, words, UiFlags.Label | UiFlags.Truncate);
            return y + UiTokens.PairRow + UiTokens.S1;
        }

        /// <summary>A section's label in capitals with a count or a link at its right. Returns the y under it.</summary>
        internal static float Section(UiScene s, UiAt at, CompanyLayout lay, float x, float y, float w, string label, string right = null, CompanyButton? link = null)
        {
            float h = UiTokens.PairRow;
            float rw = 0f;
            if (link.HasValue && !string.IsNullOrEmpty(link.Value.Label))
            {
                CompanyButton b = link.Value;
                rw = Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(b.Label, UiStyle.BodyStrong) + UiTokens.S4));
                TextLink(s, at, lay, ref b, x + w - rw, y, h, rw);
            }
            else if (!string.IsNullOrEmpty(right))
            {
                rw = Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(right, UiStyle.Body) + UiTokens.S1));
                UiDraw.TextMiddle(s, at, x + w - rw, y, h, rw, UiStyle.Body, UiRole.Text2, right, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, at, x, y, h, w - rw - (rw > 0f ? UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            return y + h + UiTokens.S1;
        }

        /// <summary>The width a switch needs to say a word beside its track: the word, the track (44) and the gaps the kit's switch keeps round them.</summary>
        internal static float SwitchWidth(string word) =>
            UiDraw.Snap(UiTextRules.Width(word ?? string.Empty, UiStyle.Body) + UiTokens.S1) + 44f + UiTokens.S3 + UiTokens.S2 * 2f;

        /// <summary>
        /// An option of a choice as a chip: the chosen one filled in the accent (marked chosen, so that it is a state and not a second primary action), the others quiet. It
        /// takes the width its words need; the target runs <paramref name="command"/> with <paramref name="arg"/>. Returns the width.
        /// </summary>
        internal static float ChoiceChip(UiScene s, UiAt at, float x, float y, string label, bool chosen, int command, int arg, bool disabled = false)
        {
            float w = UiDraw.ButtonWidth(UiButtonKind.Chip, label, UiIcon.None, UiAction.Count, null);
            // The chosen option is the bright one; the others are plain, so that a row of chips never reads as all chosen.
            int face = UiDraw.Box(s, at, x, y, w, UiTokens.Chip, chosen ? UiRole.Accent : UiRole.Row, UiRole.None, UiTokens.RadiusMedium,
                (chosen ? UiFlags.Selected : UiFlags.None) | (disabled ? UiFlags.Disabled : UiFlags.None));
            UiDraw.TextMiddle(s, at.In(face), x + UiTokens.S4, y, UiTokens.Chip, w - UiTokens.S4 * 2f + UiTokens.S1, UiStyle.Button, chosen ? UiRole.OnAccent : UiRole.Text2, label,
                UiFlags.Label | UiFlags.Centre | (disabled ? UiFlags.Disabled : UiFlags.None));
            UiDraw.Node(s, at, x, y, w, UiTokens.Chip, command, arg, face, disabled);
            return w;
        }

        /// <summary>A text link, cyan words with a hover's face, that does an action. The words end at the right edge of the box.</summary>
        internal static void TextLink(UiScene s, UiAt at, CompanyLayout lay, ref CompanyButton b, float x, float y, float h, float w)
        {
            int index = lay.Add(b);
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge, b.Disabled ? UiFlags.Disabled : UiFlags.None);
            UiDraw.TextMiddle(s, at.In(face), x, y, h, w, UiStyle.BodyStrong, b.Disabled ? UiRole.Muted : UiRole.AccentText, b.Label, UiFlags.Right | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, h, Act, index, face, b.Disabled);
        }

        /// <summary>A link row: its words in the cyan with an arrow after them, the whole row a target.</summary>
        internal static float LinkRow(UiScene s, UiAt at, CompanyLayout lay, ref CompanyButton b, float x, float y, float w, bool bleed = false)
        {
            int index = lay.Add(b);
            return UiDraw.LinkRow(s, at, x, y, w, UiTokens.Control, b.Label, Act, index, bleed);
        }

        private static string Identity(ref CompanyButton b) => (int)b.Act.Do + ":" + b.Act.S + ":" + b.Act.S2 + ":" + b.Act.A + ":" + b.Act.B + ":" + b.Act.C;

        /// <summary>A button of the page: registered in the layout so its press finds its action; a destructive one armed by a first press shows so.</summary>
        internal static float Button(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, ref CompanyButton b, float x, float y, float w, IUiKeys keys, float h = 0f)
        {
            if (h <= 0f)
            {
                h = UiTokens.Control;
            }
            int index = lay.Add(b);
            UiButtonKind kind = b.Arms && v != null && v.ArmedId == Identity(ref b) ? UiButtonKind.Armed : b.Arms && b.Kind == UiButtonKind.Secondary ? UiButtonKind.Destructive : b.Kind;
            UiDraw.Button(s, at, x, y, w, h, kind, b.Label, b.Icon, b.Key, keys, Act, index, b.Disabled);
            return w;
        }

        /// <summary>A row of buttons ending at the right edge, the last the main one; a disabled one's reason in a quiet line under them. Returns the y under it.</summary>
        internal static float Buttons(UiScene s, UiAt at, CompanyLayout lay, CompanyView v, CompanyButton[] buttons, float x, float y, float w, IUiKeys keys, bool left = false)
        {
            if (buttons == null || buttons.Length == 0)
            {
                return y;
            }
            float total = 0f;
            for (int i = 0; i < buttons.Length; i++)
            {
                total += Math.Min(w, UiDraw.ButtonWidth(buttons[i].Kind, buttons[i].Label, buttons[i].Icon, buttons[i].Key, keys)) + (i > 0 ? UiTokens.S3 : 0f);
            }
            float cx = left || total > w ? x : x + Math.Max(0f, w - total);
            string why = null;
            for (int i = 0; i < buttons.Length; i++)
            {
                float bw = Math.Min(w, UiDraw.ButtonWidth(buttons[i].Kind, buttons[i].Label, buttons[i].Icon, buttons[i].Key, keys));
                if (cx > x + 0.5f && cx + bw > x + w + 0.5f)
                {
                    cx = x;   // a row that is wider than the room goes on a line below (a language's longer words in a column of half a page)
                    y += UiTokens.Control + UiTokens.S2;
                }
                Button(s, at, lay, v, ref buttons[i], cx, y, bw, keys);
                cx += bw + UiTokens.S3;
                if (buttons[i].Disabled && !string.IsNullOrEmpty(buttons[i].Why) && why == null)
                {
                    why = buttons[i].Why;
                }
            }
            y += UiTokens.Control;
            if (why != null)
            {
                y += UiTokens.S1;
                int id = UiDraw.Text(s, at, x, y, w, UiStyle.Small, UiRole.Muted, why, UiFlags.Wrap | UiFlags.Truncate, 2);
                y = UiDraw.Snap(s.Items[id].Y + s.Items[id].H);
            }
            return y;
        }

        internal static float Sentence(UiScene s, UiAt at, string words, float x, float y, float w, UiRole ink = UiRole.Text2, int lines = 3, UiStyle style = UiStyle.Body)
        {
            if (string.IsNullOrEmpty(words))
            {
                return y;
            }
            int id = UiDraw.Text(s, at, x, y, w, style, ink, words, UiFlags.Wrap | UiFlags.Truncate, lines);
            return UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S2;
        }

        /// <summary>A line with words at the left and others at the right, each in a box of its own so they never overlap.</summary>
        internal static void Pair(UiScene s, UiAt at, float x, float y, float h, float w, UiStyle leftStyle, UiRole leftInk, string left, UiStyle rightStyle, UiRole rightInk, string right)
        {
            float rw = string.IsNullOrEmpty(right) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.6f), UiDraw.Snap(UiTextRules.Width(right, rightStyle) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x, y, h, w - rw - (rw > 0f ? UiTokens.S3 : 0f), leftStyle, leftInk, left ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            if (rw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - rw, y, h, rw, rightStyle, rightInk, right, UiFlags.Right | UiFlags.Truncate);
            }
        }

        internal static UiAt Band(UiScene s, UiAt at, float x, float y, float w, float h)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            return at.In(band);
        }

        /// <summary>Where a box of a size starts to stand in the middle of another, on the grid.</summary>
        internal static float Mid(float box, float size) => UiDraw.SnapDown((box - size) / 2f);

        internal static UiRole ToneInk(int tone) => tone > 0 ? UiRole.OkText : tone < 0 ? UiRole.DangerText : UiRole.Text;

        /// <summary>A tile with initials: a business's mark, the company's. No hue of its own: the tile's slate and the cyan of its initials.</summary>
        internal static void Mark(UiScene s, UiAt at, float x, float y, float size, string initials, UiStyle style = UiStyle.Title)
        {
            int tile = UiDraw.Box(s, at, x, y, size, size, UiRole.Tile, UiRole.None, size >= UiTokens.AvatarHero ? UiTokens.RadiusLarge : UiTokens.RadiusMedium, UiFlags.Decor);
            UiDraw.TextMiddle(s, at.In(tile), x, y, size, size, style, UiRole.AccentText, initials ?? string.Empty, UiFlags.Centre | UiFlags.Truncate);
        }

        /// <summary>The initials of a name, for a mark.</summary>
        public static string Initials(string name) => UiDraw.Initials(name);

        internal static string Number(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The page that stands in place of a tab whose part is off or stopped: the words, and the way to switch it on.</summary>
        internal static void OffPage(UiScene s, UiAt at, CompanyWords w, string title, string body, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            string head = string.IsNullOrEmpty(title) ? w.T("reallife_co_part_off_title", "This part is switched off") : title;
            string say = string.IsNullOrEmpty(body) ? w.T("reallife_co_part_off", "Switch it on in Options, Economy, to run this part of the company.") : body;
            CompanyLayout lay = _layNow;
            CompanyView v = _viewNow;
            if (lay == null || v == null)
            {
                UiDraw.EmptyState(s, at, x, y, cw, bottom - y, UiIcon.Gear, head, say, null, -1, keys);
                return;
            }
            float pad = UiTokens.S5, width = Math.Min(cw, 720f), size = UiTokens.IconTile;
            float tx = x + pad + size + UiTokens.S4, tw = width - pad * 2f - size - UiTokens.S4;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), bh = UiDraw.Snap(UiTextRules.WrappedHeight(say, UiStyle.Body, tw, 4));
            string section = v.Tab == CompanyView.Rota ? "rota" : v.Tab == CompanyView.Managers ? "managers" : "company";
            CompanyButton link = new CompanyButton { Label = w.T("reallife_set_link", "Settings"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Gear, Act = CompanyAct.Of(CompanyDo.OpenOptions,
                section) };
            float bw = Math.Min(tw, UiDraw.ButtonWidth(link.Kind, link.Label, link.Icon, link.Key, keys));
            float h = pad * 2f + th + UiTokens.S1 + bh + UiTokens.S4 + UiTokens.Control;
            UiAt band = Band(s, at, x, y, width, h);
            int tile = UiDraw.Tile(s, band, x + pad, y + pad, size, UiRole.None, false);
            float g = UiTokens.S6;
            UiDraw.Icon(s, band.In(tile), x + pad + (size - g) / 2f, y + pad + (size - g) / 2f, g, UiIcon.Gear, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            UiDraw.TextMiddle(s, band, tx, y + pad, th, tw, UiStyle.Title, UiRole.Text, head, UiFlags.Truncate);
            UiDraw.Text(s, band, tx, y + pad + th + UiTokens.S1, tw, UiStyle.Body, UiRole.Text2, say, UiFlags.Wrap | UiFlags.Truncate, 4);
            Button(s, band, lay, v, ref link, tx, y + pad + th + UiTokens.S1 + bh + UiTokens.S4, bw, keys);
        }

        private static CompanyLayout _layNow;   // the layout and the view of the page being laid out: the page of a part that is off needs them for its link, from any tab's face
        private static CompanyView _viewNow;
    }
}
