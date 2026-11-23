using System;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank app's newer parts on the game's own widgets: the risk and the regulator (a row of the Bank tab that opens its page), the Cards tab and the Desk tab, and
    /// the sale of loans on a kind of borrower's page. The figures are the kit's (<see cref="OwnBankFacts"/>, read by <see cref="OwnBankReader"/>), so both interfaces
    /// show one number. Written on a click or after the day; nothing per frame.
    /// </summary>
    public sealed partial class OwnBankApp
    {
        private const string RiskRow = "b|stress";
        private const long SaleLeastCents = 10000L;   // a sale of loans is asked for from $100

        private OwnBankFacts Facts() => OwnBankLive.Instance.Facts(_tab);

        private static string Pct(int thousandths) => (thousandths / 10.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        // ------------------------------------------------------------------------------------------------- the risk row and its page

        private void RiskTableRow(OwnBankFacts f)
        {
            TableRow row = Row(RiskRow);
            row.Pick = true;
            row.Set(0, OwnBankText.T("reallife_ob_row_stress"));
            row.Set(1, Pct(f.Ratio), f.Ratio);
            row.Set(2, !f.RiskOn ? OwnBankText.T("reallife_ob_note_stress_off")
                : f.Signs != 0 ? OwnBankText.Fill("reallife_ob_note_stress_signs", "level", f.RiskNames[f.RiskLevel], "signs", f.SignWords)
                : OwnBankText.Fill("reallife_ob_note_stress_quiet", "level", f.RiskNames[f.RiskLevel]));
        }

        private void WriteRisk(InfoBox box, OwnBank b, OwnBankFacts f)
        {
            box.Title(OwnBankText.T("reallife_ob_stress_title"));
            box.Subtitle(b.Name);
            box.Chip(f.RiskNames[f.RiskLevel], f.RiskOn ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_box_ratio", OwnBankText.Fill("reallife_ob_stress_ratio", "ratio", Pct(f.Ratio), "close", Pct(f.LawRatio / 2), "law", Pct(f.LawRatio), "watch", Pct(f.WatchRatio)),
                f.Ratio < f.LawRatio ? CellTone.Red : f.Ratio < f.WatchRatio ? CellTone.Yellow : CellTone.Plain);
            if (f.RiskOn)
            {
                box.Row("reallife_ob_stress_warn_line", Pct(f.WarnRatio), f.Warned ? CellTone.Yellow : CellTone.Plain);
                box.Line(f.WatchHeld ? OwnBankText.Fill("reallife_ob_stress_watched", "share", Pct(f.LendScale))
                    : OwnBankText.T(f.Warned ? "reallife_ob_stress_warned" : "reallife_ob_stress_clear"), f.WatchHeld || f.Warned ? CellTone.Yellow : CellTone.Green);
                box.Row("reallife_ob_stress_signs", f.Signs == 0 ? OwnBankText.T("reallife_ob_stress_no_signs") : f.SignWords, f.Signs != 0 ? CellTone.Yellow : CellTone.Green);
                box.Row("reallife_ob_stress_cover", OwnBankText.Fill("reallife_ob_stress_cover_value", "days", (f.CoverTenths / 10f).ToString("0.#", CultureInfo.InvariantCulture),
                    "need", OwnBankText.Money(f.RunNeed), "reserve", Pct(f.ReserveShare)), f.CoverTenths < 20 ? CellTone.Yellow : CellTone.Plain);
                if (f.ShockNow >= 0)
                {
                    box.Line(OwnBankText.Fill("reallife_ob_stress_shock_now", "kind", f.ShockNowName), CellTone.Red);
                }
                if (f.ShockAhead >= 0)
                {
                    box.Line(OwnBankText.Fill("reallife_ob_stress_shock_next", "kind", f.ShockAheadName), CellTone.Yellow);
                }
                box.Section("reallife_ob_stress_conc_head");
                for (int j = 0; j < f.Conc.Length; j++)
                {
                    if (f.Conc[j].Share > 0)
                    {
                        box.RowWords(f.Conc[j].Name, f.Conc[j].Excess > 0 ? OwnBankText.Fill("reallife_ob_stress_conc_over", "share", Pct(f.Conc[j].Share), "limit",
                            Pct(f.ConcLimit)) : Pct(f.Conc[j].Share),
                            f.Conc[j].Excess > 0 ? CellTone.Yellow : CellTone.Plain);
                    }
                }
                box.Section("reallife_ob_stress_funding_head");
                box.Row("reallife_ob_stress_borrowed", f.Borrowed > 0 ? OwnBankText.Fill("reallife_ob_stress_borrowed_value", "amount", OwnBankText.Money(f.Borrowed), "rate",
                    OwnBankText.Rate(f.BorrowedBp),
                    "day", FinanceText.Day(f.BorrowDue)) : OwnBankText.T("reallife_ob_stress_borrowed_none"));
                box.Row("reallife_ob_stress_borrow_room", OwnBankText.Fill("reallife_ob_stress_borrow_room_value", "amount", OwnBankText.Money(f.BorrowRoom), "rate", OwnBankText.Rate(f.BorrowBp)));
                if (f.SoldFace > 0)
                {
                    box.Row("reallife_ob_stress_sold", OwnBankText.Fill("reallife_ob_stress_sold_value", "face", OwnBankText.Money(f.SoldFace), "loss", OwnBankText.Money(f.SoldLoss)));
                }
            }
            else
            {
                box.Line(OwnBankText.T("reallife_ob_stress_off_body"), CellTone.Yellow);
            }
            box.Row("reallife_ob_stress_wf", OwnBankText.Fill("reallife_ob_stress_wf_value", "assets", OwnBankText.Money(f.WfAssets), "savers", OwnBankText.Money(f.WfSavers),
                "banks", OwnBankText.Money(f.WfBanks), "fee", OwnBankText.Money(f.WfFee), "owner", OwnBankText.Money(f.WfOwner)), f.WfOwner <= 0 ? CellTone.Red : CellTone.Plain);
            if (f.WfSaversLoss > 0)
            {
                box.Line(OwnBankText.Fill("reallife_ob_stress_wf_short", "amount", OwnBankText.Money(f.WfSaversLoss)), CellTone.Red);
            }
            Transform buttons = box.ButtonRow();
            if (f.RiskOn)
            {
                if (f.BorrowRoom >= OwnBankCatalogue.CapitalStepCents)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_stress_borrow_button", () => AskAmount("reallife_ob_ask_borrow", OwnBankCatalogue.CapitalStepCents * 10, cents =>
                    {
                        cents = Math.Min(OwnBankRules.ToStep(cents), f.BorrowRoom);
                        BankCity city = OwnBankGame.City();
                        if (cents <= 0)
                        {
                            return "reallife_ob_why_borrow_too_much";
                        }
                        Ask("reallife_ob_confirm_borrow_title".Localize(new { bank = b.Name }), "reallife_ob_confirm_borrow_body".Localize(new { amount = OwnBankText.Money(cents),
                            rate = OwnBankText.Rate(OwnBankRules.BorrowBp(city, b)), fee = OwnBankText.Money(OwnBankRules.BorrowFee(cents)) }), "reallife_ob_stress_borrow_button", false,
                            () => _director.Borrow(cents, out string why) ? null : why);
                        return null;
                    }));
                }
                if (f.Borrowed > 0)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_stress_repay_button", () => AskAmount("reallife_ob_ask_repay", Math.Min(f.Borrowed, OwnBankRules.FreeCash(b)),
                        cents => _director.Repay(cents, out string why) ? null : why));
                }
                if (f.ToClear > 0)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_ob_box_put_in", () => AskAmount("reallife_ob_ask_put_in", Math.Max(OwnBankCatalogue.CapitalStepCents * 10, f.ToClear), cents =>
                    {
                        string why;
                        return _director.PutIn(cents, out why) ? null : why;
                    }));
                }
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_box_back", Back);
            box.Note(OwnBankText.T("reallife_ob_stress_prudence"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // The sale of loans on a kind of borrower's page: what other banks pay today, and the button that asks how much.
        private void SaleRow(InfoBox box, OwnBankFacts f, int segment)
        {
            if (f.RiskOn)
            {
                box.Row("reallife_ob_sale_head", OwnBankText.Fill("reallife_ob_sale_terms", "discount", Pct(f.SaleDiscount), "most", OwnBankText.Money(f.Segments[segment].SaleMost)));
            }
        }

        private void SaleButton(OwnBank b, OwnBankFacts f, int segment, Transform buttons)
        {
            long most = f.RiskOn ? f.Segments[segment].SaleMost : 0;
            if (most >= SaleLeastCents)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_sale_button", () => AskAmount("reallife_ob_ask_sale", most, cents =>
                {
                    long face = Math.Min(cents, most);
                    BankCity city = OwnBankGame.City();
                    long loss = face * OwnBankRules.SaleDiscountOf(city, b) / 1000;
                    Ask("reallife_ob_confirm_sale_title".Localize(new { bank = b.Name }), "reallife_ob_confirm_sale_body".Localize(new { face = OwnBankText.Money(face),
                        kind = OwnBankText.Segment(segment), cash = OwnBankText.Money(face - loss), loss = OwnBankText.Money(loss) }), "reallife_ob_sale_button", true,
                        () => _director.SellLoans(segment, face, out string why) ? null : why);
                    return null;
                }));
            }
        }

        // ------------------------------------------------------------------------------------------------- the dividends' next sum

        private void DividendNext(InfoBox box, OwnBankFacts f)
        {
            box.Row("reallife_ob_div_expected", f.DivExpected > 0 ? OwnBankText.Fill("reallife_ob_div_expected_value", "amount",
                OwnBankText.Money(f.DivExpected)) : OwnBankText.T("reallife_ob_div_expected_none"));
            if (f.DivExpectedTax > 0)
            {
                box.Row("reallife_ob_div_tax", OwnBankText.Fill("reallife_ob_div_tax_value", "tax", OwnBankText.Money(f.DivExpectedTax), "rate", OwnBankText.Rate(f.DivTaxBp)));
            }
        }

        // ------------------------------------------------------------------------------------------------- the cards tab

        private void WriteCards(InfoBox box, OwnBank b, BankCity c, ref string selected)
        {
            OwnBankFacts f = Facts();
            int picked = -1;
            for (int j = 0; j < f.CardKinds.Length; j++)
            {
                OwnCardKindFact k = f.CardKinds[j];
                string id = "c|" + j;
                TableRow row = Row(id);
                row.Pick = true;
                if (id == _pick)
                {
                    selected = id;
                    picked = j;
                }
                row.Set(0, k.Tier + " (" + k.Name + ")");
                row.Set(1, Whole(k.Holders), k.Holders);
                row.Set(2, OwnBankText.Money(k.LimitCents), k.LimitCents / 100.0);
                row.Set(3, OwnBankText.Money(k.Balance), k.Balance / 100.0);
                row.Set(4, OwnBankText.Money(k.Arrears), k.Arrears / 100.0, k.Arrears > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(5, Pct(k.DefaultPerMille), k.DefaultPerMille, k.DefaultPerMille >= 100 ? TableRow.Red : k.DefaultPerMille >= 40 ? TableRow.Yellow : TableRow.Plain);
                row.Set(6, Whole(k.Queue), k.Queue, k.Queue > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(7, OwnBankText.T(k.Issue && f.CardsOn ? "reallife_ob_lends_yes" : "reallife_ob_lends_no"));
            }
            if (picked >= 0)
            {
                WriteCardKind(box, b, f, picked);
                return;
            }
            _pick = null;
            box.Title(OwnBankText.T("reallife_ob_card_title"));
            box.Subtitle(b.Name);
            box.Chip(OwnBankText.T(f.CardsOn ? "reallife_ob_card_chip_issuing" : "reallife_ibox_chip_stopped"), f.CardsOn ? CellTone.Green : CellTone.Yellow);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_card_f_cards", Whole(f.CardsHeld + (f.OwnCard ? 1 : 0)));
            box.Row("reallife_ob_card_f_book", OwnBankText.Money(f.CardBook));
            box.Row("reallife_ob_card_f_month", OwnBankText.Signed(OwnBankBuild.CardMonthProfit(f.CardFig)), OwnBankBuild.CardMonthProfit(f.CardFig) < 0 ? CellTone.Red : CellTone.Plain);
            box.Row("reallife_ob_card_f_waiting", Whole(f.CardsQueued), f.CardsQueued > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_ob_card_rival", OwnBankText.Rate(f.CardRivalBp));
            box.Row("reallife_ob_card_yours", OwnBankText.Rate(f.CardRateNow));
            box.Switch("reallife_ob_card_switch", f.CardsOn, on => UiClone.Safe(() =>
            {
                _director.SetCards(on);
                Refresh();
            }));
            box.Headline("reallife_ob_card_rate_head");
            int rates = (f.CardRateMost - f.CardRateLeast) / f.CardRateStep + 1;
            box.Selector(rates, (f.CardRate - f.CardRateLeast) / f.CardRateStep,
                i => OwnBankText.Fill("reallife_ob_card_rate_option", "rate", OwnBankText.Rate(Math.Max(f.LoanFloorBp, f.CardRivalBp + f.CardRateLeast + i * f.CardRateStep)),
                    "points", OwnBankText.Points(f.CardRateLeast + i * f.CardRateStep)),
                i => UiClone.Safe(() =>
                {
                    _director.SetCardRate(f.CardRateLeast + i * f.CardRateStep);
                    Refresh();
                }));
            box.Headline("reallife_ob_card_fee_head");
            box.Selector(f.CardFees.Length, f.CardFee, i => f.CardFees[i] <= 0 ? OwnBankText.T("reallife_ob_card_fee_none") : OwnBankText.Fill("reallife_ob_card_fee_option", "fee",
                OwnBankText.Money(f.CardFees[i])),
                i => UiClone.Safe(() =>
                {
                    _director.SetCardFee(i);
                    Refresh();
                }));
            box.Headline("reallife_ob_card_auto_head");
            int autos = (f.CardAutoMost - f.CardAutoLeast) / f.CardLineStep + 1;
            box.Selector(autos, (f.CardAuto - f.CardAutoLeast) / f.CardLineStep, i => (f.CardAutoLeast + i * f.CardLineStep).ToString(CultureInfo.InvariantCulture), i => UiClone.Safe(() =>
            {
                _director.SetCardLines(f.CardAutoLeast + i * f.CardLineStep, f.CardDecline);
                Refresh();
            }));
            box.Headline("reallife_ob_card_decline_head");
            int declines = Math.Max(1, (f.CardAuto - f.CardDeclineLeast) / f.CardLineStep + 1);
            box.Selector(declines, Math.Min(declines - 1, (f.CardDecline - f.CardDeclineLeast) / f.CardLineStep), i => (f.CardDeclineLeast + i * f.CardLineStep).ToString(CultureInfo.InvariantCulture),
                i => UiClone.Safe(() =>
                {
                    _director.SetCardLines(f.CardAuto, f.CardDeclineLeast + i * f.CardLineStep);
                    Refresh();
                }));
            box.Note(OwnBankText.T("reallife_ob_card_note"));
            WriteOwnCard(box, b, f);
            Meta.Guide.Point(box, GuideTitle);
        }

        // The owner's own card in the cards box: its limit, what is owed, and the buttons that open and close it.
        private void WriteOwnCard(InfoBox box, OwnBank b, OwnBankFacts f)
        {
            box.SectionWords(OwnBankText.Fill("reallife_ob_own_title", "card", f.OwnName));
            Transform buttons = box.ButtonRow();
            if (f.OwnCard)
            {
                box.Row("reallife_ob_own_limit", OwnBankText.Money(f.OwnLimit));
                box.Row("reallife_ob_own_owed", OwnBankText.Money(f.OwnOwed), f.OwnOwed > 0 ? CellTone.Yellow : CellTone.Plain);
                box.Row("reallife_ob_own_rate", OwnBankText.Rate(f.OwnRateBp));
                if (f.OwnLostAll > 0)
                {
                    box.Row("reallife_ob_own_lost", OwnBankText.Money(f.OwnLostAll), CellTone.Red);
                }
                box.Note(OwnBankText.T("reallife_ob_own_open_note"));
                if (f.OwnOwed <= 0)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_own_close", () =>
                    {
                        string why;
                        if (!_director.CloseOwnCard(out why))
                        {
                            Notifications.Show(NotificationType.Warning, why, null, 8f, null, null, true, false);
                        }
                        Refresh();
                    });
                }
                return;
            }
            box.Note(OwnBankText.Fill("reallife_ob_own_intro", "most", OwnBankText.Money(f.OwnMost)));
            if (!f.CardsOn)
            {
                box.Line(OwnBankText.T("reallife_ob_own_needs_cards"), CellTone.Yellow);
            }
            else if (f.OwnMost <= 0)
            {
                box.Line(OwnBankText.Fill("reallife_ob_own_needs_capital", "least", OwnBankText.Money(f.OwnLeast)), CellTone.Yellow);
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ob_own_open", () => AskAmount("reallife_ob_ask_own_limit", f.OwnMost, cents => _director.OpenOwnCard(cents / 100,
                    out string why) ? null : why));
            }
        }

        private void WriteCardKind(InfoBox box, OwnBank b, OwnBankFacts f, int j)
        {
            OwnCardKindFact k = f.CardKinds[j];
            box.Title(k.Tier);
            box.Subtitle(k.Name);
            box.Chip(OwnBankText.T(k.Issue && f.CardsOn ? "reallife_ob_card_chip_issuing" : "reallife_ibox_chip_stopped"), k.Issue && f.CardsOn ? CellTone.Green : CellTone.Yellow);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_card_holders", OwnBankText.Fill("reallife_ob_card_holders_value", "now", Whole(k.Holders), "wanted", Whole(k.Wanted)));
            box.Row("reallife_ob_card_balance", OwnBankText.Money(k.Balance));
            box.Row("reallife_ob_box_arrears", OwnBankText.Money(k.Arrears), k.Arrears > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_ob_box_defaults", Pct(k.DefaultPerMille));
            box.Row("reallife_ob_card_applicants", OwnBankText.Fill("reallife_ob_card_applicants_value", "auto", Pct(k.AutoShare), "queue", Pct(k.QueueShare),
                "declined", Pct(Math.Max(0, 1000 - k.AutoShare - k.QueueShare))));
            box.Row("reallife_ob_card_queue", Whole(k.Queue), k.Queue > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Headline("reallife_ob_card_limit_head");
            int count = f.CardLimits[j].Length;
            box.Selector(count, k.LimitStep, i => OwnBankText.Money(f.CardLimits[j][i]), i => UiClone.Safe(() =>
            {
                _director.SetCardLimit(j, i);
                Refresh();
            }));
            box.Switch("reallife_ob_card_issue_switch", k.Issue, on => UiClone.Safe(() =>
            {
                _director.SetCardIssue(j, on);
                Refresh();
            }));
            Transform buttons = box.ButtonRow();
            if (k.Queue > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ob_card_approve", () => UiClone.Safe(() => Decide(j, true)));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_card_decline", () => UiClone.Safe(() => Decide(j, false)));
                box.Note(OwnBankText.T("reallife_ob_card_queue_note"));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_box_back", Back);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void Decide(int kind, bool approve)
        {
            if (!_director.DecideCards(kind, approve, out string why))
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f, null, null, true, false);
            }
            Refresh();
        }

        // ------------------------------------------------------------------------------------------------- the desk tab

        private void WriteDesk(InfoBox box, OwnBank b, BankCity c)
        {
            OwnBankFacts f = Facts();
            for (int i = 0; i < f.Book.Length; i++)
            {
                OwnHoldingFact h = f.Book[i];
                TableRow row = Row("t|" + i);
                row.Pick = false;
                long gain = h.Value - h.Cost;
                row.Set(0, h.Name);
                row.Set(1, Whole(h.Qty), h.Qty);
                row.Set(2, OwnBankText.Money(h.Cost), h.Cost / 100.0);
                row.Set(3, OwnBankText.Money(h.Value), h.Value / 100.0);
                row.Set(4, OwnBankText.Signed(gain), gain / 100.0, gain < 0 ? TableRow.Red : gain > 0 ? TableRow.Green : TableRow.Plain);
                row.Set(5, OwnBankText.Money(h.Year), h.Year / 100.0);
            }
            box.Title(OwnBankText.T("reallife_ob_desk_title"));
            box.Subtitle(b.Name);
            box.Chip(OwnBankText.T(f.DeskOn ? "reallife_ob_desk_chip_open" : "reallife_ob_desk_chip_closed"), f.DeskOn ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_desk_f_custody", OwnBankText.Money(f.Custody));
            box.Row("reallife_ob_desk_activity", f.Activity <= 0 ? OwnBankText.T("reallife_ob_desk_market_off") : OwnBankText.Fill("reallife_ob_desk_activity_value", "share", Pct(f.Activity)));
            box.Row("reallife_ob_desk_shares", OwnBankText.Fill("reallife_ob_desk_month_value", "now", OwnBankText.Money(f.DeskBrokerage), "last", OwnBankText.Money(f.DeskLastBrokerage)));
            if (f.Derivs)
            {
                box.Row("reallife_ob_desk_derivs", OwnBankText.Fill("reallife_ob_desk_month_value", "now", OwnBankText.Money(f.DeskDerivs), "last", OwnBankText.Money(f.DeskLastDerivs)));
            }
            else
            {
                box.Note(OwnBankText.T("reallife_ob_desk_no_derivs"));
            }
            box.Row("reallife_ob_desk_all", OwnBankText.Money(f.DeskAll));
            box.Row("reallife_ob_desk_f_book", OwnBankText.Fill("reallife_ob_box_fund_value_value", "value", OwnBankText.Money(f.BookValue), "gain", OwnBankText.Signed(f.BookValue - f.BookCost)));
            box.Row("reallife_ob_desk_room", OwnBankText.Money(f.BookRoom));
            box.Row("reallife_ob_desk_peak", OwnBankText.Money(f.DeskPeak));
            box.Row("reallife_ob_desk_f_drawdown", Pct(f.DeskDrawdown), f.DeskDrawdown * 2 >= f.DeskLimit ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_ob_desk_cuts", f.DeskCuts == 0 ? OwnBankText.T("reallife_ob_desk_never") : OwnBankText.Fill("reallife_ob_desk_cuts_value", "count", Whole(f.DeskCuts), "day",
                Whole(f.DeskCutDay)),
                f.DeskCuts > 0 ? CellTone.Yellow : CellTone.Plain);
            if (f.DeskOn && f.DeskMandate != 0 && !f.DeskBuying)
            {
                box.Line(OwnBankText.T("reallife_ob_desk_waiting"), CellTone.Yellow);
            }
            box.Switch("reallife_ob_desk_switch", f.DeskOn, on => UiClone.Safe(() =>
            {
                _director.SetDesk(on);
                Refresh();
            }));
            box.Headline("reallife_ob_desk_mandate");
            box.Selector(f.MandateNames.Length, f.DeskMandate, i => f.MandateNames[i], i => UiClone.Safe(() =>
            {
                _director.SetDeskMandate(i);
                Refresh();
            }));
            box.Headline("reallife_ob_desk_share");
            box.Selector(f.DeskShareMost / f.DeskShareStep + 1, f.DeskShare / f.DeskShareStep, i => OwnBankText.Fill("reallife_ob_desk_share_option", "share", Pct(i * f.DeskShareStep)),
                i => UiClone.Safe(() =>
            {
                _director.SetDeskShare(i * f.DeskShareStep);
                Refresh();
            }));
            box.Headline("reallife_ob_desk_limit");
            box.Selector((f.DeskLimitMost - f.DeskLimitLeast) / f.DeskLimitStep + 1, (f.DeskLimit - f.DeskLimitLeast) / f.DeskLimitStep,
                i => OwnBankText.Fill("reallife_ob_desk_limit_option", "limit", Pct(f.DeskLimitLeast + i * f.DeskLimitStep)), i => UiClone.Safe(() =>
                {
                    _director.SetDeskLimit(f.DeskLimitLeast + i * f.DeskLimitStep);
                    Refresh();
                }));
            box.Note(OwnBankText.Fill("reallife_ob_desk_limit_hint", "keep", Pct(f.DeskCutKeep)));
            box.Note(OwnBankText.Fill("reallife_ob_desk_note", "weight", Pct(f.FundWeight)));
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
