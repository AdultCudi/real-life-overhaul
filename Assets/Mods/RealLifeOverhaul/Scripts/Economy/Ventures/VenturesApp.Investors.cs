using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The Investors tab. Its first page lists what is open: offers, money promised, deals. "Raise money" leads through
    /// the pages of a pitch: the business the money is for (or one not yet open: its kind and what it will cost), then
    /// the investor, then the meeting, which the director holds in the game's list window with the phone put away.
    /// </summary>
    public sealed partial class VenturesApp
    {
        private const int PageList = 0, PageCounter = 1, PageBusiness = 2, PageType = 3, PageWho = 4;
        private const string OfferRow = "offer:", DealRow = "deal:", PledgeRow = "pledge:", BusinessRow = "biz:", NewRow = "new:", TypeRow = "type:", WhoRow = "inv:";

        private int _page;
        private string _raisePlace;    // the business the money is for; null for one not yet open
        private string _raiseType;     // ...whose kind this is
        private int _budget = 3;       // ...and whose budget is this step of the catalogue's
        private int _who;              // the investor picked to approach

        private string[] WriteInvestors(InfoBox box, VenturesFile file, int day)
        {
            UiClone.Words(_shell.Header, FinanceText.T("reallife_ven_head_investors", "deals", file.Deals.Count.ToString(), "name", file.Reputation.ToString()));
            switch (_page)
            {
                case PageBusiness: return WriteRaiseBusiness(box, file, day);
                case PageType: return WriteRaiseType(box, file);
                case PageWho: return WriteRaiseWho(box, file, day);
            }
            for (int i = 0; i < file.Offers.Count; i++)
            {
                Offer o = file.Offers[i];
                FinanceRow row = Row(OfferRow, o.Id);
                row.A = _director.NameOf(o.InvestorId);
                row.B = o.ForNew ? FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(o.TypeName)) : VenturesText.Business(o.Place, o.Business);
                row.C = FinanceText.T("reallife_ven_row_terms", "amount", Money(o.Amount), "share", VenturesText.Share(o.ShareBp));
                row.D = FinanceText.T("reallife_ven_row_answer_by", "day", o.LapseDay.ToString());
                row.Tone = 1;
            }
            for (int i = 0; i < file.Pledges.Count; i++)
            {
                Pledge p = file.Pledges[i];
                FinanceRow row = Row(PledgeRow, p.Id);
                row.A = _director.NameOf(p.InvestorId);
                row.B = FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(p.TypeName));
                row.C = FinanceText.T("reallife_ven_row_terms", "amount", Money(p.Amount), "share", VenturesText.Share(p.ShareBp));
                row.D = FinanceText.T("reallife_ven_row_open_by", "day", p.OpenBy.ToString());
            }
            for (int i = 0; i < file.Deals.Count; i++)
            {
                Deal d = file.Deals[i];
                FinanceRow row = Row(DealRow, d.Id);
                row.A = _director.NameOf(d.InvestorId);
                row.B = VenturesText.Business(d.Place, d.Business);
                row.C = d.Stage == DealStage.Called ? FinanceText.T("reallife_ven_row_owed", "amount", Money(d.CallOwed))
                    : FinanceText.T("reallife_ven_row_terms", "amount", Money(d.Amount), "share", VenturesText.Share(d.ShareNow(day)));
                row.D = DealState(d);
                row.Tone = d.Stage == DealStage.Called || d.Stage == DealStage.Demand ? 2 : d.Misses > 0 ? 1 : 0;
            }
            if (_rows.Count == 0)
            {
                Say(FinanceText.T("reallife_ven_row_nothing"));
            }
            Offer offer = file.Offer(Picked(_picked, OfferRow));
            Deal deal = file.Deal(Picked(_picked, DealRow));
            Pledge pledge = null;
            for (int i = 0; i < file.Pledges.Count; i++)
            {
                pledge = file.Pledges[i].Id == Picked(_picked, PledgeRow) ? file.Pledges[i] : pledge;
            }
            if (_page == PageCounter && (offer == null || offer.Countered))
            {
                _page = PageList;   // (the counter's page is only ever an offer's that has not been countered)
            }
            if (offer != null && _page == PageCounter)
            {
                WriteCounter(box, file, offer);
            }
            else if (offer != null)
            {
                WriteOffer(box, file, offer);
            }
            else if (deal != null)
            {
                WriteDeal(box, file, deal, day);
            }
            else if (pledge != null)
            {
                WritePledge(box, pledge);
            }
            else
            {
                _picked = null;
                _page = PageList;
                box.Title(FinanceText.T("reallife_ven_tab_investors"));
                box.Line(Pair("reallife_ven_inv_name", FinanceText.T("reallife_ven_of_hundred", "value", file.Reputation.ToString())));
                box.Line(Pair("reallife_ven_inv_in", Money(file.InvestedTotal)));
                box.Line(Pair("reallife_ven_inv_out", Money(file.PaidOutTotal)));
                box.Line(FinanceText.T("reallife_ven_inv_how", "days", VenturesCatalogue.ProfitDaysNeeded.ToString(), "of", VenturesCatalogue.ProfitWindowDays.ToString(),
                    "profit", Money(VenturesCatalogue.OfferProfitFloor)));
                if (Settings.VenturesInvestors)
                {
                    box.Line(FinanceText.T("reallife_ven_inv_raise_note"));
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ven_raise", () => GoTo(PageBusiness));
                }
                else
                {
                    box.Line(FinanceText.T("reallife_ven_inv_off"));
                }
            }
            return InvestorColumns;
        }

        private static string DealState(Deal d)
        {
            switch (d.Stage)
            {
                case DealStage.Called: return FinanceText.T("reallife_ven_state_called");
                case DealStage.Demand: return FinanceText.T("reallife_ven_state_demand");
                case DealStage.Cold: return FinanceText.T("reallife_ven_state_cold");
                case DealStage.Quiet: return FinanceText.T("reallife_ven_state_quiet");
                default: return d.Misses > 0 ? FinanceText.T("reallife_ven_state_warned") : VenturesText.Mood(d);
            }
        }

        private void GoTo(int page)
        {
            _page = page;
            if (page == PageList || page == PageBusiness)
            {
                _picked = null;
            }
            _table.ClearSelection();
            Refresh();
        }

        // A click on a row of this tab. True if it was dealt with here (a page of the pitch).
        private bool InvestorRowClicked(FinanceRow row)
        {
            switch (_page)
            {
                case PageBusiness:
                    if (row.Id.StartsWith(NewRow, System.StringComparison.Ordinal))
                    {
                        _raisePlace = null;
                        _raiseType = null;
                        GoTo(PageType);
                        return true;
                    }
                    _businesses.Clear();
                    Books.Businesses(_businesses);
                    if (row.Index >= 0 && row.Index < _businesses.Count)
                    {
                        _raisePlace = Books.Key(_businesses[row.Index].Address);
                        _raiseType = null;
                        _who = 0;
                        _businesses.Clear();
                        GoTo(PageWho);
                    }
                    _businesses.Clear();
                    return true;
                case PageType:
                    _raiseType = row.Index >= 0 && row.Index < _types.Count ? _types[row.Index] : null;
                    _picked = row.Id;
                    Refresh();
                    return true;
                case PageWho:
                    _who = row.Index;
                    _picked = row.Id;
                    Refresh();
                    return true;
                default:
                    _page = PageList;
                    return false;
            }
        }

        // ---------------------------------------------------------------- an offer, a promise, a deal

        private void WriteOffer(InfoBox box, VenturesFile file, Offer offer)
        {
            Investor investor = file.Investor(offer.InvestorId);
            box.Title(investor.Name);
            box.Line(FinanceText.T("reallife_ven_who_line", "kind", VenturesText.Kind(investor), "temper", VenturesText.Temper(investor)));
            box.Line(offer.ForNew ? Pair("reallife_ven_offer_for", FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(offer.TypeName)))
                : Pair("reallife_ven_offer_for", VenturesText.Business(offer.Place, offer.Business)));
            box.Line(Pair("reallife_ven_offer_amount", Money(offer.Amount)));
            box.Line(Pair("reallife_ven_offer_share", FinanceText.T("reallife_ven_share_of_profit", "share", VenturesText.Share(offer.ShareBp))));
            box.Line(FinanceText.Fill(offer.ForNew ? "reallife_ven_offer_expect_new" : "reallife_ven_offer_expect", "growth", offer.TargetPct.ToString(), "days", offer.TargetDays.ToString(),
                "open", VenturesCatalogue.PledgeDays.ToString(), "target", Money(OfferRules.YoungTarget)));
            box.Line(Pair("reallife_ven_offer_by", FinanceText.Day(offer.LapseDay)));
            if (offer.Countered)
            {
                box.Line(FinanceText.T("reallife_ven_offer_countered"));
            }
            int id = offer.Id;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_accept", () => AcceptOffer(id));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_ven_refuse", () =>
            {
                _director.Refuse(id);
                ClosePick();
            });
            if (!offer.Countered)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_counter", () => GoTo(PageCounter));
            }
        }

        // The game's own question box: what is agreed is said before it is agreed.
        private void AcceptOffer(int id)
        {
            VenturesFile file = _director.File;
            Offer offer = file != null ? file.Offer(id) : null;
            if (offer == null)
            {
                ClosePick();
                return;
            }
            string name = _director.NameOf(offer.InvestorId);
            HudConfirm.Show("reallife_ven_confirm_accept_title".Localize(new { name }),
                (offer.ForNew ? "reallife_ven_confirm_accept_new" : "reallife_ven_confirm_accept").Localize(new
                {
                    name,
                    amount = Money(offer.Amount),
                    share = VenturesText.Share(offer.ShareBp),
                    business = offer.ForNew ? VenturesGame.TypeName(offer.TypeName) : VenturesText.Business(offer.Place, offer.Business),
                    growth = offer.TargetPct.ToString(),
                    days = offer.TargetDays.ToString(),
                    open = VenturesCatalogue.PledgeDays.ToString()
                }),
                () => UiClone.Safe(() =>
                {
                    _director.Accept(id);
                    AfterChange();
                }), null, "reallife_ven_accept", "common_cancel", false, false);
        }

        // Something was agreed or paid: the page is written again if the app is still showing.
        private void AfterChange()
        {
            if (Showing)
            {
                _ready = _director.Running;
                _picked = null;
                _page = PageList;
                Refresh();
            }
        }

        private void WriteCounter(InfoBox box, VenturesFile file, Offer offer)
        {
            Investor investor = file.Investor(offer.InvestorId);
            box.Title(FinanceText.T("reallife_ven_counter_title", "name", investor.Name));
            box.Line(FinanceText.T("reallife_ven_counter_now", "amount", Money(offer.Amount), "share", VenturesText.Share(offer.ShareBp)));
            box.Line(FinanceText.T("reallife_ven_counter_how", "money", VenturesCatalogue.CounterMoneyPct.ToString(), "share", VenturesCatalogue.CounterSharePct.ToString()));
            box.Line(FinanceText.T("reallife_ven_counter_once"));
            int id = offer.Id;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_counter_money", () => Counter(id, true));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ven_counter_share", () => Counter(id, false));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_back", () => GoTo(PageList));
        }

        private void Counter(int id, bool moreMoney)
        {
            _director.Counter(id, moreMoney);
            _page = PageList;
            Refresh();
        }

        private void WritePledge(InfoBox box, Pledge pledge)
        {
            box.Title(_director.NameOf(pledge.InvestorId));
            box.Line(Pair("reallife_ven_pledge_for", VenturesGame.TypeName(pledge.TypeName)));
            box.Line(Pair("reallife_ven_offer_amount", Money(pledge.Amount)));
            box.Line(Pair("reallife_ven_offer_share", FinanceText.T("reallife_ven_share_of_profit", "share", VenturesText.Share(pledge.ShareBp))));
            box.Line(FinanceText.T("reallife_ven_pledge_how", "day", pledge.OpenBy.ToString()));
            int id = pledge.Id;
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_ven_pledge_drop", () =>
            {
                _director.GiveUp(id);
                ClosePick();
            });
        }

        private void WriteDeal(InfoBox box, VenturesFile file, Deal deal, int day)
        {
            Investor investor = file.Investor(deal.InvestorId);
            BuildingRegistration r = VenturesGame.Business(deal.Place);
            BusinessFigures b = VenturesGame.Figures(r);
            box.Title(investor.Name);
            box.Line(FinanceText.T("reallife_ven_who_line", "kind", VenturesText.Kind(investor), "temper", VenturesText.Temper(investor)));
            box.Line(Pair("reallife_ven_offer_for", VenturesText.Business(deal.Place, deal.Business)));
            box.Line(Pair("reallife_ven_deal_in", Money(deal.Amount)));
            box.Line(Pair("reallife_ven_deal_out", Money(deal.PaidOut)));
            int id = deal.Id;
            int price = _director.BuyOutPrice(deal);
            if (deal.Stage == DealStage.Called)
            {
                box.Line(FinanceText.T("reallife_ven_deal_called", "amount", Money(deal.CallOwed), "per_day", Money(deal.CallPerDay)));
                if (deal.CallMisses > 0)
                {
                    box.Line(FinanceText.T("reallife_ven_deal_call_missed", "count", deal.CallMisses.ToString(), "most", VenturesCatalogue.CallMissesMost.ToString()));
                }
                box.Line(Pair("reallife_ven_deal_settle", Money(price)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ven_pay_off", () => BuyOut(id));
                return;
            }
            box.Line(Pair("reallife_ven_offer_share", deal.BoardUntil > day
                ? FinanceText.T("reallife_ven_deal_share_seat", "share", VenturesText.Share(deal.ShareNow(day)), "day", deal.BoardUntil.ToString())
                : FinanceText.T("reallife_ven_share_of_profit", "share", VenturesText.Share(deal.ShareBp))));
            if (deal.Arrears > 0)
            {
                box.Line(Pair("reallife_ven_deal_arrears", Money(deal.Arrears)));
            }
            if (deal.TargetDay > 0)
            {
                box.Line(FinanceText.T("reallife_ven_deal_target", "target", Money(deal.TargetProfit), "day", deal.TargetDay.ToString(), "now", Money(b.AvgProfit7)));
            }
            if (deal.CostDay > 0)
            {
                box.Line(FinanceText.T("reallife_ven_deal_promise", "target", Money(deal.CostTarget), "day", deal.CostDay.ToString(), "now", Money(b.AvgCost14)));
            }
            box.Line(Pair("reallife_ven_deal_mood", DealState(deal) == VenturesText.Mood(deal) ? VenturesText.Mood(deal)
                : FinanceText.T("reallife_ven_deal_mood_state", "mood", VenturesText.Mood(deal), "state", DealState(deal))));
            if (deal.Stage == DealStage.Demand)
            {
                box.Line(FinanceText.Fill(investor.Kind == InvestorKind.Firm ? "reallife_ven_deal_demand_firm" : "reallife_ven_deal_demand", "day", deal.DemandDay.ToString(), "extra", VenturesText.Share(VenturesCatalogue.BoardExtraBp),
                    "seat_days", VenturesCatalogue.BoardDays.ToString(), "cut", VenturesCatalogue.CostCutPct.ToString(), "cut_days", VenturesCatalogue.CostCutDays.ToString()));
                Transform answers = box.ButtonRow();
                if (investor.Kind != InvestorKind.Firm)
                {
                    InfoBox.Button(answers, InfoBox.Positive, "reallife_ven_demand_seat", () => Answer(id, true));
                }
                InfoBox.Button(answers, InfoBox.Additional, "reallife_ven_demand_costs", () => Answer(id, false));
            }
            box.Line(Pair("reallife_ven_deal_buy_out", Money(price)));
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ven_buy_out", () => BuyOut(id));
        }

        private void Answer(int id, bool board)
        {
            _director.AnswerDemand(id, board);
            Refresh();
        }

        private void BuyOut(int id)
        {
            VenturesFile file = _director.File;
            Deal deal = file != null ? file.Deal(id) : null;
            if (deal == null)
            {
                ClosePick();
                return;
            }
            string name = _director.NameOf(deal.InvestorId);
            HudConfirm.Show("reallife_ven_confirm_buy_out_title".Localize(new { name }),
                (deal.Stage == DealStage.Called ? "reallife_ven_confirm_pay_off" : "reallife_ven_confirm_buy_out").Localize(new
                {
                    name,
                    amount = Money(_director.BuyOutPrice(deal)),
                    business = VenturesText.Business(deal.Place, deal.Business),
                    share = VenturesText.Share(deal.ShareBp)
                }),
                () => UiClone.Safe(() =>
                {
                    _director.BuyOut(id);
                    AfterChange();
                }), null, deal.Stage == DealStage.Called ? "reallife_ven_pay_off" : "reallife_ven_buy_out", "common_cancel", false, false);
        }

        // ---------------------------------------------------------------- raising money: the business, its kind, the investor

        private string[] WriteRaiseBusiness(InfoBox box, VenturesFile file, int day)
        {
            _businesses.Clear();
            Books.Businesses(_businesses);
            for (int i = 0; i < _businesses.Count; i++)
            {
                BuildingRegistration r = _businesses[i];
                BusinessFigures b = VenturesGame.Figures(r);
                int deals;
                int promised = file.ShareOn(Books.Key(r.Address), day, out deals);
                FinanceRow row = Row(BusinessRow, i);
                row.A = VenturesGame.Name(r);
                row.B = FinanceText.T("reallife_ven_row_a_day", "amount", Money(b.AvgProfit7));
                row.C = FinanceText.T("reallife_ven_row_days", "days", b.AgeDays.ToString());
                row.D = promised > 0 ? VenturesText.Share(promised) : string.Empty;
                row.Tone = b.AvgProfit7 <= 0f ? 2 : 0;
            }
            _businesses.Clear();
            FinanceRow fresh = Row(NewRow, 0);
            fresh.A = FinanceText.T("reallife_ven_row_new_business");
            fresh.B = FinanceText.T("reallife_ven_row_new_business_note");
            box.Title(FinanceText.T("reallife_ven_raise"));
            box.Line(FinanceText.T("reallife_ven_raise_pick"));
            box.Line(FinanceText.T("reallife_ven_raise_how", "least", VenturesCatalogue.Questions[0].ToString(), "most", VenturesCatalogue.Questions[2].ToString()));
            box.Line(FinanceText.T("reallife_ven_raise_truth", "days", VenturesCatalogue.CaughtCloseDays.ToString()));
            box.Line(FinanceText.T("reallife_ven_raise_again", "days", VenturesCatalogue.PitchCooldownDays.ToString()));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_back", () => GoTo(PageList));
            return BusinessColumns;
        }

        private string[] WriteRaiseType(InfoBox box, VenturesFile file)
        {
            VenturesGame.StartableTypes(_types);
            for (int i = 0; i < _types.Count; i++)
            {
                Sector sector = VenturesCatalogue.SectorOf(_types[i]);
                int liked = 0;
                for (int v = 0; v < file.Investors.Count; v++)
                {
                    liked += file.Investors[v].LikesSector(sector) ? 1 : 0;
                }
                FinanceRow row = Row(TypeRow, i);
                row.A = VenturesGame.TypeName(_types[i]);
                row.B = VenturesText.SectorName(sector);
                row.C = FinanceText.T("reallife_ven_row_investors", "count", liked.ToString());
                int run = VenturesGame.CountOf(_types[i]);
                row.D = run > 0 ? run.ToString() : string.Empty;
                if (_types[i] == _raiseType)
                {
                    _picked = row.Id;
                }
            }
            if (_types.Count == 0)
            {
                Say(FinanceText.T("reallife_ven_row_no_types"));
            }
            box.Title(FinanceText.T("reallife_ven_row_new_business"));
            box.Line(_raiseType != null ? Pair("reallife_ven_new_kind", VenturesGame.TypeName(_raiseType)) : FinanceText.T("reallife_ven_new_pick"));
            box.Headline("reallife_ven_new_budget");
            int[] steps = VenturesCatalogue.BudgetSteps;
            _budget = Mathf.Clamp(_budget, 0, steps.Length - 1);
            if (!box.HasSelector || !box.Selector(steps.Length, _budget, i => Money(steps[i]), i =>
                {
                    _budget = i;
                    Refresh();
                }))
            {
                box.Line(Money(steps[_budget]));
            }
            box.Line(FinanceText.T("reallife_ven_new_how", "least", VenturesCatalogue.BudgetShareLeastPct.ToString(), "most", VenturesCatalogue.BudgetShareMostPct.ToString(),
                "days", VenturesCatalogue.PledgeDays.ToString()));
            Transform buttons = box.ButtonRow();
            if (_raiseType != null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_new_next", () =>
                {
                    _who = 0;
                    GoTo(PageWho);
                });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ven_back", () => GoTo(PageBusiness));
            return TypeColumns;
        }

        private string[] WriteRaiseWho(InfoBox box, VenturesFile file, int day)
        {
            BuildingRegistration r = VenturesGame.Business(_raisePlace);
            if (r == null && _raiseType == null)
            {
                _page = PageBusiness;
                return WriteRaiseBusiness(box, file, day);
            }
            string about = r != null ? VenturesGame.Name(r) : FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(_raiseType));
            Sector sector = VenturesCatalogue.SectorOf(r != null ? r.businessTypeName : _raiseType);
            Investor picked = null;
            for (int i = 0; i < file.Investors.Count; i++)
            {
                Investor v = file.Investors[i];
                FinanceRow row = Row(WhoRow, v.Id);
                row.A = v.Name;
                row.B = FinanceText.T("reallife_ven_row_looks", "kind", VenturesText.Kind(v), "likes", VenturesText.Likes(v));
                row.C = FinanceText.T("reallife_ven_row_range", "least", Money(v.ChequeLeast), "most", Money(v.ChequeMost));
                bool backs = r != null || v.Kind != InvestorKind.Tycoon;   // (a tycoon backs no business that is not open yet)
                row.D = !backs ? FinanceText.T("reallife_ven_row_open_only") : v.Open(day) ? FinanceText.T("reallife_ven_row_open_now")
                    : VenturesText.Never(v, day) ? FinanceText.T("reallife_ven_row_never") : FinanceText.Day(v.OpenDay);
                row.Tone = backs && v.Open(day) ? (v.LikesSector(sector) ? 1 : 0) : 2;
                if (v.Id == _who)
                {
                    picked = v;
                    _picked = row.Id;
                }
            }
            if (picked == null)
            {
                _picked = null;
                box.Title(FinanceText.T("reallife_ven_who_title"));
                box.Line(Pair("reallife_ven_offer_for", about));
                box.Line(FinanceText.T("reallife_ven_who_pick", "sector", VenturesText.SectorName(sector)));
                box.Line(FinanceText.T("reallife_ven_who_kinds"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_back", () => GoTo(r != null ? PageBusiness : PageType));
                return WhoColumns;
            }
            int k = (int)picked.Kind;
            box.Title(picked.Name);
            box.Line(FinanceText.T(k == 0 ? "reallife_ven_who_angel" : k == 1 ? "reallife_ven_who_tycoon" : "reallife_ven_who_firm"));
            box.Line(Pair("reallife_ven_who_temper", VenturesText.Temper(picked)));
            box.Line(Pair("reallife_ven_who_likes", VenturesText.Likes(picked)));
            box.Line(Pair("reallife_ven_who_sums", FinanceText.T("reallife_ven_row_range", "least", Money(picked.ChequeLeast), "most", Money(picked.ChequeMost))));
            box.Line(Pair("reallife_ven_who_share", FinanceText.T("reallife_ven_row_range", "least", VenturesText.Share(VenturesCatalogue.ShareLeastBp[k]), "most", VenturesText.Share(VenturesCatalogue.ShareMostBp[k]))));
            box.Line(Pair("reallife_ven_who_checks", FinanceText.T(picked.Diligence < 50 ? "reallife_ven_checks_lightly" : picked.Diligence < 78 ? "reallife_ven_checks_carefully" : "reallife_ven_checks_closely")));
            box.Line(Pair("reallife_ven_who_questions", VenturesCatalogue.Questions[k].ToString()));
            box.Line(Pair("reallife_ven_offer_for", about));
            bool meets = picked.Open(day) && (r != null || picked.Kind != InvestorKind.Tycoon);
            if (r == null && picked.Kind == InvestorKind.Tycoon)
            {
                box.Line(FinanceText.T("reallife_ven_who_not_open"));
            }
            else if (!picked.Open(day))
            {
                box.Line(VenturesText.Never(picked, day) ? FinanceText.T("reallife_ven_who_never") : FinanceText.T("reallife_ven_who_closed", "day", picked.OpenDay.ToString()));
            }
            Transform buttons = box.ButtonRow();
            if (meets)
            {
                int id = picked.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_who_meet", () => Meet(id));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ven_back", () => GoTo(r != null ? PageBusiness : PageType));
            return WhoColumns;
        }

        // The meeting is held in the game's list window, with the phone put away: the director sees to both.
        private void Meet(int investorId)
        {
            string place = _raisePlace, type = _raiseType;
            int budget = VenturesCatalogue.BudgetSteps[Mathf.Clamp(_budget, 0, VenturesCatalogue.BudgetSteps.Length - 1)];
            _page = PageList;
            _picked = null;
            if (!_director.StartPitch(investorId, place, place != null ? null : type, budget) && Showing)
            {
                _ready = _director.Running;
                Refresh();
            }
        }
    }
}
