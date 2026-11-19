using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A firm's page of exits in the Tech app's own pages (the kit has the same page): the offers for the firm with accept, ask for more and turn down, the player's
    /// own shares sold later, and the way to the market. A notice of an offer lands here as it does on the kit.
    /// </summary>
    public sealed partial class TechApp
    {
        private const int PageExits = RealLifeOverhaul.Ui.TechView.PageExits;
        private int _counterAt = 1, _bankAt, _roadAt;
        private int _termKind, _termStep, _termTarget, _termMeasure;   // the terms picked for an offer: how to be paid, its size, an earn-out's target and what it counts

        private void WriteExits(InfoBox box, FirmPlace firm)
        {
            string place = firm.File.Place;
            ExitFile exits = _director.Exits;
            FirmExit e = exits != null ? exits.Exit(place, false) : null;
            box.Title(TechText.Fill("reallife_exit_title", "firm", firm.File.Name));
            if (exits == null)
            {
                box.Line(TechText.T("reallife_exit_off"));
                ExitsBack(box);
                return;
            }
            box.Section("reallife_ui_tech_exit_offers");
            TermDeal contract = _director.ContractOf(place);
            if (contract != null)
            {
                WriteDeal(box, contract);
            }
            else
            {
                ExitOffers(box, firm, e);
            }
            box.Section("reallife_ui_tech_exit_shares");
            ExitShares(box, firm);
            box.Section("reallife_ui_tech_exit_market");
            ExitMarket(box, firm, e);
            ExitsBack(box);
            Meta.Guide.Point(box, StartupsGuide);
        }

        private void ExitsBack(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_roles_button_back", () =>
            {
                _page = PageCap;
                Refresh();
            });
        }

        private void ExitOffers(InfoBox box, FirmPlace firm, FirmExit e)
        {
            string place = firm.File.Place;
            if (e == null || e.Offers.Count == 0)
            {
                box.Line(TechText.Fill("reallife_exit_offer_none", "firm", firm.File.Name, "least", TechText.Cents(_director.Level(ExitCatalogue.OfferMrrLeast))));
                if (e != null && e.SoldDay > 0)
                {
                    box.Line(TechText.Fill("reallife_exit_sold_line", "buyer", e.SoldTo, "amount", TechText.Cents(_director.Level(e.SoldFor)), "day", e.SoldDay.ToString()));
                }
                return;
            }
            TermChoice pick = TermPick(box, e.Offers[0]);
            int day = SaveGameManager.Current.Day;
            for (int i = 0; i < e.Offers.Count; i++)
            {
                ExitOffer o = e.Offers[i];
                int id = o.Id;
                box.Line(TechText.Fill(o.Hire ? "reallife_exit_hire_line" : "reallife_exit_offer_line", "buyer", o.Name, "amount", TechText.Cents(_director.Level(o.Price)),
                    "firm", firm.File.Name, "day", o.Lapse.ToString()));
                TermChoice choice = o.Hire ? default(TermChoice) : pick;
                TermPlan plan = _director.TermPlanFor(place, o, choice, out SaleResult r);
                box.Line(r.Owed <= 0 ? TechText.Fill("reallife_exit_offer_free", "you", TechText.Cents(_director.Level(Math.Max(0L, r.Player))))
                    : r.Gap > 0 ? TechText.Fill("reallife_exit_offer_gap", "owed", TechText.Cents(_director.Level(r.Owed)), "paid", TechText.Cents(_director.Level(r.Paid)),
                        "gap", TechText.Cents(_director.Level(r.Gap)))
                    : TechText.Fill("reallife_exit_offer_after", "investors", TechText.Cents(_director.Level(r.Paid)), "you", TechText.Cents(_director.Level(Math.Max(0L, r.Player)))));
                foreach (string line in SaleTermText.Lines(_director, plan, r, o, day))
                {
                    box.Line(line);   // every figure of the terms, before the game's box
                }
                string title = TechText.Fill("reallife_exit_confirm_accept_title", "firm", firm.File.Name);
                string body = choice.Kind != TermKind.Cash && plan.Ok ? SaleTermText.ConfirmBody(_director, firm.File.Name, o, plan, r, day)
                    : TechText.Fill(o.Hire ? "reallife_exit_confirm_hire_body" : "reallife_exit_confirm_accept_body", "firm", firm.File.Name, "buyer", o.Name,
                        "amount", TechText.Cents(_director.Level(o.Price)));
                Transform row = box.ButtonRow();
                InfoBox.Button(row, i == 0 ? InfoBox.Positive : InfoBox.Additional, "reallife_exit_button_accept", () => GameConfirm.Show(title, body,
                    () => UiClone.Safe(() => Do(() => _director.ExitAccept(place, id, choice))), null, null, null, true, true));
                InfoBox.Button(row, InfoBox.Additional, "reallife_exit_button_decline", () => Do(() => _director.ExitDecline(place, id)));
            }
            ExitOffer first = e.Offers[0];
            if (!first.Countered && !first.Hire)
            {
                string[] steps = RealLifeOverhaul.Ui.TechLive.CounterKeys;
                _counterAt = Math.Max(0, Math.Min(steps.Length - 1, _counterAt));
                if (box.HasSelector)
                {
                    box.Selector(steps.Length, _counterAt, i => TechText.T(steps[i]), i => Set(() => _counterAt = i));
                }
                int at = _counterAt, offer = first.Id;
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_exit_button_counter", () => Do(() => _director.ExitCounter(place, offer, at)));
            }
        }

        // The terms to be paid on: how, and how much of each. Shares only when the buyer is a listed company whose stock can be bought.
        private TermChoice TermPick(InfoBox box, ExitOffer first)
        {
            if (first.Hire || _director.Terms == null || !box.HasSelector)
            {
                return default(TermChoice);
            }
            bool shares = _director.TermsHaveShares(first);
            int[] kinds = shares ? new[] { 0, 1, 2, 3, 4 } : new[] { 0, 2, 3, 4 };
            int at = Math.Max(0, Array.IndexOf(kinds, Math.Max(0, Math.Min(TermKind.Count - 1, _termKind))));
            _termKind = kinds[at];
            int kind = _termKind;
            box.Selector(kinds.Length, at, i => TechText.T(SaleTermText.KindKeys[kinds[i]]), i => Set(() => _termKind = kinds[i]));
            if (kind == TermKind.Shares || kind == TermKind.Slice)
            {
                string[] keys = kind == TermKind.Shares ? SaleTermText.ShareKeys : SaleTermText.SliceKeys;
                _termStep = Math.Max(0, Math.Min(keys.Length - 1, _termStep));
                box.Selector(keys.Length, _termStep, i => TechText.T(keys[i]), i => Set(() => _termStep = i));
            }
            if (kind == TermKind.Boss)
            {
                _termStep = Math.Max(0, Math.Min(SaleTermText.YearKeys.Length - 1, _termStep));
                box.Selector(SaleTermText.YearKeys.Length, _termStep, i => TechText.T(SaleTermText.YearKeys[i]), i => Set(() => _termStep = i));
            }
            if (kind == TermKind.Earn || kind == TermKind.Boss)
            {
                _termTarget = Math.Max(0, Math.Min(SaleTermText.TargetKeys.Length - 1, _termTarget));
                box.Selector(SaleTermText.TargetKeys.Length, _termTarget, i => TechText.T(SaleTermText.TargetKeys[i]), i => Set(() => _termTarget = i));
                _termMeasure = Math.Max(0, Math.Min(SaleTermText.MeasureKeys.Length - 1, _termMeasure));
                box.Selector(SaleTermText.MeasureKeys.Length, _termMeasure, i => TechText.T(SaleTermText.MeasureKeys[i]), i => Set(() => _termMeasure = i));
            }
            return new TermChoice { Kind = kind, Step = _termStep, Target = _termTarget, Measure = _termMeasure };
        }

        // A deal of a sale's terms: how it stands, and for a contract the way to resign.
        private void WriteDeal(InfoBox box, TermDeal d)
        {
            int id = d.Id;
            foreach (string line in SaleTermText.DealLines(_director, d, SaveGameManager.Current.Day))
            {
                box.Line(line);
            }
            if (d.Kind == TermKind.Boss && d.Open)
            {
                string title = TechText.Fill("reallife_terms_confirm_resign_title", "firm", d.Firm);
                string body = TechText.Fill("reallife_terms_confirm_resign_body", "firm", d.Firm, "buyer", d.Buyer);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_terms_button_resign", () => GameConfirm.Show(title, body,
                    () => UiClone.Safe(() => Do(() => _director.TermResign(id))), null, null, null, true, true));
            }
        }

        private void ExitShares(InfoBox box, FirmPlace firm)
        {
            string place = firm.File.Place;
            StockInfo stock = _director.StockOf(place);
            if (stock.Live)
            {
                int stake = stock.Shares > 0 ? (int)(stock.Founder * 1000L / stock.Shares) : 0;
                box.Line(TechText.Fill("reallife_exit_stock_line", "ticker", stock.Ticker, "price", TechText.Cents(stock.Last), "stake", TechText.Mille(stake)));
                int held = _director.ExitLockLeft(place);
                box.Line(held > 0 ? TechText.Fill("reallife_exit_shares_locked", "days", held.ToString()) : TechText.T("reallife_exit_stock_free"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_exit_button_stock", () => Do(() => _director.StockShow(place)));
                return;
            }
            int locked = _director.ExitLockLeft(place);
            long room = _director.ExitRoom(place, out long cents, out int discount);
            if (locked > 0)
            {
                box.Line(TechText.Fill("reallife_exit_shares_locked", "days", locked.ToString()));
            }
            else if (room > 0)
            {
                box.Line(TechText.Fill("reallife_exit_shares_room", "shares", TechText.Whole(room), "amount", TechText.Cents(_director.Level(cents)), "discount", TechText.Mille(discount)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_exit_button_sell", () => Do(() => _director.ExitSell(place)));
            }
            else
            {
                box.Line(TechText.Fill("reallife_exit_shares_none", "days", ExitCatalogue.SecondaryDays.ToString(), "lock", ExitCatalogue.LockUpDays.ToString()));
            }
        }

        private void ExitMarket(InfoBox box, FirmPlace firm, FirmExit e)
        {
            string place = firm.File.Place;
            Transform row = box.ButtonRow();
            if (e == null || e.Stage == ListStage.None || e.Stage == ListStage.CalledOff)
            {
                if (e != null && e.Stage == ListStage.CalledOff)
                {
                    box.Line(TechText.Fill("reallife_exit_market_called", "firm", firm.File.Name, "day", e.Called.ToString()));
                }
                long mrr = _director.ListMonthOf(firm);
                if (mrr < ExitCatalogue.ListMrrLeast)
                {
                    box.Line(TechText.Fill("reallife_exit_market_not_ready", "least", TechText.Cents(_director.Level(ExitCatalogue.ListMrrLeast)),
                        "now", TechText.Cents(_director.Level(mrr)), "firm", firm.File.Name));
                }
                else
                {
                    IReadOnlyList<string> banks = _director.ExitBanks();
                    _bankAt = Math.Max(0, Math.Min(banks.Count - 1, _bankAt));
                    long fee = ExitRules.Retainer(_director.ListValueOf(firm));
                    box.Line(TechText.T("reallife_exit_market_start"));
                    if (box.HasSelector && banks.Count > 1)
                    {
                        box.Selector(banks.Count, _bankAt, i => TechText.Fill("reallife_exit_bank_line", "bank", banks[i], "fee", TechText.Cents(_director.Level(fee))),
                            i => Set(() => _bankAt = i));
                    }
                    int at = _bankAt;
                    InfoBox.Button(row, InfoBox.Additional, "reallife_exit_button_bankers", () => Do(() => _director.ExitBankers(place, at)));
                }
            }
            else if (e.Stage == ListStage.Bankers)
            {
                box.Line(TechText.Fill("reallife_exit_market_bankers", "bank", e.BankName, "fee", TechText.Cents(_director.Level(e.Fee))));
                string[] roads = RealLifeOverhaul.Ui.TechLive.RoadKeys;
                _roadAt = Math.Max(0, Math.Min(roads.Length - 1, _roadAt));
                if (box.HasSelector)
                {
                    box.Selector(roads.Length, _roadAt, i => TechText.T(roads[i]), i => Set(() => _roadAt = i));
                }
                int at = _roadAt;
                InfoBox.Button(row, InfoBox.Additional, "reallife_exit_button_road", () => Do(() => _director.ExitRoadshow(place, at)));
                InfoBox.Button(row, InfoBox.Additional, "reallife_exit_button_withdraw", () => Do(() => _director.ExitWithdraw(place)));
            }
            else if (e.Stage == ListStage.Roadshow)
            {
                box.Line(TechText.Fill("reallife_exit_market_wait", "demand", TechText.Mille(e.Demand), "day", e.PriceDay.ToString(), "low", TechText.Cents(_director.Level(e.Low)),
                    "high", TechText.Cents(_director.Level(e.High))));
                InfoBox.Button(row, InfoBox.Additional, "reallife_exit_button_withdraw", () => Do(() => _director.ExitWithdraw(place)));
            }
            else
            {
                box.Line(TechText.Fill("reallife_exit_market_listed", "firm", firm.File.Name, "price", TechText.Cents(_director.Level(e.Price)), "gross", TechText.Cents(_director.Level(e.Gross)),
                    "net", TechText.Cents(_director.Level(e.Net)), "bank", e.BankName));
                int locked = _director.ExitLockLeft(place);
                box.Line(locked > 0 ? TechText.Fill("reallife_exit_shares_locked", "days", locked.ToString()) : TechText.T("reallife_exit_market_free"));
            }
            InfoBox.Button(row, InfoBox.Additional, "reallife_exit_button_whole", () => Do(() => _director.ListWholeCompany(place)));
        }
    }
}
