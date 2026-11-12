using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class FinanceFace
    {
        // ------------------------------------------------------------------------------------------------- Overview

        // You as a borrower: your own picture, the grade and the score, what you owe, cash and worth; then the debts and what leaves, and the cash over the days that exist.
        private static void OverviewPage(UiScene s, UiAt at, FinanceFacts f, UtilitiesWords w, FinanceLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            FinanceOverviewFacts o = f.Overview;
            FinanceScore sc = f.Score;
            var profile = new UiProfileModel
            {
                Name = o.Player ?? string.Empty,
                Hue = UiColour.HueOf(o.Player ?? string.Empty),
                Role = sc.Grade,
                RoleIcon = UiIcon.IdCard,
                Status = sc.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                StatusTone = sc.Tone,
                FactLabels = new[] { w.T("reallife_fin_f_owed", "What you owe"), w.T("reallife_fin_f_cash", "Cash"), w.T("reallife_fin_f_worth", "Net worth") },
                FactValues = new[] { o.Owed, o.Cash, o.Worth },
                RatingLabel = w.T("reallife_fin_score", "Credit score"),
                Rating = sc.Halves
            };
            UiActionModel[] acts = Second(f, new FinanceView { Tab = Overview }, w, out UiActionModel second) ? new[] { second } : new UiActionModel[0];
            y = UiAppWindow.Profile(s, at, profile, acts, x, y, cw, keys);
            string line = !string.IsNullOrEmpty(o.LeavesNote) ? o.LeavesNote : sc.Sentence;
            y = Sentence(s, at, line, x, y, cw);
            if (!string.IsNullOrEmpty(o.RiseNote))
            {
                y = Sentence(s, at, o.RiseNote, x, y, cw, UiRole.WarnText, 2);
            }
            float gap = UiTokens.S4;
            float h = bottom - y;
            float chartH = o.Cash30 != null && h > UiTokens.S16 * 7f ? UiDraw.SnapDown(Math.Min(UiTokens.S16 * 2.5f, h * 0.28f)) : 0f;
            float topH = h - chartH - (chartH > 0f ? gap : 0f);
            float lw = UiDraw.SnapDown(cw * 0.62f), rx = x + lw + gap, rw = cw - lw - gap;
            UiAt lon = at.InRegion(1);
            float ly = y;
            if (o.Debts != null && o.Debts.RowCount > 0)
            {
                float ty = Title(s, lon, w.T("reallife_fin_owe_when", "What you owe and when"), x, y, lw);
                float tableH = UiDraw.SnapDown(Math.Min(topH - UiTokens.Control, UiTokens.Control + o.Debts.RowCount * UiTokens.S12 + UiTokens.S2));
                Table(s, lon, lay, 0, o.Debts, x, ty, lw, tableH, keys, TableA);
                ly = ty + tableH + gap;
            }
            else
            {
                float nh = Math.Min(topH, UiTokens.S16 * 3f);
                NoDebts(s, lon, o, w, x, y, lw, nh, keys);
                ly = y + nh + gap;
            }
            if (o.CityLabels.Length > 0)
            {
                Facts(s, at.InRegion(2), w.T("reallife_fin_city", "The city's economy"), o.CityLabels, o.CityValues, x, ly, lw, Math.Max(0f, y + topH - ly));
            }
            float ry = y;
            UiAt ron = at.InRegion(3);
            if (o.Donut.Length > 0 && topH > UiTokens.S16 * 5f)
            {
                float dh = UiTokens.S16 * 3f;
                UiAt on = Band(s, ron, rx, ry, rw, dh);
                UiDraw.Text(s, on, rx + UiTokens.S4, ry + UiTokens.S3, rw - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text, w.T("reallife_fin_by_kind", "By kind"), UiFlags.Label | UiFlags.Truncate);
                float size = UiTokens.S16 * 2f, dy = ry + UiTokens.S3 + UiTokens.PairRow;
                UiDraw.Donut(s, on, rx + UiTokens.S4, dy, size, o.Donut, o.Donut.Length);
                int n = Math.Min(o.DonutLabels.Length, Math.Min(o.DonutValues.Length, o.Donut.Length));
                UiDraw.Legend(s, on, rx + UiTokens.S4 + size + UiTokens.S6, dy, rw - size - UiTokens.S4 * 2f - UiTokens.S6, o.DonutLabels, o.DonutValues, n);
                ry += dh + gap;
            }
            Facts(s, ron, w.T("reallife_fin_next", "Next to leave"), o.NextLabels, o.NextValues, rx, ry, rw, Math.Max(0f, y + topH - ry));
            if (chartH > 0f)
            {
                UiChart.Lay(s, at.InRegion(4), o.Cash30, x, bottom - chartH, cw, chartH);
            }
        }

        // No debts: a short card that says so, with the best offer and the way to it, never an empty table.
        private static void NoDebts(UiScene s, UiAt at, FinanceOverviewFacts o, UtilitiesWords w, float x, float y, float width, float h, IUiKeys keys)
        {
            string body = o.BestOn ? w.T("reallife_fin_best_today", "The best offer today is {line}.").Replace("{line}", o.BestLine ?? o.BestName ?? string.Empty)
                : w.T("reallife_fin_no_debts_body", "A loan or a card will be listed here once you take one.");
            EmptyCard(s, at, x, y, width, h, w.T("reallife_fin_no_debts", "You owe nothing"), body, o.BestOn ? w.T("reallife_fin_see_offers", "See what you can borrow") : null, keys);
        }

        /// <summary>A short card that says there is nothing here yet, and where to go about it; never a tall empty state floating in an empty page.</summary>
        private static void EmptyCard(UiScene s, UiAt at, float x, float y, float width, float h, string title, string body, string action, IUiKeys keys)
        {
            UiAt on = Band(s, at, x, y, width, h);
            float pad = UiTokens.S4, ix = x + pad, iw = width - pad * 2f;
            float cy = Title(s, on, title, ix, y + UiTokens.S2, iw);
            cy = Sentence(s, on, body, ix, cy, iw, UiRole.Text2, 2);
            if (!string.IsNullOrEmpty(action) && cy + UiTokens.Control <= y + h)
            {
                var go = new UiActionModel { Label = action, Kind = UiButtonKind.Secondary, Icon = UiIcon.Bank, Key = UiAction.Count, Command = UiAppWindow.NavCommand, Arg = Borrow };
                Buttons(s, on, new[] { go }, ix, cy, iw, keys);
            }
        }

        // ------------------------------------------------------------------------------------------------- Credit

        // The score explained: the number and what it means, each factor's bar with its weight and what moves it, and the offers it opens.
        private static void CreditPage(UiScene s, UiAt at, FinanceFacts f, FinanceView v, UtilitiesWords w, FinanceLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            FinanceCreditFacts c = f.Credit;
            FinanceScore sc = f.Score;
            float gap = UiTokens.S4, top = y;
            float lw = UiDraw.SnapDown(cw * 0.56f), rx = x + lw + gap, rw = cw - lw - gap;
            // The score: the figure large, the grade, the stars, how it moved, and what the floor means.
            float bh = UiTokens.S16 * 2f + UiTokens.S4;
            UiAt on = Band(s, at.InRegion(1), x, y, lw, bh);
            float pad = UiTokens.S5, ix = x + pad;
            string figure = sc.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            float figW = UiDraw.Snap(UiTextRules.Width(figure, UiStyle.Display) + UiTokens.S2);
            UiDraw.Text(s, on, ix, y + UiTokens.S3, figW, UiStyle.Display, UiRole.Text, figure, UiFlags.Truncate);
            float tx = ix + figW + UiTokens.S4, tw = x + lw - pad - tx;
            UiDraw.Text(s, on, tx, y + UiTokens.S3, tw, UiStyle.SectionHeader, UiRole.Text2, w.T("reallife_fin_your_rating", "Your credit rating"), UiFlags.Label | UiFlags.Truncate);
            float cy = y + UiTokens.S3 + UiTokens.PairRow + UiTokens.S1;
            float chip = UiDraw.Chip(s, on, tx, cy, sc.Grade ?? string.Empty, sc.Tone);
            float sx = tx + (chip > 0f ? chip : UtilitiesFace.ChipWidth(sc.Grade ?? string.Empty)) + UiTokens.S4;
            float starsW = UiDraw.Stars(s, on, sx, cy + UiTokens.S1, UiTokens.Icon, sc.Halves);
            if (sc.HasChange)
            {
                string move = (sc.Change >= 0 ? "+" : "-") + Math.Abs(sc.Change).ToString(System.Globalization.CultureInfo.InvariantCulture);
                float mx = sx + (starsW > 0f ? starsW : 5f * UiTokens.Icon) + UiTokens.S3;
                UiDraw.Text(s, on, mx, cy + UiTokens.S1, Math.Max(UiTokens.S16, x + lw - pad - mx), UiStyle.BodyStrong, sc.Change >= 0 ? UiRole.OkText : UiRole.DangerText,
                    w.F("reallife_fin_score_move", "{move} in {days} days", "move", move, "days", sc.ChangeDays.ToString(System.Globalization.CultureInfo.InvariantCulture)), UiFlags.Truncate);
            }
            Sentence(s, on, sc.Sentence, tx, cy + UiTokens.Control, tw, UiRole.Text2, 3);
            y += bh + gap;
            // Each factor: its bar, how it stands, and what moves it.
            UiAt lon = at.InRegion(1);
            for (int i = 0; i < c.Factors.Length; i++)
            {
                FinanceFactor fa = c.Factors[i];
                float need = UiTokens.S10 + UiTokens.S4;
                if (y + need > bottom)
                {
                    break;
                }
                y += UiDraw.GradeBar(s, lon, x, y, lw, fa.Name + "  " + fa.Weight, fa.Part, UiDraw.SnapDown(lw * 0.34f)) + UiTokens.S1;
                y = Sentence(s, lon, fa.Stands, x, y, lw, UiRole.Text2, 2);
                if (!string.IsNullOrEmpty(fa.Tip) && y + UiTokens.PairRow <= bottom)
                {
                    y = Sentence(s, lon, fa.Tip, x, y, lw, UiRole.AccentText, 2);
                }
            }
            // The offers at this score, each with Apply, then what would raise the score and its last events.
            UiAt ron = at.InRegion(2);
            float ry = Title(s, ron, c.OffersTitle, rx, top, rw);
            float tableH = UiDraw.SnapDown(Math.Max(UiTokens.S16 * 3f, (bottom - ry) * 0.42f));
            Table(s, ron, lay, 0, c.Offers, rx, ry, rw, tableH, keys, TableA);
            ry += tableH + UiTokens.S2;
            if (Main(f, v, w, out UiActionModel apply))
            {
                Buttons(s, ron, new[] { apply }, rx, ry, rw, keys);
                ry += UiTokens.Control + UiTokens.S3;
            }
            if (c.Tips.Length > 0 || !string.IsNullOrEmpty(c.Worst))
            {
                var lines = new string[c.Tips.Length + (string.IsNullOrEmpty(c.Worst) ? 0 : 1)];
                Array.Copy(c.Tips, lines, c.Tips.Length);
                if (!string.IsNullOrEmpty(c.Worst))
                {
                    lines[lines.Length - 1] = c.Worst;
                }
                ry += Notes(s, ron, w.T("reallife_econ_credit_raise", "What would raise it most"), lines, rx, ry, rw, Math.Max(0f, bottom - ry - UiTokens.S16 * 2f)) + UiTokens.S3;
            }
            if (c.Events != null && c.Events.RowCount > 0 && bottom - ry > UiTokens.S16 * 2f)
            {
                Table(s, at.InRegion(3), lay, 1, c.Events, rx, ry, rw, bottom - ry, keys, TableB);
            }
        }

        /// <summary>A band with a title and sentences under it; the sentences that would not fit are left out. Returns its height, 0 when nothing fits.</summary>
        private static float Notes(UiScene s, UiAt at, string title, string[] lines, float x, float y, float w, float limit)
        {
            float pad = UiTokens.S4, iw = w - pad * 2f;
            float want = UiTokens.S2 + (string.IsNullOrEmpty(title) ? 0f : UiTokens.Control);
            int fit = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                float h = UiDraw.Snap(UiTextRules.WrappedHeight(lines[i], UiStyle.Body, iw, 3)) + UiTokens.S2;
                if (want + h + UiTokens.S2 > limit)
                {
                    break;
                }
                want += h;
                fit++;
            }
            if (fit == 0)
            {
                return 0f;
            }
            float height = UiDraw.Snap(want + UiTokens.S2);
            UiAt on = Band(s, at, x, y, w, height);
            float cy = Title(s, on, title, x + pad, y + UiTokens.S2, iw);
            for (int i = 0; i < fit; i++)
            {
                cy = Sentence(s, on, lines[i], x + pad, cy, iw, UiRole.Text, 3);
            }
            return height;
        }
    }
}
