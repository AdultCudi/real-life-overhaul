using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Your bank page's newer parts: the risk and the regulator (a row of the Bank tab that opens its page), the credit cards tab and the trading desk tab, and the
    /// sale of loans on a kind of borrower's page. Written from the facts of the bank like the rest, and every button is an act of the director the old app uses. Game-free.
    /// </summary>
    public static partial class OwnBankBuild
    {
        public const int CardsTab = 5, DeskTab = 6;
        public const string RiskRow = "b|stress";

        // ------------------------------------------------------------------------------------------------- the dividends' sums

        private static void DividendNext(List<UtilitiesLine> lines, OwnBankFacts f, UtilitiesWords w)
        {
            lines.Add(BankParts.Pair(w.T("reallife_ob_div_expected", "Expected sum"), f.DivExpected > 0
                ? w.F("reallife_ob_div_expected_value", "{amount}, if profit goes on as now", "amount", C(w, f.DivExpected)) : w.T("reallife_ob_div_expected_none", "Nothing yet")));
            if (f.DivExpectedTax > 0)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_div_tax", "Tax withheld"), w.F("reallife_ob_div_tax_value", "{tax} of it, at {rate}, as the stock market's dividends are taxed", "tax",
                    C(w, f.DivExpectedTax), "rate", Rate(f.DivTaxBp))));
            }
        }

        // ------------------------------------------------------------------------------------------------- the risk row and its page

        private static string RiskName(OwnBankFacts f) => f.RiskLevel >= 0 && f.RiskLevel < f.RiskNames.Length ? f.RiskNames[f.RiskLevel] : string.Empty;

        private static UiDataRow RiskTableRow(OwnBankFacts f, UtilitiesWords w)
        {
            string note = !f.RiskOn ? w.T("reallife_ob_note_stress_off", "Off in the options: the bank is held to the usual rules.")
                : f.Signs != 0 ? w.F("reallife_ob_note_stress_signs", "{level}. Warning signs: {signs}", "level", RiskName(f), "signs", f.SignWords)
                : w.F("reallife_ob_note_stress_quiet", "{level}. No warning signs.", "level", RiskName(f));
            return BankParts.Row(new[] { w.T("reallife_ob_row_stress", "Risk and the regulator"), Pct(f.Ratio), note },
                BankParts.Tones(3, 2, f.Signs != 0 ? UiRole.WarnText : UiRole.None));
        }

        // The regulator's ladder, the warning signs, the cash against a run, the book's concentration, what is borrowed and what the owner would keep if the bank were taken,
        // with the tools to put it right: capital in, money from other banks and its repayment.
        private static void RiskCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var lines = new List<UtilitiesLine>(24);
            var steps = new List<UtilitiesStep>(2);
            var moves = new List<MoneyStep>(2);
            var actions = new List<UiActionModel>(3);
            lines.Add(BankParts.Pair(w.T("reallife_ob_box_ratio", "Capital ratio:"), w.F("reallife_ob_stress_ratio",
                "{ratio}: taken under {close}, in breach under {law}, watched under {watch}", "ratio",
                Pct(f.Ratio), "close", Pct(f.LawRatio / 2), "law", Pct(f.LawRatio), "watch", Pct(f.WatchRatio)),
                f.Ratio < f.LawRatio ? UiRole.DangerText : f.Ratio < f.WatchRatio ? UiRole.WarnText : UiRole.Text));
            if (f.RiskOn)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_stress_warn_line", "Lends freely over"), Pct(f.WarnRatio), f.Warned ? UiRole.WarnText : UiRole.Text));
                lines.Add(BankParts.Say(f.WatchHeld
                    ? w.F("reallife_ob_stress_watched", "The regulator is watching the bank: new lending is cut to {share}, and no dividend or pay is paid out.", "share", Pct(f.LendScale))
                    : f.Warned ? w.T("reallife_ob_stress_warned", "Under the warning line: the bank lends and buys no further until its capital is back over it.")
                    : w.T("reallife_ob_stress_clear", "Clear of the regulator's lines."), f.WatchHeld || f.Warned ? UiRole.WarnText : UiRole.OkText));
                lines.Add(BankParts.Pair(w.T("reallife_ob_stress_signs", "Warning signs"), f.Signs == 0 ? w.T("reallife_ob_stress_no_signs", "None") : f.SignWords,
                    f.Signs != 0 ? UiRole.WarnText : UiRole.OkText));
                lines.Add(BankParts.Pair(w.T("reallife_ob_stress_cover", "Cash against a run"), w.F("reallife_ob_stress_cover_value",
                    "{days} days of withdrawals ({need} a day), reserves kept at {reserve}", "days",
                    (f.CoverTenths / 10f).ToString("0.#", Inv), "need", C(w, f.RunNeed), "reserve", Pct(f.ReserveShare)), f.CoverTenths < 20 ? UiRole.WarnText : UiRole.Text));
                if (f.ShockNow >= 0)
                {
                    lines.Add(BankParts.Say(w.F("reallife_ob_stress_shock_now", "This month {kind} borrowers are in trouble: more of their loans and cards fail.", "kind", f.ShockNowName),
                        UiRole.DangerText));
                }
                if (f.ShockAhead >= 0)
                {
                    lines.Add(BankParts.Say(w.F("reallife_ob_stress_shock_next", "Next month {kind} borrowers will be in trouble. Look at how much of the book is theirs.", "kind",
                        f.ShockAheadName), UiRole.WarnText));
                }
                for (int j = 0; j < f.Conc.Length; j++)
                {
                    if (f.Conc[j].Share > 0)
                    {
                        lines.Add(BankParts.Pair(f.Conc[j].Name, f.Conc[j].Excess > 0 ? w.F("reallife_ob_stress_conc_over", "{share} of the book, over the {limit} one kind may be", "share",
                            Pct(f.Conc[j].Share),
                            "limit", Pct(f.ConcLimit)) : Pct(f.Conc[j].Share), f.Conc[j].Excess > 0 ? UiRole.WarnText : UiRole.Text));
                    }
                }
                lines.Add(BankParts.Pair(w.T("reallife_ob_stress_borrowed", "Borrowed from other banks"), f.Borrowed > 0
                    ? w.F("reallife_ob_stress_borrowed_value", "{amount} at {rate}, due {day}", "amount", C(w, f.Borrowed), "rate", Rate(f.BorrowedBp), "day",
                        w.Day(f.BorrowDue)) : w.T("reallife_ob_stress_borrowed_none", "Nothing")));
                lines.Add(BankParts.Pair(w.T("reallife_ob_stress_borrow_room", "It may borrow"), w.F("reallife_ob_stress_borrow_room_value", "{amount} more, at {rate} today", "amount", C(w,
                    f.BorrowRoom), "rate",
                    Rate(f.BorrowBp))));
                if (f.SoldFace > 0)
                {
                    lines.Add(BankParts.Pair(w.T("reallife_ob_stress_sold", "Loans sold"), w.F("reallife_ob_stress_sold_value", "{face} in all, {loss} lost on the sales", "face", C(w,
                        f.SoldFace), "loss", C(w,
                        f.SoldLoss))));
                }
            }
            else
            {
                lines.Add(BankParts.Say(w.T("reallife_ob_stress_off_body",
                    "The bank risk is off in the options. Switch it to Standard or Hard for credit risk that follows the economy, runs with warning signs and the tools to meet them, and a " +
                        "regulator whose watch limits lending and dividends."), UiRole.WarnText));
            }
            lines.Add(BankParts.Pair(w.T("reallife_ob_stress_wf", "If the regulator took the bank now"), w.F("reallife_ob_stress_wf_value",
                "Its assets fetch {assets}. Savers are paid {savers}, other banks {banks}, the regulator keeps {fee} and you keep {owner}.", "assets", C(w, f.WfAssets), "savers", C(w, f.WfSavers),
                "banks", C(w, f.WfBanks), "fee", C(w, f.WfFee), "owner", C(w, f.WfOwner)), f.WfOwner <= 0 ? UiRole.DangerText : UiRole.Text));
            if (f.WfSaversLoss > 0)
            {
                lines.Add(BankParts.Say(w.F("reallife_ob_stress_wf_short", "Its savers would lose {amount}.", "amount", C(w, f.WfSaversLoss)), UiRole.DangerText));
            }
            lines.Add(BankParts.Say(w.T("reallife_ob_stress_prudence",
                "To keep clear of it: hold your capital over the warning line, spread the book over more kinds of borrower, keep the cash for a few days of a run, pay savers fairly, and " +
                    "keep the owner's dividends for good years."),
                UiRole.Text2));
            if (f.RiskOn)
            {
                long step = StepDollars(f);
                int card = cards.Next;
                int[] ladder = Ladder(step);
                int most = (int)Math.Min(999999999L / step * step, Math.Max(0L, f.BorrowRoom / 100L) / step * step);
                if (most >= step)
                {
                    steps.Add(BankParts.AmountStep(w, w.T("reallife_ob_stress_borrow", "Borrow"), BorrowDollars(m.View, f), (int)step, most, MoneyView.FBorrow, card, steps.Count,
                        w.F("reallife_ob_stress_borrow_hint", "Up to {most}, for a month, with a fee of {fee} on the most", "most", w.Money(most), "fee", C(w, f.BorrowFee)),
                            out MoneyStep borrow, ladder, (int)step));
                    moves.Add(borrow);
                    actions.Add(BankParts.Button(w.T("reallife_ob_stress_borrow_button", "Borrow from other banks"), UiButtonKind.Secondary, UiIcon.Plus, MoneyAct.Borrow, false));
                }
                if (f.Borrowed > 0)
                {
                    actions.Add(BankParts.Button(w.T("reallife_ob_stress_repay_button", "Repay other banks"), UiButtonKind.Secondary, UiIcon.Minus, MoneyAct.Repay, false));
                }
                if (f.ToClear > 0)
                {
                    steps.Add(BankParts.AmountStep(w, w.T("reallife_ui_ob_put", "Put in"), PutDollars(m.View, f), (int)step, (int)(999999999L / step * step), MoneyView.FPut, card, steps.Count,
                        w.F("reallife_ob_stress_clear_hint", "{amount} would put the capital over the warning line", "amount", C(w, f.ToClear)), out MoneyStep put, ladder, (int)step));
                    moves.Add(put);
                    actions.Add(BankParts.Button(w.T("reallife_ob_box_put_in", "Put in capital"), UiButtonKind.Primary, UiIcon.Plus, MoneyAct.PutIn, true));
                }
            }
            cards.Add(new UtilitiesCard
            {
                Title = w.T("reallife_ob_stress_title", "Risk and the regulator"), Sub = f.Name, Chip = RiskName(f), ChipTone = f.RiskOn ? UiRole.Warn : UiRole.None, Lines = lines.ToArray(),
                Steps = steps.ToArray(), Actions = actions.ToArray()
            }, moves.ToArray());
        }

        /// <summary>The money to borrow from other banks: the player's figure kept to the most and the step, else one step.</summary>
        public static int BorrowDollars(MoneyView v, OwnBankFacts f)
        {
            long step = StepDollars(f), most = Math.Max(step, Math.Max(0L, f.BorrowRoom / 100L) / step * step);
            long value = v.F[MoneyView.FBorrow] > 0 ? v.F[MoneyView.FBorrow] : step;
            return (int)Math.Max(step, Math.Min(Math.Min(most, 999999999L), value / step * step));
        }

        /// <summary>The loans to sell of a kind: the player's figure kept to what one sale may take, else all of it.</summary>
        public static int SaleDollars(MoneyView v, OwnBankFacts f, int kind)
        {
            long most = kind >= 0 && kind < f.Segments.Length ? Math.Max(0L, f.Segments[kind].SaleMost / 100L) : 0L;
            long value = v.F[MoneyView.FSale] > 0 ? v.F[MoneyView.FSale] : most;
            return (int)Math.Max(0L, Math.Min(Math.Min(most, 999999999L), value));
        }

        // The sale of loans on a kind of borrower's card: the sum, the discount other banks take today and the button.
        private static void SaleControls(MoneyView view, OwnBankFacts f, UtilitiesWords w, OwnSegmentFact pick, int kind, int card, List<UtilitiesLine> lines, List<UtilitiesStep> steps,
            List<MoneyStep> moves,
            List<UiActionModel> actions)
        {
            if (!f.RiskOn)
            {
                return;
            }
            int most = (int)Math.Min(999999999L, pick.SaleMost / 100L);
            lines.Add(BankParts.Pair(w.T("reallife_ob_sale_head", "Sell to other banks"), w.F("reallife_ob_sale_terms", "At {discount} off their face today, up to {most} at a time",
                "discount", Pct(f.SaleDiscount),
                "most", w.Money(most))));
            if (most < 100)
            {
                return;
            }
            steps.Add(BankParts.AmountStep(w, w.T("reallife_ob_sale_amount", "Loans to sell"), SaleDollars(view, f, kind), 100, most, MoneyView.FSale, card, steps.Count,
                w.T("reallife_ob_sale_hint", "At their face. You get the face less the discount, at once"), out MoneyStep sale));
            moves.Add(sale);
            actions.Add(BankParts.Button(w.T("reallife_ob_sale_button", "Sell the loans"), UiButtonKind.Secondary, UiIcon.Coin, MoneyAct.SellLoans, false, false, kind));
        }

        // ------------------------------------------------------------------------------------------------- the cards tab

        private static void CardsPage(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var rows = new List<UiDataRow>(BankCardsKinds);
            var ids = new string[f.CardKinds.Length];
            int selected = -1;
            for (int j = 0; j < f.CardKinds.Length; j++)
            {
                OwnCardKindFact k = f.CardKinds[j];
                ids[j] = "c|" + j.ToString(Inv);
                selected = m.View.Row == ids[j] ? j : selected;
                rows.Add(BankParts.Row(new[]
                {
                    k.Tier + " (" + k.Name + ")", Whole(k.Holders), C(w, k.LimitCents), C(w, k.Balance), C(w, k.Arrears), Pct(k.DefaultPerMille), Whole(k.Queue),
                    k.Issue ? w.T("reallife_ob_lends_yes", "yes") : w.T("reallife_ob_lends_no", "no")
                }, BankParts.Tones(8, 7, k.Issue && f.CardsOn ? UiRole.Ok : UiRole.Warn)));
            }
            m.Table = BankParts.Table(new[] { w.T("reallife_ob_card_col_kind", "Cards for"), w.T("reallife_ob_card_col_cards", "Cards"), w.T("reallife_ob_card_col_limit", "Limit"),
                w.T("reallife_ob_card_col_balance", "Balance"), w.T("reallife_ob_col_arrears", "In arrears"), w.T("reallife_ob_col_defaults", "Defaults a year"),
                w.T("reallife_ob_card_col_wait", "Waiting"), w.T("reallife_ob_card_col_issue", "Issuing") },
                new[] { UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Chip }, rows, selected,
                w.T("reallife_ob_card_empty", "No cards yet."), string.Empty, UiIcon.Coin);
            m.RowIds = ids;
            long month = CardMonthProfit(f.CardFig);
            m.Figures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ob_card_f_cards", "Cards out"), Value = Whole(f.CardsHeld + (f.OwnCard ? 1 : 0)) },
                new UiFigureModel { Label = w.T("reallife_ob_card_f_book", "On the cards"), Value = C(w, f.CardBook) },
                new UiFigureModel { Label = w.T("reallife_ob_card_f_month", "This month"), Value = Signed(w, month) },
                new UiFigureModel { Label = w.T("reallife_ob_card_f_waiting", "Waiting for you"), Value = Whole(f.CardsQueued) }
            };
            if (selected < 0)
            {
                m.View.Row = null;
                CardRulesCard(m, f, w, cards);
                CardMonthCard(f, w, cards);
                OwnCardCard(m, f, w, cards);
                BlackTierCard(f, w, cards);
                return;
            }
            CardKindCard(m, f, w, cards, selected);
        }

        private const int BankCardsKinds = 5;

        /// <summary>A month's card profit: what the cards earned less what they lost and cost.</summary>
        public static long CardMonthProfit(long[] fig)
        {
            if (fig == null || fig.Length < CardTerms.Figures)
            {
                return 0;
            }
            return fig[CardTerms.FigInterest] + fig[CardTerms.FigInterchange] + fig[CardTerms.FigFees] - fig[CardTerms.FigLost] - fig[CardTerms.FigFraud] - fig[CardTerms.FigCosts];
        }

        // The owner's rules for every kind of client: the business on or off, the rate over the rivals', the yearly fee and the two lines of approval.
        private static void CardRulesCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var steps = new List<UtilitiesStep>(4);
            var moves = new List<MoneyStep>(4);
            int rates = (f.CardRateMost - f.CardRateLeast) / f.CardRateStep + 1;
            var rateValues = new int[rates];
            for (int i = 0; i < rates; i++)
            {
                rateValues[i] = f.CardRateLeast + i * f.CardRateStep;
            }
            int rateAt = Math.Max(0, Math.Min(rates - 1, (f.CardRate - f.CardRateLeast) / f.CardRateStep));
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_card_rate_head", "Your card rate"), w.F("reallife_ob_card_rate_option", "{rate} a year ({points} against the rival banks' cards)", "rate",
                Rate(Math.Max(f.LoanFloorBp, f.CardRivalBp + rateValues[rateAt])), "points", Points(w, rateValues[rateAt])), null, rateAt, rates, -1, MoneyAct.CardRate, 0, rateValues,
                    out MoneyStep rate));
            moves.Add(rate);
            if (f.CardFees.Length > 0)
            {
                var feeValues = new int[f.CardFees.Length];
                for (int i = 0; i < feeValues.Length; i++)
                {
                    feeValues[i] = i;
                }
                int feeAt = Math.Max(0, Math.Min(feeValues.Length - 1, f.CardFee));
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_card_fee_head", "Yearly fee"), f.CardFees[feeAt] <= 0 ? w.T("reallife_ob_card_fee_none", "No fee") : w.F("reallife_ob_card_fee_option",
                    "{fee} a year", "fee", C(w, f.CardFees[feeAt])), w.T("reallife_ob_card_fee_hint", "A fee brings in money and keeps some clients away"), feeAt, feeValues.Length, -1,
                        MoneyAct.CardFee, 0, feeValues,
                    out MoneyStep fee));
                moves.Add(fee);
            }
            int autos = (f.CardAutoMost - f.CardAutoLeast) / f.CardLineStep + 1;
            var autoValues = new int[autos];
            for (int i = 0; i < autos; i++)
            {
                autoValues[i] = f.CardAutoLeast + i * f.CardLineStep;
            }
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_card_auto_head", "Approved at once from a score of"), f.CardAuto.ToString(Inv), w.T("reallife_ob_card_auto_hint",
                "Applicants at or over this score get a card without asking you"), Math.Max(0, Math.Min(autos - 1, (f.CardAuto - f.CardAutoLeast) / f.CardLineStep)), autos, -1,
                    MoneyAct.CardAuto, 0, autoValues, out MoneyStep auto));
            moves.Add(auto);
            int declines = (f.CardAuto - f.CardDeclineLeast) / f.CardLineStep + 1;
            var declineValues = new int[Math.Max(1, declines)];
            for (int i = 0; i < declineValues.Length; i++)
            {
                declineValues[i] = f.CardDeclineLeast + i * f.CardLineStep;
            }
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_card_decline_head", "Declined under a score of"), f.CardDecline.ToString(Inv), w.T("reallife_ob_card_decline_hint",
                "Scores between the two lines wait in a queue for your answer"), Math.Max(0, Math.Min(declineValues.Length - 1, (f.CardDecline - f.CardDeclineLeast) / f.CardLineStep)),
                    declineValues.Length, -1,
                MoneyAct.CardBelow, 0, declineValues, out MoneyStep decline));
            moves.Add(decline);
            var lines = new List<UtilitiesLine>(4)
            {
                BankParts.Pair(w.T("reallife_ob_card_rival", "The rival banks' card rate"), Rate(f.CardRivalBp)),
                BankParts.Pair(w.T("reallife_ob_card_yours", "Your card rate now"), Rate(f.CardRateNow)),
                BankParts.Say(w.T("reallife_ob_card_note",
                    "Cards go to your own clients, a kind at a time. A card earns interest on what is carried and the shops' fee on what is spent, and loses to defaults and fraud. Pick a " +
                        "kind to set its limit."))
            };
            cards.Add(new UtilitiesCard
            {
                Title = w.T("reallife_ob_card_title", "Credit cards"), Sub = f.Name, Lines = lines.ToArray(), Steps = steps.ToArray(), SwitchLabel = w.T("reallife_ob_card_switch",
                    "Issue cards"), SwitchOn = f.CardsOn
            }, moves.ToArray(), MoneyAct.CardsOn);
        }

        private static void CardMonthCard(OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            long[] g = f.CardFig, l = f.CardLast;
            var lines = new List<UtilitiesLine>(8)
            {
                BankParts.Pair(w.T("reallife_ob_card_interest", "Interest"), C(w, g[CardTerms.FigInterest])),
                BankParts.Pair(w.T("reallife_ob_card_interchange", "Shops' fee on sales"), C(w, g[CardTerms.FigInterchange])),
                BankParts.Pair(w.T("reallife_ob_card_fees", "Yearly fees"), C(w, g[CardTerms.FigFees])),
                BankParts.Pair(w.T("reallife_ob_card_lost", "Written off"), C(w, g[CardTerms.FigLost]), g[CardTerms.FigLost] > 0 ? UiRole.WarnText : UiRole.Text),
                BankParts.Pair(w.T("reallife_ob_card_fraud", "Lost to fraud"), C(w, g[CardTerms.FigFraud])),
                BankParts.Pair(w.T("reallife_ob_card_costs", "Rewards and running costs"), C(w, g[CardTerms.FigCosts])),
                BankParts.Pair(w.T("reallife_ob_card_spend", "Spent on the cards"), C(w, g[CardTerms.FigSpend]))
            };
            if (f.HasLast)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_last", "Last month:"), Signed(w, CardMonthProfit(l))));
            }
            cards.Add(new UtilitiesCard { Title = w.T("reallife_ui_ob_month_title", "This month"), Heading = true, Lines = lines.ToArray() });
        }

        // One kind of client: its limit, the applicants the owner's lines let through and the queue.
        private static void CardKindCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards, int j)
        {
            OwnCardKindFact k = f.CardKinds[j];
            var steps = new List<UtilitiesStep>(1);
            var moves = new List<MoneyStep>(1);
            int count = f.CardLimits.Length > j && f.CardLimits[j] != null ? f.CardLimits[j].Length : 0;
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = i;
            }
            if (count > 0)
            {
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_card_limit_head", "Limit on each card"), C(w, k.LimitCents), null, Math.Max(0, Math.Min(count - 1, k.LimitStep)), count, -1,
                    MoneyAct.CardLimit, j, values,
                    out MoneyStep limit));
                moves.Add(limit);
            }
            int declined = Math.Max(0, 1000 - k.AutoShare - k.QueueShare);
            var lines = new List<UtilitiesLine>(8)
            {
                BankParts.Pair(w.T("reallife_ob_card_holders", "Cards out"), w.F("reallife_ob_card_holders_value", "{now}, and its clients would hold {wanted}", "now", Whole(k.Holders),
                    "wanted", Whole(k.Wanted))),
                BankParts.Pair(w.T("reallife_ob_card_balance", "Carried on them"), C(w, k.Balance)),
                BankParts.Pair(w.T("reallife_ob_box_arrears", "In arrears:"), C(w, k.Arrears), k.Arrears > 0 ? UiRole.WarnText : UiRole.Text),
                BankParts.Pair(w.T("reallife_ob_box_defaults", "Defaults a year:"), Pct(k.DefaultPerMille)),
                BankParts.Pair(w.T("reallife_ob_card_applicants", "Applicants"), w.F("reallife_ob_card_applicants_value", "{auto} approved at once, {queue} queued, {declined} declined",
                    "auto", Pct(k.AutoShare),
                    "queue", Pct(k.QueueShare), "declined", Pct(declined))),
                BankParts.Pair(w.T("reallife_ob_card_queue", "Waiting for you"), Whole(k.Queue), k.Queue > 0 ? UiRole.WarnText : UiRole.Text)
            };
            var actions = new List<UiActionModel>(2);
            if (k.Queue > 0)
            {
                actions.Add(BankParts.Button(w.T("reallife_ob_card_approve", "Approve them all"), UiButtonKind.Primary, UiIcon.Check, MoneyAct.CardApprove, true, false, j));
                actions.Add(BankParts.Button(w.T("reallife_ob_card_decline", "Decline them all"), UiButtonKind.Secondary, UiIcon.Minus, MoneyAct.CardRefuse, false, false, j));
                lines.Add(BankParts.Say(w.T("reallife_ob_card_queue_note", "Approved applicants have scores under your line, so their cards fail more often."), UiRole.Text2));
            }
            cards.Add(new UtilitiesCard
            {
                Title = k.Tier, Sub = k.Name, Chip = k.Issue && f.CardsOn ? w.T("reallife_ob_card_chip_issuing", "Issuing") : w.T("reallife_ibox_chip_stopped", "Stopped"), ChipTone = k.Issue
                    && f.CardsOn ? UiRole.Ok : UiRole.Warn,
                Lines = lines.ToArray(), Steps = steps.ToArray(), Actions = actions.ToArray(), SwitchLabel = w.T("reallife_ob_card_issue_switch", "Issue to them"), SwitchOn = k.Issue
            }, moves.ToArray(), MoneyAct.CardIssue);
        }

        // The owner's own card: the bank's top tier, its limit and what is owed on it.
        private static void OwnCardCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var lines = new List<UtilitiesLine>(6);
            var steps = new List<UtilitiesStep>(1);
            var moves = new List<MoneyStep>(1);
            var actions = new List<UiActionModel>(2);
            if (f.OwnCard)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_own_limit", "Limit"), C(w, f.OwnLimit)));
                lines.Add(BankParts.Pair(w.T("reallife_ob_own_owed", "You owe the bank"), C(w, f.OwnOwed), f.OwnOwed > 0 ? UiRole.WarnText : UiRole.Text));
                lines.Add(BankParts.Pair(w.T("reallife_ob_own_rate", "Rate"), Rate(f.OwnRateBp)));
                if (f.OwnLostAll > 0)
                {
                    lines.Add(BankParts.Pair(w.T("reallife_ob_own_lost", "Written off by the bank"), C(w, f.OwnLostAll), UiRole.DangerText));
                }
                lines.Add(BankParts.Say(w.T("reallife_ob_own_open_note",
                    "It is in your Finance app like any card, with its statement, minimum and interest. Cash you draw comes out of the bank's reserves.")));
                actions.Add(BankParts.Button(w.T("reallife_ob_own_close", "Close the card"), UiButtonKind.Secondary, UiIcon.Trash, MoneyAct.OwnCardClose, false, f.OwnOwed > 0));
            }
            else
            {
                lines.Add(BankParts.Say(w.F("reallife_ob_own_intro",
                    "A card of your own from your bank, in its top tier: a high limit and a rate under its best. It is lent from the bank's cash like any client's, so a large limit and a " +
                        "card left unpaid hurt the bank. The most is {most}.",
                    "most", C(w, f.OwnMost))));
                if (!f.CardsOn)
                {
                    lines.Add(BankParts.Say(w.T("reallife_ob_own_needs_cards", "Open the card business first."), UiRole.WarnText));
                }
                else if (f.OwnMost <= 0)
                {
                    lines.Add(BankParts.Say(w.F("reallife_ob_own_needs_capital", "The bank's capital is too small: its limit starts at {least}.", "least", C(w, f.OwnLeast)), UiRole.WarnText));
                }
                else
                {
                    long step = Math.Max(1L, f.OwnStep / 100L), least = Math.Max(step, f.OwnLeast / 100L), most = Math.Max(least, f.OwnMost / 100L);
                    int[] ladder = Ladder(step);
                    steps.Add(BankParts.AmountStep(w, w.T("reallife_ob_own_limit", "Limit"), OwnLimitDollars(m.View, f), (int)least, (int)Math.Min(most, 999999999L), MoneyView.FOwnLimit,
                        cards.Next, 0,
                        w.F("reallife_ob_own_limit_hint", "Between {least} and {most}", "least", w.Money(least), "most", w.Money(most)), out MoneyStep limit, ladder, (int)step));
                    moves.Add(limit);
                    actions.Add(BankParts.Button(w.T("reallife_ob_own_open", "Open your card"), UiButtonKind.Primary, UiIcon.Check, MoneyAct.OwnCardOpen, true));
                }
            }
            cards.Add(new UtilitiesCard { Title = w.F("reallife_ob_own_title", "Your {card} card", "card", f.OwnName), Sub = f.Name, Chip = f.OwnCard ? w.T("reallife_ob_own_chip",
                "Held") : null, Lines = lines.ToArray(),
                Steps = steps.ToArray(), Actions = actions.ToArray() }, moves.ToArray());
        }

        // The Black card tier: whom it is offered to (the least wealth and credit score), what it costs them a year and what it pays back, and how it stands in the books.
        private static void BlackTierCard(OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var steps = new List<UtilitiesStep>(4);
            var moves = new List<MoneyStep>(4);
            if (f.BlackWealths.Length > 0 && f.BlackFees.Length > 0 && f.BlackRewards.Length > 0)
            {
                var wealthValues = new int[f.BlackWealths.Length];
                for (int i = 0; i < wealthValues.Length; i++)
                {
                    wealthValues[i] = i;
                }
                int wealthAt = Math.Max(0, Math.Min(wealthValues.Length - 1, f.BlackWealth));
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_black_wealth_head", "Offered to savers holding at least"), C(w, f.BlackWealths[wealthAt]),
                    w.T("reallife_ob_black_wealth_hint", "Only savers this rich are offered the card"), wealthAt, wealthValues.Length, -1, MoneyAct.BlackWealth, 0, wealthValues,
                    out MoneyStep wealth));
                moves.Add(wealth);
                int scores = (f.BlackScoreMost - f.BlackScoreLeast) / f.BlackScoreStep + 1;
                var scoreValues = new int[scores];
                for (int i = 0; i < scores; i++)
                {
                    scoreValues[i] = f.BlackScoreLeast + i * f.BlackScoreStep;
                }
                int scoreAt = Math.Max(0, Math.Min(scores - 1, (f.BlackScore - f.BlackScoreLeast) / f.BlackScoreStep));
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_black_score_head", "And a credit score of at least"), f.BlackScore.ToString(Inv),
                    w.T("reallife_ob_black_score_hint", "A lower line brings more clients and more cards that fail"), scoreAt, scores, -1, MoneyAct.BlackScore, 0, scoreValues,
                    out MoneyStep score));
                moves.Add(score);
                var feeValues = new int[f.BlackFees.Length];
                for (int i = 0; i < feeValues.Length; i++)
                {
                    feeValues[i] = i;
                }
                int feeAt = Math.Max(0, Math.Min(feeValues.Length - 1, f.BlackFee));
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_black_fee_head", "Yearly fee"), w.F("reallife_ob_card_fee_option", "{fee} a year", "fee", C(w, f.BlackFees[feeAt])),
                    w.T("reallife_ob_black_fee_hint", "A high fee brings in money and keeps clients away"), feeAt, feeValues.Length, -1, MoneyAct.BlackFee, 0, feeValues, out MoneyStep fee));
                moves.Add(fee);
                var rewardValues = new int[f.BlackRewards.Length];
                for (int i = 0; i < rewardValues.Length; i++)
                {
                    rewardValues[i] = i;
                }
                int rewardAt = Math.Max(0, Math.Min(rewardValues.Length - 1, f.BlackReward));
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_black_reward_head", "Rewards on what is spent"), Rate(f.BlackRewards[rewardAt]),
                    w.T("reallife_ob_black_reward_hint", "Rewards cost the bank a share of every sale and draw clients in"), rewardAt, rewardValues.Length, -1, MoneyAct.BlackReward, 0, rewardValues,
                    out MoneyStep reward));
                moves.Add(reward);
            }
            var lines = new List<UtilitiesLine>(9)
            {
                BankParts.Pair(w.T("reallife_ob_black_rich", "Savers who qualify by wealth"), Whole(f.BlackRich)),
                BankParts.Pair(w.T("reallife_ob_black_scores", "Of them with the score"), Pct(f.BlackScoreShare)),
                BankParts.Pair(w.T("reallife_ob_card_holders", "Cards out"), w.F("reallife_ob_card_holders_value", "{now}, and its clients would hold {wanted}", "now", Whole(f.BlackHolders),
                    "wanted", Whole(f.BlackWanted))),
                BankParts.Pair(w.T("reallife_ob_card_balance", "Carried on them"), C(w, f.BlackBook)),
                BankParts.Pair(w.T("reallife_ob_box_arrears", "In arrears:"), C(w, f.BlackArrears), f.BlackArrears > 0 ? UiRole.WarnText : UiRole.Text),
                BankParts.Pair(w.T("reallife_ob_box_defaults", "Defaults a year:"), Pct(f.BlackDefaultPerMille)),
                BankParts.Pair(w.T("reallife_ob_card_yours", "Your card rate now"), Rate(f.BlackRateBp)),
                BankParts.Pair(w.T("reallife_ui_ob_month_title", "This month"), Signed(w, CardMonthProfit(f.BlackFig))),
                BankParts.Say(w.T("reallife_ob_black_note",
                    "The Black card is for your richest savers. It pays them rewards and a concierge, brings in a yearly fee and a bigger share of every sale, "
                        + "and counts in the card books and the regulator's risk like any card."))
            };
            if (f.BlackOn && !f.CardsOn)
            {
                lines.Add(BankParts.Say(w.T("reallife_ob_black_needs_cards", "Open the card business first."), UiRole.WarnText));
            }
            cards.Add(new UtilitiesCard
            {
                Title = w.T("reallife_ob_black_tier", "Black card service"), Sub = f.Name,
                Chip = f.BlackOn && f.CardsOn ? w.T("reallife_ob_card_chip_issuing", "Issuing") : w.T("reallife_ibox_chip_stopped", "Stopped"),
                ChipTone = f.BlackOn && f.CardsOn ? UiRole.Ok : UiRole.Warn, Lines = lines.ToArray(), Steps = steps.ToArray(), SwitchLabel = w.T("reallife_ob_black_switch", "Offer the Black card"),
                SwitchOn = f.BlackOn
            }, moves.ToArray(), MoneyAct.BlackOn);
        }

        /// <summary>The limit asked for the owner's own card: the player's figure kept to the least and the most, else the most.</summary>
        public static int OwnLimitDollars(MoneyView v, OwnBankFacts f)
        {
            long step = Math.Max(1L, f.OwnStep / 100L), least = Math.Max(step, f.OwnLeast / 100L), most = Math.Max(least, f.OwnMost / 100L);
            long value = v.F[MoneyView.FOwnLimit] > 0 ? v.F[MoneyView.FOwnLimit] : most;
            return (int)Math.Max(least, Math.Min(Math.Min(most, 999999999L), value / step * step));
        }

        // ------------------------------------------------------------------------------------------------- the desk tab

        private static void DeskPage(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var rows = new List<UiDataRow>(f.Book.Length);
            for (int i = 0; i < f.Book.Length; i++)
            {
                OwnHoldingFact h = f.Book[i];
                long gain = h.Value - h.Cost;
                rows.Add(BankParts.Row(new[] { h.Name, Whole(h.Qty), C(w, h.Cost), C(w, h.Value), Signed(w, gain), C(w, h.Year) }, BankParts.Tones(6, 4,
                    gain < 0 ? UiRole.DangerText : gain > 0 ? UiRole.OkText : UiRole.None)));
            }
            m.Table = BankParts.Table(new[] { w.T("reallife_ob_col_company", "Company"), w.T("reallife_ob_col_shares", "Shares"), w.T("reallife_ob_col_cost", "Cost"),
                w.T("reallife_ob_col_value", "Value"),
                w.T("reallife_ob_col_gain", "Gain"), w.T("reallife_ob_col_dividends", "Dividends a year") }, null, rows, -1, w.T("reallife_ob_desk_empty", "The own book holds no shares"),
                    string.Empty, UiIcon.ChartPie);
            m.RowIds = new string[rows.Count];
            m.Figures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ob_desk_f_custody", "Held for clients"), Value = C(w, f.Custody) },
                new UiFigureModel { Label = w.T("reallife_ob_desk_f_commission", "Commission this month"), Value = C(w, f.DeskBrokerage + f.DeskDerivs) },
                new UiFigureModel { Label = w.T("reallife_ob_desk_f_book", "Own book"), Value = C(w, f.BookValue) },
                new UiFigureModel { Label = w.T("reallife_ob_desk_f_drawdown", "Under its peak"), Value = Pct(f.DeskDrawdown) }
            };
            var steps = new List<UtilitiesStep>(3);
            var moves = new List<MoneyStep>(3);
            int mandates = Math.Max(1, f.MandateNames.Length);
            var mandateValues = new int[mandates];
            for (int i = 0; i < mandates; i++)
            {
                mandateValues[i] = i;
            }
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_desk_mandate", "Mandate of the own book"), f.DeskMandate >= 0
                && f.DeskMandate < f.MandateNames.Length ? f.MandateNames[f.DeskMandate] : string.Empty,
                w.T("reallife_ob_desk_mandate_hint", "None, the index (the largest companies), income (the best dividends) or growth (mid and small companies)"), Math.Max(0,
                    Math.Min(mandates - 1, f.DeskMandate)), mandates,
                -1, MoneyAct.DeskMandate, 0, mandateValues, out MoneyStep mandate));
            moves.Add(mandate);
            int shares = f.DeskShareMost / f.DeskShareStep + 1;
            var shareValues = new int[shares];
            for (int i = 0; i < shares; i++)
            {
                shareValues[i] = i * f.DeskShareStep;
            }
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_desk_share", "Share of the capital it may hold"), w.F("reallife_ob_desk_share_option", "At most {share} of the capital", "share",
                Pct(f.DeskShare)), null,
                Math.Max(0, Math.Min(shares - 1, f.DeskShare / f.DeskShareStep)), shares, -1, MoneyAct.DeskShare, 0, shareValues, out MoneyStep share));
            moves.Add(share);
            int limits = (f.DeskLimitMost - f.DeskLimitLeast) / f.DeskLimitStep + 1;
            var limitValues = new int[limits];
            for (int i = 0; i < limits; i++)
            {
                limitValues[i] = f.DeskLimitLeast + i * f.DeskLimitStep;
            }
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_desk_limit", "Risk limit"), w.F("reallife_ob_desk_limit_option", "Cut at a loss of {limit} from its peak", "limit", Pct(f.DeskLimit)),
                w.F("reallife_ob_desk_limit_hint", "A cut sells all but {keep} of the book and stops buying for a month", "keep", Pct(f.DeskCutKeep)),
                Math.Max(0, Math.Min(limits - 1, (f.DeskLimit - f.DeskLimitLeast) / f.DeskLimitStep)), limits, -1, MoneyAct.DeskLimit, 0, limitValues, out MoneyStep limit));
            moves.Add(limit);
            var lines = new List<UtilitiesLine>(12)
            {
                BankParts.Pair(w.T("reallife_ob_desk_activity", "The market today"), f.Activity <= 0 ? w.T("reallife_ob_desk_market_off",
                    "Not running: no commission") : w.F("reallife_ob_desk_activity_value",
                    "{share} of an ordinary day's activity", "share", Pct(f.Activity))),
                BankParts.Pair(w.T("reallife_ob_desk_shares", "Clients' trades in shares"), w.F("reallife_ob_desk_month_value", "{now} this month, {last} last month", "now", C(w,
                    f.DeskBrokerage), "last",
                    C(w, f.DeskLastBrokerage)))
            };
            if (f.Derivs)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_desk_derivs", "Clients' options and futures"), w.F("reallife_ob_desk_month_value", "{now} this month, {last} last month", "now",
                    C(w, f.DeskDerivs), "last",
                    C(w, f.DeskLastDerivs))));
            }
            else
            {
                lines.Add(BankParts.Say(w.T("reallife_ob_desk_no_derivs", "Options and futures are not offered: the exchange for raw materials is off in the options."), UiRole.Text2));
            }
            lines.Add(BankParts.Pair(w.T("reallife_ob_desk_all", "Commission in all"), C(w, f.DeskAll)));
            lines.Add(BankParts.Pair(w.T("reallife_ob_desk_room", "The own book may hold now"), C(w, f.BookRoom)));
            lines.Add(BankParts.Pair(w.T("reallife_ob_desk_peak", "Its peak"), C(w, f.DeskPeak)));
            lines.Add(BankParts.Pair(w.T("reallife_ob_desk_cuts", "Cut by the risk limit"), f.DeskCuts == 0 ? w.T("reallife_ob_desk_never", "Never") : w.F("reallife_ob_desk_cuts_value",
                "{count} times, last on day {day}", "count", f.DeskCuts.ToString(Inv), "day", f.DeskCutDay.ToString(Inv)), f.DeskCuts > 0 ? UiRole.WarnText : UiRole.Text));
            if (f.DeskOn && f.DeskMandate != 0 && !f.DeskBuying)
            {
                lines.Add(BankParts.Say(w.T("reallife_ob_desk_waiting", "Buying is stopped for a month after a cut."), UiRole.WarnText));
            }
            lines.Add(BankParts.Say(w.F("reallife_ob_desk_note",
                "Marked to the stock market's prices each day. The regulator counts the book at {weight} of its value, like the fund. Clients' commission follows how lively the market is. A " +
                    "wild market also costs the desk its clients' failed margin calls.",
                "weight", Pct(f.FundWeight))));
            cards.Add(new UtilitiesCard
            {
                Title = w.T("reallife_ob_desk_title", "Trading desk"), Sub = f.Name, Lines = lines.ToArray(), Steps = steps.ToArray(), SwitchLabel = w.T("reallife_ob_desk_switch",
                    "Desk open"), SwitchOn = f.DeskOn
            }, moves.ToArray(), MoneyAct.DeskOn);
        }
    }
}
