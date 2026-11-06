using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The sections of a place's page, each in the shape its job needs and all on one two-column split, so every page of
    /// BizMan lines up with the next: a building as its photo beside its facts with the lease and the offers under them;
    /// starting a business as a catalogue of the kinds; insight as customers over time beside the capacity, then promotion
    /// and satisfaction as graded bars; prices as the week and the best sellers over the goods' table with each price to
    /// step; deliveries as the contracts and the chosen one's terms beside its goods; marketing as campaigns with their
    /// switches beside the agencies to call; settings and real estate as two columns of rows and controls; and a section only
    /// the game's own view shows as a clear handover. Game-free.
    /// </summary>
    public static partial class BizManPages
    {
        private const float PhotoAspect = 1.6f; // the building's photo tile, as the game frames it
        private const float ItemRow = 56f;      // px: a contract's or an agency's row, a logo and two lines
        private const float LeastChart = 200f;  // px: the customers' chart at its shortest
        private const float MostChart = 400f;   // px: and tallest

        // The page's two columns: each half of its width with the one gap between, the same on every page.
        private static float Half(float w) => UiDraw.SnapDown((w - UiTokens.S8) / 2f);

        // ------------------------------------------------------------------------------------------------- a building

        // The building's photo in the left column, its facts in the right with what is said about it, its lease and its
        // offers under them where the column holds them all; a page with more (a rival's hours and two offers) puts what is
        // said and the first offer under the photo, the facts, the hours and the second offer down the right. The actions at
        // the foot.
        private static void BuildingPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float half = Half(w), rx = x + half + UiTokens.S8;
            float room = bottom - y - (m.Actions.Length > 0 ? UiTokens.Control + UiTokens.S6 : 0f);
            float stacked = TitleHeight(m.FactsTitle) + Count(m.Facts) * UiTokens.PairRow + UiTokens.S6 + LeftHeight(m, half) + RightHeight(m, half);
            if (stacked <= room)
            {
                Picture(s, at, m.Picture, m.PictureAspect, UiIcon.Image, x, y, half, UiDraw.SnapDown(Math.Min(half / PhotoAspect, room)));
                float sy = Title(s, at, rx, y, half, m.FactsTitle);
                sy = Facts(s, at, m.Facts, rx, sy, half) + UiTokens.S6;
                sy = Title(s, at, rx, sy, half, m.PartTitle);
                sy = Lead(s, at, m.Lead, rx, sy, half);
                sy = Amount(s, at, m.Amount, rx, sy, half, keys);
                if (m.More.Length > 0)
                {
                    sy = Title(s, at, rx, sy, half, m.MoreTitle);
                    sy = Facts(s, at, m.More, rx, sy, half) + UiTokens.S6;
                }
                Amount(s, at, m.Second, rx, sy, half, keys);
                Buttons(s, at, m.Actions, x + w, bottom - UiTokens.Control, keys, Action);
                return;
            }
            // The photo gives way to what is said under it, so the first offer always fits above the foot.
            float ph = UiDraw.SnapDown(Math.Max(UiTokens.S16 * 4f, Math.Min(half / PhotoAspect, room - LeftHeight(m, half) - UiTokens.S6)));
            float pw = string.IsNullOrEmpty(m.Picture) || m.PictureAspect <= 0f ? half : Math.Min(half, UiDraw.SnapDown(ph * m.PictureAspect));
            Picture(s, at, m.Picture, m.PictureAspect, UiIcon.Image, x, y, pw, ph);
            float top = UiDraw.Snap(y + ph) + UiTokens.S6;
            float ly = Title(s, at, x, top, pw, m.PartTitle);
            ly = Lead(s, at, m.Lead, x, ly, pw);
            Amount(s, at, m.Amount, x, ly, pw, keys);
            float fx = x + pw + UiTokens.S8, fw = x + w - fx;
            _split = fx - UiTokens.S4;   // the pad's columns part where the photo's column ends
            float ry = Title(s, at, fx, y, fw, m.FactsTitle);
            ry = Facts(s, at, m.Facts, fx, ry, fw) + UiTokens.S6;
            if (m.More.Length > 0)
            {
                ry = Title(s, at, fx, ry, fw, m.MoreTitle);
                ry = Facts(s, at, m.More, fx, ry, fw) + UiTokens.S6;
            }
            Amount(s, at, m.Second, fx, ry, fw, keys);
            Buttons(s, at, m.Actions, x + w, bottom - UiTokens.Control, keys, Action);
        }

        private static int Count(BizManFact[] facts)
        {
            int n = 0;
            for (int i = 0; i < facts.Length; i++)
            {
                n += string.IsNullOrEmpty(facts[i].Value) ? 0 : 1;
            }
            return n;
        }

        // The heights of the two blocks under the photo, as BuildingPage lays them.
        private static float LeftHeight(BizManModel m, float w) => TitleHeight(m.PartTitle) + LeadHeight(m.Lead, w) + AmountHeight(m.Amount);

        private static float RightHeight(BizManModel m, float w) =>
            (m.More.Length > 0 ? TitleHeight(m.MoreTitle) + Count(m.More) * UiTokens.PairRow + UiTokens.S6 : 0f) + AmountHeight(m.Second);

        private static float TitleHeight(string title) => string.IsNullOrEmpty(title) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;

        private static float LeadHeight(string words, float w) => string.IsNullOrEmpty(words) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(words, UiStyle.Body, w, 3)) + UiTokens.S4;

        private static float AmountHeight(BizManAmount a) => a == null ? 0f
            : TitleHeight(a.Title) + (string.IsNullOrEmpty(AmountLine(a)) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2) + UiTokens.Control + UiTokens.S6;

        // ------------------------------------------------------------------------------------------------- insight

        // Customers over time in the left column with the period beside its title, the capacity in the right, and under them
        // promotion and satisfaction as graded bars, one a column.
        private static void InsightPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom)
        {
            float half = Half(w), rx = x + half + UiTokens.S8;
            int rows = Math.Max(m.Grades.Length, m.SecondGrades.Length > 0 ? m.SecondGrades.Length : 3);
            float gradesH = TitleHeight(m.GradesTitle) + rows * UiTokens.PairRow;
            float heroH = 0f;
            if (m.Figures.Length > 0)
            {
                heroH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S4;
            }
            float ch = UiDraw.SnapDown(Math.Max(LeastChart, Math.Min(MostChart, bottom - y - UiTokens.Control - UiTokens.S2 - heroH - UiTokens.S6 - gradesH)));
            float tabsW = PeriodWidth(m.Periods);
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, half - tabsW - UiTokens.S4, UiStyle.SectionHeader, UiRole.Text, m.PartTitle, UiFlags.Label | UiFlags.Truncate);
            if (m.Periods.Length > 1)
            {
                UiDraw.Tabs(s, at, x + half - tabsW, y, tabsW, m.Periods, m.Period, null, Period);
            }
            float chartY = y + UiTokens.Control + UiTokens.S2;
            if (m.Figures.Length > 0)
            {
                UiDraw.Figure(s, at, x, chartY, half, ref m.Figures[0], true);
                chartY += heroH;
            }
            if (m.Chart != null)
            {
                UiChart.Lay(s, at, m.Chart, x, chartY, half, ch);
            }
            float cy = Capacity(s, at, m, rx, y, half, chartY + ch);
            Grades(s, at, m.GradesTitle, m.Grades, null, x, chartY + ch + UiTokens.S6, half);
            Grades(s, at, m.SecondTitle, m.SecondGrades, m.SecondEmpty, rx, cy + UiTokens.S4, half);
        }

        // The period's tabs' width, as UiDraw.Tabs lays them.
        private static float PeriodWidth(string[] periods)
        {
            float sum = 0f;
            for (int i = 0; i < periods.Length; i++)
            {
                sum += UiDraw.Snap(UiTextRules.Width(periods[i], UiStyle.Nav) + UiTokens.S4 * 2f) + (i > 0 ? UiTokens.S1 : 0f);
            }
            return sum;
        }

        // The capacity: how many customers an hour the shelves allow against the building's limit, then each shelf that caps
        // it with a bar of its share in its tone.
        private static float Capacity(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom)
        {
            float cy = RowTitle(s, at, x, y, w, m.CapacityTitle);
            cy = Lead(s, at, m.CapacityLine, x, cy, w);
            float rowH = UiTokens.PairRow * 2f;
            for (int i = 0; i < m.Capacity.Length && cy + rowH <= bottom; i++)
            {
                ref BizManCapacity c = ref m.Capacity[i];
                float fw = UiDraw.Snap(UiTextRules.Width(c.Figure ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, w - fw - UiTokens.S3, UiStyle.Body, UiRole.Text, c.Name, UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + w - fw, cy, UiTokens.PairRow, fw, UiStyle.BodyStrong, UiRole.Text, c.Figure, UiFlags.Right);
                UiDraw.Meter(s, at, x, cy + UiTokens.PairRow + UiTokens.S1, w, UiTokens.S1, c.Share, c.Tone);
                cy += rowH;
            }
            return cy;
        }

        // A block of graded bars under its title, their tracks from one edge; or why there is nothing to grade yet.
        private static float Grades(UiScene s, UiAt at, string title, BizManGrade[] grades, string empty, float x, float y, float w)
        {
            y = Title(s, at, x, y, w, title);
            if (grades.Length == 0)
            {
                return Lead(s, at, empty, x, y, w);
            }
            var labels = new string[grades.Length];
            for (int i = 0; i < grades.Length; i++)
            {
                labels[i] = grades[i].Label;
            }
            float lw = UiDraw.LabelColumn(w, labels);
            for (int i = 0; i < grades.Length; i++)
            {
                y += UiDraw.GradeBar(s, at, x, y, w, grades[i].Label, grades[i].Value, lw);
            }
            return y;
        }

        // ------------------------------------------------------------------------------------------------- prices

        // What the week sold in the left column and the best sellers as a donut with its legend in the right, then the goods
        // as the big table across the page, each price to step.
        private static void PricingPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float half = Half(w), rx = x + half + UiTokens.S8;
            float top = y;
            if (m.More.Length > 0 || m.Shares.Length > 0)
            {
                float ly = Title(s, at, x, y, half, m.MoreTitle);
                ly = Facts(s, at, m.More, x, ly, half);
                float ry = y;
                if (m.Shares.Length > 0)
                {
                    ry = Title(s, at, rx, y, half, m.SharesTitle);
                    float d = UiDraw.SnapDown(Math.Max(UiTokens.S16, Math.Min(UiTokens.S16 + UiTokens.S8, m.ShareLabels.Length * UiTokens.PairRow)));
                    UiDraw.Donut(s, at, rx, ry, d, m.Shares, m.Shares.Length, m.ShareRest);
                    float legend = UiDraw.Legend(s, at, rx + d + UiTokens.S6, ry, half - d - UiTokens.S6, m.ShareLabels, m.ShareValues, m.ShareLabels.Length);
                    ry += Math.Max(d, legend);
                }
                top = UiDraw.Snap(Math.Max(ly, ry)) + UiTokens.S6;
            }
            if (m.Goods != null)
            {
                UiDataTable.Lay(s, at, m.Goods, x, top, w, bottom - top, keys, Goods);
            }
        }

        // ------------------------------------------------------------------------------------------------- deliveries

        // The wholesale contracts in the left column, the chosen one's terms, its switch and its actions under them; its goods
        // as the big table in the right column, each amount to step.
        private static void DeliveriesPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Items.Length == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Truck, m.ItemsEmpty, m.ItemsEmptyBody, null, -1, keys);
                return;
            }
            float half = Half(w), rx = x + half + UiTokens.S8;
            float ly = Items(s, at, m.ItemsTitle, m.Items, x, y, half, bottom, Item, -1, false, keys) + UiTokens.S5;
            ly = Title(s, at, x, ly, half, m.PartTitle);
            ly = Facts(s, at, m.Facts, x, ly, half);
            ly = Lead(s, at, m.Lead, x, ly + UiTokens.S2, half);
            if (m.HasRepeat)
            {
                Label(s, at, x, ly, half - UiTokens.S16, m.RepeatWords);
                UiDraw.Switch(s, at, x + half - UiTokens.S16, ly, UiTokens.S16, m.Repeat, null, Repeat, 0, false);
                ly += UiTokens.Control + UiTokens.S4;
            }
            Buttons(s, at, m.Actions, x + half, ly, keys, Action);
            if (m.Goods != null)
            {
                float gy = Title(s, at, rx, y, half, m.GoodsTitle);
                UiDataTable.Lay(s, at, m.Goods, rx, gy, half, bottom - gy, keys, Goods);
            }
        }

        // Rows each with its logo on a tile (or, where the game has none, its kind's glyph on the slate tile, as a contact's
        // firm wears it), its name over its line, its value at the right, a chip where the game marks it, and a button for
        // what the row offers (a call); the chosen one in the cyan fifth. Returns the y under them.
        private static float Items(UiScene s, UiAt at, string title, BizManItem[] items, float x, float y, float w, float bottom, int command, int linkCommand, bool onRow,
            IUiKeys keys)
        {
            y = onRow ? RowTitle(s, at, x, y, w, title) : Title(s, at, x, y, w, title);
            const bool tiles = true;
            float tile = UiTokens.IconTile;
            for (int i = 0; i < items.Length && y + ItemRow <= bottom; i++)
            {
                ref BizManItem it = ref items[i];
                int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, ItemRow, it.Chosen ? UiRole.RowSel : UiRole.None, UiRole.None,
                    UiTokens.RadiusLarge, it.Chosen ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                Picture(s, inside, it.Picture, 1f, it.Glyph, x, y + (ItemRow - tile) / 2f, tile, tile, UiTokens.Icon + UiTokens.S1);
                float linkW = string.IsNullOrEmpty(it.Link) || linkCommand < 0 ? 0f : UiDraw.ButtonWidth(UiButtonKind.Secondary, it.Link, UiIcon.Phone, UiAction.Count, keys);
                float valueW = string.IsNullOrEmpty(it.Value) ? 0f : UiDraw.Snap(UiTextRules.Width(it.Value, UiStyle.BodyStrong) + UiTokens.S1);
                float chipW = string.IsNullOrEmpty(it.WarnWords) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(it.WarnWords, UiStyle.SmallStrong) + UiTokens.S1);
                float tx = x + (tiles ? tile + UiTokens.S3 : 0f), right = x + w - (linkW > 0f ? linkW + UiTokens.S3 : 0f);
                float tw = right - tx - valueW - (chipW > 0f ? chipW + UiTokens.S3 : 0f) - UiTokens.S3;
                float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
                float top = y + (ItemRow - lh - sh) / 2f;
                UiDraw.Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, it.Name, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, inside, tx, top + lh, tw, UiStyle.Small, it.Chosen ? UiRole.Text : UiRole.Text2, it.Line, UiFlags.Truncate | UiFlags.Middle);
                if (chipW > 0f)
                {
                    UiDraw.Chip(s, inside, right - valueW - chipW, y + (ItemRow - UiTokens.Badge - UiTokens.S1) / 2f, it.WarnWords, UiRole.Warn, chipW);
                }
                if (valueW > 0f)
                {
                    UiDraw.TextMiddle(s, inside, right - valueW, y, ItemRow, valueW, UiStyle.BodyStrong, UiRole.Text, it.Value, UiFlags.Right);
                }
                if (command >= 0)
                {
                    UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, ItemRow, command, i, face);
                }
                if (linkW > 0f)
                {
                    UiDraw.Button(s, inside, x + w - linkW, y + (ItemRow - UiTokens.Control) / 2f, linkW, UiTokens.Control, UiButtonKind.Secondary, it.Link, UiIcon.Phone,
                        UiAction.Count, keys, linkCommand, i);
                }
                y += ItemRow + UiTokens.S1;
            }
            return y;
        }

        // ------------------------------------------------------------------------------------------------- marketing

        // How well the marketing works and what it costs a day on a band, then each agency's campaign with its kinds as
        // switches and its Cancel in the left column, and the agencies to call in the right.
        private static void MarketingPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float half = Half(w), rx = x + half + UiTokens.S8;
            float bandH = UiTokens.Control + UiTokens.S2;
            int band = UiDraw.Box(s, at, x, y, w, bandH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            if (m.Grades.Length > 0)
            {
                UiDraw.GradeBar(s, on, x + UiTokens.S4, y + (bandH - UiTokens.PairRow) / 2f, half - UiTokens.S4, m.Grades[0].Label, m.Grades[0].Value);
            }
            if (m.Facts.Length > 0)
            {
                UiDraw.InfoRow(s, on, rx, y + (bandH - UiTokens.PairRow) / 2f, half - UiTokens.S4, m.Facts[0].Label, m.Facts[0].Value);
            }
            float top = y + bandH + UiTokens.S6;
            if (m.Items.Length == 0)
            {
                float ty = Title(s, at, x, top, half, m.ItemsTitle);
                UiDraw.EmptyState(s, at, x, ty, half, bottom - ty, UiIcon.Megaphone, m.ItemsEmpty, m.ItemsEmptyBody, null, -1, keys);
            }
            else
            {
                Campaigns(s, at, m, x, top, half, bottom, keys);
            }
            if (m.Others.Length == 0)
            {
                float ty = Title(s, at, rx, top, half, m.OthersTitle);
                Lead(s, at, m.SecondEmpty, rx, ty, half);
            }
            else
            {
                Items(s, at, m.OthersTitle, m.Others, rx, top, half, bottom, -1, ItemLink, false, keys);
            }
        }

        // Each agency's campaign: its name and what it costs a day on a band with Cancel at its right, then its kinds of
        // marketing, each its words and its switch.
        private static void Campaigns(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Title(s, at, x, y, w, m.ItemsTitle);
            for (int g = 0; g < m.Items.Length; g++)
            {
                ref BizManItem it = ref m.Items[g];
                if (y + UiTokens.Control * 2f > bottom)
                {
                    break;
                }
                float bh = UiTokens.Control + UiTokens.S2;
                int band = UiDraw.Box(s, at, x, y, w, bh, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                UiAt on = at.In(band);
                string cancel = it.Link;
                float bw = string.IsNullOrEmpty(cancel) ? 0f : UiDraw.ButtonWidth(UiButtonKind.Destructive, cancel, UiIcon.Minus, UiAction.Count, keys);
                if (bw > 0f)
                {
                    UiDraw.Button(s, on, x + w - UiTokens.S1 - bw, y + UiTokens.S1, bw, UiTokens.Control, UiButtonKind.Destructive, cancel, UiIcon.Minus, UiAction.Count, keys, Cancel, g);
                }
                float vw = string.IsNullOrEmpty(it.Value) ? 0f : UiDraw.Snap(UiTextRules.Width(it.Value, UiStyle.BodyStrong) + UiTokens.S1);
                float vx = x + w - UiTokens.S1 - bw - UiTokens.S6 - vw;
                UiDraw.TextMiddle(s, on, x + UiTokens.S4, y, bh, vx - x - UiTokens.S8, UiStyle.Title, UiRole.Text, it.Name, UiFlags.Truncate);
                if (vw > 0f)
                {
                    UiDraw.TextMiddle(s, on, vx, y, bh, vw, UiStyle.BodyStrong, UiRole.Text, it.Value, UiFlags.Right);
                }
                y += bh + UiTokens.S2;
                for (int i = 0; i < m.Toggles.Length; i++)
                {
                    ref BizManToggle t = ref m.Toggles[i];
                    if (t.Group != g || y + UiTokens.Control > bottom)
                    {
                        continue;
                    }
                    UiDraw.TextMiddle(s, at, x + UiTokens.S4, y, UiTokens.Control, w - UiTokens.S16 - UiTokens.S8, UiStyle.Body, UiRole.Text, t.Words, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Switch(s, at, x + w - UiTokens.S16, y, UiTokens.S16, t.On, null, Toggle, i, t.Disabled);
                    y += UiTokens.Control;
                }
                y += UiTokens.S4;
            }
        }

        // ------------------------------------------------------------------------------------------------- settings

        // Two columns, each of rows with their labels at the left and their controls at the right: the business's name with
        // Randomise, its kind as a choice, the uniforms and Save with what is not saved under it; the logo with its font, Save
        // logo and its shape and colours; the danger at the foot. A choice's list opens last, over the page.
        private static void SettingsPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float half = Half(w), rx = x + half + UiTokens.S8;
            float labelW = UiDraw.SnapDown(Math.Min(UiDraw.LabelColumn(half * 0.6f, m.NameLabel, m.KindLabel, m.FontLabel), half * 0.4f));
            float fx = x + labelW + UiTokens.S4, fw = half - labelW - UiTokens.S4;
            float ly = Title(s, at, x, y, half, m.PartTitle);
            int random = Find(m.Actions, DoRandom);
            float rbw = random >= 0 ? UiDraw.ButtonWidth(m.Actions[random].Kind, m.Actions[random].Label, m.Actions[random].Icon, m.Actions[random].Key, keys) : 0f;
            Label(s, at, x, ly, labelW, m.NameLabel);
            UiDraw.TextField(s, at, fx, ly, fw - (rbw > 0f ? rbw + UiTokens.S3 : 0f), m.NameText, m.NamePlaceholder, Type, FieldName, false, m.Typing);
            if (random >= 0)
            {
                ref UiActionModel a = ref m.Actions[random];
                UiDraw.Button(s, at, fx + fw - rbw, ly, rbw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
            }
            ly += UiTokens.Control + UiTokens.S3;
            float kindY = ly;
            if (m.Kinds.Length > 0)
            {
                Label(s, at, x, ly, labelW, m.KindLabel);
                UiDraw.Choice(s, at, fx, ly, fw, m.KindChosen >= 0 && m.KindChosen < m.Kinds.Length ? m.Kinds[m.KindChosen] : null, Kind, 0, m.Kinds.Length < 2);
                ly += UiTokens.Control + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.Uniforms))
            {
                ly += UiDraw.LinkRow(s, at, fx, ly, fw, UiTokens.Control, m.Uniforms, Uniforms, 0) + UiTokens.S3;
            }
            int save = Find(m.Actions, DoSave);
            if (save >= 0)
            {
                ref UiActionModel a = ref m.Actions[save];
                UiButtonKind kind = a.Disabled ? UiButtonKind.Secondary : a.Kind;   // nothing to save: it waits as the logo's Save does
                float bw = UiDraw.ButtonWidth(kind, a.Label, a.Icon, a.Key, keys);
                UiDraw.Button(s, at, fx, ly, bw, UiTokens.Control, kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                if (!string.IsNullOrEmpty(m.Lead))
                {
                    // What is not saved stands beside Save, the one it is about, or under it where a language's words want more room.
                    float nx = fx + bw + UiTokens.S4, ny = ly;
                    if (UiTextRules.Lines(m.Lead, UiStyle.Small, x + half - nx - UiTokens.Icon - UiTokens.S2, 3, out float _) > 2)
                    {
                        nx = fx;
                        ny = ly + UiTokens.Control + UiTokens.S2;
                    }
                    UiDraw.Icon(s, at, nx, ny + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Warning, UiRole.Warn, UiFlags.Middle, true);
                    float tx = nx + UiTokens.Icon + UiTokens.S2;
                    UiDraw.Text(s, at, tx, ny, x + half - tx, UiStyle.Small, UiRole.Text2, m.Lead, UiFlags.Label | UiFlags.Wrap, 2);
                }
            }
            float fontY = -1f, ofx = rx + labelW + UiTokens.S4;
            if (!string.IsNullOrEmpty(m.LogoTitle))
            {
                float ry = Title(s, at, rx, y, half, m.LogoTitle);
                float tile = UiTokens.S16 + UiTokens.S8 + UiTokens.S4;
                Label(s, at, rx, ry, labelW, m.LogoLabel);
                Picture(s, at, m.Picture, m.PictureAspect, m.Head != null ? m.Head.Glyph : UiIcon.Store, ofx, ry, tile, tile, UiTokens.S8, true);
                ry += tile + UiTokens.S3;
                if (m.Fonts.Length > 0)
                {
                    fontY = ry;
                    Label(s, at, rx, ry, labelW, m.FontLabel);
                    UiDraw.Choice(s, at, ofx, ry, half - labelW - UiTokens.S4, m.FontChosen >= 0 && m.FontChosen < m.Fonts.Length ? m.Fonts[m.FontChosen] : null, Font, 0, false);
                    ry += UiTokens.Control + UiTokens.S3;
                }
                int more = Find(m.Actions, DoLogoEdit);
                if (more >= 0)
                {
                    ry += UiDraw.LinkRow(s, at, ofx, ry, half - labelW - UiTokens.S4, UiTokens.Control, m.Actions[more].Label, Action, m.Actions[more].Arg) + UiTokens.S3;
                }
                int logo = Find(m.Actions, DoSaveLogo);
                if (logo >= 0)
                {
                    ref UiActionModel a = ref m.Actions[logo];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    UiDraw.Button(s, at, ofx, ry, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                }
            }
            // The danger at the foot, each the game's own question first.
            float dx = x;
            for (int i = 0; i < m.Actions.Length; i++)
            {
                ref UiActionModel a = ref m.Actions[i];
                if (a.Kind != UiButtonKind.Destructive)
                {
                    continue;
                }
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                UiDraw.Button(s, at, dx, bottom - UiTokens.Control, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                dx += bw + UiTokens.S3;
            }
            if (m.KindOpen && m.Kinds.Length > 0)
            {
                Behind(s, x, y, w, bottom);
                UiDraw.ChoiceList(s, at, fx, kindY + UiTokens.Control + UiTokens.S1, fw, m.Kinds, m.Kinds.Length, m.KindChosen, 0f, KindOption);
            }
            else if (m.FontOpen && m.Fonts.Length > 0 && fontY >= 0f)
            {
                Behind(s, x, y, w, bottom);
                UiDraw.ChoiceList(s, at, ofx, fontY + UiTokens.Control + UiTokens.S1, half - labelW - UiTokens.S4, m.Fonts, m.Fonts.Length, m.FontChosen, 0f, FontOption);
            }
        }

        // A field's label at the left of its row, in the quiet colour, on the control's middle.
        private static void Label(UiScene s, UiAt at, float x, float y, float w, string words)
        {
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w, UiStyle.Body, UiRole.Text2, words, UiFlags.Label | UiFlags.Truncate);
        }

        // ------------------------------------------------------------------------------------------------- real estate

        // Two columns: the rent, its new price as a slider with Apply, and the unit of its own at the left; selling it and
        // the building's facts at the right.
        private static void EstatePage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float cw = Half(w), rx = x + cw + UiTokens.S8;
            float ly = y;
            if (m.More.Length > 0)
            {
                ly = Title(s, at, x, ly, cw, m.MoreTitle);
                ly = Facts(s, at, m.More, x, ly, cw) + UiTokens.S3;
                if (!string.IsNullOrEmpty(m.SliderFigure))
                {
                    int apply = Find(m.Actions, DoApplyRent);
                    float bw = apply >= 0 ? UiDraw.ButtonWidth(m.Actions[apply].Kind, m.Actions[apply].Label, m.Actions[apply].Icon, m.Actions[apply].Key, keys) : 0f;
                    Label(s, at, x, ly, UiDraw.SnapDown(cw * 0.3f), m.SliderLabel);
                    float sx = x + UiDraw.SnapDown(cw * 0.3f) + UiTokens.S3, sw = cw - (sx - x) - (bw > 0f ? bw + UiTokens.S3 : 0f);
                    UiDraw.Slider(s, at, sx, ly, sw, m.SliderFigure, m.SliderShare, Slider, 0, false, m.SliderHeld);
                    if (apply >= 0)
                    {
                        ref UiActionModel a = ref m.Actions[apply];
                        UiDraw.Button(s, at, x + cw - bw, ly, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                    }
                    ly += UiTokens.Control + UiTokens.S2;
                }
                ly = Lead(s, at, m.Lead, x, ly, cw) + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.SecondTitle))
            {
                ly = Title(s, at, x, ly, cw, m.SecondTitle);
                ly = Lead(s, at, m.SecondEmpty, x, ly, cw);
                int unit = Find(m.Actions, DoTakeoverUnit);
                if (unit >= 0)
                {
                    ref UiActionModel a = ref m.Actions[unit];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    UiDraw.Button(s, at, x, ly, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                }
            }
            float ry = Title(s, at, rx, y, cw, m.PartTitle);
            ry = Facts(s, at, m.Facts, rx, ry, cw) + UiTokens.S5;
            if (m.Amount != null)
            {
                ry = Amount(s, at, m.Amount, rx, ry, cw, keys);
            }
            int cancel = Find(m.Actions, DoCancelSale);
            if (cancel >= 0)
            {
                ry = Lead(s, at, m.ItemsEmpty, rx, ry, cw);
                ref UiActionModel a = ref m.Actions[cancel];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                UiDraw.Button(s, at, rx, ry, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                ry += UiTokens.Control + UiTokens.S6;
            }
            if (m.BuildingFacts.Length > 0)
            {
                ry = Title(s, at, rx, ry, cw, m.FactsTitle);
                Facts(s, at, m.BuildingFacts, rx, ry, cw);
            }
        }

        // ------------------------------------------------------------------------------------------------- the game's own view

        // A section only the game's own view shows yet: its glyph on a tile, what it is, why it opens there and the button
        // that opens it, held in the middle of the page so it reads as meant.
        private static void ClassicPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            int open = Find(m.Actions, DoClassic);
            float bw = UiDraw.SnapDown(Math.Min(w - UiTokens.S8 * 2f, UiTokens.S16 * 8f));
            float block = UiTokens.S12 + UiTokens.S4 + (string.IsNullOrEmpty(m.ClassicTitle) ? 0f : UiTextRules.WrappedHeight(m.ClassicTitle, UiStyle.Title, bw, 2))
                + (string.IsNullOrEmpty(m.ClassicBody) ? 0f : UiTokens.S2 + UiTextRules.WrappedHeight(m.ClassicBody, UiStyle.Body, bw, 3));
            float ah = open >= 0 ? UiTokens.S5 + UiTokens.Control : 0f;
            block = (float)Math.Ceiling((block + UiTokens.S1) / UiTokens.S1) * UiTokens.S1;   // the state's room on the grid, its lines whole inside it
            float top = UiDraw.Snap(y + Math.Max(0f, (bottom - y - block - ah) / 2f));
            UiDraw.EmptyState(s, at, x, top, w, block, m.ClassicIcon, m.ClassicTitle, m.ClassicBody, null, -1, keys);
            if (open >= 0)
            {
                ref UiActionModel a = ref m.Actions[open];
                float btn = Math.Min(w - UiTokens.S4, UiDraw.ButtonWidth(UiButtonKind.Primary, a.Label, a.Icon, a.Key, keys));
                UiDraw.Button(s, at, x + UiDraw.SnapDown((w - btn) / 2f), UiDraw.Snap(top + block + UiTokens.S5), btn, UiTokens.Control, UiButtonKind.Primary, a.Label, a.Icon,
                    a.Key, keys, Classic, 0, a.Disabled);
            }
        }

        /// <summary>The index of the action that does a thing, or -1.</summary>
        private static int Find(UiActionModel[] actions, int what)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                if (actions[i].Arg == what)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
