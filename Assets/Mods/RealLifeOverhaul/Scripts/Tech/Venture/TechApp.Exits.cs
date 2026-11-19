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
            ExitOffers(box, firm, e);
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
            for (int i = 0; i < e.Offers.Count; i++)
            {
                ExitOffer o = e.Offers[i];
                int id = o.Id;
                box.Line(TechText.Fill(o.Hire ? "reallife_exit_hire_line" : "reallife_exit_offer_line", "buyer", o.Name, "amount", TechText.Cents(_director.Level(o.Price)),
                    "firm", firm.File.Name, "day", o.Lapse.ToString()));
                SaleResult r = _director.ExitPreview(place, o);
                box.Line(r.Owed <= 0 ? TechText.Fill("reallife_exit_offer_free", "you", TechText.Cents(_director.Level(Math.Max(0L, r.Player))))
                    : r.Gap > 0 ? TechText.Fill("reallife_exit_offer_gap", "owed", TechText.Cents(_director.Level(r.Owed)), "paid", TechText.Cents(_director.Level(r.Paid)),
                        "gap", TechText.Cents(_director.Level(r.Gap)))
                    : TechText.Fill("reallife_exit_offer_after", "investors", TechText.Cents(_director.Level(r.Paid)), "you", TechText.Cents(_director.Level(Math.Max(0L, r.Player)))));
                if (ExitRules.IsAgreed(e, id))
                {
                    // The player gave their word at a desk inside the firm: the sale completes when they step outside, until then they may call it off.
                    box.Line(TechText.Fill("reallife_exit_agreed_line", "buyer", o.Name, "amount", TechText.Cents(_director.Level(e.Agreed.Price)), "firm", firm.File.Name,
                        "day", e.Agreed.Lapse.ToString()));
                    InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_exit_button_call_off", () => Do(() => _director.ExitCallOff(place)));
                    continue;
                }
                string title = TechText.Fill("reallife_exit_confirm_accept_title", "firm", firm.File.Name);
                string body = TechText.Fill(o.Hire ? "reallife_exit_confirm_hire_body" : "reallife_exit_confirm_accept_body", "firm", firm.File.Name, "buyer", o.Name,
                    "amount", TechText.Cents(_director.Level(o.Price)));
                Transform row = box.ButtonRow();
                InfoBox.Button(row, i == 0 ? InfoBox.Positive : InfoBox.Additional, "reallife_exit_button_accept", () => GameConfirm.Show(title, body,
                    () => UiClone.Safe(() => Do(() => _director.ExitAccept(place, id))), null, null, null, true, true));
                InfoBox.Button(row, InfoBox.Additional, "reallife_exit_button_decline", () => Do(() => _director.ExitDecline(place, id)));
            }
            ExitOffer first = e.Offers[0];
            if (!first.Countered && !first.Hire && !ExitRules.IsAgreed(e, first.Id))
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
