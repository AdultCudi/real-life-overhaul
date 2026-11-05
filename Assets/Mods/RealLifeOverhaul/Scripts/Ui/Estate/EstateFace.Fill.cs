using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class EstateFace
    {
        private const byte RegionStrip = 11, RegionShares = 12;

        // What fills a short page: a band of the page's figures under its head, its shares as graded meters with their percent, and a list of
        // the things related to it. None of them repeats a table or a sentence of the page: a figure said in a figure is not said again below.

        // The band of figures across the page, or the y unchanged with none. Returns the y under it.
        private static float StripBand(UiScene s, UiAt at, UiFigureModel[] figures, float x, float y, float w)
        {
            if (figures == null || figures.Length == 0)
            {
                return y;
            }
            return UiAppWindow.Figures(s, at.InRegion(RegionStrip), figures, x, y, w) - UiTokens.S3;   // a step closer to what stands under it: no wide gap between the figures and the cards
        }

        private static float StripHeight(UiFigureModel[] figures) => figures == null || figures.Length == 0 ? 0f
            : UiDraw.Snap(UiTokens.S3 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1
                + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display))) + UiTokens.S5 - UiTokens.S3;

        // The shares as meters in one column, or two where the width allows; each is its label, its bar and its percent. Returns the height.
        private static float SharesHeight(string[] labels, int[] values, string title, float w)
        {
            int n = labels == null || values == null ? 0 : Math.Min(labels.Length, values.Length);
            if (n == 0)
            {
                return 0f;
            }
            int columns = w >= UiTokens.S16 * 40f ? 2 : 1;
            int rows = (n + columns - 1) / columns;
            return UiDraw.Snap((string.IsNullOrEmpty(title) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3) + rows * UiTokens.PairRow + UiTokens.S3);
        }

        private static float Shares(UiScene s, UiAt at, string title, string[] labels, int[] values, float x, float y, float w, string[] texts = null, bool plain = false)
        {
            int n = labels == null || values == null ? 0 : Math.Min(labels.Length, values.Length);
            if (n == 0)
            {
                return y;
            }
            UiAt on = at.InRegion(RegionShares);
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.Text(s, on, x, y, w, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            }
            int columns = w >= UiTokens.S16 * 40f ? 2 : 1;
            float gap = UiTokens.S8, cw = UiDraw.SnapDown((w - gap * (columns - 1)) / columns);
            int rows = (n + columns - 1) / columns;
            float lw = UiDraw.LabelColumn(cw, labels);
            for (int i = 0; i < n; i++)
            {
                bool said = texts != null && i < texts.Length && !string.IsNullOrEmpty(texts[i]);
                if (plain)
                {
                    PlainBar(s, on, x + (i / rows) * (cw + gap), y + (i % rows) * UiTokens.PairRow, cw, labels[i], values[i], lw);
                    continue;
                }
                UiDraw.GradeBar(s, on, x + (i / rows) * (cw + gap), y + (i % rows) * UiTokens.PairRow, cw, labels[i], values[i], lw, said ? null : "%", said ? texts[i] : null);
            }
            return y + rows * UiTokens.PairRow + UiTokens.S3;
        }

        // A share of a whole (the buyers by rating): one colour and its percent, never a grade, since a third of them being A is no warning.
        private static void PlainBar(UiScene s, UiAt at, float x, float y, float w, string label, int value, float labelW)
        {
            float h = UiTokens.PairRow, fw = UiTextRules.Width("100%", UiStyle.BodyStrong) + UiTokens.S1;
            UiDraw.TextMiddle(s, at, x, y, h, labelW, UiStyle.Body, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            float bx = x + labelW + UiTokens.S3, bw = w - labelW - UiTokens.S3 - fw - UiTokens.S2;
            int track = UiDraw.Box(s, at, bx, y + (h - UiTokens.S1) / 2f, bw, UiTokens.S1, UiRole.Track, UiRole.None, UiTokens.S1 / 2f, UiFlags.Middle | UiFlags.Share);
            UiDraw.Box(s, at.In(track), bx, y + (h - UiTokens.S1) / 2f, Math.Max(UiTokens.S1, bw * Math.Max(0, Math.Min(100, value)) / 100f), UiTokens.S1, UiRole.Accent, UiRole.None,
                UiTokens.S1 / 2f, UiFlags.Middle | UiFlags.Share);
            string percent = Math.Max(0, Math.Min(100, value)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            UiDraw.TextMiddle(s, at, x + w - fw, y, h, fw, UiStyle.BodyStrong, UiRole.Text, percent, UiFlags.Right);
        }

        private static float RelatedHeight(EstateModel m) => m.Related == null || m.Related.Length == 0 ? 0f
            : UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + Math.Min(m.Related.Length, Math.Max(1, m.RelatedRows)) * RowHeight);

        // The related things as the roll's short rows under a label with their count at the right. Returns the y under them.
        private static float Related(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom)
        {
            if (m.Related == null || m.Related.Length == 0)
            {
                return y;
            }
            UiAt on = at.InRegion(RegionLists);
            y = Section(s, on, m.RelatedTitle, m.RelatedCount, x, y, w);
            for (int i = 0; i < m.Related.Length && i < m.RelatedRows && y + RowHeight <= bottom + 0.5f; i++)
            {
                Row(s, on, m.Related[i], x, y, w);
                y += RowHeight;
            }
            return y;
        }

        // Equal buttons in a block of their own (two or three across, as many rows as they need), under a label: never a ragged run of them.
        private static int GridColumns(float w, int count) => count <= 1 ? 1 : w >= UiTokens.S16 * 24f ? 3 : 2;

        private static float GridHeight(EstateModel m, float w)
        {
            int n = m.Grid == null ? 0 : m.Grid.Length;
            if (n == 0)
            {
                return 0f;
            }
            int rows = (n + GridColumns(w, n) - 1) / GridColumns(w, n);
            return UiDraw.Snap((string.IsNullOrEmpty(m.GridTitle) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2)
                + rows * UiTokens.Control + (rows - 1) * UiTokens.S2 + UiTokens.S3);
        }

        private static float Grid(UiScene s, UiAt at, EstateModel m, float x, float y, float w, IUiKeys keys)
        {
            int n = m.Grid == null ? 0 : m.Grid.Length;
            if (n == 0)
            {
                return y;
            }
            UiAt on = at.InRegion(RegionRight);
            if (!string.IsNullOrEmpty(m.GridTitle))
            {
                UiDraw.Text(s, on, x, y, w, UiStyle.SectionHeader, UiRole.Text2, m.GridTitle, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            int columns = GridColumns(w, n);
            float bw = UiDraw.SnapDown((w - UiTokens.S2 * (columns - 1)) / columns);
            for (int i = 0; i < n; i++)
            {
                ref UiActionModel a = ref m.Grid[i];
                UiDraw.Button(s, on, x + (i % columns) * (bw + UiTokens.S2), y + (i / columns) * (UiTokens.Control + UiTokens.S2), bw,
                    UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
            }
            return y + ((n + columns - 1) / columns) * (UiTokens.Control + UiTokens.S2) + UiTokens.S1;
        }

        private const float CardWidth = 1180f;

        // A decision about one thing as a card over the dimmed world, as tall as what it holds (a game's decision, not a window of empty floor): the
        // title strip with its close, then the page's own body: the way back and the actions, who or what it is about, its figures, its two columns.
        private static void CardLay(UiScene s, EstateModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("estate", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float cw = UiDraw.SnapDown(Math.Min(CardWidth, frame.Width - UiTokens.S8 * 2f)), inner = cw - UiTokens.S5 * 2f;
            // A decision card has no title strip and no back button: its kicker over its title, the body, and the way out at its foot (Esc), the safe answer.
            float kickH = string.IsNullOrEmpty(m.Kicker) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            float footH = string.IsNullOrEmpty(m.FootBack) ? 0f : UiTokens.S4 + UiTokens.Control;
            float body = m.Shape == EstateShape.Auction ? AuctionHeight(m, inner, keys) : DetailHeight(m, inner, keys);
            float h = UiDraw.Snap(Math.Min(UiTokens.S6 + kickH + body + footH + UiTokens.S6, frame.BandY - UiTokens.S8 * 2f));
            float x = UiDraw.SnapDown((frame.Width - cw) / 2f), y = UiDraw.SnapDown((frame.BandY - h) / 2f);
            int card = UiDraw.Box(s, UiAt.Screen, x, y, cw, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(card);
            float top = y + UiTokens.S6;
            if (kickH > 0f)
            {
                UiDraw.Text(s, at, x + UiTokens.S5, top, inner, UiStyle.SectionHeader, UiRole.Text2, m.Kicker, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                top += kickH;
            }
            float bottom = y + h - UiTokens.S6 - footH;
            if (m.Shape == EstateShape.Auction)
            {
                Auction(s, at, m, x + UiTokens.S5, top, inner, bottom, keys);
            }
            else
            {
                DetailPage(s, at, m, x + UiTokens.S5, top, inner, bottom, keys);
            }
            if (footH > 0f)
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.FootBack, UiIcon.None, UiAction.Back, keys);
                int way = UiDraw.Button(s, at, x + cw - UiTokens.S5 - bw, y + h - UiTokens.S6 - UiTokens.Control, bw, UiTokens.Control, UiButtonKind.Secondary, m.FootBack, UiIcon.None,
                    UiAction.Back, keys, BackCommand, 0);
                // The keys start on the card's main action (Bid, Make an offer, Repair), never on the way out; with no enabled main action the way out is all there is.
                int main = -1;
                for (int i = 0; i < s.NodeCount && main < 0; i++)
                {
                    main = s.Nodes[i].Key == UiAction.Alt && !s.Nodes[i].Disabled ? i : -1;
                }
                WrapWayOut(s);
                s.FocusFirst = main >= 0 ? main : way;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The auction card's body as tall as its parts: the way back and the kicker, then the car and its facts and the other lots, against the bidding.
        private static float AuctionHeight(EstateModel m, float w, IUiKeys keys)
        {
            float back = m.Card ? 0f : UiTokens.Control + UiTokens.S4;
            EstateAuction a = m.Auction;
            float lw = UiDraw.SnapDown((w - UiTokens.S6) * 0.42f), rw = w - UiTokens.S6 - lw;
            float picH = AuctionPicture(m, lw, UiFactCard.Height(a.Right, rw, keys, true), keys);
            float left = picH + UiTokens.S3 + UiFactCard.Height(a.Left, lw, keys, true) + (m.Related.Length > 0 ? UiTokens.S5 + RelatedHeight(m) : 0f);
            return back + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3 + Math.Max(left, UiFactCard.Height(a.Right, rw, keys, true));
        }

        // The car's picture: a tile of its own shape, taller where the bidding's column is, so the two columns end together and no slate stands bare under the facts.
        private static float AuctionPicture(EstateModel m, float lw, float rightH, IUiKeys keys)
        {
            EstateAuction a = m.Auction;
            float basic = Math.Min(UiDraw.SnapDown(lw / Math.Max(1f, a.Aspect)), UiTokens.S16 * 3f);
            float rest = UiTokens.S3 + UiFactCard.Height(a.Left, lw, keys, true) + (m.Related.Length > 0 ? UiTokens.S5 + RelatedHeight(m) : 0f);
            float slack = rightH - (basic + rest);
            return UiDraw.SnapDown(basic + Math.Max(0f, Math.Min(slack, lw * 0.8f - basic)));
        }

        // A card's body as tall as its parts: the way back, the person or thing, the figures and the taller of the two columns.
        private static float DetailHeight(EstateModel m, float w, IUiKeys keys)
        {
            float h = (m.Card && m.DetailActions.Length == 0 ? 0f : UiTokens.Control + UiTokens.S4) + TileBig + UiTokens.S4 + StripHeight(m.Strip);
            bool related = m.Related != null && m.Related.Length > 0, grid = m.Grid != null && m.Grid.Length > 0, shares = m.ShareLabels != null && m.ShareLabels.Length > 0;
            bool related2 = m.Related2 != null && m.Related2.Length > 0, person = m.Person != null && !string.IsNullOrEmpty(m.Person.TenantName);
            bool cards2 = m.Left.Length > 0 && m.Right.Length > 0;
            bool both = cards2 || (related || related2 || grid || shares || person) && (m.Left.Length > 0 || m.Right.Length > 0 || related && (grid || shares || person || related2));
            float cw = both ? UiDraw.SnapDown((w - UiTokens.S4) / 2f) : UiDraw.SnapDown(Math.Min(w, UiTokens.S16 * 40f)), rw = w - cw - UiTokens.S4;
            UiFactCardModel[] leftCards = m.Left.Length > 0 ? m.Left : m.Right, rightCards = cards2 ? m.Right : new UiFactCardModel[0];
            float left = Stack(leftCards, cw, keys) + RelatedHeight(m);
            float right = Stack(rightCards, rw, keys) + (person ? PersonHeight(m.Person, rw) : 0f) + GridHeight(m, rw) + SharesHeight(m.ShareLabels, m.ShareValues, m.ShareTitle, both ? rw : cw);
            return h + Math.Max(left, right) + (related2 ? UiTokens.S3 + Related2Height(m, w) : 0f);
        }

        private static float PersonHeight(EstateUnitPage p, float w)
        {
            if (p == null || string.IsNullOrEmpty(p.TenantName))
            {
                return 0f;
            }
            return UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3 + UiTokens.AvatarHero + UiTokens.S3
                + Math.Min(p.MeterLabels.Length, p.MeterValues.Length) * UiTokens.PairRow + UiTokens.S3);
        }

        // A list across a wide page runs in two columns, half the rows in each, so a name and its figure stand a row's width apart and no more.
        private static bool Pairs(float w, int count) => count >= 2 && w >= PairFrom;

        private static float Related2Height(EstateModel m, float w)
        {
            if (m.Related2 == null || m.Related2.Length == 0)
            {
                return 0f;
            }
            int shown = Math.Min(m.Related2.Length, Math.Max(1, m.RelatedRows));
            return UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + (Pairs(w, shown) ? (shown + 1) / 2 : shown) * RowHeight);
        }

        private static float Related2(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom)
        {
            if (m.Related2 == null || m.Related2.Length == 0)
            {
                return y;
            }
            UiAt on = at.InRegion(RegionLists);
            y = Section(s, on, m.Related2Title, m.Related2Count, x, y, w);
            int shown = Math.Min(m.Related2.Length, Math.Max(1, m.RelatedRows));
            if (Pairs(w, shown))
            {
                int left = (shown + 1) / 2;
                float half = UiDraw.SnapDown((w - UiTokens.S6) / 2f);
                for (int i = 0; i < shown; i++)
                {
                    float ry = y + (i < left ? i : i - left) * RowHeight;
                    if (ry + RowHeight <= bottom + 0.5f)
                    {
                        Row(s, on, m.Related2[i], i < left ? x : x + half + UiTokens.S6, ry, half);
                    }
                }
                return y + left * RowHeight;
            }
            for (int i = 0; i < shown && y + RowHeight <= bottom + 0.5f; i++)
            {
                Row(s, on, m.Related2[i], x, y, w);
                y += RowHeight;
            }
            return y;
        }

        // A section with nothing in it: its glyph on a tile, what it is and the card that starts it, as one group in the middle of the page.
        private static void EmptyPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float cw = UiDraw.SnapDown(Math.Min(w, UiTokens.S16 * 30f));
            float cardH = m.Side != null ? UiFactCard.Height(m.Side, cw, keys, true) : 0f;
            float titleH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)), bodyH = UiDraw.Snap(UiTextRules.WrappedHeight(m.OffBody ?? string.Empty, UiStyle.Body, cw, 3));
            float groupH = TileBig + UiTokens.S4 + titleH + UiTokens.S2 + bodyH + (cardH > 0f ? UiTokens.S6 + cardH : 0f);
            float top = y + UiDraw.SnapDown(Math.Max(0f, bottom - y - groupH) / 2f), cx = x + UiDraw.SnapDown((w - cw) / 2f);
            UiAt on = at.InRegion(RegionHead);
            int tile = UiDraw.Tile(s, on, cx + UiDraw.SnapDown((cw - TileBig) / 2f), top, TileBig, UiRole.None, false);
            UiDraw.Icon(s, on.In(tile), cx + UiDraw.SnapDown((cw - UiTokens.S8) / 2f), top + (TileBig - UiTokens.S8) / 2f, UiTokens.S8, m.OffIcon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            top += TileBig + UiTokens.S4;
            UiDraw.Text(s, on, cx, top, cw, UiStyle.Display, UiRole.Text, m.OffTitle, UiFlags.Centre | UiFlags.Truncate | UiFlags.Middle);
            top += titleH + UiTokens.S2;
            UiDraw.Text(s, on, cx, top, cw, UiStyle.Body, UiRole.Text2, m.OffBody, UiFlags.Centre | UiFlags.Wrap | UiFlags.Label, 3);
            top += bodyH + UiTokens.S6;
            if (m.Side != null)
            {
                UiFactCard.Lay(s, at.InRegion(RegionRight), m.Side, cx, top, cw, cardH, keys, true);
            }
        }

        // The foot of a page under its table: up to three blocks side by side (the shares, the related things, a second list), or one across the page.
        private static int FootBlocks(EstateModel m) => (m.ShareLabels != null && m.ShareLabels.Length > 0 ? 1 : 0) + (m.Related != null && m.Related.Length > 0 ? 1 : 0)
            + (m.Related2 != null && m.Related2.Length > 0 ? 1 : 0);

        // One list alone across a wide page would stand its name and its figures 800 px apart: it runs in two columns, half the rows in each.
        private static bool FootPaired(EstateModel m, float w) =>
            FootBlocks(m) == 1 && m.Related != null && m.Related.Length >= 2 && (m.ShareLabels == null || m.ShareLabels.Length == 0) && w >= UiTokens.S16 * 16f;

        private static float FootHeight(EstateModel m, float w)
        {
            int n = FootBlocks(m);
            if (n == 0)
            {
                return 0f;
            }
            if (FootPaired(m, w))
            {
                int rows = (Math.Min(m.Related.Length, Math.Max(1, m.RelatedRows)) + 1) / 2;
                return UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + rows * RowHeight);
            }
            float each = UiDraw.SnapDown((w - UiTokens.S6 * (n - 1)) / n);
            float h = m.ShareLabels != null && m.ShareLabels.Length > 0 ? SharesHeight(m.ShareLabels, m.ShareValues, m.ShareTitle, each) : 0f;
            return Math.Max(h, Math.Max(RelatedHeight(m), Related2Height(m, each)));
        }

        private static void Foot(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom)
        {
            if (FootPaired(m, w))
            {
                UiAt on = at.InRegion(RegionLists);
                float ry = Section(s, on, m.RelatedTitle, m.RelatedCount, x, y, w), half = UiDraw.SnapDown((w - UiTokens.S6) / 2f);
                int count = Math.Min(m.Related.Length, Math.Max(1, m.RelatedRows)), left = (count + 1) / 2;
                for (int i = 0; i < count; i++)
                {
                    Row(s, on, m.Related[i], i < left ? x : x + half + UiTokens.S6, ry + (i < left ? i : i - left) * RowHeight, half);
                }
                return;
            }
            int n = FootBlocks(m);
            float each = UiDraw.SnapDown((w - UiTokens.S6 * (n - 1)) / n), bx = x;
            if (m.ShareLabels != null && m.ShareLabels.Length > 0)
            {
                Shares(s, at, m.ShareTitle, m.ShareLabels, m.ShareValues, bx, y, each, m.ShareTexts, m.SharePlain);
                bx += each + UiTokens.S6;
            }
            if (m.Related != null && m.Related.Length > 0)
            {
                Related(s, at, m, bx, y, each, bottom);
                bx += each + UiTokens.S6;
            }
            if (m.Related2 != null && m.Related2.Length > 0)
            {
                Related2(s, at, m, bx, y, each, bottom);
            }
        }
    }
}
