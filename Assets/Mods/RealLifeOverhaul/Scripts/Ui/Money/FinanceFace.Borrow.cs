using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class FinanceFace
    {
        // ------------------------------------------------------------------------------------------------- Borrow

        // The banks and the other kinds of credit down the left; the one chosen as a hero, the amount, what it costs over the term, what you still owe day by day, and who else is borrowing.
        private static void BorrowPage(UiScene s, UiAt at, FinanceFacts f, FinanceView v, UtilitiesWords w, FinanceLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            FinanceBorrowFacts b = f.Borrow;
            float gap = UiTokens.S4;
            float lw = UiDraw.SnapDown(Math.Max(UiTokens.S16 * 5f, cw * 0.3f));
            float rx = x + lw + gap, rw = cw - lw - gap;
            UiAt lon = at.InRegion(1);
            if (b.List != null)
            {
                Table(s, lon, lay, 0, b.List, x, y, lw, bottom - y, keys, TableA);
            }
            UiAt ron = at.InRegion(2);
            if (!string.IsNullOrEmpty(b.Refused))
            {
                float h = UiTokens.S16 * 3f;
                UiAt on = Band(s, ron, rx, y, rw, h);
                float ry0 = Title(s, on, w.T("reallife_econ_borrow_refused_title", "No lender will lend"), rx + UiTokens.S4, y + UiTokens.S2, rw - UiTokens.S8);
                Sentence(s, on, b.Refused, rx + UiTokens.S4, ry0, rw - UiTokens.S8, UiRole.DangerText, 3);
                if (b.RefusedTips.Length > 0)
                {
                    Notes(s, ron, w.T("reallife_econ_credit_raise", "What would raise it most"), b.RefusedTips, rx, y + h + UiTokens.S3, rw, bottom - y - h - UiTokens.S3);
                }
                return;
            }
            FinanceQuoteFacts q = b.Quote;
            if (q == null)
            {
                UiDraw.EmptyState(s, ron, rx, y, rw, bottom - y, UiIcon.Bank, w.T("reallife_fin_pick_offer", "Pick an offer"),
                    w.T("reallife_fin_pick_offer_body", "Choose a bank or a kind of credit on the left to see what it costs."), null, -1, keys);
                return;
            }
            var hero = new UiHeroModel { Icon = UiIcon.Bank, Kicker = q.Kind, Title = q.Name, Line = q.Rate, Note = q.Big, NoteIcon = UiIcon.Coin, Progress = -1 };
            y += UiAppWindow.Hero(s, ron, hero, rx, y, rw) + UiTokens.S3;
            if (!string.IsNullOrEmpty(q.BigNote))
            {
                y = Sentence(s, ron, q.BigNote, rx, y, rw, UiRole.Text2, 1);
            }
            float colW = UiDraw.SnapDown((rw - gap) * 0.54f), sx = rx + colW + gap, sw = rw - colW - gap;
            float cy = y;
            if (q.HasAmount)
            {
                cy = Title(s, ron, w.T("reallife_econ_offer_amount", "How much"), rx, cy, colW);
                string figure = v.Typing ?? w.Money(q.Amount);
                float field = UiDraw.SnapDown(Math.Min(colW, UiTokens.S16 * 6f));
                UiDraw.Stepper(s, ron, rx, cy, field, figure, q.Amount > q.Least, q.Amount < q.Most, AmountLess, AmountType, AmountMore, ArgAmount, false);
                cy += UiTokens.Control + UiTokens.S1;
                cy = Sentence(s, ron, q.AmountHint, rx, cy, colW, UiRole.Text2, 1);
            }
            var lines = new System.Collections.Generic.List<UtilitiesLine>(q.Labels.Length + 2);
            for (int i = 0; i < q.Labels.Length && i < q.Values.Length; i++)
            {
                lines.Add(new UtilitiesLine { Label = q.Labels[i], Value = q.Values[i], Ink = UiRole.Text });
            }
            if (!string.IsNullOrEmpty(q.Note))
            {
                lines.Add(new UtilitiesLine { Value = q.Note });
            }
            if (!string.IsNullOrEmpty(q.Note2))
            {
                lines.Add(new UtilitiesLine { Value = q.Note2, Ink = UiRole.AccentText });
            }
            var actions = new System.Collections.Generic.List<UiActionModel>(3);
            for (int i = 0; i < q.TakeMore.Length && i < q.TakeMoreActs.Length; i++)
            {
                actions.Add(Act(q.TakeMore[i], UiButtonKind.Secondary, UiIcon.None, UiAction.Count, q.TakeMoreActs[i], 0, i < q.TakeMoreOn.Length && !q.TakeMoreOn[i]));
            }
            if (Main(f, v, w, out UiActionModel take))
            {
                actions.Add(take);
            }
            var card = new UtilitiesCard
            {
                Title = w.T("reallife_fin_costs_term", "What it costs over the term"),
                Heading = true,
                Lines = lines.ToArray(),
                Steps = q.Steps,
                Actions = actions.ToArray()
            };
            UtilitiesFace.Card(s, ron, card, rx, cy, colW, bottom - cy, -1, StepLess, StepMore, keys, true, true);
            // What you still owe, day by day, and who else is borrowing.
            float ry = y;
            if (q.Owing != null && bottom - ry > UiTokens.S16 * 5f)
            {
                float ch = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 4f, (bottom - ry) * 0.46f));
                UiChart.Lay(s, at.InRegion(3), q.Owing, sx, ry, sw, ch);
                ry += ch + gap;
            }
            if (b.Feed != null && bottom - ry > UiTokens.S16 * 2f)
            {
                float fy = Title(s, at.InRegion(4), b.FeedTitle, sx, ry, sw);
                Table(s, at.InRegion(4), lay, 1, b.Feed, sx, fy, sw, bottom - fy, keys, TableB);
            }
        }

        // ------------------------------------------------------------------------------------------------- Cash flow

        // The statement for the period chosen, with the running balance; the cash over the days that exist, where it went, what is coming up.
        private static void CashPage(UiScene s, UiAt at, FinanceFacts f, FinanceView v, UtilitiesWords w, FinanceLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            FinanceCashFacts c = f.Cash;
            float gap = UiTokens.S4;
            UiAt top = at.InRegion(1);
            float exportW = Second(f, v, w, out UiActionModel export) ? UiDraw.ButtonWidth(export.Kind, export.Label, export.Icon, export.Key, keys) + UiTokens.S3 : 0f;
            UiDraw.Tabs(s, top, x, y, cw - exportW, c.Periods, v.Period, keys, Period);
            if (exportW > 0f)
            {
                Buttons(s, top, new[] { export }, x, y, cw, keys);
            }
            y += UiTokens.Control + UiTokens.S3;
            float lw = UiDraw.SnapDown(cw * 0.6f), rx = x + lw + gap, rw = cw - lw - gap;
            UiAt lon = at.InRegion(2);
            if (c.Statement != null)
            {
                c.Statement.Offset = lay.Offset[3];
                c.Statement.Reveal = lay.Reveal[3];
                lay.Reveal[3] = -1;
                short list = UiStatement.Lay(s, lon, c.Statement, x, y, lw, bottom - y, keys);
                lay.Offset[3] = c.Statement.Offset;
                lay.List[3] = list;
                lay.Clip[3] = list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;
            }
            UiAt ron = at.InRegion(3);
            float ry = y;
            float chartH = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 3f, (bottom - ry) * 0.3f));
            if (c.Chart != null && chartH > UiTokens.S16 * 2f)
            {
                UiAt on = Band(s, ron, rx, ry, rw, chartH + UiTokens.S16);
                float pad = UiTokens.S4;
                Pair(s, on, rx + pad, ry + UiTokens.S2, UiTokens.PairRow, rw - pad * 2f, UiStyle.SectionHeader, UiRole.Text, c.ChartTitle, UiStyle.Small, UiRole.Text2, c.ChartRange);
                float ty = ry + UiTokens.S2 + UiTokens.PairRow;
                float nw = UiDraw.Snap(UiTextRules.Width(c.ChartTotal ?? string.Empty, UiStyle.Number)) + UiTokens.S1;
                UiDraw.Text(s, on, rx + pad, ty, nw, UiStyle.Number, UiRole.Text, c.ChartTotal ?? string.Empty, UiFlags.Truncate);
                if (!string.IsNullOrEmpty(c.ChartChange))
                {
                    UiDraw.Text(s, on, rx + pad + nw + UiTokens.S3, ty + UiTokens.S1, rw - pad * 2f - nw - UiTokens.S3, UiStyle.BodyStrong,
                        c.ChangeGood ? UiRole.OkText : UiRole.DangerText, c.ChartChange, UiFlags.Truncate);
                }
                float chy = ty + UiTokens.S8;
                c.Chart.Title = null;
                UiChart.Lay(s, on, c.Chart, rx, chy, rw, ry + chartH + UiTokens.S16 - chy);
                ry += chartH + UiTokens.S16 + UiTokens.S3;
            }
            else if (c.Chart != null && bottom - ry > UiTokens.S16 * 2f)
            {
                float h = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 2f, bottom - ry));
                UiChart.Lay(s, ron, c.Chart, rx, ry, rw, h);
                ry += h + UiTokens.S3;
            }
            if (c.InNames.Length + c.OutNames.Length > 0 && bottom - ry > UiTokens.S16 * 2f)
            {
                ry += Where(s, ron, c, w, rx, ry, rw, bottom - ry) + UiTokens.S3;
            }
            if (c.ComingLabels.Length > 0 && bottom - ry > UiTokens.S12)
            {
                ry += Facts(s, ron, c.ComingTitle, c.ComingLabels, c.ComingValues, rx, ry, rw, bottom - ry) + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(c.Reach) && bottom - ry >= UiTokens.PairRow)
            {
                Sentence(s, ron, c.Reach, rx, ry, rw, UiRole.Muted, 2);
            }
        }

        // Where it went: what came in and what went out by kind, each with a bar and its sum.
        private static float Where(UiScene s, UiAt at, FinanceCashFacts c, UtilitiesWords w, float x, float y, float w2, float limit)
        {
            float row = UiTokens.PairRow + UiTokens.S1;
            int inRows = c.InNames.Length, outRows = c.OutNames.Length;
            float want = UiTokens.S2 + (string.IsNullOrEmpty(c.WhereTitle) ? 0f : UiTokens.Control) + (inRows > 0 ? row * (inRows + 1)
                : 0f) + (outRows > 0 ? row * (outRows + 1) : 0f) + row * 2f + UiTokens.S2;
            int cut = 0;
            while (want > limit && (inRows > 1 || outRows > 1) && cut < 12)
            {
                if (outRows >= inRows && outRows > 1)
                {
                    outRows--;
                }
                else if (inRows > 1)
                {
                    inRows--;
                }
                want -= row;
                cut++;
            }
            float h = UiDraw.Snap(Math.Min(limit, want));
            UiAt on = Band(s, at, x, y, w2, h);
            float pad = UiTokens.S4, ix = x + pad, iw = w2 - pad * 2f;
            float cy = Title(s, on, c.WhereTitle, ix, y + UiTokens.S2, iw);
            cy = BarRows(s, on, c.CameInLabel, c.CameIn, c.InNames, c.InValues, c.InShare, inRows, UiRole.Ok, ix, cy, iw, y + h);
            cy = BarRows(s, on, c.WentOutLabel, c.WentOut, c.OutNames, c.OutValues, c.OutShare, outRows, UiRole.Warn, ix, cy, iw, y + h);
            if (!string.IsNullOrEmpty(c.Net) && cy + UiTokens.PairRow <= y + h)
            {
                Pair(s, on, ix, cy, UiTokens.PairRow, iw, UiStyle.BodyStrong, UiRole.Text, c.NetLabel, UiStyle.BodyStrong, c.NetGood ? UiRole.OkText : UiRole.DangerText, c.Net);
            }
            return h;
        }

        private static float BarRows(UiScene s, UiAt at, string label, string total, string[] names, string[] values, int[] shares, int rows,
            UiRole tone, float x, float y, float w, float limit)
        {
            if (rows <= 0)
            {
                return y;
            }
            float h = UiTokens.PairRow;
            Pair(s, at, x, y, h, w, UiStyle.SectionHeader, UiRole.Text2, label, UiStyle.SmallStrong, UiRole.Text2, total);
            y += h + UiTokens.S1;
            float nameW = UiDraw.SnapDown(w * 0.34f), valW = UiDraw.SnapDown(w * 0.28f);
            float barX = x + nameW + UiTokens.S2, barW = w - nameW - valW - UiTokens.S4;
            for (int i = 0; i < rows && i < names.Length && y + h <= limit; i++)
            {
                UiDraw.TextMiddle(s, at, x, y, h, nameW, UiStyle.Body, UiRole.Text, names[i], UiFlags.Truncate);
                UiDraw.Meter(s, at, barX, y + (h - UiTokens.S2) / 2f, barW, UiTokens.S2, i < shares.Length ? shares[i] : 0, tone);
                UiDraw.TextMiddle(s, at, x + w - valW, y, h, valW, UiStyle.Body, UiRole.Text, i < values.Length ? values[i] : string.Empty, UiFlags.Right | UiFlags.Truncate);
                y += h + UiTokens.S1;
            }
            return y;
        }
    }
}
