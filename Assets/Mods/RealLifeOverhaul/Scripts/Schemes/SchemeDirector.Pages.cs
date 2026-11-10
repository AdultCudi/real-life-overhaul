using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Schemes
{
    // The pages: each scheme said plainly before the player commits, with what it brings in green and what it risks in red.
    public sealed partial class SchemeDirector
    {
        private const int Gain = 1, Risk = -1;

        private static SchemePage Page(string titleKey, string kicker, string body)
        {
            return new SchemePage { Title = SchemeText.T(titleKey), Kicker = kicker ?? string.Empty, Body = body };
        }

        // The two sizes and the way out, the same on every page that offers them.
        private static void Sizes(SchemePage page, string smallHint, string smallGain, string largeHint, string largeGain, System.Action small, System.Action large)
        {
            page.Add(SchemeText.T("reallife_scheme_size_small"), smallHint, small).Line(smallGain, Gain).Line(SchemeText.T("reallife_scheme_risk_small"), 0)
                .Add(SchemeText.T("reallife_scheme_size_large"), largeHint, large).Line(largeGain, Gain).Line(SchemeText.T("reallife_scheme_risk_large"), Risk)
                .Add(SchemeText.T("reallife_scheme_leave"), SchemeText.T("reallife_scheme_leave_hint"), null);
        }

        private void ShowClaim(string place, int cover)
        {
            if (!Shop(place, out SchemeShopInfo shop) || SchemeBridges.CoverMost == null)
            {
                return;
            }
            int most = SchemeBridges.CoverMost(cover);
            string small = SchemeText.Money(SchemeRules.ClaimLoss(most, SchemeCatalogue.Small)), large = SchemeText.Money(SchemeRules.ClaimLoss(most, SchemeCatalogue.Large));
            SchemePage page = Page("reallife_scheme_claim_title", shop.Name,
                SchemeText.Fill("reallife_scheme_claim_body", "business", shop.Name, "share", SchemeText.Percent(SchemeCatalogue.SurchargePermille)));
            Sizes(page, SchemeText.Fill("reallife_scheme_claim_hint", "amount", small), SchemeText.Fill("reallife_scheme_claim_gain", "amount", small),
                SchemeText.Fill("reallife_scheme_claim_hint", "amount", large), SchemeText.Fill("reallife_scheme_claim_gain", "amount", large),
                () => DoClaim(place, cover, SchemeCatalogue.Small), () => DoClaim(place, cover, SchemeCatalogue.Large));
            _panel.Show(page);
        }

        private void ShowExpense()
        {
            int percent = SchemeBridges.TaxPercent != null ? SchemeBridges.TaxPercent() : 0;
            long average = AverageTakings();
            long small = SchemeRules.ExpenseAmount(average, SchemeCatalogue.Small), large = SchemeRules.ExpenseAmount(average, SchemeCatalogue.Large);
            SchemePage page = Page("reallife_scheme_expense_title", SchemeText.T("reallife_scheme_expense_kicker"),
                SchemeText.Fill("reallife_scheme_expense_body", "percent", SchemeText.Whole(percent), "share", SchemeText.Percent(SchemeCatalogue.SurchargePermille)));
            Sizes(page, SchemeText.Fill("reallife_scheme_expense_hint", "amount", SchemeText.Money(small)),
                SchemeText.Fill("reallife_scheme_expense_gain", "amount", SchemeText.Money(SchemeRules.ExpenseGain(small, percent))),
                SchemeText.Fill("reallife_scheme_expense_hint", "amount", SchemeText.Money(large)),
                SchemeText.Fill("reallife_scheme_expense_gain", "amount", SchemeText.Money(SchemeRules.ExpenseGain(large, percent))),
                () => DoExpense(SchemeCatalogue.Small), () => DoExpense(SchemeCatalogue.Large));
            _panel.Show(page);
        }

        // A business's second set of books: the sales that never happened, and the cash that goes through the till.
        private void ShowBooks(string place)
        {
            if (!Shop(place, out SchemeShopInfo shop))
            {
                return;
            }
            SchemeShop kept = _state.Shop(place, false);
            SchemePage page = Page("reallife_scheme_books_title", shop.Name, SchemeText.Fill("reallife_scheme_books_body", "business", shop.Name,
                "cash", SchemeText.Money(_state.Dirty)));
            page.Add(SchemeText.T("reallife_scheme_books_lift"), SchemeText.Fill("reallife_scheme_books_lift_hint", "share", SchemeText.Percent(kept != null ? kept.Lift : 0)),
                    () => ShowLift(place))
                .Add(SchemeText.T("reallife_scheme_books_wash"), SchemeText.Fill("reallife_scheme_books_wash_hint", "share", SchemeText.Percent(kept != null ? kept.Wash : 0)),
                    () => ShowWash(place))
                .Add(SchemeText.T("reallife_scheme_leave"), SchemeText.T("reallife_scheme_leave_hint"), null);
            _panel.Show(page);
        }

        private void ShowLift(string place)
        {
            if (!Shop(place, out SchemeShopInfo shop))
            {
                return;
            }
            SchemeShop kept = _state.Shop(place, false);
            int now = kept != null ? kept.Lift : 0, little = SchemeCatalogue.LiftSteps[1], lot = SchemeCatalogue.LiftSteps[2];
            SchemePage page = Page("reallife_scheme_lift_title", shop.Name, SchemeText.Fill("reallife_scheme_lift_body", "business", shop.Name, "now", SchemeText.Percent(now),
                "share", SchemeText.Percent(SchemeCatalogue.SurchargePermille)));
            page.Add(SchemeText.Fill("reallife_scheme_lift_set", "share", SchemeText.Percent(little)), SchemeText.T("reallife_scheme_lift_little_hint"), () => SetLift(place, little))
                .Line(SchemeText.T("reallife_scheme_lift_risk_little"), 0)
                .Add(SchemeText.Fill("reallife_scheme_lift_set", "share", SchemeText.Percent(lot)), SchemeText.T("reallife_scheme_lift_lot_hint"), () => SetLift(place, lot))
                .Line(SchemeText.T("reallife_scheme_lift_risk_lot"), Risk);
            if (now > 0)
            {
                page.Add(SchemeText.T("reallife_scheme_lift_plain"), SchemeText.T("reallife_scheme_lift_plain_hint"), () => SetLift(place, 0));
            }
            else
            {
                page.Add(SchemeText.T("reallife_scheme_leave"), SchemeText.T("reallife_scheme_leave_hint"), null);
            }
            _panel.Show(page);
        }

        private void ShowWash(string place)
        {
            if (!Shop(place, out SchemeShopInfo shop))
            {
                return;
            }
            SchemeShop kept = _state.Shop(place, false);
            int now = kept != null ? kept.Wash : 0, little = SchemeCatalogue.WashSteps[1], lot = SchemeCatalogue.WashSteps[2], level = SchemeGame.Level;
            string body = SchemeText.Fill(shop.Cash ? "reallife_scheme_wash_body" : "reallife_scheme_wash_body_no_till", "business", shop.Name, "cash", SchemeText.Money(_state.Dirty),
                "cut", SchemeText.Percent(SchemeCatalogue.WashCut), "report", SchemeText.Money(SchemeCatalogue.AtPrices(SchemeCatalogue.ReportFrom, level)),
                "now", SchemeText.Percent(now));
            if (_state.Watched)
            {
                body += "\n\n" + SchemeText.T("reallife_scheme_wash_watched");
            }
            SchemePage page = Page("reallife_scheme_wash_title", shop.Name, body);
            if (shop.Cash)
            {
                page.Add(SchemeText.Fill("reallife_scheme_wash_set", "share", SchemeText.Percent(little)),
                        SchemeText.Fill("reallife_scheme_wash_hint", "amount", SchemeText.Money(SchemeCatalogue.Share(shop.Average, little))), () => SetWash(place, little))
                    .Line(SchemeText.T("reallife_scheme_wash_risk_little"), 0)
                    .Add(SchemeText.Fill("reallife_scheme_wash_set", "share", SchemeText.Percent(lot)),
                        SchemeText.Fill("reallife_scheme_wash_hint", "amount", SchemeText.Money(SchemeCatalogue.Share(shop.Average, lot))), () => SetWash(place, lot))
                    .Line(SchemeText.T("reallife_scheme_wash_risk_lot"), Risk);
            }
            if (now > 0)
            {
                page.Add(SchemeText.T("reallife_scheme_wash_stop"), SchemeText.T("reallife_scheme_wash_stop_hint"), () => SetWash(place, 0));
            }
            else
            {
                page.Add(SchemeText.T("reallife_scheme_leave"), SchemeText.T("reallife_scheme_leave_hint"), null);
            }
            _panel.Show(page);
        }

        private void ShowClub(string place)
        {
            if (!Shop(place, out SchemeShopInfo shop))
            {
                return;
            }
            bool people = !SchemeGame.InSession && SchemeBridges.People != null && SchemeBridges.People();
            if (!people)
            {
                // The city's people are another machine's, or nobody's: said before anything is taken, and nothing is.
                SchemePage none = Page("reallife_scheme_club_title", shop.Name, SchemeText.T("reallife_scheme_note_club_no_people"));
                none.Add(SchemeText.T("reallife_scheme_leave"), SchemeText.T("reallife_scheme_leave_hint"), null);
                _panel.Show(none);
                return;
            }
            int level = SchemeGame.Level;
            string stake = SchemeText.Money(SchemeCatalogue.AtPrices(SchemeCatalogue.ClubStakeMost, level));
            SchemePage page = Page("reallife_scheme_club_title", shop.Name, SchemeText.Fill("reallife_scheme_club_body", "business", shop.Name, "stake", stake,
                "share", SchemeText.Percent(SchemeCatalogue.SurchargePermille)));
            Sizes(page, SchemeText.Fill("reallife_scheme_club_hint", "count", SchemeText.Whole(SchemeCatalogue.ClubSmall)),
                SchemeText.Fill("reallife_scheme_club_gain", "amount", SchemeText.Money(SchemeCatalogue.ClubSmall * SchemeCatalogue.AtPrices(SchemeCatalogue.ClubStakeMost, level))),
                SchemeText.Fill("reallife_scheme_club_hint", "count", SchemeText.Whole(SchemeCatalogue.ClubLarge)),
                SchemeText.Fill("reallife_scheme_club_gain", "amount", SchemeText.Money(SchemeCatalogue.ClubLarge * SchemeCatalogue.AtPrices(SchemeCatalogue.ClubStakeMost, level))),
                () => DoClub(place, SchemeCatalogue.Small), () => DoClub(place, SchemeCatalogue.Large));
            _panel.Show(page);
        }

        /// <summary>The fund a rival offers, as a page: put the money in, have it looked at first, or turn it down. False with no offer open.</summary>
        public bool ShowFund()
        {
            SchemeFund f = Ready("a fund's page") ? _state.Fund : null;
            if (f == null || f.State != FundState.Open)
            {
                return false;
            }
            SchemeGame.PutPhoneAway();
            long fee = SchemeRules.FundFee(f.Amount, SchemeGame.Level), back = SchemeRules.FundBack(f.Amount);
            string holding = RivalHolding(f.Tycoon);
            string body = SchemeText.Fill("reallife_scheme_fund_body", "rival", RivalName(f.Tycoon), "holding", holding, "amount", SchemeText.Money(f.Amount),
                "back", SchemeText.Money(back), "days", SchemeText.Whole(SchemeCatalogue.FundDays), "day", SchemeText.Whole(f.Due));
            if (f.Checked)
            {
                body += "\n\n" + SchemeText.Fill(f.Sham ? "reallife_scheme_fund_look_sham" : "reallife_scheme_fund_look_honest", "holding", holding);
            }
            SchemePage page = Page("reallife_scheme_fund_title", holding, body);
            page.Add(SchemeText.Fill("reallife_scheme_fund_put", "amount", SchemeText.Money(f.Amount)), SchemeText.T("reallife_scheme_fund_put_hint"), () => Answer(FundPut()))
                .Line(SchemeText.Fill("reallife_scheme_fund_put_cost", "amount", SchemeText.Money(f.Amount)), UiConsequence.Loss)
                .Line(SchemeText.Fill("reallife_scheme_fund_put_gain", "amount", SchemeText.Money(back)), f.Checked && f.Sham ? Risk : 0);
            if (!f.Checked)
            {
                page.Add(SchemeText.T("reallife_scheme_fund_look"), SchemeText.T("reallife_scheme_fund_look_hint"), () =>
                {
                    Answer(FundLook());
                    ShowFund();
                }).Line(SchemeText.Fill("reallife_scheme_fund_look_cost", "amount", SchemeText.Money(fee)), UiConsequence.Loss);
            }
            page.Add(SchemeText.T("reallife_scheme_fund_down"), SchemeText.T("reallife_scheme_fund_down_hint"), () => Answer(FundTurnDown()));
            return _panel.Show(page);
        }

        // What a press came to, as one of the game's notices.
        private static void Answer(SchemeSaid said)
        {
            if (!string.IsNullOrEmpty(said.Key))
            {
                SchemeGame.Tell(UI.Notification.NotificationType.Info, said.Key, said.Pairs != null && said.Pairs.Length > 1 ? SchemeGame.Data(said.Pairs) : null);
            }
        }
    }
}
