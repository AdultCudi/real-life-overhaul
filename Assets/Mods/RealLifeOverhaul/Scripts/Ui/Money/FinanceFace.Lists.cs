using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class FinanceFace
    {
        /// <summary>The acts a tab's profile head offers, in the order they are drawn: what the head's button of that number does.</summary>
        public static FinanceAct[] ProfileActs(FinanceFacts f, int tab)
        {
            if (tab == Overview)
            {
                return f.Overview.EconomyApp ? new[] { FinanceAct.Economy } : new FinanceAct[0];
            }
            if (tab != Rivals)
            {
                return new FinanceAct[0];
            }
            var list = new System.Collections.Generic.List<FinanceAct>(3);
            if (f.Rivals.CanFollow)
            {
                list.Add(FinanceAct.Follow);
            }
            return list.ToArray();
        }

        // ------------------------------------------------------------------------------------------------- Accounts

        // Your accounts and cards with what each holds, and the one chosen with what you can do about it.
        private static void AccountsPage(UiScene s, UiAt at, FinanceFacts f, FinanceView v, UtilitiesWords w, FinanceLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            FinanceAccountsFacts a = f.Accounts;
            y = Title(s, at, a.Headline, x, y, cw);
            if (a.Table == null || a.Table.RowCount == 0)
            {
                EmptyCard(s, at.InRegion(1), x, y, cw, UiTokens.S16 * 3f, a.Table != null ? a.Table.EmptyTitle : null,
                    a.Table != null ? a.Table.EmptyBody : null, w.T("reallife_fin_see_offers", "See what you can borrow"), keys);
                return;
            }
            float gap = UiTokens.S4, lw = UiDraw.SnapDown(cw * 0.6f), rx = x + lw + gap, rw = cw - lw - gap;
            Table(s, at.InRegion(1), lay, 0, a.Table, x, y, lw, bottom - y, keys, TableA);
            if (a.Card == null)
            {
                return;
            }
            UiAt ron = at.InRegion(2);
            float cy = y;
            if (a.CardPay)
            {
                UiAt on = Band(s, ron, rx, cy, rw, UiTokens.S16 * 2f);
                float pad = UiTokens.S4;
                float ty = Title(s, on, w.T("reallife_fin_amount", "Amount"), rx + pad, cy + UiTokens.S2, rw - pad * 2f);
                float field = UiDraw.SnapDown(Math.Min(rw - pad * 2f, UiTokens.S16 * 6f));
                UiDraw.Stepper(s, on, rx + pad, ty, field, v.Typing ?? w.Money(a.PayAmount), a.PayAmount > a.PayLeast, a.PayAmount < a.PayMost,
                    AmountLess, AmountType, AmountMore, ArgPay, false);
                cy += UiTokens.S16 * 2f + UiTokens.S3;
            }
            UtilitiesFace.Card(s, ron, a.Card, rx, cy, rw, bottom - cy, -1, -1, -1, keys, true, true);
        }

        // ------------------------------------------------------------------------------------------------- Standing

        // How the city sees you: the reputation and its four parts with what each means, then every row of it and the one chosen.
        private static void StandingPage(UiScene s, UiAt at, FinanceFacts f, FinanceView v, UtilitiesWords w, FinanceLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            FinanceStandingFacts st = f.Standing;
            float gap = UiTokens.S4;
            if (!f.StandingOn)
            {
                UiDraw.EmptyState(s, at, x, y, cw, bottom - y, UiIcon.Flag, w.T("reallife_fame_off", "Standing is switched off"),
                    w.T("reallife_fin_standing_off_body", "Switch Standing on in the mod settings to see how the city sees you."), null, -1, keys);
                return;
            }
            int parts = st.PartNames.Length;
            float barH = UiTokens.S8;
            float bh = UiDraw.Snap(UiTokens.S4 * 2f + Math.Max(UiTokens.S16 * 2f, parts * (barH + UiTokens.S1 + UiTokens.PairRow + UiTokens.S2)));
            UiAt top = at.InRegion(1);
            UiAt on = Band(s, top, x, y, cw, bh);
            float pad = UiTokens.S5, ix = x + pad;
            float lw = UiDraw.SnapDown(cw * 0.34f);
            UiDraw.Text(s, on, ix, y + UiTokens.S3, lw - pad, UiStyle.SectionHeader, UiRole.Text2, w.T("reallife_fame_title", "Standing"), UiFlags.Label | UiFlags.Truncate);
            string figure = st.OverallValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            float figW = UiDraw.Snap(UiTextRules.Width(figure, UiStyle.Display) + UiTokens.S2);
            UiDraw.Text(s, on, ix, y + UiTokens.S3 + UiTokens.PairRow, figW, UiStyle.Display, UiRole.Text, figure, UiFlags.Truncate);
            float cy = y + UiTokens.S3 + UiTokens.PairRow;
            float chipX = ix + figW + UiTokens.S3;
            if (!string.IsNullOrEmpty(st.Profile))
            {
                UiDraw.Chip(s, on, chipX, cy + UiTokens.S2, st.Profile, UiRole.Accent, Math.Min(lw - pad - figW - UiTokens.S3, UtilitiesFace.ChipWidth(st.Profile)));
            }
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S1;
            Sentence(s, on, !string.IsNullOrEmpty(st.Scandal) ? st.Scandal : st.Overall, ix, cy, lw - pad, !string.IsNullOrEmpty(st.Scandal) ? UiRole.DangerText : UiRole.Text2, 3);
            float bx = x + lw + gap, bw = x + cw - pad - bx;
            float by = y + UiTokens.S4;
            for (int i = 0; i < parts; i++)
            {
                string label = st.PartNames[i] + (i < st.PartWords.Length && !string.IsNullOrEmpty(st.PartWords[i]) ? "  " + st.PartWords[i] : string.Empty);
                by += UiDraw.GradeBar(s, on, bx, by, bw, label, i < st.PartMarks.Length ? st.PartMarks[i] : 0, UiDraw.SnapDown(bw * 0.3f)) + UiTokens.S1;
                if (i < st.PartNotes.Length && !string.IsNullOrEmpty(st.PartNotes[i]))
                {
                    UiDraw.TextMiddle(s, on, bx, by, UiTokens.PairRow, bw, UiStyle.Small, UiRole.Text2, st.PartNotes[i], UiFlags.Truncate);
                    by += UiTokens.PairRow;
                }
                by += UiTokens.S2;
            }
            y += bh + gap;
            float tw = UiDraw.SnapDown(cw * 0.6f), rx = x + tw + gap, rw = cw - tw - gap;
            Table(s, at.InRegion(2), lay, 0, st.Table, x, y, tw, bottom - y, keys, TableA);
            if (st.Card != null)
            {
                UtilitiesFace.Card(s, at.InRegion(3), st.Card, rx, y, rw, bottom - y, -1, -1, -1, keys, true, true);
            }
        }

        // ------------------------------------------------------------------------------------------------- Rivals

        // The rich list with each fortune's worth, its change and its path; the one chosen with their business, their moves, and the answers that do something.
        private static void RivalsPage(UiScene s, UiAt at, FinanceFacts f, FinanceView v, UtilitiesWords w, FinanceLayout lay, float x, float y, float cw, float bottom, IUiKeys keys)
        {
            FinanceRivalsFacts r = f.Rivals;
            float gap = UiTokens.S4;
            if (!f.RivalsOn)
            {
                UiDraw.EmptyState(s, at, x, y, cw, bottom - y, UiIcon.Trophy, w.T("reallife_rival_title", "Rivals"), w.T("reallife_rival_off",
                    "The rivals are switched off in the mod settings."), null, -1, keys);
                return;
            }
            float lw = UiDraw.SnapDown(cw * 0.54f), rx = x + lw + gap, rw = cw - lw - gap;
            UiAt lon = at.InRegion(1);
            float ty = Title(s, lon, r.Headline, x, y, lw);
            float chartH = r.Chart != null ? UiDraw.SnapDown(Math.Min(UiTokens.S16 * 4f, (bottom - ty) * 0.36f)) : 0f;
            float tableH = bottom - ty - chartH - (chartH > 0f ? gap : 0f);
            Table(s, lon, lay, 0, r.Table, x, ty, lw, tableH, keys, TableA);
            if (chartH > UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at.InRegion(2), r.Chart, x, bottom - chartH, lw, chartH);
            }
            UiAt ron = at.InRegion(3);
            float ry = y;
            if (r.Profile != null)
            {
                FinanceAct[] acts = ProfileActs(f, Rivals);
                var models = new UiActionModel[acts.Length];
                for (int i = 0; i < acts.Length; i++)
                {
                    models[i] = new UiActionModel { Label = w.T("reallife_money_follow", "Follow"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Megaphone, Key = UiAction.Option };
                }
                ry = UiAppWindow.Profile(s, ron, r.Profile, models, rx, ry, rw, keys);
            }
            if (r.Labels.Length > 0 && bottom - ry > UiTokens.S12)
            {
                ry += Facts(s, ron, null, r.Labels, r.Values, rx, ry, rw, Math.Min(UiTokens.S16 * 2f, bottom - ry)) + UiTokens.S3;
            }
            ry = Moves(s, ron, r, rx, ry, rw, bottom);
            if (r.Answers.Length > 0 && bottom - ry > UiTokens.S16 * 2f)
            {
                ry = Title(s, ron, r.AnswersTitle, rx, ry, rw);
                for (int i = 0; i < r.Answers.Length && i < 3; i++)
                {
                    FinanceAnswer a = r.Answers[i];
                    var key = (UiAction)((int)UiAction.Pick1 + i);
                    string label = a.Title;   // the key cap on the button says its number
                    float bw = Math.Min(rw, UiDraw.ButtonWidth(UiButtonKind.Secondary, label, UiIcon.None, key, keys));
                    if (ry + UiTokens.Control > bottom)
                    {
                        break;
                    }
                    UiDraw.Button(s, ron, rx, ry, bw, UiTokens.Control, UiButtonKind.Secondary, label, UiIcon.None, key, keys, Answer, i, !a.On);
                    ry += UiTokens.Control;
                    ry = Sentence(s, ron, a.Hint, rx, ry, rw, a.On ? UiRole.Text2 : UiRole.Muted, 2);
                }
            }
            // Their holding in the Stocks app and the rich list as the paper prints it: the way on from this page.
            var foot = new System.Collections.Generic.List<UiActionModel>(2);
            if (r.CanHolding)
            {
                foot.Add(Act(r.HoldingLabel, UiButtonKind.Secondary, UiIcon.ChartLineUp, UiAction.Count, FinanceAct.OpenHolding));
            }
            if (!string.IsNullOrEmpty(r.ListLabel))
            {
                foot.Add(Act(r.ListLabel, UiButtonKind.Secondary, UiIcon.Newspaper, UiAction.Count, FinanceAct.ShowList));
            }
            if (foot.Count > 0 && bottom - UiTokens.Control >= ry)
            {
                Buttons(s, ron, foot.ToArray(), rx, bottom - UiTokens.Control, rw, keys);
            }
        }

        // What they did lately, dated, the live one marked.
        private static float Moves(UiScene s, UiAt at, FinanceRivalsFacts r, float x, float y, float w, float bottom)
        {
            if (r.Profile == null || bottom - y < UiTokens.S16 * 2f)
            {
                return y;
            }
            y = Title(s, at, r.MovesTitle, x, y, w);
            if (r.Moves.Length == 0)
            {
                return Sentence(s, at, r.MovesNone, x, y, w, UiRole.Text2, 2);
            }
            float dayW = UiDraw.SnapDown(UiTokens.S16 * 1.4f);
            for (int i = 0; i < r.Moves.Length && y + UiTokens.PairRow <= bottom - UiTokens.S16 * 2f; i++)
            {
                FinanceRivalMove m = r.Moves[i];
                if (m.Live)
                {
                    UiDraw.Chip(s, at, x, y + UiTokens.S1, m.Day, UiRole.Warn, dayW);
                }
                else
                {
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, dayW, UiStyle.SmallStrong, UiRole.Text2, m.Day, UiFlags.Truncate);
                }
                UiDraw.TextMiddle(s, at, x + dayW + UiTokens.S3, y, UiTokens.PairRow, w - dayW - UiTokens.S3, UiStyle.Body, UiRole.Text, m.What, UiFlags.Truncate);
                y += UiTokens.PairRow + UiTokens.S1;
            }
            return y + UiTokens.S3;
        }
    }
}
