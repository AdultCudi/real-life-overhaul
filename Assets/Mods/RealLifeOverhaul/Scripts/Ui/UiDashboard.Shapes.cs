using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a page is about. One look for every page reads as a template, so each job gets its own shape.</summary>
    public enum UiPageShape : byte
    {
        Board = 0,   // a place at a glance: its figures in a strip, its list as a table, its facts at the side
        Profile,     // a person: their face, name and state beside their figures and facts
        Ledger,      // a household: its month and its home as a ledger, its people beside it
        Feed         // Flare: entries with their faces down a column, the account at the side
    }

    /// <summary>A section of a ledger: its caption and its lines.</summary>
    public struct UiLedgerSection
    {
        public string Title;
        public UiPairModel[] Lines;
    }

    /// <summary>
    /// The dashboard's other shapes. A person's page is a profile, a household's a ledger entry and Flare a feed, since
    /// a row of boxed figures over a table fits a city and nothing else. Every panel is sized to what it holds and two
    /// panels side by side share a foot when they nearly do. Rows a press opens are targets, the rest are words only.
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static partial class UiDashboard
    {
        private const float CardWidth = SideWidth;   // a profile's card, as wide as the side column so the pages line up

        private static void Shaped(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            switch (m.Shape)
            {
                case UiPageShape.Profile: Profile(s, at, m, x, y, w, keys); break;
                case UiPageShape.Ledger: Ledger(s, at, m, x, y, w, h); break;
                default: Feed(s, at, m, x, y, w, h); break;
            }
        }

        // ------------------------------------------------------------------------------------------------- profile

        // A person's page as one profile: their face, name and state across its head with where the page leads at the right
        // (as Contacts heads a contact), then their figures, then their facts in two columns, each part ruled off the next.
        private static void Profile(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, IUiKeys keys)
        {
            const float face = UiTokens.AvatarHero;
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero));
            bool status = !string.IsNullOrEmpty(m.Subtitle);
            int figures = Math.Min(m.FigureCount, MostFigures);
            float stripH = figures > 0 ? StripHeight(m) : 0f;
            int rows = (m.SideCount + 1) / 2;
            float headH = UiTokens.S6 + face + UiTokens.S6;
            float h = headH + (figures > 0 ? UiTokens.S5 + stripH + UiTokens.S5 : 0f) + (m.SideCount > 0 ? UiTokens.S5 + Caption() + rows * UiTokens.PairRow + UiTokens.S4 : 0f);
            int panel = UiDraw.Box(s, at, x, y, w, UiDraw.Snap(h), UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = at.In(panel);
            float fx = x + UiTokens.S6, fy = y + UiTokens.S6;
            UiDraw.Circle(s, inside, fx - UiTokens.S1, fy - UiTokens.S1, face + UiTokens.S2, face + UiTokens.S2, UiRole.Accent, UiFlags.Decor);
            UiDraw.Circle(s, inside, fx - UiTokens.Hairline * 2f, fy - UiTokens.Hairline * 2f, face + UiTokens.S1, face + UiTokens.S1, UiRole.Panel, UiFlags.Decor);
            UiDraw.Avatar(s, inside, fx, fy, face, m.Title, m.Hue);
            float right = x + w - UiTokens.S6;
            if (m.HasAction)
            {
                float bw = UiDraw.ButtonWidth(m.Action.Kind, m.Action.Label, m.Action.Icon, m.Action.Key, keys);
                right -= bw;
                UiDraw.Button(s, inside, right, fy + UiDraw.SnapDown((face - UiTokens.Control) / 2f), bw, UiTokens.Control, m.Action.Kind, m.Action.Label, m.Action.Icon,
                    m.Action.Key, keys, ActionCommand, 0, m.Action.Disabled);
                right -= UiTokens.S8;
            }
            float tx = fx + face + UiTokens.S6, tw = right - tx;
            float block = nameH + (status ? UiTokens.S2 + UiTokens.S8 : 0f);
            float ty = fy + UiDraw.SnapDown((face - block) / 2f);
            UiDraw.Text(s, inside, tx, ty, tw, UiStyle.Hero, UiRole.Text, m.Title, UiFlags.Truncate);
            if (status)
            {
                UiDraw.StatusChip(s, inside, tx, ty + nameH + UiTokens.S2, m.Subtitle, UiTone.Quiet);
            }
            float py = y + headH;
            if (figures > 0)
            {
                UiDraw.Box(s, inside, x + UiTokens.S5, py, w - UiTokens.S5 * 2f, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                py += UiTokens.S5;
                Strip(s, inside, m, x, py, w, stripH, false);
                py += stripH + UiTokens.S5;
            }
            if (m.SideCount > 0)
            {
                UiDraw.Box(s, inside, x + UiTokens.S5, py, w - UiTokens.S5 * 2f, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                py += UiTokens.S5;
                UiDraw.Text(s, inside, x + UiTokens.S5, py, w - UiTokens.S5 * 2f, UiStyle.SectionHeader, UiRole.Muted, m.SideTitle, UiFlags.Label | UiFlags.Truncate);
                py += Caption();
                float colW = UiDraw.SnapDown((w - UiTokens.S5 * 2f - UiTokens.S12) / 2f);
                for (int i = 0; i < m.SideCount; i++)
                {
                    float cx = x + UiTokens.S5 + (i < rows ? 0f : colW + UiTokens.S12);
                    UiDraw.KeyValue(s, inside, cx, py + (i % rows) * UiTokens.PairRow, colW, m.Side[i].Key, m.Side[i].Value);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- ledger

        // The household's month and home as a ledger at the left, its people at the right.
        private static void Ledger(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, float h)
        {
            float lw = UiDraw.SnapDown((w - UiTokens.S4) * 0.56f), px = x + lw + UiTokens.S4, pw = x + w - px;
            float ledgerH = UiTokens.S5;
            for (int i = 0; i < m.Ledger.Length; i++)
            {
                ledgerH += (i > 0 ? UiTokens.S6 : 0f) + Caption() + m.Ledger[i].Lines.Length * UiTokens.PairRow;
            }
            ledgerH += UiTokens.S5;
            float peopleH = EntriesHeight(m, pw, h);
            float panelH = UiDraw.Snap(Math.Abs(ledgerH - peopleH) <= UiTokens.MostSlack ? Math.Max(ledgerH, peopleH) : ledgerH);

            int ledger = UiDraw.Box(s, at, x, y, lw, panelH, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inLedger = at.In(ledger);
            float ly = y + UiTokens.S5, iw = lw - UiTokens.S5 * 2f;
            for (int i = 0; i < m.Ledger.Length; i++)
            {
                ly += i > 0 ? UiTokens.S6 : 0f;
                UiDraw.Text(s, inLedger, x + UiTokens.S5, ly, iw, UiStyle.SectionHeader, UiRole.Muted, m.Ledger[i].Title, UiFlags.Label | UiFlags.Truncate);
                ly += Caption();
                foreach (UiPairModel line in m.Ledger[i].Lines)
                {
                    ly += Line(s, inLedger, x + UiTokens.S5, ly, iw, line.Key, line.Value);
                }
            }
            Entries(s, at, m, px, y, pw, Math.Abs(ledgerH - peopleH) <= UiTokens.MostSlack ? panelH : peopleH, h);
        }

        // A ledger's line: what it is at the left, its sum at the right in the serif's even figures, a rule under them.
        private static float Line(UiScene s, UiAt at, float x, float y, float w, string label, string value)
        {
            float h = UiTokens.PairRow;
            float vw = Math.Min(UiTextRules.Width(value, UiStyle.Body) + UiTokens.S1, w * 0.5f);
            UiDraw.TextMiddle(s, at, x, y, h, w - vw - UiTokens.S4, UiStyle.Body, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + w - vw, y, h, vw, UiStyle.Body, UiRole.Text, value, UiFlags.Right | UiFlags.Truncate);
            UiDraw.Box(s, at, x, y + h - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            return h;
        }

        // ------------------------------------------------------------------------------------------------- feed

        // The entries down a column with their faces, the account's figures and facts at the side.
        private static void Feed(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, float h)
        {
            int figures = Math.Min(m.FigureCount, MostFigures);
            bool side = figures > 0 || m.SideCount > 0;
            float fw = side ? w - SideWidth - UiTokens.S4 : w;
            float sideH = 0f;
            if (side)
            {
                float figure = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
                sideH = UiTokens.S5 + figures * figure + Math.Max(0, figures - 1) * UiTokens.S6
                        + (m.SideCount > 0 ? (figures > 0 ? UiTokens.S6 : 0f) + Caption() + m.SideCount * UiTokens.PairRow : 0f) + UiTokens.S5;
                sideH = UiDraw.Snap(Math.Min(sideH, h));
            }
            float feedH = EntriesHeight(m, fw, h);
            bool level = side && Math.Abs(feedH - sideH) <= UiTokens.MostSlack;
            Entries(s, at, m, x, y, fw, level ? Math.Max(feedH, sideH) : feedH, h);
            if (!side)
            {
                return;
            }
            float sx = x + fw + UiTokens.S4;
            int panel = UiDraw.Box(s, at, sx, y, SideWidth, level ? Math.Max(feedH, sideH) : sideH, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inSide = at.In(panel);
            float sy = y + UiTokens.S5, iw = SideWidth - UiTokens.S5 * 2f;
            for (int i = 0; i < figures; i++)
            {
                UiDraw.Text(s, inSide, sx + UiTokens.S5, sy, iw, UiStyle.SectionHeader, UiRole.Muted, m.Figures[i].Label, UiFlags.Label | UiFlags.Truncate);
                sy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                var count = new UiItem
                {
                    Kind = UiKind.Text, Flags = UiFlags.Truncate | UiFlags.CountUp, Ink = UiRole.Text, Style = UiStyle.Display, X = sx + UiTokens.S5, Y = sy, W = iw,
                    H = UiTextRules.LineHeight(UiStyle.Display), Text = m.Figures[i].Value ?? string.Empty, Clip = inSide.Clip, Node = -1, Layer = inSide.Layer,
                    Order = (short)i, Parent = inSide.Parent, Alpha = 1f, Value = 1f
                };
                s.Add(ref count);
                sy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + (i < figures - 1 ? UiTokens.S6 : 0f);
            }
            if (m.SideCount > 0)
            {
                sy += figures > 0 ? UiTokens.S6 : 0f;
                UiDraw.Text(s, inSide, sx + UiTokens.S5, sy, iw, UiStyle.SectionHeader, UiRole.Muted, m.SideTitle, UiFlags.Label | UiFlags.Truncate);
                sy += Caption();
                for (int i = 0; i < m.SideCount && sy + UiTokens.PairRow <= y + h - UiTokens.S5; i++)
                {
                    sy += UiDraw.KeyValue(s, inSide, sx + UiTokens.S5, sy, iw, m.Side[i].Key, m.Side[i].Value);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- entries

        // How tall a panel of entries is at most a height: its caption and the rows that fit whole, or its empty state.
        private static float EntriesHeight(UiDashboardModel m, float w, float most)
        {
            float head = UiTokens.S5 + (string.IsNullOrEmpty(m.EntriesTitle) ? 0f : Caption());
            if (m.EntryCount == 0)
            {
                // A feed with nothing yet still reads as the page's main column, not a strip at its top.
                return UiDraw.Snap(Math.Min(most, Math.Max(UiTokens.S16 * 4f, head + UiDraw.EmptyHeight(w, m.Table.EmptyTitle, m.Table.EmptyBody))));
            }
            int fit = Math.Max(1, (int)((most - head - UiTokens.S5 + UiTokens.S2) / (UiTokens.Row + UiTokens.S2)));
            int rows = Math.Min(m.EntryCount, fit);
            return head + rows * (UiTokens.Row + UiTokens.S2) - UiTokens.S2 + UiTokens.S5;
        }

        // A panel of entries as rows with faces, the ones a press opens as targets; its empty state, wrapped inside it, when there are none.
        private static void Entries(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, float h, float most)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = at.In(panel);
            float ey = y + UiTokens.S5;
            if (!string.IsNullOrEmpty(m.EntriesTitle))
            {
                UiDraw.Text(s, inside, x + UiTokens.S5, ey, w - UiTokens.S5 * 2f, UiStyle.SectionHeader, UiRole.Muted, m.EntriesTitle, UiFlags.Label | UiFlags.Truncate);
                ey += Caption();
            }
            if (m.EntryCount == 0)
            {
                UiDraw.Empty(s, inside, x, ey, w, y + h - UiTokens.S5 - ey, m.Table.EmptyTitle, m.Table.EmptyBody);
                return;
            }
            for (int i = 0; i < m.EntryCount && ey + UiTokens.Row <= y + h - UiTokens.S5 + 0.5f; i++)
            {
                bool leads = m.EntryLeads == null || (i < m.EntryLeads.Length && m.EntryLeads[i]);
                if (i > 0)
                {
                    UiDraw.Box(s, inside, x + UiTokens.S5, ey - UiTokens.S1 - UiTokens.Hairline, w - UiTokens.S5 * 2f, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                }
                UiDraw.ListRow(s, inside.Arriving(i), x + UiTokens.S4, ey, w - UiTokens.S8, ref m.Entries[i], RowCommand, i, -1, i, leads, true);
                ey += UiTokens.Row + UiTokens.S2;
            }
        }

        // ------------------------------------------------------------------------------------------------- shared

        // A caption's line and the step under it.
        private static float Caption() => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;

        // The figures' strip's height: a label, the number, and a change's line when any figure has one (no sparkline here).
        private static float StripHeight(UiDashboardModel m)
        {
            bool change = false;
            for (int i = 0; i < m.FigureCount && i < MostFigures; i++)
            {
                change |= !string.IsNullOrEmpty(m.Figures[i].Change);
            }
            return UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display))
                               + (change ? UiDraw.Snap(Math.Max(UiTextRules.LineHeight(UiStyle.SmallStrong), UiTokens.Icon)) : 0f));
        }

        // Figures across a width inside a panel, a thin rule between each two.
        private static void Strip(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, float h, bool lines)
        {
            int count = Math.Min(m.FigureCount, MostFigures);
            float slot = UiDraw.SnapDown((w - UiTokens.S5 * 2f - UiTokens.S8 * (count - 1)) / count);
            for (int i = 0; i < count; i++)
            {
                float fx = x + UiTokens.S5 + i * (slot + UiTokens.S8);
                if (i > 0)
                {
                    UiDraw.Box(s, at, fx - UiTokens.S4, y, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                }
                UiFigureModel f = m.Figures[i];
                if (!lines)
                {
                    f.Trend = null;
                }
                UiDraw.Figure(s, at.Arriving(i), fx, y, slot, ref f, true);
            }
        }
    }

    /// <summary>
    /// A dashboard screen's scroll, kept between layouts: its screen makes the model afresh each time, so where its tables
    /// stand lives here. The d-pad past the last row in view brings the next one in, and the wheel moves a table, so no
    /// row of a long table is out of reach. Game-free. Cost: a few floats.
    /// </summary>
    public sealed class UiDashboardScroll
    {
        private float _main, _more;
        private int _revealMain = -1, _revealMore = -1;

        /// <summary>A fresh model's tables set where they were, before it is laid out.</summary>
        public void Apply(UiDashboardModel m)
        {
            m.Table.Offset = _main;
            m.Table.Reveal = _revealMain;
            if (m.More != null)
            {
                m.More.Offset = _more;
                m.More.Reveal = _revealMore;
            }
        }

        /// <summary>Where the tables came to once laid out (UiDraw.Table writes its offset back).</summary>
        public void Keep(UiDashboardModel m)
        {
            _main = m.Table.Offset;
            _more = m.More != null ? m.More.Offset : 0f;
            _revealMain = _revealMore = -1;
        }

        /// <summary>The d-pad wants a row out of view: the main table's list is the scene's first, the second table's the next.</summary>
        public void Reveal(int list, int index)
        {
            if (list <= 0)
            {
                _revealMain = index;
            }
            else
            {
                _revealMore = index;
            }
        }

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            if (clip <= 0)
            {
                _main = Math.Max(0f, _main + by);
            }
            else
            {
                _more = Math.Max(0f, _more + by);
            }
        }

        /// <summary>A new page starts at its top.</summary>
        public void Reset()
        {
            _main = _more = 0f;
            _revealMain = _revealMore = -1;
        }
    }
}
